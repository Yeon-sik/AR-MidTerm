param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe',
    [string]$ReuseStage,
    [switch]$Release,
    [switch]$RegistrationProbe
)

$ErrorActionPreference = 'Stop'
if ($Release -and $RegistrationProbe) { throw 'RegistrationProbe is a separate development-only build, not a release.' }
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
$taskStage = if ($ReuseStage) { [IO.Path]::GetFullPath($ReuseStage).TrimEnd('\') }
    else { Join-Path $taskTempRoot ('vr-midterm-build-' + [Guid]::NewGuid().ToString('N')) }
if (-not $taskStage.StartsWith($taskTempRoot, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $taskStage) -notmatch '^vr-midterm-build-[a-f0-9]{32}$') { throw 'Build stage must be a dedicated Product AR temporary directory.' }
if ($ReuseStage -and (-not (Test-Path -LiteralPath (Join-Path $taskStage 'Library')) -or ((Get-Item -LiteralPath $taskStage).Attributes -band [IO.FileAttributes]::ReparsePoint))) { throw 'Invalid reusable build stage.' }
if ($taskStage -match '[^\x00-\x7F]') { throw 'The temporary build directory must have an ASCII-only absolute path.' }
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw 'Unity editor executable was not found.' }
if (-not (Test-Path -LiteralPath (Join-Path $taskRoot 'Assets\Scenes\ProductAR.unity'))) { throw 'Run Product AR / Prepare Project and Scene in Unity first.' }
New-Item -ItemType Directory -Path $taskStage -Force | Out-Null
foreach ($taskDirectory in @('Assets', 'Packages', 'ProjectSettings', 'ThirdParty')) {
    $taskSource = Join-Path $taskRoot $taskDirectory
    if (Test-Path -LiteralPath $taskSource) {
        $taskDestination = [IO.Path]::GetFullPath((Join-Path $taskStage $taskDirectory))
        if (-not $taskDestination.StartsWith($taskStage + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe stage destination.' }
        if ((Test-Path -LiteralPath $taskDestination) -and ((Get-Item -LiteralPath $taskDestination).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Stage input must not be a junction.' }
        if ($ReuseStage) {
            # 검증한 임시 입력 폴더만 동기화한다. Library 캐시와 원본 저장소는 삭제하지 않는다.
            & robocopy $taskSource $taskDestination /MIR /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Stage synchronization failed: ' + $taskDirectory) }
        }
        else { Copy-Item -LiteralPath $taskSource -Destination $taskDestination -Recurse }
    }
}
$taskArtifacts = Join-Path $taskRoot 'artifacts'
New-Item -ItemType Directory -Path $taskArtifacts -Force | Out-Null
$taskLog = Join-Path $taskArtifacts $(if ($RegistrationProbe) { 'registration-probe-build.log' } else { 'android-build-ascii.log' })
$taskMethod = if ($RegistrationProbe) { 'MidtermAR.Editor.RuntimeRegistrationProbeBuilder.BuildAndroid' }
    elseif ($Release) { 'MidtermAR.Editor.ProjectBuilder.BuildAndroidRelease' } else { 'MidtermAR.Editor.ProjectBuilder.BuildAndroid' }
$taskArgs = @('-batchmode', '-nographics', '-quit', '-buildTarget', 'Android', '-projectPath', ('"' + $taskStage + '"'), '-executeMethod', $taskMethod, '-logFile', ('"' + $taskLog + '"'))
Write-Output "Building isolated source copy: $taskStage"
$taskProcess = Start-Process -FilePath $UnityEditor -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit()
if ($taskProcess.ExitCode -ne 0) { throw "Unity build failed ($($taskProcess.ExitCode)). Log: $taskLog. Isolated copy retained: $taskStage" }
$taskApkName = if ($RegistrationProbe) { 'RegistrationProbe.apk' } else { 'ProductAR.apk' }
$taskBuiltApk = Join-Path $taskStage ('Builds\Android\' + $taskApkName)
if (-not (Test-Path -LiteralPath $taskBuiltApk -PathType Leaf)) { throw "No APK was produced. Isolated copy retained: $taskStage" }
$taskOutput = Join-Path $taskRoot 'Builds\Android'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskApk = Join-Path $taskOutput $taskApkName
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
    buildType = if ($RegistrationProbe) { 'registration-probe-development' } elseif ($Release) { 'release' } else { 'development' }
}
$taskSummaryName = if ($RegistrationProbe) { 'registration-probe-build-summary.json' } else { 'android-build-summary.json' }
$taskRecord | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskArtifacts $taskSummaryName) -Encoding UTF8
Write-Output "APK: $taskApk"
Write-Output "SHA256: $($taskRecord.sha256)"
Write-Output "Temporary build copy retained for diagnosis: $taskStage"
