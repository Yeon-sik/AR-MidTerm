param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskStage = Join-Path ([IO.Path]::GetTempPath()) 'vr-midterm-image-probe'
if ($taskStage -match '[^\x00-\x7F]') { throw 'The build path must contain ASCII characters only.' }
$taskStage = [IO.Path]::GetFullPath($taskStage)
$taskOwner = Join-Path $taskStage '.image-probe-build-owner'
if ((Test-Path -LiteralPath $taskStage) -and -not (Test-Path -LiteralPath $taskOwner)) { throw 'Refusing to reuse an unowned temporary directory.' }
if ((Test-Path -LiteralPath $taskOwner) -and (Get-Content -LiteralPath $taskOwner -Raw).Trim() -ne $taskRoot) { throw 'The build directory belongs to another source root.' }
New-Item -ItemType Directory -Path $taskStage -Force | Out-Null
Set-Content -LiteralPath $taskOwner -Value $taskRoot -Encoding UTF8
# 이 스크립트 전용 임시 폴더만 동기화한다. Library는 재빌드 캐시로 보존한다.
foreach ($taskDirectory in @('Assets', 'Packages', 'ProjectSettings', 'ThirdParty')) {
    $taskTarget = [IO.Path]::GetFullPath((Join-Path $taskStage $taskDirectory))
    if (-not $taskTarget.StartsWith($taskStage + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Copy target escaped the dedicated build folder.' }
    & robocopy (Join-Path $taskRoot $taskDirectory) $taskTarget /MIR /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { throw "Source copy failed: $taskDirectory" }
}
$taskArtifacts = Join-Path $taskRoot 'artifacts\image-tracking-probe'
New-Item -ItemType Directory -Path $taskArtifacts -Force | Out-Null
$taskLog = Join-Path $taskArtifacts 'build.log'
$taskArgs = @('-batchmode', '-nographics', '-quit', '-buildTarget', 'Android', '-projectPath', ('"' + $taskStage + '"'), '-executeMethod', 'MidtermAR.Editor.ImageTrackingProbeBuilder.BuildAndroid', '-logFile', ('"' + $taskLog + '"'))
$taskProcess = Start-Process -FilePath $UnityEditor -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit()
if ($taskProcess.ExitCode -ne 0) { throw "Probe build failed ($($taskProcess.ExitCode)). See $taskLog" }
$taskBuiltApk = Join-Path $taskStage 'Builds\Android\ImageTrackingProbe.apk'
if (-not (Test-Path -LiteralPath $taskBuiltApk)) { throw 'No probe APK was produced.' }
$taskOutput = Join-Path $taskRoot 'Builds\Android'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskApk = Join-Path $taskOutput 'ImageTrackingProbe.apk'
Copy-Item -LiteralPath $taskBuiltApk -Destination $taskApk -Force
# 빌드 설정은 복사본에만 적용한다. 새 씬과 참조 에셋·GUID만 원본으로 돌려준다.
foreach ($taskAsset in @('Assets\Scenes\ImageTrackingProbe.unity', 'Assets\Scenes\ImageTrackingProbe.unity.meta', 'Assets\Images\ProbeImageLibrary.asset', 'Assets\Images\ProbeImageLibrary.asset.meta', 'Assets\Images\ProbeCube.mat', 'Assets\Images\ProbeCube.mat.meta')) {
    Copy-Item -LiteralPath (Join-Path $taskStage $taskAsset) -Destination (Join-Path $taskRoot $taskAsset) -Force
}
Get-ChildItem -LiteralPath (Join-Path $taskStage 'Assets') -Filter '*.meta' -Recurse | ForEach-Object {
    $taskRelative = $_.FullName.Substring($taskStage.Length + 1)
    $taskDestination = Join-Path $taskRoot $taskRelative
    if (-not (Test-Path -LiteralPath $taskDestination)) { Copy-Item -LiteralPath $_.FullName -Destination $taskDestination }
}
$taskInputs = foreach ($taskDirectory in @('Assets', 'Packages')) {
    Get-ChildItem -LiteralPath (Join-Path $taskRoot $taskDirectory) -File -Recurse | ForEach-Object {
        $taskRelative = $_.FullName.Substring($taskRoot.Length + 1)
        $taskStageFile = Join-Path $taskStage $taskRelative
        $taskSourceHash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        $taskStageHash = if (Test-Path -LiteralPath $taskStageFile) { (Get-FileHash -LiteralPath $taskStageFile -Algorithm SHA256).Hash } else { '' }
        [ordered]@{ path = $taskRelative; sourceSha256 = $taskSourceHash; buildSha256 = $taskStageHash; matches = $taskSourceHash -eq $taskStageHash }
    }
}
$taskInputs | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskArtifacts 'source-inventory.json') -Encoding UTF8
if (@($taskInputs | Where-Object { -not $_.matches }).Count -gt 0) { throw 'Source/build inputs differ. Inspect source-inventory.json before installation.' }
[ordered]@{
    timestampUtc = [DateTime]::UtcNow.ToString('o'); isolatedBuildRoot = $taskStage; package = 'com.yeonsik.midtermar'
    apk = $taskApk; sha256 = (Get-FileHash -LiteralPath $taskApk -Algorithm SHA256).Hash.ToLowerInvariant()
    sizeBytes = (Get-Item -LiteralPath $taskApk).Length; result = 'BUILD_SUCCEEDED_DEVICE_NOT_VERIFIED'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskArtifacts 'build-summary.json') -Encoding UTF8
Write-Output "Probe APK: $taskApk"
