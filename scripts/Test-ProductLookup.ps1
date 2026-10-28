#Requires -Version 7.0
<#
.SYNOPSIS
Read-only PT -> approved nutrition / OFF checks, with local snapshots reported separately.
.EXAMPLE
./scripts/Test-ProductLookup.ps1 -Barcodes '8809375984692','8801104671507'
.EXAMPLE
./scripts/Test-ProductLookup.ps1 -Barcode '3017624010701' -OutputPath artifacts/online-lookup/check.json
.NOTES
Uses only the two public read RPCs and OFF GET. Does not register identifiers or change data.
PT v1 calls an unlinked barcode "not_linked", not "not_found". Only that status permits OFF.
This is a server-data diagnostic, not proof of camera recognition or APK behavior.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][Alias('Barcode')][ValidateCount(1, 15)][string[]]$Barcodes,
    [string]$ConfigPath = '',
    [string]$OutputPath = '',
    [ValidateRange(1, 30)][int]$TimeoutSeconds = 8
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $ConfigPath) { $ConfigPath = Join-Path $taskRoot 'Assets/Config/remote-lookup.local.json' }

# Never emit request headers, URLs, contact information, raw responses, or exception messages.
try { $taskConfig = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json -AsHashtable }
catch { throw 'Local lookup configuration is missing or invalid; no network calls made.' }
try { $taskCatalog = Get-Content -LiteralPath (Join-Path $taskRoot 'Assets/Data/demo-products.json') -Raw | ConvertFrom-Json -AsHashtable }
catch { throw 'Local snapshot is missing or invalid; no network calls made.' }
$taskPrivateValues = @('ptPublishableKey', 'nutritionPublishableKey', 'ptUrl', 'nutritionUrl', 'offContact') |
    ForEach-Object { [string]$taskConfig[$_] } | Where-Object { $_.Length -gt 0 }

