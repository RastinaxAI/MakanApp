[CmdletBinding()]
param(
    [string]$OutputPath = '.artifacts/deployment/migrations-idempotent-safe.sql',
    [string]$ToMigration,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$migrations = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'MakanApp.Infrastructure/Persistence/Migrations') -Filter '*.cs' |
    Where-Object { $_.BaseName -match '^\d{14}_' -and $_.Name -notlike '*.Designer.cs' } |
    Select-Object -ExpandProperty BaseName | Sort-Object)
if ([string]::IsNullOrWhiteSpace($ToMigration)) { $ToMigration = $migrations[-1] }
if ($ToMigration -notin $migrations) { throw 'Migration مقصد باید یکی از شناسه‌های نسخه‌بندی‌شده باشد.' }
if (-not [IO.Path]::IsPathRooted($OutputPath)) { $OutputPath = Join-Path $repositoryRoot $OutputPath }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if (-not $OutputPath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetExtension($OutputPath) -ne '.sql') { throw 'خروجی SQL باید در workspace پروژه باشد.' }
if (Test-Path -LiteralPath $OutputPath) { throw 'برای حفظ خروجی قبلی، مسیر فایل جدید انتخاب کنید.' }
$outputDirectory = Split-Path $OutputPath -Parent
[void][IO.Directory]::CreateDirectory($outputDirectory)
$rawPath = Join-Path $outputDirectory ('ef-generated-' + [Guid]::NewGuid().ToString('N') + '.sql')

function ConvertTo-SqlLiteral([string]$Value) { return "N'" + $Value.Replace("'", "''") + "'" }

Push-Location $repositoryRoot
try {
    $efArguments = @('ef', 'migrations', 'script', '0', $ToMigration, '--idempotent',
        '--project', 'MakanApp.Infrastructure', '--startup-project', 'MakanApp.Infrastructure',
        '--context', 'MakanDbContext', '--configuration', $Configuration, '--output', $rawPath)
    if ($NoBuild) { $efArguments += '--no-build' }
    $efOutput = & dotnet @efArguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'تولید SQL توسط EF موفق نشد؛ فایل قابل اجرا تحویل داده نشده است.' }
    $rawSql = [IO.File]::ReadAllText($rawPath)

    # شرط history حفظ می‌شود؛ هر عملیات در زمان اجرا و پس از DDL قبلی کامپایل می‌شود.
    $guardPattern = @'
(?ms)^(?<guard>IF NOT EXISTS \(\r?\n    SELECT \* FROM \[__EFMigrationsHistory\]\r?\n    WHERE \[MigrationId\] = N'[^']+'\r?\n\)\r?\nBEGIN\r?\n)(?<body>.*?)(?<end>^END;[ \t]*\r?$)
'@
    $guardRegex = [regex]$guardPattern.Trim()
    $guards = $guardRegex.Matches($rawSql)
    if ($guards.Count -eq 0 -or $guards.Count -ne [regex]::Matches($rawSql, '(?m)^IF NOT EXISTS \(').Count) {
        throw 'ساختار خروجی EF با قالب بررسی‌شده سازگار نیست؛ بازبینی دستی لازم است.'
    }
    $deferredSql = $guardRegex.Replace($rawSql, [Text.RegularExpressions.MatchEvaluator]{
        param($match)
        return $match.Groups['guard'].Value + '    EXEC(' +
            (ConvertTo-SqlLiteral $match.Groups['body'].Value.TrimEnd()) + ');' +
            [Environment]::NewLine + $match.Groups['end'].Value
    })
    $migrationIds = @([regex]::Matches($rawSql, "VALUES \(N'(\d{14}_[^']+)', N'[0-9.]+'\)") |
        ForEach-Object { $_.Groups[1].Value })
    $expectedIds = @($migrations | Where-Object { $_ -le $ToMigration })
    if (@(Compare-Object $expectedIds $migrationIds).Count -ne 0) { throw 'فهرست Migrationهای SQL با کد تطابق ندارد.' }

    # فایل نهایی یک batch است؛ خطا، ادامه مراحل بعدی را متوقف و تراکنش همین فایل را rollback می‌کند.
    $script = New-Object Text.StringBuilder
    [void]$script.AppendLine('-- ماکان: SQL idempotent با کامپایل مؤخر و توقف در خطا؛ فایل قدیمی استفاده نشود.')
    [void]$script.AppendLine('-- پیش از اجرا: Backup، دیتابیس صحیح، بازبینی history و یک پنجره تازه SSMS.')
    [void]$script.AppendLine('-- فایل را کامل اجرا کنید؛ اتصال یا Migration روی سرور توسط ابزار تولید انجام نمی‌شود.')
    [void]$script.AppendLine("-- آخرین Migration: $ToMigration")
    [void]$script.AppendLine(@'
IF @@TRANCOUNT <> 0 OR XACT_STATE() <> 0
BEGIN
    RAISERROR(N'اجرای Migration به نشست تازه بدون تراکنش باز نیاز دارد.', 16, 1);
    RETURN;
END;
SET XACT_ABORT ON;
BEGIN TRY
'@)
    foreach ($batch in [regex]::Split($deferredSql, '(?m)^[ \t]*GO[ \t]*\r?$')) {
        if (-not [string]::IsNullOrWhiteSpace($batch)) {
            [void]$script.AppendLine('    EXEC(' + (ConvertTo-SqlLiteral $batch.Trim()) + ');')
        }
    }
    [void]$script.AppendLine(@'
    IF @@TRANCOUNT <> 0
        THROW 51001, N'تراکنش Migration تکمیل نشده است.', 1;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
SELECT DB_NAME() AS DatabaseName, @@TRANCOUNT AS OpenTransactions, XACT_STATE() AS TransactionState;
'@)
    [IO.File]::WriteAllText($OutputPath, $script.ToString(), (New-Object Text.UTF8Encoding($true)))
    [ordered]@{
        Output = $OutputPath
        RawEfOutput = $rawPath
        MigrationCount = $migrationIds.Count
        LastMigration = $ToMigration
        DeferredOperations = $guards.Count
        DatabaseExecuted = $false
        Sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $OutputPath).Hash
    } | ConvertTo-Json
}
finally { $efOutput = $null; Pop-Location }
