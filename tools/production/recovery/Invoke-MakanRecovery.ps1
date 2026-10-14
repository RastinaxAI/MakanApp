[CmdletBinding(DefaultParameterSetName = 'Backup')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Backup')][switch]$Backup,
    [Parameter(Mandatory, ParameterSetName = 'Restore')][switch]$Restore,
    [Parameter(Mandatory)][string]$ConnectionEnvironmentVariable,
    [Parameter(Mandatory)][string]$BundlePath,
    [Parameter(Mandatory)][string]$ApplicationRoot,
    [Parameter(Mandatory, ParameterSetName = 'Backup')][string]$StorageRoot,
    [Parameter(Mandatory, ParameterSetName = 'Backup')][switch]$WritersStopped,
    [Parameter(ParameterSetName = 'Backup')][string]$SqlServiceIdentity,
    [Parameter(Mandatory, ParameterSetName = 'Restore')][string]$TargetDatabase,
    [Parameter(Mandatory, ParameterSetName = 'Restore')][string]$TargetStorageRoot,
    [Parameter(Mandatory, ParameterSetName = 'Restore')][string]$ExpectedManifestHash,
    [Parameter(Mandatory, ParameterSetName = 'Restore')][string]$MigrationListPath
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'MakanRecovery.psm1') -Force
try {
    if ($Backup) {
        Invoke-MakanBackup -ConnectionEnvironmentVariable $ConnectionEnvironmentVariable `
            -StorageRoot $StorageRoot -BundlePath $BundlePath -ApplicationRoot $ApplicationRoot `
            -WritersStopped:$WritersStopped -SqlServiceIdentity $SqlServiceIdentity | ConvertTo-Json -Depth 5
    }
    else {
        $migrations = @(Get-Content -LiteralPath $MigrationListPath -Encoding UTF8 | Where-Object { $_.Trim() })
        Invoke-MakanRestore -ConnectionEnvironmentVariable $ConnectionEnvironmentVariable `
            -TargetDatabase $TargetDatabase -TargetStorageRoot $TargetStorageRoot -BundlePath $BundlePath `
            -ApplicationRoot $ApplicationRoot -ExpectedManifestHash $ExpectedManifestHash `
            -ExpectedMigrations $migrations | ConvertTo-Json -Depth 5
    }
}
catch {
    # جزئیات provider و connection string به خروجی مشترک منتقل نشود.
    [Console]::Error.WriteLine('RECOVERY_FAILED; PHASE=' + (Get-RecoveryPhase))
    exit 1
}
