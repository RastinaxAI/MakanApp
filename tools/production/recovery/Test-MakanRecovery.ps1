$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'MakanRecovery.psm1') -Force
$checks = 0
function Assert-Rejected([scriptblock]$Action) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw 'عملیات ناامن رد نشد.' }
    $script:checks++
}

foreach ($name in @('MakanApp', 'master', 'MakanApp_Recovery_', 'MakanApp_Recovery_abc;DROP', 'Production')) {
    Assert-Rejected { Assert-RecoveryTarget $name }
}
Assert-RecoveryTarget 'MakanApp_Recovery_Test_12345678'
$checks++
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('MakanApp_RecoveryToolTests_' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory((Join-Path $testRoot 'year\month'))
[IO.File]::WriteAllText((Join-Path $testRoot 'year\month\sample.txt'), 'synthetic recovery test')
foreach ($key in @('../escape', '/absolute', 'C:/escape', 'a\escape', 'a/../escape', 'a//b', 'a/NUL.txt', 'a/alias.', 'a/alias ', 'a/*')) {
    Assert-Rejected { Resolve-RecoveryKey $testRoot $key }
}
$files = @(Get-RecoveryFiles $testRoot)
if ($files.Count -ne 1 -or $files[0].Key -ne 'year/month/sample.txt') { throw 'مسیر nested snapshot صحیح نیست.' }
$checks++
$asset = [pscustomobject]@{ Key = $files[0].Key; Status = 2; Size = $files[0].Size; Hash = $files[0].Hash }
$result = Test-RecoveryFiles $testRoot $files @($asset)
if ($result.Files -ne 1 -or $result.UnreferencedFiles -ne 0) { throw 'تطبیق صحیح فایل رد شد.' }
$checks++
Assert-Rejected { Test-RecoveryFiles $testRoot @() @($asset) }
$missing = [pscustomobject]@{ Key = 'missing.txt'; Status = 2; Size = 1; Hash = ('a' * 64) }
Assert-Rejected { Test-RecoveryFiles $testRoot $files @($missing) }
$badHash = [pscustomobject]@{ Key = $files[0].Key; Size = $files[0].Size; Hash = ('b' * 64) }
Assert-Rejected { Test-RecoveryFiles $testRoot @($badHash) @($asset) }
$badSize = [pscustomobject]@{ Key = $files[0].Key; Size = 99; Hash = $files[0].Hash }
Assert-Rejected { Test-RecoveryFiles $testRoot @($badSize) @($asset) }
$result = Test-RecoveryFiles $testRoot $files @()
if ($result.UnreferencedFiles -ne 1 -or -not (Test-Path -LiteralPath (Join-Path $testRoot $files[0].Key))) { throw 'orphan باید حفظ شود.' }
$checks++
Assert-Rejected { Invoke-MakanBackup -ConnectionEnvironmentVariable 'UNSET_RECOVERY_TEST_ONLY' -StorageRoot $testRoot -BundlePath ($testRoot + '_backup') -ApplicationRoot $PSScriptRoot }
Write-Output "RECOVERY_TOOL_TESTS_PASSED=$checks"
# فایل‌های مصنوعی کوچک حفظ می‌شوند؛ ابزار بازیابی مسئول حذف داده نیست.
