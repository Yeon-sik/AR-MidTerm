param(
    [Parameter(Mandatory = $true)][string]$DeviceSerial,
    [string]$ApkPath,
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $ApkPath) { $ApkPath = Join-Path $taskRoot 'Builds\Android\ProductAR.apk' }
$taskSdk = 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK'
$taskAdb = Join-Path $taskSdk 'platform-tools\adb.exe'
$taskSigner = Join-Path $taskSdk 'build-tools\36.0.0\apksigner.bat'
$taskAapt = Join-Path $taskSdk 'build-tools\36.0.0\aapt.exe'
$taskPackage = 'com.yeonsik.midtermar'
$taskArtifacts = Join-Path $taskRoot 'artifacts\install'
if (-not (Test-Path -LiteralPath $ApkPath -PathType Leaf)) { throw 'APK not found. Build the Android app first.' }
$taskInstallStage = Join-Path ([IO.Path]::GetTempPath()) ('vr-midterm-install-' + [Guid]::NewGuid().ToString('N'))
if ($taskInstallStage -match '[^\x00-\x7F]') { throw 'APK verification requires an ASCII-only temporary path.' }
New-Item -ItemType Directory -Path $taskInstallStage | Out-Null
$taskOriginalApk = $ApkPath
$ApkPath = Join-Path $taskInstallStage 'ProductAR.apk'
Copy-Item -LiteralPath $taskOriginalApk -Destination $ApkPath
New-Item -ItemType Directory -Path $taskArtifacts -Force | Out-Null
$taskBadging = & $taskAapt dump badging $ApkPath
if ($LASTEXITCODE -ne 0) { throw 'Could not read APK identity.' }
$taskIdentity = [regex]::Match(($taskBadging -join "`n"), "package: name='([^']+)' versionCode='(\d+)'")
if (-not $taskIdentity.Success -or $taskIdentity.Groups[1].Value -ne $taskPackage) { throw 'APK package identity does not match Product AR.' }
$taskNewVersion = [long]$taskIdentity.Groups[2].Value
$taskNewSigning = & $taskSigner verify --print-certs $ApkPath
if ($LASTEXITCODE -ne 0) { throw 'APK signing verification failed.' }
$taskNewDigest = [regex]::Match(($taskNewSigning -join "`n"), 'certificate SHA-256 digest: ([a-fA-F0-9]+)').Groups[1].Value
if (-not $taskNewDigest) { throw 'APK signing certificate could not be read.' }
$taskPackages = & $taskAdb -s $DeviceSerial shell pm list packages --user 0 $taskPackage
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect installed package. Nothing was installed.' }
$taskExisting = @()
if ($taskPackages -contains ('package:' + $taskPackage)) {
    $taskExisting = & $taskAdb -s $DeviceSerial shell pm path --user 0 $taskPackage
    if ($LASTEXITCODE -ne 0) { throw 'Could not read installed APK path. Nothing was installed.' }
}
$taskMode = 'new-install'
if ($taskExisting -match '^package:') {
    $taskRemote = ($taskExisting | Where-Object { $_ -match '^package:.*base\.apk' } | Select-Object -First 1) -replace '^package:', ''
    if (-not $taskRemote) { throw 'Installed base APK path is unknown. Nothing was installed.' }
    $taskBaseline = Join-Path $taskInstallStage 'installed-baseline.apk'
    & $taskAdb -s $DeviceSerial pull $taskRemote.Trim() $taskBaseline
    if ($LASTEXITCODE -ne 0) { throw 'Could not preserve installed signing baseline.' }
    Copy-Item -LiteralPath $taskBaseline -Destination (Join-Path $taskArtifacts 'installed-baseline.apk') -Force
    $taskOldSigning = & $taskSigner verify --print-certs $taskBaseline
    if ($LASTEXITCODE -ne 0) { throw 'Installed signing verification failed.' }
    $taskOldDigest = [regex]::Match(($taskOldSigning -join "`n"), 'certificate SHA-256 digest: ([a-fA-F0-9]+)').Groups[1].Value
    if (-not $taskOldDigest -or $taskOldDigest -ne $taskNewDigest) { throw 'Signing certificates differ. Refusing an incompatible update.' }
    $taskOldBadging = & $taskAapt dump badging $taskBaseline
    $taskOldVersion = [regex]::Match(($taskOldBadging -join "`n"), "versionCode='(\d+)'").Groups[1].Value
    if (-not $taskOldVersion -or $taskNewVersion -lt [long]$taskOldVersion) { throw 'Refusing an unknown version or downgrade.' }
    $taskMode = 'compatible-update-r'
}
& $taskAdb -s $DeviceSerial install --user 0 -r $ApkPath
if ($LASTEXITCODE -ne 0) { throw 'ADB install failed. No uninstall or data reset was attempted.' }
[ordered]@{
    timestampUtc = [DateTime]::UtcNow.ToString('o')
    package = $taskPackage
    versionCode = $taskNewVersion
    mode = $taskMode
    signerSha256 = $taskNewDigest
    apkSha256 = (Get-FileHash -LiteralPath $ApkPath -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskArtifacts 'result.json') -Encoding UTF8
if ($Launch) {
    & $taskAdb -s $DeviceSerial shell pm grant --user 0 $taskPackage android.permission.CAMERA
    if ($LASTEXITCODE -ne 0) { throw 'Camera permission could not be granted. App remains installed.' }
    & $taskAdb -s $DeviceSerial shell am start --user 0 -W -n ($taskPackage + '/com.unity3d.player.UnityPlayerActivity')
    if ($LASTEXITCODE -ne 0) { throw 'Installed app could not be launched.' }
}
Write-Output "Installed $taskPackage ($taskMode); versionCode=$taskNewVersion"
