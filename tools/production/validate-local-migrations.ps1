[CmdletBinding()]
param(
    [switch]$ConfirmLocalDisposableDatabase,
    [ValidatePattern('^MakanApp_Gap02_[a-f0-9]{32}$')]
    [string]$DatabaseName = ('MakanApp_Gap02_' + [Guid]::NewGuid().ToString('N'))
)

$ErrorActionPreference = 'Stop'
if (-not $ConfirmLocalDisposableDatabase) {
    throw 'ساخت دیتابیس مستقل LocalDB نیازمند ConfirmLocalDisposableDatabase است.'
}

# مقصد فقط LocalDB و نام مستقل است؛ این ابزار هرگز DB عادی یا Production را انتخاب/حذف نمی‌کند.
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$originalConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__MakanDatabase')
$connection = New-Object System.Data.SqlClient.SqlConnection
$command = $null
$result = [ordered]@{
    Database = $DatabaseName
    Target = 'LocalDB-only; not staging SQL TLS validation'
    EmptyDatabase = 'NOT_EXECUTED'
    Migrations = 'NOT_EXECUTED'
    PendingModel = 'NOT_EXECUTED'
    Connectivity = 'NOT_EXECUTED'
    AppliedCount = 0
    LastMigration = $null
    Retained = $true
}

Push-Location $repositoryRoot
try {
    $connection.ConnectionString = 'Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True'
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT DB_ID(@database)'
    [void]$command.Parameters.AddWithValue('@database', $DatabaseName)
    if ($command.ExecuteScalar() -isnot [DBNull]) {
        $result['FailureCode'] = 'DATABASE_ALREADY_EXISTS'
        throw 'مقصد از قبل موجود است؛ ابزار برای حفاظت از داده متوقف شد.'
    }
    $connection.Close()
    $result.EmptyDatabase = 'PASS'

    $env:ConnectionStrings__MakanDatabase = "Server=(localdb)\MSSQLLocalDB;Database=$DatabaseName;Integrated Security=True;Encrypt=False;TrustServerCertificate=True"
    # خروجی خام EF ممکن است شامل جزئیات محیط باشد؛ فقط نتیجه و شناسه migration گزارش می‌شوند.
    $efOutput = & dotnet ef database update --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --context MakanDbContext --configuration Debug --no-build 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'اجرای migration موفق نشد؛ خروجی خام منتشر نشده است.' }
    $result.Migrations = 'PASS'
    $efOutput = & dotnet ef migrations has-pending-model-changes --project MakanApp.Infrastructure --startup-project MakanApp.Infrastructure --context MakanDbContext --configuration Debug --no-build 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'مدل و migration همگام نیستند؛ خروجی خام منتشر نشده است.' }
    $result.PendingModel = 'PASS'

    $connection.ConnectionString = $env:ConnectionStrings__MakanDatabase
    $connection.Open()
    $command.Dispose()
    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT [MigrationId] FROM [__EFMigrationsHistory] ORDER BY [MigrationId]'
    $reader = $command.ExecuteReader()
    $applied = New-Object 'System.Collections.Generic.List[string]'
    try { while ($reader.Read()) { $applied.Add($reader.GetString(0)) } }
    finally { $reader.Dispose() }
    $expected = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'MakanApp.Infrastructure/Persistence/Migrations') -Filter '*.cs' |
        Where-Object { $_.BaseName -match '^\d{14}_' -and $_.Name -notlike '*.Designer.cs' } |
        ForEach-Object { $_.BaseName } | Sort-Object)
    if (@(Compare-Object $expected @($applied)).Count -ne 0) { throw 'تاریخچه migration با سورس منطبق نیست.' }
    $result.AppliedCount = $applied.Count
    $result.LastMigration = $applied[$applied.Count - 1]
    $result.Connectivity = 'PASS'
    $result | ConvertTo-Json
}
catch {
    if (-not $result.Contains('FailureCode')) { $result['FailureCode'] = 'LOCAL_VALIDATION_FAILED' }
    $result['Failure'] = 'اعتبارسنجی محلی ناموفق بود؛ جزئیات خام/connection string چاپ نشده است.'
    $result | ConvertTo-Json
    exit 1
}
finally {
    if ($null -ne $command) { $command.Dispose() }
    $connection.Dispose()
    [Environment]::SetEnvironmentVariable('ConnectionStrings__MakanDatabase', $originalConnection)
    $efOutput = $null
    Pop-Location
}
