param(
    [string]$EnvPath = 'C:\Github\창팡맨\.env.local',
    [string]$OffContact = '',
    [switch]$Disable
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskAllowed = @('NEXT_PUBLIC_SUPABASE_URL', 'NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY',
    'NEXT_PUBLIC_NUTRITION_SUPABASE_URL', 'NEXT_PUBLIC_NUTRITION_SUPABASE_PUBLISHABLE_KEY')
$taskValues = @{}
foreach ($taskLine in [IO.File]::ReadAllLines($EnvPath)) {
    if ($taskLine -match '^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$' -and $taskAllowed -contains $Matches[1]) {
        $taskValues[$Matches[1]] = $Matches[2].Trim().Trim('"').Trim("'")
    }
}
foreach ($taskName in $taskAllowed) { if (-not $taskValues[$taskName]) { throw ('Missing required public setting: ' + $taskName) } }
foreach ($taskName in @('NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY', 'NEXT_PUBLIC_NUTRITION_SUPABASE_PUBLISHABLE_KEY')) {
    if (-not $taskValues[$taskName].StartsWith('sb_publishable_')) { throw 'Only publishable keys are accepted.' }
}
$taskPath = Join-Path $taskRoot 'Assets\Config\remote-lookup.local.json'
if (-not $PSBoundParameters.ContainsKey('OffContact') -and (Test-Path -LiteralPath $taskPath)) {
    $OffContact = (Get-Content -LiteralPath $taskPath -Raw | ConvertFrom-Json).offContact
}
$taskConfig = [ordered]@{
    enabled = -not $Disable
    ptUrl = $taskValues['NEXT_PUBLIC_SUPABASE_URL']
    ptPublishableKey = $taskValues['NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY']
    nutritionUrl = $taskValues['NEXT_PUBLIC_NUTRITION_SUPABASE_URL']
    nutritionPublishableKey = $taskValues['NEXT_PUBLIC_NUTRITION_SUPABASE_PUBLISHABLE_KEY']
    offContact = $OffContact
}
[IO.File]::WriteAllText($taskPath, ($taskConfig | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
Write-Output ('Local config written; enabled=' + $taskConfig.enabled + '; OFF contact configured=' + (-not [string]::IsNullOrWhiteSpace($OffContact)))
