$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskRun = Join-Path $taskRoot ('artifacts\submission-tests\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskRun -Force | Out-Null
$taskCases = @(
    @{name='clean'; path='Assets/sample.txt'; text='Descriptive prefix: sb_publishable_'; reject=$false},
    @{name='local-config'; path='Assets/Config/remote-lookup.local.json'; text='{}'; reject=$true},
    @{name='local-meta'; path='Assets/Config/remote-lookup.local.json.meta'; text='guid: sample'; reject=$true},
    @{name='env'; path='.env.local'; text='sample'; reject=$true},
    @{name='logs'; path='Assets/Logs/session.txt'; text='sample'; reject=$true},
    @{name='publishable'; path='Assets/sample.bin'; text=([char]0 + 'sb_' + 'publishable_' + ('a' * 48) + [char]0); reject=$true},
    @{name='secret'; path='docs/sample.txt'; text=('sb_' + 'secret_' + ('a' * 48)); reject=$true},
    @{name='jwt'; path='scripts/sample.txt'; text=('eyJ' + ('a' * 16) + '.' + ('b' * 24) + '.' + ('c' * 24)); reject=$true},
    @{name='private-key'; path='ProjectSettings/sample.txt'; text=('-----BEGIN ' + 'PRIVATE KEY-----'); reject=$true},
    @{name='traversal'; path='Assets/../outside.txt'; text='sample'; reject=$true}
)
$taskPassed = 0
foreach ($taskCase in $taskCases) {
    $taskPath = Join-Path $taskRun ($taskCase.name + '.zip')
    $taskZip = [IO.Compression.ZipFile]::Open($taskPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $taskEntries = [ordered]@{
            'Assets/base.txt' = 'sample'; 'Packages/base.txt' = 'sample'; 'ProjectSettings/base.txt' = 'sample'
            'docs/base.txt' = 'sample'; 'scripts/base.txt' = 'sample'; 'README.md' = 'sample'
            'SUBMISSION-MANIFEST.json' = (@{sourceCommit=('a' * 40)} | ConvertTo-Json)
        }
        $taskEntries[$taskCase.path] = $taskCase.text
        foreach ($taskName in $taskEntries.Keys) {
            $taskWriter = [IO.StreamWriter]::new($taskZip.CreateEntry($taskName).Open(), [Text.UTF8Encoding]::new($false))
            try { $taskWriter.Write($taskEntries[$taskName]) } finally { $taskWriter.Dispose() }
        }
    }
    finally { $taskZip.Dispose() }
    $taskRejected = $false
    try { & (Join-Path $PSScriptRoot 'Export-Submission.ps1') -VerifyArchive $taskPath | Out-Null }
    catch { $taskRejected = $true }
    if ($taskRejected -ne $taskCase.reject) { throw ('Submission policy case failed: ' + $taskCase.name) }
    $taskPassed++
}
[ordered]@{result='PASS'; total=$taskCases.Count; passed=$taskPassed; failed=0} | ConvertTo-Json