function Safe-Text($Value) {
    if ($null -eq $Value) { return $null }
    if ($Value -is [DateTime] -or $Value -is [DateTimeOffset]) { return $Value.ToString('o') }
    $text = [string]$Value
    foreach ($private in $taskPrivateValues) { $text = $text.Replace($private, '[redacted]') }
    $text = $text -replace 'sb_(publishable|secret)_[A-Za-z0-9_-]+', '[redacted]'
    $text = $text -replace 'eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+', '[redacted]'
    $text = $text -replace 'https?://\S+', '[url omitted]' -replace '[\x00-\x1f]', ' '
    if ($text.Length -gt 240) { $text = $text.Substring(0, 240) }
    return $text
}
function Test-Endpoint([string]$Value) {
    $uri = $null
    return [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri) -and $uri.Scheme -eq 'https' -and
        $uri.IsDefaultPort -and -not $uri.UserInfo -and $uri.AbsolutePath -eq '/' -and -not $uri.Query -and -not $uri.Fragment
}
function Test-Key([string]$Value) { return $Value -match '^sb_publishable_[A-Za-z0-9_-]+$' -and $Value.Length -gt 20 }
function Test-Ean13([string]$Value) {
    if ($Value -notmatch '^[0-9]{13}$') { return $false }
    $sum = 0
    for ($i = 0; $i -lt 12; $i++) { $sum += ([int]$Value.Substring($i, 1)) * $(if ($i % 2) { 3 } else { 1 }) }
    return ((10 - $sum % 10) % 10) -eq [int]$Value.Substring(12, 1)
}
function Test-Number($Value, [bool]$Positive = $false) {
    if ($null -eq $Value -or $Value -is [string] -or $Value -is [bool]) { return $false }
    try { $number = [double]$Value } catch { return $false }
    return [double]::IsFinite($number) -and $number -ge 0 -and (-not $Positive -or $number -gt 0)
}
function Test-Date($Value) {
    if ($Value -is [DateTime] -or $Value -is [DateTimeOffset]) { return $true }
    $date = [DateTimeOffset]::MinValue
    return $Value -is [string] -and [DateTimeOffset]::TryParse($Value, [ref]$date)
}
function Test-Id($Value) {
    $id = [Guid]::Empty
    return $Value -is [string] -and [Guid]::TryParse($Value, [ref]$id) -and $id -ne [Guid]::Empty
}
function Read-Json([string]$Url, [string]$Key = '', $Body = $null, [string]$UserAgent = '') {
    $client = [Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
    $request = $null
    $response = $null
    try {
        $method = if ($null -eq $Body) { [Net.Http.HttpMethod]::Get } else { [Net.Http.HttpMethod]::Post }
        $request = [Net.Http.HttpRequestMessage]::new($method, $Url)
        if ($Key) { [void]$request.Headers.TryAddWithoutValidation('apikey', $Key) }
        if ($UserAgent) { [void]$request.Headers.TryAddWithoutValidation('User-Agent', $UserAgent) }
        if ($null -ne $Body) { $request.Content = [Net.Http.StringContent]::new(($Body | ConvertTo-Json -Compress), [Text.Encoding]::UTF8, 'application/json') }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $raw = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        try { $data = ConvertFrom-Json -InputObject $raw -AsHashtable -NoEnumerate }
        catch { return @{ httpStatus = [int]$response.StatusCode; error = 'invalid_json'; data = $null } }
        return @{ httpStatus = [int]$response.StatusCode; error = $null; data = $data }
    }
    catch { return @{ httpStatus = $null; error = 'transport_failed'; data = $null } }
    finally {
        if ($response) { $response.Dispose() }
        if ($request) { $request.Dispose() }
        $client.Dispose()
    }
}
function Price-Summary($Price, [bool]$Local = $false) {
    if ($null -eq $Price) { return $null }
    $kind = if ($Local -and -not $Price.kind) { 'observed' } else { $Price.kind }
    return [ordered]@{
        kind = Safe-Text $kind
        amountKrw = if ($Local) { $Price.unitPriceKrw } else { $Price.amountKrw }
        seller = Safe-Text $Price.seller
        observedAt = Safe-Text $Price.observedAt
        sourceLabel = Safe-Text $(if ($Local) { $Price.displaySource } else { $Price.sourceLabel })
    }
}
function Read-Nutrition([string]$Id) {
    if (-not (Test-Endpoint $taskConfig.nutritionUrl) -or -not (Test-Key $taskConfig.nutritionPublishableKey)) {
        return @{ status = 'config_unavailable'; source = 'Fitness Nutrition public RPC'; present = $false }
    }
    $reply = Read-Json ($taskConfig.nutritionUrl.TrimEnd('/') + '/rest/v1/rpc/get_public_product_nutrition_v1') `
        $taskConfig.nutritionPublishableKey @{ p_namespace = 'pricetrace'; p_catalog_product_id = $Id }
    $result = [ordered]@{ status = 'unavailable'; source = 'Fitness Nutrition public RPC'; httpStatus = $reply.httpStatus; present = $false }
    if ($reply.error -or $reply.httpStatus -ne 200) { return $result }
    $rows = $null
    if ($reply.data -is [System.Collections.IDictionary]) { $rows = @($reply.data) }
    elseif ($reply.data -is [array]) { $rows = $reply.data }
    if ($null -eq $rows) { $result.status = 'contract_error'; return $result }
    $result.rowCount = $rows.Count
    if ($rows.Count -eq 0) { $result.status = 'not_found'; return $result }
    if ($rows.Count -ne 1) { $result.status = 'multiple_rows_require_review'; return $result }
    $row = $rows[0]
    if ($row.contract_version -ne 'nutrition-read.v1' -or $row.catalog_product_id -ne $Id -or
        -not (Test-Number $row.basis_amount $true) -or -not $row.basis_unit -or
        $row.nutrition_values -isnot [System.Collections.IDictionary]) {
        $result.status = 'contract_error'; return $result
    }
    $values = [ordered]@{}
    foreach ($field in @('calories_kcal', 'protein_grams', 'carbs_grams', 'fat_grams', 'sodium_mg', 'saturated_fat_grams', 'sugars_grams', 'fiber_grams')) {
        $value = $row.nutrition_values[$field]
        if ($null -ne $value -and -not (Test-Number $value)) { $result.status = 'contract_error'; return $result }
        $values[$field] = $value
    }
    $result.status = 'found'
    $result.present = @($values.Values | Where-Object { $null -ne $_ }).Count -gt 0
    $result.catalogProductId = $Id
    $result.sourceType = Safe-Text $row.source_type
    $result.basisAmount = $row.basis_amount
    $result.basisUnit = Safe-Text $row.basis_unit
    $result.values = $values
    return $result
}
function Read-Off([string]$Barcode) {
    $result = [ordered]@{ status = 'unavailable'; source = 'Open Food Facts'; price = $null; nutrition = $null }
    $contact = [string]$taskConfig.offContact
    if ([string]::IsNullOrWhiteSpace($contact) -or $contact.Length -gt 200 -or $contact -match '[\r\n()]') {
        $result.status = 'config_unavailable'; return $result
    }
    $reply = Read-Json ('https://world.openfoodfacts.org/api/v3/product/' + $Barcode + '?fields=product_name_ko,product_name,brands,quantity,nutriments,nutrition_data_per') `
        -UserAgent ('ProductAR/0.7.0-lookup-check (' + $contact + ')')
    $result.httpStatus = $reply.httpStatus
    if ($reply.error -or $reply.httpStatus -notin @(200, 404)) { return $result }
    $root = $reply.data
    if ($root -isnot [System.Collections.IDictionary] -or $root.code -ne $Barcode -or $root.errors -isnot [array]) {
        $result.status = 'contract_error'; return $result
    }
    if ($reply.httpStatus -eq 404 -and $root.status -eq 'failure' -and $root.result.id -eq 'product_not_found' -and
        $root.errors.Count -gt 0 -and @($root.errors | Where-Object { $_.message.id -ne 'product_not_found' }).Count -eq 0) {
        $result.status = 'not_found'; return $result
    }
    if ($reply.httpStatus -ne 200 -or $root.status -notin @('success', 'success_with_warnings') -or
        $root.result.id -ne 'product_found' -or $root.errors.Count -ne 0 -or $root.product -isnot [System.Collections.IDictionary]) {
        $result.status = 'contract_error'; return $result
    }
    $name = if ($root.product.product_name_ko) { $root.product.product_name_ko } else { $root.product.product_name }
    $result.status = if ($name) { 'found' } else { 'found_without_name' }
    $result.name = Safe-Text $name
    $result.brand = Safe-Text $root.product.brands
    $result.quantity = Safe-Text $root.product.quantity
    $nutriments = $root.product.nutriments
    $values = [ordered]@{}
    foreach ($field in @('energy-kcal_100g', 'energy-kj_100g', 'energy_100g', 'proteins_100g', 'carbohydrates_100g', 'fat_100g', 'sugars_100g', 'saturated-fat_100g', 'fiber_100g', 'sodium_100g', 'salt_100g')) {
        $values[$field] = if ($nutriments -is [System.Collections.IDictionary] -and (Test-Number $nutriments[$field])) { $nutriments[$field] } else { $null }
    }
    $result.nutrition = [ordered]@{
        present = @($values.Values | Where-Object { $null -ne $_ }).Count -gt 0
        source = 'Open Food Facts'
        fields = 'OFF per-100g field names; raw numeric values, no serving conversion'
        reportedNutritionDataPer = Safe-Text $root.product.nutrition_data_per
        values = $values
    }
    return $result
}

$taskResults = foreach ($rawBarcode in $Barcodes) {
    $barcode = $rawBarcode -replace '[\s-]', ''
    $record = [ordered]@{ barcode = Safe-Text $barcode; pt = $null; nutrition = $null; off = $null; localSnapshot = $null }
    if (-not (Test-Ean13 $barcode)) { $record.pt = @{ status = 'invalid_input'; networkCalled = $false }; $record; continue }
    $local = @($taskCatalog.products | Where-Object { $_.barcode -ceq $barcode })
    if ($local.Count -eq 1) {
        $record.localSnapshot = [ordered]@{
            source = 'bundled snapshot; not a server response'; snapshotDate = Safe-Text $taskCatalog.snapshotDate
            catalogProductId = Safe-Text $local[0].catalogProductId; name = Safe-Text $local[0].name
            price = Price-Summary $local[0].price $true; nutritionPresent = $null -ne $local[0].nutrition
        }
    }
    if (-not $taskConfig.enabled -or -not (Test-Endpoint $taskConfig.ptUrl) -or -not (Test-Key $taskConfig.ptPublishableKey)) {
        $record.pt = @{ status = 'config_unavailable'; networkCalled = $false }; $record; continue
    }
    $reply = Read-Json ($taskConfig.ptUrl.TrimEnd('/') + '/rest/v1/rpc/get_public_product_by_barcode_v1') `
        $taskConfig.ptPublishableKey @{ p_barcode = $barcode }
    $record.pt = [ordered]@{ status = 'unavailable'; source = 'PT public barcode RPC'; httpStatus = $reply.httpStatus }
    if ($reply.error -or $reply.httpStatus -ne 200) { $record; continue }
    $root = $reply.data
    if ($root -isnot [System.Collections.IDictionary] -or $root.contractVersion -ne 'pricetrace-barcode-lookup.v1' -or
        $root.barcode -cne $barcode -or -not (Test-Date $root.checkedAt) -or $root.matches -isnot [array]) {
        $record.pt.status = 'contract_error'; $record; continue
    }
    $count = $root.matches.Count
    $validCount = ($root.status -in @('not_linked', 'invalid') -and $count -eq 0) -or
        ($root.status -eq 'matched' -and $count -eq 1) -or ($root.status -eq 'conflict' -and ($count -eq 0 -or $count -ge 2))
    if (-not $validCount) { $record.pt.status = 'contract_error'; $record; continue }
    $record.pt.status = $root.status
    $record.pt.matchCount = $count
    if ($root.status -eq 'not_linked') { $record.off = Read-Off $barcode }
    if ($root.status -eq 'matched') {
        $product = $root.matches[0]
        if ($product -isnot [System.Collections.IDictionary]) { $record.pt.status = 'contract_error'; $record; continue }
        $price = $product.latestPublicPrice
        $validPrice = $null -eq $price -or ($price.kind -in @('observed', 'listed') -and (Test-Number $price.amountKrw) -and
            $price.seller -and (Test-Date $price.observedAt) -and $price.sourceLabel)
        if (-not (Test-Id $product.catalogProductId) -or -not (Test-Id $product.standardProductId) -or
            -not $product.name -or -not $product.Contains('latestPublicPrice') -or -not $validPrice) {
            $record.pt.status = 'contract_error'; $record; continue
        }
        $hasAmount = $null -ne $product.contentAmount
        $hasUnit = -not [string]::IsNullOrWhiteSpace($product.contentUnit)
        if ($hasAmount -ne $hasUnit -or ($hasAmount -and
            (-not (Test-Number $product.contentAmount $true) -or $product.contentUnit -notin @('g', 'ml', 'each')))) {
            $record.pt.status = 'contract_error'; $record; continue
        }
        $record.pt.catalogProductId = $product.catalogProductId
        $record.pt.name = Safe-Text $product.name
        $record.pt.brand = Safe-Text $product.brand
        $record.pt.quantity = [ordered]@{ amount = $product.contentAmount; unit = Safe-Text $product.contentUnit; label = Safe-Text $product.specificationLabel }
        $record.pt.price = Price-Summary $price
        $record.pt.observedPricePresent = $null -ne $price -and $price.kind -eq 'observed'
        $record.nutrition = Read-Nutrition $product.catalogProductId
    }
    $record
}
$taskReport = [ordered]@{
    schemaVersion = 1; checkedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    scope = 'Read-only server data; not physical recognition or device UI validation'
    fallbackRule = 'Only PT not_linked calls OFF; server values and bundled snapshots stay separate'
    results = @($taskResults)
}
$taskJson = $taskReport | ConvertTo-Json -Depth 14
foreach ($private in $taskPrivateValues) { $taskJson = $taskJson.Replace($private, '[redacted]') }
if ($OutputPath) {
    $taskOutput = [IO.Path]::GetFullPath($OutputPath)
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskOutput))
    [IO.File]::WriteAllText($taskOutput, $taskJson, [Text.UTF8Encoding]::new($false))
}
$taskJson
