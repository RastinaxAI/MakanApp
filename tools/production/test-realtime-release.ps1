[CmdletBinding()]
param(
    [ValidateRange(5, 20)][int]$Repetitions = 5,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$OutputDirectory = ('.artifacts/be23-realtime-' + [Guid]::NewGuid().ToString('N'))
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not [IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path $repository $OutputDirectory }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repository '.artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $OutputDirectory.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase) -or
    (Test-Path -LiteralPath $OutputDirectory)) { throw 'مسیر شواهد باید تازه و داخل artifacts خصوصی باشد.' }
[void][IO.Directory]::CreateDirectory($OutputDirectory)

$names = @(
    'PersonalAccountExplicitReplayAndLiveWorkWithoutChangingSelectedTeacherOrganization',
    'OfficialSpacesManagerGetsFirstActivityWithoutOpeningConversationAndReplayHasNoDuplicateEffect',
    'RevokingOneSessionStopsRestHubAndFuturePrivateEventsButKeepsOtherSessionWorking',
    'StabilizationTwoIndependentReceiverSessionsSynchronizeReadWithoutRegression',
    'P2SilentStillPersistsUnreadRealtimeAndInternalNotificationButSuppressesOnlyExternalDelivery',
    'AccountRealtimeExpiredPublisherCannotCompleteRecoveredClaim',
    'ProvisioningLookupValidatesFilterAndRequiresPlatformReadRatherThanOrganizationRole'
)
$filter = ($names | ForEach-Object { 'FullyQualifiedName~' + $_ }) -join '|'
$records = @()
for ($run = 1; $run -le $Repetitions; $run++) {
    $runPath = Join-Path $OutputDirectory ('run-' + $run)
    [void][IO.Directory]::CreateDirectory($runPath)
    $arguments = @('test', (Join-Path $repository 'tests/MakanApp.IntegrationTests/MakanApp.IntegrationTests.csproj'),
        '--configuration', $Configuration, '--no-build', '--no-restore', '--nologo', '--filter', $filter,
        '--logger', 'console;verbosity=minimal', '--logger', 'trx;LogFileName=stress.trx', '--results-directory', $runPath)
    # Process متعلق به همین رانر است؛ stdout/stderr هم‌زمان خوانده می‌شوند و ExitCode از همان handle گرفته می‌شود.
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = 'dotnet'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WorkingDirectory = $repository
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Arguments = ($arguments | ForEach-Object {
        '"' + [regex]::Replace([regex]::Replace($_, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
    }) -join ' '
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    $started = [DateTime]::UtcNow
    try {
        [void]$process.Start()
        $processId = $process.Id
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $exitCode = $process.ExitCode
        [IO.File]::WriteAllText((Join-Path $runPath 'stdout.log'), $stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $runPath 'stderr.log'), $stderr.GetAwaiter().GetResult())
    }
    finally { $process.Dispose() }
    $ended = [DateTime]::UtcNow
    $trxPath = Join-Path $runPath 'stress.trx'
    if (-not (Test-Path -LiteralPath $trxPath)) { throw 'خروجی TRX ایجاد نشد؛ اجرای بدون شاهد موفق محسوب نمی‌شود.' }
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    $cases = @($trx.TestRun.Results.UnitTestResult | ForEach-Object {
        $failureType = if ($_.outcome -eq 'Passed') { $null } else { 'TEST_FAILURE_SEE_PRIVATE_TRX' }
        [pscustomobject]@{ Test=$_.testName; Duration=$_.duration; Outcome=$_.outcome; StartUtc=$_.startTime; FailureType=$failureType }
    })
    $records += [pscustomobject]@{
        Run=$run; ProcessId=$processId; ExitCode=$exitCode; StartUtc=$started.ToString('o'); EndUtc=$ended.ToString('o')
        ElapsedSeconds=($ended-$started).TotalSeconds; DatabaseFixture='LocalDB:MakanApp_MessagingSafety_IntegrationTests_Step7E'
        Total=[int]$counters.total; Passed=[int]$counters.passed; Failed=[int]$counters.failed; Skipped=[int]$counters.notExecuted; Cases=$cases
    }
    $records | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'runs.json') -Encoding UTF8
    Write-Output ('REALTIME_RUN=' + $run + '; TOTAL=' + $counters.total + '; FAILED=' + $counters.failed + '; EXIT=' + $exitCode)
}
# شکست هیچ دوری با موفقیت دور بعد مخفی نمی‌شود؛ همهٔ شواهد و شمارها حفظ می‌شوند.
if (@($records | Where-Object { $_.ExitCode -ne 0 -or $_.Total -ne 8 -or $_.Passed -ne 8 -or $_.Failed -ne 0 -or $_.Skipped -ne 0 }).Count -ne 0) {
    throw 'حداقل یک اجرای Realtime/Lookup ناموفق یا فاقد کشف کامل آزمون بود.'
}
Write-Output ('REALTIME_STRESS_PASS; RUNS=' + $Repetitions)
