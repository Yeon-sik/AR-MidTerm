param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe',
    [string]$TestFilter
)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskRunId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$taskOutput = Join-Path $taskRoot ('artifacts\editmode\' + $taskRunId)
$taskBackup = Join-Path $taskOutput 'preserved-inputs'
New-Item -ItemType Directory -Path $taskBackup -Force | Out-Null
$taskPreserve = @(
    'Assets\Scenes\ImageTrackingProbe.unity', 'Assets\Scenes\ImageTrackingProbe.unity.meta',
    'Assets\Images\ProbeImageLibrary.asset', 'Assets\Images\ProbeImageLibrary.asset.meta',
    'Assets\Fonts\Generated\NotoSansKR SDF.asset', 'Assets\Fonts\Generated\NotoSansKR SDF.asset.meta'
)
$taskPreserve += @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'ProjectSettings') -File -Recurse | ForEach-Object { $_.FullName.Substring($taskRoot.Length + 1) })
foreach ($taskRelative in $taskPreserve) {
    $taskCopy = Join-Path $taskBackup $taskRelative
    New-Item -ItemType Directory -Path (Split-Path -Parent $taskCopy) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $taskRoot $taskRelative) -Destination $taskCopy
}
$taskResults = Join-Path $taskOutput 'results.xml'
$taskLog = Join-Path $taskOutput 'editor.log'
# EditMode compiles and executes code inside the Editor. No BuildPlayer, player test or APK generation.
$taskArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $taskRoot + '"'),
    '-runTests', '-testPlatform', 'EditMode', '-assemblyNames', 'MidtermAR.Tests',
    '-testResults', ('"' + $taskResults + '"'), '-logFile', ('"' + $taskLog + '"'))
if ($TestFilter) { $taskArguments += @('-testFilter', $TestFilter) }
try {
    $taskProcess = Start-Process -FilePath $UnityEditor -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
    $taskProcess.WaitForExit()
}
finally {
    # 기존 probe 생성 시험과 동적 폰트 검사는 보관된 원본 에셋에 영향을 남기지 않는다.
    foreach ($taskRelative in $taskPreserve) {
        $taskOriginal = Join-Path $taskBackup $taskRelative
        $taskDestination = Join-Path $taskRoot $taskRelative
        if ((Get-FileHash -LiteralPath $taskOriginal -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash) {
            Copy-Item -LiteralPath $taskOriginal -Destination $taskDestination -Force
        }
    }
}
if (-not (Test-Path -LiteralPath $taskResults)) { throw "EditMode results missing (Unity exit: $($taskProcess.ExitCode)). Inspect $taskLog" }
[xml]$taskXml = Get-Content -LiteralPath $taskResults -Raw -Encoding utf8
$taskRun = $taskXml.'test-run'
$taskSummary = [ordered]@{
    timestampUtc = [DateTime]::UtcNow.ToString('o'); unityExitCode = $taskProcess.ExitCode
    result = $taskRun.result; total = [int]$taskRun.total; passed = [int]$taskRun.passed
    failed = [int]$taskRun.failed; skipped = [int]$taskRun.skipped
    playerBuildRequested = $false; results = $taskResults; log = $taskLog
}
$taskSummary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'summary.json') -Encoding utf8
$taskSummary | ConvertTo-Json
if ($taskProcess.ExitCode -ne 0 -or $taskRun.result -ne 'Passed') { throw 'EditMode functional verification failed.' }
