$ErrorActionPreference = 'Stop'
# Checks the name lists. PIP's names are read from the PIP repo checked out beside this one.
# Each list is one file holding one VESSEL_NAMES node: <Category>.cfg, or <Category>-<Class>.cfg
# for a category split by size (Launchers-Heavy.cfg holds category = Launchers, class = Heavy).
$dir = Join-Path $PSScriptRoot '..\GameData\PhilsBuilds\Names'
$pipFile = Join-Path $PSScriptRoot '..\..\IterativeDevelopment\Core\Names.cs'
$expectedFiles = 25
$namesPerList = 50
$classes = 'Light', 'Medium', 'Heavy', 'SuperHeavy'

# Approved examples by category; a split category's examples may sit in any of its lists.
$approved = @{
    Launchers      = 'Colossus','Goliath','Denali','Matterhorn','Juggernaut','Kilimanjaro'
    UpperStages    = 'Sherpa','Mule','Drover','Ox','Porter','Yeoman'
    Capsules       = 'Argo','Valiant','Perseus','Ulysses','Odyssey','Intrepid'
    Landers        = 'Eagle','Mantis','Sparrow','Wren','Lark','Spider'
    DeepSpace      = 'Resolute','Dauntless','Steadfast','Endeavour','Tenacity','Defiant'
    Rescue         = 'Samaritan','Lifeline','Shepherd','Guardian','Mercy','Bernard'
    Probes         = 'Pathfinder','Wayfarer','Magellan','Cabot','Nomad','Polaris'
    ScienceLanders = 'Sentinel','Vigil','Lantern','Lookout','Warden','Owl'
    Relays         = 'Herald','Courier','Envoy','Echo','Tidings','Hermes'
    Survey         = 'Argus','Lynx','Mercator','Osprey','Surveyor','Compass'
    Tankers        = 'Oasis','Wellspring','Caravan','Cistern','Fountain','Tributary'
    Mining         = 'Badger','Mole','Prospector','Aardvark','Wombat','Pickaxe'
    Stations       = 'Haven','Harbor','Concord','Unity','Sanctuary','Accord'
    Bases          = 'Homestead','Bastion','Frontier','Stockade','Waystation','Trailhead'
    Rovers         = 'Ibex','Yak','Armadillo','Tortoise','Burro','Gecko'
    Hoppers        = 'Cricket','Flea','Jackrabbit','Kangaroo','Springbok'
    Spaceplanes    = 'Zephyr','Sirocco','Mistral','Bora','Monsoon','Tradewind'
    Aircraft       = 'Peregrine','Goshawk','Saker','Gyrfalcon','Kite','Lanner'
    CargoPlanes    = 'Pelican','Stork','Albatross','Heron','Condor','Crane'
    Bombers        = 'Thunderbolt','Tempest','Squall','Thunderhead','Hailstone','Cyclone'
    Rotorcraft     = 'Dragonfly','Hummingbird','Hornet','Kestrel','Damselfly','Hoverfly'
    Boats          = 'Marlin','Narwhal','Manta','Nautilus','Orca','Barracuda'
}

$problems = New-Object System.Collections.Generic.List[string]
$ci = [System.StringComparer]::OrdinalIgnoreCase

# PIP's names: every quoted string inside the ByType dictionary initializer.
$cs = Get-Content -Raw $pipFile
$start = $cs.IndexOf('ByType')
$end = $cs.IndexOf('Themes', $start)
$block = $cs.Substring($start, $end - $start)
$pip = New-Object System.Collections.Generic.HashSet[string]($ci)
foreach ($m in [regex]::Matches($block, '"([^"]+)"')) {
    $v = $m.Groups[1].Value
    if (-not $approved.ContainsKey($v)) { [void]$pip.Add($v) }   # skip the dictionary keys ("liquid" etc. are lower-case ids, not in $approved anyway)
}
$allApproved = New-Object System.Collections.Generic.HashSet[string]($ci)
foreach ($k in $approved.Keys) { foreach ($n in $approved[$k]) { [void]$allApproved.Add($n) } }

$files = Get-ChildItem -Path $dir -Filter *.cfg
Write-Output ("Files: {0}" -f $files.Count)
if ($files.Count -ne $expectedFiles) { $problems.Add("Expected $expectedFiles files, found $($files.Count)") }

