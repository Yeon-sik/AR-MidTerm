param(
    [string]$SourceRef = 'HEAD',
    [string]$OutputPath,
    [string]$VerifyArchive
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskFolders = @('Assets', 'Packages', 'ProjectSettings', 'docs', 'scripts', 'ThirdParty')
$taskRootFiles = @('README.md', 'PRODUCT.md', 'DESIGN.md', '.gitignore', '.gitattributes', 'SUBMISSION-MANIFEST.json')

function Test-ExcludedSubmissionPath([string]$EntryPath) {
    $taskName = $EntryPath.Replace('\', '/')
    return $taskName -match '(^|/)(Library|Temp|Logs|obj|Builds|artifacts|UserSettings|\.git|\.aws|\.codex|\.serena)(/|$)' `
        -or $taskName -match '(^|/)\.env([^/]*$)|\.local\.json(\.meta)?$|\.(keystore|jks|p12|pfx|pem|key|apk|aab|log)(\.meta)?$'
}

function Test-SourceArchive([string]$ArchivePath, [string]$ExpectedCommit) {
    $taskZip = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $taskManifestEntry = $taskZip.GetEntry('SUBMISSION-MANIFEST.json')
        if (-not $taskManifestEntry) { throw 'Submission manifest missing.' }
        $taskSeen = @{}
        $taskCount = 0
        $taskManifest = $null
        $taskPatterns = @(
            'sb_(publishable|secret)_[A-Za-z0-9_-]{20,}',
            'sk-(proj-)?[A-Za-z0-9_-]{20,}',
            '(gh[pousr]_|github_pat_)[A-Za-z0-9_]{20,}',
            'eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}',
            '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----'
        )
        foreach ($taskEntry in $taskZip.Entries) {
            $taskName = $taskEntry.FullName.Replace('\', '/')
            if ($taskName.StartsWith('/') -or $taskName -match '(^|/)\.\.(/|$)|^[A-Za-z]:' -or (Test-ExcludedSubmissionPath $taskName)) {
                throw ('Excluded or unsafe archive path: ' + $taskName)
            }
            $taskTop = ($taskName -split '/')[0]
            if ($taskTop -notin $taskFolders -and $taskName -notin $taskRootFiles) { throw ('Unapproved archive path: ' + $taskName) }
            if ($taskEntry.FullName.EndsWith('/')) { continue }
            if ($taskEntry.Length -gt 256MB) { throw ('Archive entry exceeds source-file limit: ' + $taskName) }
            $taskSeen[$taskTop] = $true
            $taskCount++
            $taskStream = $taskEntry.Open()
            $taskBuffer = [IO.MemoryStream]::new()
            try { $taskStream.CopyTo($taskBuffer); $taskText = [Text.Encoding]::GetEncoding(28591).GetString($taskBuffer.ToArray()) }
            finally { $taskStream.Dispose(); $taskBuffer.Dispose() }
            foreach ($taskPattern in $taskPatterns) {
                if ([regex]::IsMatch($taskText, $taskPattern)) { throw ('Key-shaped value detected in archive entry: ' + $taskName) }
            }
            if ($taskName -eq 'SUBMISSION-MANIFEST.json') {
                try { $taskManifest = [Text.Encoding]::UTF8.GetString([Text.Encoding]::GetEncoding(28591).GetBytes($taskText)) | ConvertFrom-Json }
                catch { throw 'Submission manifest invalid.' }
            }
        }
        foreach ($taskRequired in @('Assets', 'Packages', 'ProjectSettings', 'docs', 'scripts', 'README.md')) {
            if (-not $taskSeen[$taskRequired]) { throw ('Required source path missing: ' + $taskRequired) }
        }
        if ($taskManifest.sourceCommit -notmatch '^[a-f0-9]{40,64}$' -or ($ExpectedCommit -and $taskManifest.sourceCommit -ne $ExpectedCommit)) {
            throw 'Submission source commit mismatch.'
        }
        return [ordered]@{ result = 'PASS'; sourceCommit = $taskManifest.sourceCommit; files = $taskCount; keyPatternMatches = 0; excludedPaths = 0 }
    }
    finally { $taskZip.Dispose() }
}

if ($VerifyArchive) { Test-SourceArchive ([IO.Path]::GetFullPath($VerifyArchive)) | ConvertTo-Json; return }
$taskCommit = (& git -C $taskRoot rev-parse --verify --end-of-options ($SourceRef + '^{commit}') | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $taskCommit -notmatch '^[a-f0-9]{40,64}$') { throw 'SourceRef must identify an existing local commit.' }
if (-not $OutputPath) { $OutputPath = Join-Path $taskRoot ('Builds\Submission\ProductAR-source-' + $taskCommit.Substring(0, 12) + '.zip') }
$taskOutput = [IO.Path]::GetFullPath($OutputPath)
if (-not $taskOutput.StartsWith($taskRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($taskOutput) -ne '.zip') {
    throw 'Source archive output must be a ZIP inside this workspace.'
}
if (Test-Path -LiteralPath $taskOutput) { throw 'Refusing to overwrite an existing source archive.' }
$taskParent = Split-Path -Parent $taskOutput
New-Item -ItemType Directory -Path $taskParent -Force | Out-Null
$taskDraft = Join-Path $taskParent ('submission-' + [Guid]::NewGuid().ToString('N') + '.tmp.zip')
try {
    $taskCandidates = @('Assets', 'Packages', 'ProjectSettings', 'docs', 'scripts', 'README.md', 'PRODUCT.md', 'DESIGN.md', 'ThirdParty', '.gitignore', '.gitattributes')
    $taskTree = @(& git -C $taskRoot ls-tree --name-only $taskCommit)
    $taskIncludes = @($taskCandidates | Where-Object { $_ -in $taskTree })
    & git -C $taskRoot archive --format=zip ('--output=' + $taskDraft) $taskCommit -- @taskIncludes
    if ($LASTEXITCODE -ne 0) { throw 'Git source export failed.' }
    $taskZip = [IO.Compression.ZipFile]::Open($taskDraft, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $taskExcluded = @()
        foreach ($taskEntry in @($taskZip.Entries)) {
            if (Test-ExcludedSubmissionPath $taskEntry.FullName) { $taskExcluded += $taskEntry.FullName; $taskEntry.Delete() }
        }
        $taskManifest = [ordered]@{
            schemaVersion = 1; sourceCommit = $taskCommit; sourceRef = $SourceRef
            createdUtc = [DateTime]::UtcNow.ToString('o'); includedRoots = $taskIncludes
            excludedTrackedPaths = $taskExcluded
            exclusionPolicy = @('Library', 'Temp', 'Logs', 'obj', 'Builds', 'artifacts', '*.local.json and metadata', '.env*', 'signing and secret-key files')
            configuration = 'No local lookup configuration included; default runtime is local-only. See README.'
        }
        $taskEntry = $taskZip.CreateEntry('SUBMISSION-MANIFEST.json')
        $taskWriter = [IO.StreamWriter]::new($taskEntry.Open(), [Text.UTF8Encoding]::new($false))
        try { $taskWriter.Write(($taskManifest | ConvertTo-Json -Depth 6)) } finally { $taskWriter.Dispose() }
    }
    finally { $taskZip.Dispose() }
    $taskScan = Test-SourceArchive $taskDraft $taskCommit
    Move-Item -LiteralPath $taskDraft -Destination $taskOutput
    [ordered]@{
        result = 'PASS'; sourceCommit = $taskCommit; archive = $taskOutput.Substring($taskRoot.Length + 1).Replace('\', '/')
        sizeBytes = (Get-Item -LiteralPath $taskOutput).Length
        sha256 = (Get-FileHash -LiteralPath $taskOutput -Algorithm SHA256).Hash.ToLowerInvariant()
        files = $taskScan.files; keyPatternMatches = 0; excludedPaths = 0
    } | ConvertTo-Json
}
finally { if (Test-Path -LiteralPath $taskDraft) { Remove-Item -LiteralPath $taskDraft } }
