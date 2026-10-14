Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:RecoveryPhase = 'INPUT_VALIDATION'

function Get-RecoveryPhase { return $script:RecoveryPhase }

function Get-RecoveryPath([string]$Path) {
    if (-not [IO.Path]::IsPathRooted($Path) -or $Path.StartsWith('\\')) {
        throw 'مسیر باید مطلق و روی دیسک محلی باشد.'
    }
    $resolved = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    if ($resolved -eq [IO.Path]::GetPathRoot($resolved).TrimEnd('\', '/')) { throw 'ریشه دیسک مجاز نیست.' }
    $cursor = $resolved
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'مسیر دارای junction یا symbolic link مجاز نیست.'
            }
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    return $resolved
}

function Test-RecoveryOverlap([string]$First, [string]$Second) {
    return $First.Equals($Second, [StringComparison]::OrdinalIgnoreCase) -or
        $First.StartsWith($Second + '\', [StringComparison]::OrdinalIgnoreCase) -or
        $Second.StartsWith($First + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-RecoveryTarget([string]$DatabaseName) {
    if ($DatabaseName -cnotmatch '^MakanApp_Recovery_[A-Za-z0-9_]{8,80}\z') {
        throw 'نام مقصد باید با MakanApp_Recovery_ شروع شود و فقط حروف، عدد و underscore داشته باشد.'
    }
}

function Get-RecoveryConnection([string]$EnvironmentVariable) {
    $value = [Environment]::GetEnvironmentVariable($EnvironmentVariable)
    if ([string]::IsNullOrWhiteSpace($value)) { throw 'متغیر اتصال بازیابی تنظیم نشده است.' }
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $value
    # ابزار فایل محلی است؛ SQL راه‌دور نیازمند runbook مستقل DBA است.
    $server = $builder.DataSource.Split('\')[0]
    if ($server -notin @('.', 'localhost', '(localdb)', $env:COMPUTERNAME)) {
        throw 'این ابزار فقط SQL Server محلی Windows را پشتیبانی می‌کند.'
    }
    if ($builder.InitialCatalog -notmatch '^[A-Za-z][A-Za-z0-9_]{0,119}\z' -or
        $builder.InitialCatalog -in @('master', 'model', 'msdb', 'tempdb')) { throw 'دیتابیس منبع معتبر نیست.' }
    return $builder
}

function Invoke-RecoverySql($Connection, [string]$Database, [string]$Sql) {
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $Connection.ConnectionString
    $builder['Initial Catalog'] = $Database
    $connectionObject = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
    $command = $null
    $adapter = $null
    try {
        $connectionObject.Open()
        $command = $connectionObject.CreateCommand()
        $command.CommandTimeout = 600
        $command.CommandText = $Sql
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $command
        $table = New-Object System.Data.DataTable
        [void]$adapter.Fill($table)
        return ,$table
    }
    catch { throw 'عملیات SQL بازیابی ناموفق بود؛ اپراتور باید خطا را در محیط امن SQL بررسی کند.' }
    finally {
        if ($null -ne $adapter) { $adapter.Dispose() }
        if ($null -ne $command) { $command.Dispose() }
        $connectionObject.Dispose()
    }
}

function ConvertTo-RecoveryLiteral([string]$Value) { return "N'" + $Value.Replace("'", "''") + "'" }

function New-RecoveryDirectory([string]$Path, [string]$SqlServiceIdentity) {
    if (Test-Path -LiteralPath $Path) { throw 'مسیر مقصد از قبل وجود دارد؛ overwrite ممنوع است.' }
    # ACL قبل از نوشتن داده حساس محدود می‌شود؛ SQL service فقط در صورت نیاز اضافه می‌شود.
    $security = New-Object Security.AccessControl.DirectorySecurity
    $security.SetAccessRuleProtection($true, $false)
    $current = [Security.Principal.WindowsIdentity]::GetCurrent().User
    foreach ($identity in @($current, (New-Object Security.Principal.SecurityIdentifier 'S-1-5-18'))) {
        $security.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
            $identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    }
    if ($SqlServiceIdentity) {
        $security.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
            $SqlServiceIdentity, 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    }
    [void][IO.Directory]::CreateDirectory($Path, $security)
}

function Get-RecoveryFiles([string]$Root, [string]$BaseRoot = $Root) {
    # پیمایش سطح‌به‌سطح مانع دنبال‌کردن reparse point در زیرشاخه‌ها است.
    foreach ($entry in Get-ChildItem -LiteralPath $Root -Force) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'فایل یا پوشه پیوندی مجاز نیست.' }
        if ($entry.PSIsContainer) { Get-RecoveryFiles $entry.FullName $BaseRoot }
        else {
            [pscustomobject]@{
                Key = $entry.FullName.Substring($BaseRoot.Length + 1).Replace('\', '/')
                Size = $entry.Length
                Hash = (Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash
            }
        }
    }
}

function Resolve-RecoveryKey([string]$Root, [string]$Key) {
    if ([string]::IsNullOrWhiteSpace($Key) -or $Key -match '\\|:|^/|//|(^|/)\.{1,2}(/|$)' -or $Key.EndsWith('/')) {
        throw 'StorageKey یا مسیر manifest معتبر نیست.'
    }
    foreach ($segment in $Key.Split('/')) {
        if ($segment.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or
            $segment -match '[. ]$|^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)') {
            throw 'نام فایل Windows معتبر نیست.'
        }
    }
    $path = Get-RecoveryPath ([IO.Path]::Combine($Root, $Key.Replace('/', '\')))
    if (-not $path.StartsWith($Root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'مسیر از root خارج است.' }
    return $path
}

function Get-RecoveryMigrations($Connection, [string]$Database) {
    $table = Invoke-RecoverySql $Connection $Database 'SELECT [MigrationId] FROM [dbo].[__EFMigrationsHistory] ORDER BY [MigrationId];'
    return @($table.Rows | ForEach-Object { [string]$_.MigrationId })
}

function Get-RecoveryAssets($Connection, [string]$Database) {
    $table = Invoke-RecoverySql $Connection $Database 'SELECT [StorageKey], [Status], [SizeBytes], [Sha256Hash] FROM [storage].[FileAssets] ORDER BY [StorageKey];'
    return @($table.Rows | ForEach-Object {
        [pscustomobject]@{ Key = [string]$_.StorageKey; Status = [int]$_.Status; Size = [long]$_.SizeBytes; Hash = [string]$_.Sha256Hash }
    })
}

function Test-RecoveryFiles([string]$Root, [object[]]$Files, [object[]]$Assets) {
    $actual = @(Get-RecoveryFiles $Root)
    $byKey = @{}
    foreach ($file in $actual) { $byKey[$file.Key] = $file }
    $expectedKeys = @{}
    foreach ($file in $Files) {
        [void](Resolve-RecoveryKey $Root $file.Key)
        if ($expectedKeys.ContainsKey($file.Key)) { throw 'مسیر تکراری در manifest وجود دارد.' }
        $expectedKeys[$file.Key] = $true
        if (-not $byKey.ContainsKey($file.Key) -or $byKey[$file.Key].Hash -ne $file.Hash -or $byKey[$file.Key].Size -ne $file.Size) {
            throw 'فایل snapshot مفقود یا hash/اندازه آن ناسازگار است.'
        }
    }
    if ($actual.Count -ne $Files.Count) { throw 'تعداد فایل‌ها با snapshot برابر نیست.' }
    $assetKeys = @{}
    $nonReadyMissing = 0
    foreach ($asset in $Assets) {
        [void](Resolve-RecoveryKey $Root $asset.Key)
        $assetKeys[$asset.Key] = $true
        if ($asset.Status -eq 2) {
            if (-not $byKey.ContainsKey($asset.Key) -or $byKey[$asset.Key].Size -ne $asset.Size -or $byKey[$asset.Key].Hash -ne $asset.Hash) {
                throw 'FileAsset آماده فاقد بایت صحیح است؛ بازیابی کامل نیست.'
            }
        }
        elseif (-not $byKey.ContainsKey($asset.Key)) { $nonReadyMissing++ }
    }
    return [pscustomobject]@{
        Files = $actual.Count
        ReadyAssets = @($Assets | Where-Object Status -eq 2).Count
        NonReadyMissing = $nonReadyMissing
        UnreferencedFiles = @($actual | Where-Object { -not $assetKeys.ContainsKey($_.Key) }).Count
    }
}

function Invoke-MakanBackup {
    param(
        [Parameter(Mandatory)][string]$ConnectionEnvironmentVariable,
        [Parameter(Mandatory)][string]$StorageRoot,
        [Parameter(Mandatory)][string]$BundlePath,
        [Parameter(Mandatory)][string]$ApplicationRoot,
        [switch]$WritersStopped,
        [string]$SqlServiceIdentity
    )
    if (-not $WritersStopped) { throw 'توقف همه writerها و workerها باید صریحاً تأیید شود.' }
    $script:RecoveryPhase = 'BACKUP_PREFLIGHT'
    $root = Get-RecoveryPath $StorageRoot
    $bundle = Get-RecoveryPath $BundlePath
    $app = Get-RecoveryPath $ApplicationRoot
    if (-not (Test-Path -LiteralPath $root -PathType Container) -or
        (Test-RecoveryOverlap $bundle $root) -or (Test-RecoveryOverlap $bundle $app)) { throw 'محل bundle ناامن است.' }
    $connection = Get-RecoveryConnection $ConnectionEnvironmentVariable
    if ($connection.DataSource -notlike '(localdb)\*' -and -not $SqlServiceIdentity) { throw 'identity سرویس SQL باید صریح مشخص شود.' }
    $database = $connection.InitialCatalog
    $beforeFiles = @(Get-RecoveryFiles $root)
    $assets = @(Get-RecoveryAssets $connection $database)
    [void](Test-RecoveryFiles $root $beforeFiles $assets)
    $migrations = @(Get-RecoveryMigrations $connection $database)
    if ($migrations.Count -eq 0) { throw 'migration history خالی است.' }
    New-RecoveryDirectory $bundle $SqlServiceIdentity
    $backupPath = Join-Path $bundle 'database.bak'
    $backupLiteral = ConvertTo-RecoveryLiteral $backupPath
    $started = [DateTime]::UtcNow
    $script:RecoveryPhase = 'SQL_BACKUP_VERIFY'
    [void](Invoke-RecoverySql $connection 'master' "BACKUP DATABASE [$database] TO DISK = $backupLiteral WITH COPY_ONLY, CHECKSUM;")
    [void](Invoke-RecoverySql $connection 'master' "RESTORE VERIFYONLY FROM DISK = $backupLiteral WITH CHECKSUM;")
    $fileRoot = Join-Path $bundle 'files'
    $script:RecoveryPhase = 'STORAGE_SNAPSHOT'
    [void][IO.Directory]::CreateDirectory($fileRoot)
    foreach ($file in $beforeFiles) {
        $target = Resolve-RecoveryKey $fileRoot $file.Key
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        [IO.File]::Copy((Resolve-RecoveryKey $root $file.Key), $target, $false)
    }
    [void](Test-RecoveryFiles $fileRoot $beforeFiles $assets)
    [void](Test-RecoveryFiles $root $beforeFiles $assets)
    if (($assets | ConvertTo-Json -Depth 5 -Compress) -cne ((Get-RecoveryAssets $connection $database) | ConvertTo-Json -Depth 5 -Compress)) {
        throw 'metadata فایل حین snapshot تغییر کرده است.'
    }
    $manifest = [ordered]@{
        Format = 1; SourceDatabase = $database
        BackupStartedAtUtc = $started.ToString('O'); CompletedAtUtc = [DateTime]::UtcNow.ToString('O')
        BackupSha256 = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
        Migrations = @($migrations); Assets = @($assets); Files = @($beforeFiles)
    }
    $manifestPath = Join-Path $bundle 'manifest.json'
    $script:RecoveryPhase = 'MANIFEST_WRITE'
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding $false))
    return [pscustomobject]@{ Result = 'BACKUP_VERIFIED'; ManifestSha256 = (Get-FileHash -LiteralPath $manifestPath).Hash; Files = $beforeFiles.Count }
}

function Invoke-MakanRestore {
    param(
        [Parameter(Mandatory)][string]$ConnectionEnvironmentVariable,
        [Parameter(Mandatory)][string]$TargetDatabase,
        [Parameter(Mandatory)][string]$BundlePath,
        [Parameter(Mandatory)][string]$ExpectedManifestHash,
        [Parameter(Mandatory)][string[]]$ExpectedMigrations,
        [Parameter(Mandatory)][string]$TargetStorageRoot,
        [Parameter(Mandatory)][string]$ApplicationRoot
    )
    Assert-RecoveryTarget $TargetDatabase
    $script:RecoveryPhase = 'RESTORE_PREFLIGHT'
    $connection = Get-RecoveryConnection $ConnectionEnvironmentVariable
    if ($TargetDatabase -eq $connection.InitialCatalog) { throw 'منبع و مقصد نباید یکسان باشند.' }
    $bundle = Get-RecoveryPath $BundlePath
    $root = Get-RecoveryPath $TargetStorageRoot
    if ((Test-RecoveryOverlap $root $bundle) -or (Test-RecoveryOverlap $root (Get-RecoveryPath $ApplicationRoot)) -or
        (Test-Path -LiteralPath $root)) { throw 'storage مقصد باید جدید، جدا و خارج برنامه باشد.' }
    $manifestPath = Join-Path $bundle 'manifest.json'
    if ($ExpectedManifestHash -notmatch '^[A-Fa-f0-9]{64}\z' -or
        (Get-FileHash -LiteralPath $manifestPath).Hash -ne $ExpectedManifestHash) { throw 'digest مستقل manifest تطبیق ندارد.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.Format -ne 1 -or $TargetDatabase -eq $manifest.SourceDatabase) { throw 'manifest یا مقصد معتبر نیست.' }
    if ($ExpectedMigrations.Count -eq 0 -or ($ExpectedMigrations -join "`n") -cne ($manifest.Migrations -join "`n")) {
        throw 'نسخه artifact و migrationهای snapshot سازگار نیستند؛ migration خودکار اجرا نمی‌شود.'
    }
    $backupPath = Get-RecoveryPath (Join-Path $bundle 'database.bak')
    if ((Get-FileHash -LiteralPath $backupPath).Hash -ne $manifest.BackupSha256) { throw 'hash فایل SQL backup معتبر نیست.' }
    [void](Test-RecoveryFiles (Get-RecoveryPath (Join-Path $bundle 'files')) @($manifest.Files) @($manifest.Assets))
    $exists = Invoke-RecoverySql $connection 'master' "SELECT [name] FROM sys.databases WHERE [name] = N'$TargetDatabase';"
    if ($exists.Rows.Count -ne 0) { throw 'دیتابیس مقصد موجود است؛ هر نوع overwrite ممنوع است.' }
    $backupLiteral = ConvertTo-RecoveryLiteral $backupPath
    [void](Invoke-RecoverySql $connection 'master' "RESTORE VERIFYONLY FROM DISK = $backupLiteral WITH CHECKSUM;")
    $fileList = Invoke-RecoverySql $connection 'master' "RESTORE FILELISTONLY FROM DISK = $backupLiteral;"
    $locations = Invoke-RecoverySql $connection 'master' "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)) AS DataPath, CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000)) AS LogPath;"
    $moves = @()
    foreach ($file in $fileList.Rows) {
        if ($file.Type -notin @('D', 'L')) { throw 'نوع فایل SQL نیازمند بررسی DBA است.' }
        $parent = if ($file.Type -eq 'L') { [string]$locations.Rows[0].LogPath } else { [string]$locations.Rows[0].DataPath }
        $target = Get-RecoveryPath (Join-Path $parent ($TargetDatabase + '_' + $file.FileId + $(if ($file.Type -eq 'L') { '.ldf' } else { '.mdf' })))
        if (Test-Path -LiteralPath $target) { throw 'فایل SQL مقصد از قبل موجود است.' }
        $moves += 'MOVE ' + (ConvertTo-RecoveryLiteral $file.LogicalName) + ' TO ' + (ConvertTo-RecoveryLiteral $target)
    }
    if ($moves.Count -eq 0) { throw 'فایل منطقی backup پیدا نشد.' }
    $started = [DateTime]::UtcNow
    $script:RecoveryPhase = 'SQL_RESTORE'
    # عمداً بدون REPLACE، DROP، SINGLE_USER یا rollback کاربران.
    [void](Invoke-RecoverySql $connection 'master' "RESTORE DATABASE [$TargetDatabase] FROM DISK = $backupLiteral WITH CHECKSUM, RECOVERY, $($moves -join ', ');")
    $script:RecoveryPhase = 'DATABASE_INTEGRITY'
    $check = Invoke-RecoverySql $connection 'master' "DBCC CHECKDB ([$TargetDatabase]) WITH NO_INFOMSGS, ALL_ERRORMSGS, TABLERESULTS;"
    if ($check.Rows.Count -gt 0) { throw 'DBCC CHECKDB خطا گزارش کرد.' }
    if (((Get-RecoveryMigrations $connection $TargetDatabase) -join "`n") -cne ($ExpectedMigrations -join "`n")) { throw 'migration history بازیابی‌شده تطبیق ندارد.' }
    $assets = @(Get-RecoveryAssets $connection $TargetDatabase)
    if (($assets | ConvertTo-Json -Depth 5 -Compress) -cne ($manifest.Assets | ConvertTo-Json -Depth 5 -Compress)) { throw 'metadata بازیابی‌شده تطبیق ندارد.' }
    New-RecoveryDirectory $root ''
    $script:RecoveryPhase = 'STORAGE_RESTORE_VERIFY'
    foreach ($file in $manifest.Files) {
        $target = Resolve-RecoveryKey $root $file.Key
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        [IO.File]::Copy((Resolve-RecoveryKey (Join-Path $bundle 'files') $file.Key), $target, $false)
    }
    $integrity = Test-RecoveryFiles $root @($manifest.Files) $assets
    return [pscustomobject]@{
        Result = 'RESTORE_VERIFIED'; StartedAtUtc = $started.ToString('O'); EndedAtUtc = [DateTime]::UtcNow.ToString('O')
        Database = $TargetDatabase; Integrity = $integrity
    }
}

Export-ModuleMember -Function Invoke-MakanBackup, Invoke-MakanRestore, Test-RecoveryFiles, Assert-RecoveryTarget, Resolve-RecoveryKey, Get-RecoveryFiles, Get-RecoveryPhase
