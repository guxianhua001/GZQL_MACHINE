$zhKeys = [System.Collections.Generic.HashSet[string]]::new()
$zhFile = 'c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.zh-CN.xaml'
foreach ($line in Get-Content $zhFile) {
    if ($line -match 'x:Key="([^"]+)"') {
        $zhKeys.Add($Matches[1]) | Out-Null
    }
}

$enKeys = [System.Collections.Generic.HashSet[string]]::new()
$enFile = 'c:\WorkFiles\GZQL_MACHINE\MainApp\Languages\Strings.en-US.xaml'
foreach ($line in Get-Content $enFile) {
    if ($line -match 'x:Key="([^"]+)"') {
        $enKeys.Add($Matches[1]) | Out-Null
    }
}

$allLangKeys = [System.Collections.Generic.HashSet[string]]::new($zhKeys)
foreach ($k in $enKeys) { $allLangKeys.Add($k) | Out-Null }

$referencedKeys = [System.Collections.Generic.HashSet[string]]::new()
$xamlReferencedKeys = [System.Collections.Generic.HashSet[string]]::new()
$csReferencedKeys = [System.Collections.Generic.HashSet[string]]::new()

# Keys referenced in code but NOT in resource files
$missingKeys = [System.Collections.Generic.HashSet[string]]::new()

$xamlFiles = Get-ChildItem -Path 'c:\WorkFiles\GZQL_MACHINE' -Filter '*.xaml' -Recurse | Where-Object { $_.FullName -notmatch '\\Languages\\' }
$dynamicResourceNonLangSet = [System.Collections.Generic.HashSet[string]]::new()

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($null -eq $content) { continue }

    $drMatches = [regex]::Matches($content, 'DynamicResource\s+([\w.-]+)')
    foreach ($m in $drMatches) {
        $key = $m.Groups[1].Value
        if ($allLangKeys.Contains($key)) {
            $referencedKeys.Add($key) | Out-Null
            $xamlReferencedKeys.Add($key) | Out-Null
        } else {
            $dynamicResourceNonLangSet.Add($key) | Out-Null
        }
    }

    $langMatches = [regex]::Matches($content, 'lang:Lang\s+([\w.-]+)')
    foreach ($m in $langMatches) {
        $key = $m.Groups[1].Value
        if ($allLangKeys.Contains($key)) {
            $referencedKeys.Add($key) | Out-Null
            $xamlReferencedKeys.Add($key) | Out-Null
        } else {
            $missingKeys.Add($key) | Out-Null
        }
    }
}

$csFiles = Get-ChildItem -Path 'c:\WorkFiles\GZQL_MACHINE' -Filter '*.cs' -Recurse

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($null -eq $content) { continue }

    # GetResource("key")
    $grMatches = [regex]::Matches($content, 'GetResource\(\s*"([^"]+)"\s*[\),]')
    foreach ($m in $grMatches) {
        $key = $m.Groups[1].Value
        if ($allLangKeys.Contains($key)) {
            $referencedKeys.Add($key) | Out-Null
            $csReferencedKeys.Add($key) | Out-Null
        } else {
            $missingKeys.Add($key) | Out-Null
        }
    }

    # GetResourceOrDefault("key"
    $groMatches = [regex]::Matches($content, 'GetResourceOrDefault\(\s*"([^"]+)"')
    foreach ($m in $groMatches) {
        $key = $m.Groups[1].Value
        if ($allLangKeys.Contains($key)) {
            $referencedKeys.Add($key) | Out-Null
            $csReferencedKeys.Add($key) | Out-Null
        } else {
            $missingKeys.Add($key) | Out-Null
        }
    }

    # L("key")
    $lMatches = [regex]::Matches($content, '\bL\(\s*"([^"]+)"\s*[\),]')
    foreach ($m in $lMatches) {
        $key = $m.Groups[1].Value
        if ($allLangKeys.Contains($key)) {
            $referencedKeys.Add($key) | Out-Null
            $csReferencedKeys.Add($key) | Out-Null
        } else {
            $missingKeys.Add($key) | Out-Null
        }
    }
}

# Also check for Tree_ keys dynamically generated
$treeKeyPattern = [regex]::Matches((Get-Content 'c:\WorkFiles\GZQL_MACHINE\Framework\ViewModels\TreeViewModel.cs' -Raw), 'Tree_')
# Tree keys are dynamically generated, so we check which Tree_ keys exist in resource files
$treeKeysInResources = [System.Collections.Generic.List[string]]::new()
foreach ($k in $zhKeys) {
    if ($k.StartsWith("Tree_")) { $treeKeysInResources.Add($k) }
}

# Unused keys
$unusedKeys = [System.Collections.Generic.List[string]]::new()
foreach ($k in $zhKeys) {
    if (-not $referencedKeys.Contains($k)) {
        $unusedKeys.Add($k)
    }
}

# Keys only in en-US
$onlyEn = [System.Collections.Generic.List[string]]::new()
foreach ($k in $enKeys) { if (-not $zhKeys.Contains($k)) { $onlyEn.Add($k) } }

# Keys only in zh-CN
$onlyZh = [System.Collections.Generic.List[string]]::new()
foreach ($k in $zhKeys) { if (-not $enKeys.Contains($k)) { $onlyZh.Add($k) } }

Write-Host "============================================"
Write-Host "       LANGUAGE KEY USAGE SCAN REPORT"
Write-Host "============================================"
Write-Host ""
Write-Host "=== BASIC COUNTS ==="
Write-Host "zh-CN keys: $($zhKeys.Count)"
Write-Host "en-US keys: $($enKeys.Count)"
Write-Host "XAML referenced lang keys: $($xamlReferencedKeys.Count)"
Write-Host "CS referenced lang keys: $($csReferencedKeys.Count)"
Write-Host "Total unique referenced lang keys: $($referencedKeys.Count)"
Write-Host "Unused keys (in zh-CN, not referenced): $($unusedKeys.Count)"
Write-Host "Keys only in en-US: $($onlyEn.Count)"
Write-Host "Keys only in zh-CN: $($onlyZh.Count)"
Write-Host "Non-lang DynamicResource keys: $($dynamicResourceNonLangSet.Count)"
Write-Host "Keys referenced in code but NOT in resource files: $($missingKeys.Count)"
Write-Host "Tree_ keys in resources: $($treeKeysInResources.Count)"
Write-Host ""

Write-Host "=== KEYS ONLY IN EN-US (not in ZH-CN) ==="
foreach ($k in $onlyEn) { Write-Host "  $k" }
Write-Host ""

Write-Host "=== KEYS ONLY IN ZH-CN (not in EN-US) ==="
foreach ($k in $onlyZh) { Write-Host "  $k" }
Write-Host ""

Write-Host "=== KEYS REFERENCED IN CODE BUT NOT IN RESOURCE FILES ==="
foreach ($k in $missingKeys) { Write-Host "  $k" }
Write-Host ""

Write-Host "=== UNUSED KEYS (in zh-CN but not referenced anywhere) ==="
Write-Host "Count: $($unusedKeys.Count)"
foreach ($k in $unusedKeys) { Write-Host "  $k" }
Write-Host ""

Write-Host "=== NON-LANG DYNAMICRESOURCE KEYS (style/theme resources) ==="
$nonLangSorted = [System.Collections.Generic.List[string]]::new($dynamicResourceNonLangSet)
$nonLangSorted.Sort()
foreach ($k in $nonLangSorted) { Write-Host "  $k" }
