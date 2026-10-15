[CmdletBinding()]
param([switch]$ConfirmLocalDisposableDatabase)

$ErrorActionPreference = 'Stop'
if (-not $ConfirmLocalDisposableDatabase) { throw 'اجرای تست SQL ایزوله به تأیید صریح نیاز دارد.' }
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$outputDirectory = Join-Path $repositoryRoot ('.artifacts/deployment/sql-regression-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($outputDirectory)
$databases = New-Object 'System.Collections.Generic.List[string]'
$checks = New-Object 'System.Collections.Generic.List[string]'
$baselineId = '20261007075650_AddExamGradingAndRelease'
$ownerId = [Guid]::NewGuid().ToString('D')
$adminId = [Guid]::NewGuid().ToString('D')
$conversationId = [Guid]::NewGuid().ToString('D')
$ownerParticipantId = [Guid]::NewGuid().ToString('D')
$messageId = [Guid]::NewGuid().ToString('D')

function New-TestConnection([string]$DatabaseName) {
    if ($DatabaseName -ne 'master' -and $DatabaseName -notmatch '^MakanApp_SqlScript_[a-f0-9]{32}$') { throw 'مقصد تست مجاز نیست.' }
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = "Server=(localdb)\MSSQLLocalDB;Database=$DatabaseName;Integrated Security=True;Encrypt=False;Pooling=False"
    return $connection
}

function Invoke-TestSql([string]$DatabaseName, [string]$Sql, [switch]$Scalar) {
    $connection = New-TestConnection $DatabaseName
    $command = $null
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 300
        $command.CommandText = $Sql
        if ($Scalar) { return $command.ExecuteScalar() }
        [void]$command.ExecuteNonQuery()
    }
    finally { if ($null -ne $command) { $command.Dispose() }; $connection.Dispose() }
}

function New-TestDatabase {
    $name = 'MakanApp_SqlScript_' + [Guid]::NewGuid().ToString('N')
    Invoke-TestSql 'master' "IF DB_ID(N'$name') IS NOT NULL THROW 51002, N'Database already exists', 1; CREATE DATABASE [$name];"
    $databases.Add($name)
    return $name
}

function Assert-Equal($Expected, $Actual, [string]$Label) {
    if ($Expected -ne $Actual) { throw "Assertion failed: $Label; expected=$Expected; actual=$Actual" }
}

function Assert-SqlFailure([string]$DatabaseName, [string]$Sql, [int]$ErrorNumber) {
    $sqlException = $null
    try { Invoke-TestSql $DatabaseName $Sql }
    catch {
        $exception = $_.Exception
        while ($null -ne $exception) {
            if ($exception -is [System.Data.SqlClient.SqlException]) { $sqlException = $exception; break }
            $exception = $exception.InnerException
        }
    }
    if ($null -eq $sqlException -or $ErrorNumber -notin @($sqlException.Errors | ForEach-Object { $_.Number })) {
        throw "Expected SQL error $ErrorNumber was not observed."
    }
}

function Assert-History([string]$DatabaseName, [int]$Count, [string]$LastMigration) {
    Assert-Equal $Count (Invoke-TestSql $DatabaseName 'SELECT COUNT(*) FROM dbo.__EFMigrationsHistory' -Scalar) 'migration count'
    Assert-Equal $LastMigration (Invoke-TestSql $DatabaseName 'SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory' -Scalar) 'last migration'
}

function Add-LegacyData([string]$DatabaseName) {
    Invoke-TestSql $DatabaseName @"
INSERT INTO [identity].[Users] (Id, CreatedAtUtc) VALUES ('$ownerId', SYSUTCDATETIME()), ('$adminId', SYSUTCDATETIME());
INSERT INTO messaging.Conversations
    (Id, Type, Scope, NextMessageSequence, NextChangeSequence, Status, CreatedAtUtc, ManagementPolicy, Title, CreatedByUserId, ClientOperationId, CreationPayloadHash)
VALUES ('$conversationId', 2, 1, 2, 1, 1, SYSUTCDATETIME(), 1, N'legacy-script-test', '$ownerId', NEWID(), CONVERT(binary(32), 0x01));
INSERT INTO messaging.ConversationParticipants (Id, ConversationId, UserId, Status, JoinedAtUtc, Role)
VALUES ('$ownerParticipantId', '$conversationId', '$ownerId', 1, SYSUTCDATETIME(), 1),
       (NEWID(), '$conversationId', '$adminId', 1, SYSUTCDATETIME(), 2);
INSERT INTO messaging.Messages (Id, ConversationId, SenderUserId, SenderParticipantId, ClientMessageId, Sequence, Kind, Text, SearchText, SentAtUtc, CurrentRevisionNumber)
VALUES ('$messageId', '$conversationId', '$ownerId', '$ownerParticipantId', NEWID(), 1, 1, N'legacy-message-preserved', N'legacy-message-preserved', SYSUTCDATETIME(), 1);
"@
}

function Assert-LegacyData([string]$DatabaseName) {
    Assert-Equal 'legacy-message-preserved' (Invoke-TestSql $DatabaseName "SELECT Text FROM messaging.Messages WHERE Id='$messageId'" -Scalar) 'message preserved'
    Assert-Equal '1:1:3' (Invoke-TestSql $DatabaseName "SELECT CONCAT(Visibility, ':', GroupHistoryPolicy, ':', MemberCapabilities) FROM messaging.Conversations WHERE Id='$conversationId'" -Scalar) 'conversation backfill'
    Assert-Equal 51 (Invoke-TestSql $DatabaseName "SELECT AdminPermissions FROM messaging.ConversationParticipants WHERE ConversationId='$conversationId' AND UserId='$adminId'" -Scalar) 'admin permissions backfill'
    Assert-Equal 2 (Invoke-TestSql $DatabaseName "SELECT COUNT(*) FROM messaging.ConversationParticipants WHERE ConversationId='$conversationId' AND HistoryVisibleFromSequence=1" -Scalar) 'history backfill'
}