$seen = New-Object 'System.Collections.Generic.Dictionary[string,string]'($ci)
$byCategory = @{}    # category -> every name in its lists
$plain = @{}         # categories with a <Category>.cfg list
$split = @{}         # category -> the classes it has <Category>-<Class>.cfg lists for
$total = 0
foreach ($f in ($files | Sort-Object Name)) {
    $id = [IO.Path]::GetFileNameWithoutExtension($f.Name)
    $wantCat, $wantClass = $id -split '-', 2
    $lines = @(Get-Content $f.FullName)
    $raw = Get-Content -Raw $f.FullName
    if ($raw -notmatch '^VESSEL_NAMES\r?\n\{\r?\n') { $problems.Add("${id}: does not start with VESSEL_NAMES {") }
    if ($raw.TrimEnd() -notmatch '\}$') { $problems.Add("${id}: does not end with }") }
    $cat = $null; $class = $null; $theme = $null; $names = @()
    $nodes = 0; $catCount = 0; $classCount = 0; $themeCount = 0; $catAt = -1; $classAt = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l -match '^\tcategory = (.+)$') { $cat = $Matches[1]; $catCount++; $catAt = $i }
        elseif ($l -match '^\tclass = (.+)$') { $class = $Matches[1]; $classCount++; $classAt = $i }
        elseif ($l -match '^\ttheme = (.+)$') { $theme = $Matches[1]; $themeCount++ }
        elseif ($l -match '^\tname = (.+)$') { $names += $Matches[1] }
        elseif ($l -ceq 'VESSEL_NAMES') { $nodes++ }
        elseif ($l -notin @('{', '}')) { $problems.Add("${id}: unexpected line '$l'") }
    }
    if ($nodes -ne 1) { $problems.Add("${id}: has $nodes VESSEL_NAMES nodes, expected 1") }
    if ($catCount -gt 1) { $problems.Add("${id}: has $catCount category lines") }
    if ($themeCount -gt 1) { $problems.Add("${id}: has $themeCount theme lines") }
    if ($classCount -gt 1) { $problems.Add("${id}: has $classCount class lines") }
    if ($cat -cne $wantCat) { $problems.Add("${id}: category is '$cat', the file name says '$wantCat'") }
    if (-not $theme) { $problems.Add("${id}: no theme") }
    if (-not $approved.ContainsKey($wantCat)) { $problems.Add("${id}: '$wantCat' is not one of the 22 category ids") }
    if ($wantClass) {
        if ($classes -cnotcontains $wantClass) { $problems.Add("${id}: '$wantClass' is not a class (classes: $($classes -join ', '))") }
        if ($class -cne $wantClass) { $problems.Add("${id}: class is '$class', the file name says '$wantClass'") }
        elseif ($classAt -ne $catAt + 1) { $problems.Add("${id}: the class line should come right after the category line") }
        if (-not $split.ContainsKey($wantCat)) { $split[$wantCat] = @() }
        $split[$wantCat] += $wantClass
    } else {
        if ($classCount -gt 0) { $problems.Add("${id}: has class = '$class' but the file name has no class") }
        $plain[$wantCat] = $true
    }
    if (-not $byCategory.ContainsKey($wantCat)) { $byCategory[$wantCat] = @() }
    $byCategory[$wantCat] += $names
    Write-Output ("{0,-21} {1,3}  ({2})" -f $id, $names.Count, $theme)
    $total += $names.Count
    if ($names.Count -ne $namesPerList) { $problems.Add("${id}: has $($names.Count) names, expected $namesPerList") }
    foreach ($n in $names) {
        if ($n.Length -gt 14) { $problems.Add("${id}: '$n' is $($n.Length) characters") }
        if ($n -cnotmatch '^[A-Za-z][A-Za-z -]*[A-Za-z]$') { $problems.Add("${id}: '$n' has characters outside letters, spaces and hyphens") }
        if (($n -split ' ').Count -gt 2) { $problems.Add("${id}: '$n' is more than two words") }
        if ($seen.ContainsKey($n)) { $problems.Add("Duplicate: '$n' in $id and $($seen[$n])") } else { $seen[$n] = $id }
        if ($pip.Contains($n) -and -not $allApproved.Contains($n)) { $problems.Add("${id}: '$n' is a PIP name") }
    }
}

# Per category: it has lists, either one plain list or one list per class, and its approved examples.
foreach ($k in ($approved.Keys | Sort-Object)) {
    if (-not $byCategory.ContainsKey($k)) { $problems.Add("${k}: no name list"); continue }
    if ($split.ContainsKey($k)) {
        if ($plain.ContainsKey($k)) { $problems.Add("${k}: has both $k.cfg and class lists") }
        foreach ($c in $classes) {
            $have = @($split[$k] | Where-Object { $_ -ceq $c }).Count
            if ($have -ne 1) { $problems.Add("${k}: $have list(s) for class $c, expected 1") }
        }
    }
    foreach ($a in $approved[$k]) {
        if (-not ($byCategory[$k] | Where-Object { $_ -ceq $a })) { $problems.Add("${k}: approved example '$a' missing") }
    }
}

Write-Output ("Total names: {0}   (PIP names read: {1})" -f $total, $pip.Count)
if ($problems.Count -eq 0) { Write-Output 'ALL CHECKS PASSED' }
else { Write-Output ("{0} problem(s):" -f $problems.Count); $problems | ForEach-Object { Write-Output "  $_" } }
