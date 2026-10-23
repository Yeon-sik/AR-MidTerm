param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskStage = Join-Path ([IO.Path]::GetTempPath()) ('vr-midterm-build-' + [Guid]::NewGuid().ToString('N'))
if ($taskStage -match '[^\x00-\x7F]') { throw 'The temporary build directory must have an ASCII-only absolute path.' }
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw 'Unity editor executable was not found.' }
if (-not (Test-Path -LiteralPath (Join-Path $taskRoot 'Assets\Scenes\ProductAR.unity'))) { throw 'Run Product AR / Prepare Project and Scene in Unity first.' }
New-Item -ItemType Directory -Path $taskStage | Out-Null
foreach ($taskDirectory in @('Assets', 'Packages', 'ProjectSettings', 'ThirdParty')) {
    $taskSource = Join-Path $taskRoot $taskDirectory
    if (Test-Path -LiteralPath $taskSource) { Copy-Item -LiteralPath $taskSource -Destination (Join-Path $taskStage $taskDirectory) -Recurse }
}
$taskArtifacts = Join-Path $taskRoot 'artifacts'
New-Item -ItemType Directory -Path $taskArtifacts -Force | Out-Null
$taskLog = Join-Path $taskArtifacts 'android-build-ascii.log'
$taskArgs = @('-batchmode', '-nographics', '-quit', '-buildTarget', 'Android', '-projectPath', ('"' + $taskStage + '"'), '-executeMethod', 'MidtermAR.Editor.ProjectBuilder.BuildAndroid', '-logFile', ('"' + $taskLog + '"'))
Write-Output "Building isolated source copy: $taskStage"
$taskProcess = Start-Process -FilePath $UnityEditor -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit()
if ($taskProcess.ExitCode -ne 0) { throw "Unity build failed ($($taskProcess.ExitCode)). Log: $taskLog. Isolated copy retained: $taskStage" }
$taskBuiltApk = Join-Path $taskStage 'Builds\Android\ProductAR.apk'
if (-not (Test-Path -LiteralPath $taskBuiltApk -PathType Leaf)) { throw "No APK was produced. Isolated copy retained: $taskStage" }
$taskOutput = Join-Path $taskRoot 'Builds\Android'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskApk = Join-Path $taskOutput 'ProductAR.apk'
Copy-Item -LiteralPath $taskBuiltApk -Destination $taskApk -Force
$taskRecord = [ordered]@{
    timestampUtc = [DateTime]::UtcNow.ToString('o')
    sourceRoot = $taskRoot
    isolatedBuildRoot = $taskStage
    unity = $UnityEditor
    apk = $taskApk
    sha256 = (Get-FileHash -LiteralPath $taskApk -Algorithm SHA256).Hash.ToLowerInvariant()
    sizeBytes = (Get-Item -LiteralPath $taskApk).Length
    result = 'BUILD_SUCCEEDED_DEVICE_NOT_VERIFIED'
}
$taskRecord | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskArtifacts 'android-build-summary.json') -Encoding UTF8
Write-Output "APK: $taskApk"
Write-Output "SHA256: $($taskRecord.sha256)"
Write-Output "Temporary build copy retained for diagnosis: $taskStage"