try {
    $exporter = Join-Path $PSScriptRoot 'export-migration-sql.ps1'
    $fullMetadata = (& $exporter -NoBuild -OutputPath (Join-Path $outputDirectory 'full-safe.sql')) | ConvertFrom-Json
    $baselineMetadata = (& $exporter -NoBuild -ToMigration $baselineId -OutputPath (Join-Path $outputDirectory 'baseline-safe.sql')) | ConvertFrom-Json
    $fullSql = [IO.File]::ReadAllText($fullMetadata.Output)
    $baselineSql = [IO.File]::ReadAllText($baselineMetadata.Output)
    $rawSql = [IO.File]::ReadAllText($fullMetadata.RawEfOutput)

    $fresh = New-TestDatabase
    Invoke-TestSql $fresh $fullSql
    Assert-History $fresh $fullMetadata.MigrationCount $fullMetadata.LastMigration
    $checks.Add('fresh_install')
    Invoke-TestSql $fresh $fullSql
    Assert-History $fresh $fullMetadata.MigrationCount $fullMetadata.LastMigration
    $checks.Add('already_current_retry')

    $upgrade = New-TestDatabase
    Invoke-TestSql $upgrade $baselineSql
    Assert-History $upgrade 20 $baselineId
    Add-LegacyData $upgrade
    foreach ($batch in [regex]::Split($rawSql, '(?m)^[ \t]*GO[ \t]*\r?$')) {
        if ($batch -match 'BEGIN TRANSACTION;') { Assert-SqlFailure $upgrade $batch 207 }
        elseif (-not [string]::IsNullOrWhiteSpace($batch)) { Invoke-TestSql $upgrade $batch }
    }
    Assert-History $upgrade 20 $baselineId
    $checks.Add('original_error_207_reproduced')
    Invoke-TestSql $upgrade $fullSql
    Assert-History $upgrade $fullMetadata.MigrationCount $fullMetadata.LastMigration
    Assert-LegacyData $upgrade
    $checks.Add('upgrade_from_exam_grading_with_existing_data')
    $messageVersion = Invoke-TestSql $upgrade "SELECT CONVERT(varchar(100), RowVersion, 1) FROM messaging.Messages WHERE Id='$messageId'" -Scalar
    Invoke-TestSql $upgrade $fullSql
    Assert-LegacyData $upgrade
    Assert-Equal $messageVersion (Invoke-TestSql $upgrade "SELECT CONVERT(varchar(100), RowVersion, 1) FROM messaging.Messages WHERE Id='$messageId'" -Scalar) 'retry must not rewrite message'
    $checks.Add('upgrade_retry_preserves_data')

    $failure = New-TestDatabase
    Invoke-TestSql $failure $baselineSql
    Add-LegacyData $failure
    Assert-Equal 1 ([regex]::Matches($fullSql, 'COMMIT;').Count) 'single owned transaction'
    $failingSql = $fullSql.Replace('COMMIT;', "THROW 51099, N''INJECTED_TEST_FAILURE'', 1; COMMIT;")
    Assert-SqlFailure $failure $failingSql 51099
    Assert-History $failure 20 $baselineId
    Assert-Equal 0 (Invoke-TestSql $failure "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'messaging.Conversations') AND name=N'Visibility'" -Scalar) 'failed upgrade must rollback DDL'
    Assert-Equal 'legacy-message-preserved' (Invoke-TestSql $failure "SELECT Text FROM messaging.Messages WHERE Id='$messageId'" -Scalar) 'rollback preserves message'
    $checks.Add('runtime_failure_rolls_back_pending_migrations')

    $connection = New-TestConnection $upgrade
    try {
        $connection.Open()
        $transaction = $connection.BeginTransaction()
        try {
            $command = $connection.CreateCommand()
            try {
                $command.Transaction = $transaction
                $command.CommandText = $fullSql
                try { [void]$command.ExecuteNonQuery(); throw 'Expected ambient-transaction rejection' }
                catch [System.Data.SqlClient.SqlException] { Assert-Equal 50000 $_.Exception.Number 'ambient transaction rejected' }
                $command.CommandText = 'SELECT @@TRANCOUNT'
                Assert-Equal 1 $command.ExecuteScalar() 'caller transaction retained'
            }
            finally { $command.Dispose() }
        }
        finally { $transaction.Rollback(); $transaction.Dispose() }
    }
    finally { $connection.Dispose() }
    $checks.Add('ambient_transaction_rejected_without_rollback')

    $report = [ordered]@{ Passed=$checks.Count; Failed=0; Checks=@($checks); Databases=@($databases); Retained=$true; Target='LocalDB-only'; OutputDirectory=$outputDirectory; ScriptSha256=$fullMetadata.Sha256; MigrationCount=$fullMetadata.MigrationCount; LastMigration=$fullMetadata.LastMigration }
    $json = $report | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText((Join-Path $outputDirectory 'test-report.json'), $json, (New-Object Text.UTF8Encoding($true)))
    $json
}
catch {
    [ordered]@{ Passed=$checks.Count; Failed=1; Checks=@($checks); Databases=@($databases); Retained=$true; Target='LocalDB-only'; Failure=$_.Exception.Message } | ConvertTo-Json -Depth 5
    throw
}
