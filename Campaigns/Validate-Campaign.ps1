param([string]$Path)

$json = Get-Content $Path -Raw | ConvertFrom-Json
$nodes = $json.nodes
$errors = @()

$byCoord = @{}
$byId = @{}
foreach ($n in $nodes) {
    $key = "$($n.q),$($n.r)"
    if ($byCoord.ContainsKey($key)) { $errors += "Duplicate coord ${key}: '$($n.id)' vs '$($byCoord[$key].id)'" }
    else { $byCoord[$key] = $n }
    if ($byId.ContainsKey($n.id)) { $errors += "Duplicate id '$($n.id)'" }
    else { $byId[$n.id] = $n }
}

$starts = @($nodes | Where-Object { $_.start -eq $true })
if ($starts.Count -ne 1) { $errors += "Expected exactly 1 start node, found $($starts.Count)" }

function Get-Elev($n) { if ($null -eq $n.elevation) { 1 } else { $n.elevation } }
function Is-Impassable($n) { $n.terrain -eq 'Water' -or $n.terrain -eq 'Lava' }

$offsets = @(@(1,0), @(-1,0), @(0,1), @(0,-1), @(1,-1), @(-1,1))

# Edges: ladders, trees and doors (see README). Each is checked for its kind, and the reach below
# is worked out twice, with every edge closed and with every edge open.
$edges = @()
if ($null -ne $json.edges) { $edges = @($json.edges) }
$edgeByPair = @{}
function Pair-Key($x, $y) { if ([string]::CompareOrdinal($x, $y) -le 0) { "$x|$y" } else { "$y|$x" } }
function Hex-Distance($a, $b) {
    $dq = $a.q - $b.q; $dr = $a.r - $b.r
    [int]([Math]::Abs($dq) + [Math]::Abs($dr) + [Math]::Abs($dq + $dr)) / 2
}
$edgeKinds = @('ladder', 'tree', 'door')
foreach ($e in $edges) {
    $kind = "$($e.kind)".ToLower()
    if ($edgeKinds -notcontains $kind) { $errors += "Unknown edge kind '$($e.kind)' on '$($e.a)' - '$($e.b)'"; continue }
    if (-not $byId.ContainsKey($e.a) -or -not $byId.ContainsKey($e.b)) { $errors += "Edge '$($e.a)' - '$($e.b)' names a node that does not exist"; continue }
    $key = Pair-Key $e.a $e.b
    if ($edgeByPair.ContainsKey($key)) { $errors += "Two edges between '$($e.a)' and '$($e.b)'"; continue }
    $edgeByPair[$key] = $e
    $na = $byId[$e.a]; $nb = $byId[$e.b]
    $d = Hex-Distance $na $nb
    if (Is-Impassable $na -or (Is-Impassable $nb)) { $errors += "Edge '$($e.a)' - '$($e.b)' has an impassable end" }
    if ($kind -eq 'ladder') {
        if ($d -ne 1) { $errors += "Ladder '$($e.a)' - '$($e.b)': the ends are not neighbours" }
        elseif ([Math]::Abs((Get-Elev $na) - (Get-Elev $nb)) -lt 2) { $errors += "Ladder '$($e.a)' - '$($e.b)': needs a height difference of 2 or more" }
    } elseif ($kind -eq 'tree') {
        if ($d -ne 2) { $errors += "Tree '$($e.a)' - '$($e.b)': the ends must be 2 hexes apart" }
        else {
            foreach ($off in $offsets) {
                $mid = [pscustomobject]@{ q = $na.q + $off[0]; r = $na.r + $off[1] }
                if ((Hex-Distance $mid $nb) -ne 1) { continue }
                $mk = "$($mid.q),$($mid.r)"
                if ($byCoord.ContainsKey($mk) -and -not (Is-Impassable $byCoord[$mk])) { $errors += "Tree '$($e.a)' - '$($e.b)': the hex between ($mk) can be walked" }
            }
        }
    } elseif ($d -ne 1) { $errors += "Door '$($e.a)' - '$($e.b)': the ends are not neighbours" }
}

# The step rule, as WorldMapData.CanStep: neighbours (or the ends of an open tree), onto a passable
# hex, up at most 1 unless an open ladder joins them (any drop is free), not through a closed door.
function Get-Reach([bool]$open) {
    $seen = @{}
    $queue = New-Object System.Collections.Queue
    $queue.Enqueue($starts[0])
    $seen[$starts[0].id] = 0
    while ($queue.Count -gt 0) {
        $cur = $queue.Dequeue()
        $candidates = @()
        foreach ($off in $offsets) {
            $nk = "$($cur.q + $off[0]),$($cur.r + $off[1])"
            if ($byCoord.ContainsKey($nk)) { $candidates += $byCoord[$nk] }
        }
        if ($open) {
            foreach ($e in $edges) {
                if ("$($e.kind)".ToLower() -ne 'tree') { continue }
                if ($e.a -eq $cur.id -and $byId.ContainsKey($e.b)) { $candidates += $byId[$e.b] }
                elseif ($e.b -eq $cur.id -and $byId.ContainsKey($e.a)) { $candidates += $byId[$e.a] }
            }
        }
        foreach ($nb in $candidates) {
            if ($seen.ContainsKey($nb.id) -or (Is-Impassable $nb)) { continue }
            $edge = $edgeByPair[(Pair-Key $cur.id $nb.id)]
            $kind = if ($null -ne $edge) { "$($edge.kind)".ToLower() } else { '' }
            if ((Hex-Distance $cur $nb) -ne 1 -and -not ($open -and $kind -eq 'tree')) { continue }
            if ((Get-Elev $nb) - (Get-Elev $cur) -gt 1 -and -not ($open -and $kind -eq 'ladder')) { continue }
            if ($kind -eq 'door' -and -not $open) { continue }
            $seen[$nb.id] = $seen[$cur.id] + 1
            $queue.Enqueue($nb)
        }
    }
    return $seen
}
$visitedClosed = Get-Reach $false
$visited = Get-Reach $true

# A shortcut opens from its far end b: the end farther from the start by walking with every edge closed
# (WorldMapData.NormaliseEdges swaps a tree or door at load when it is the wrong way; a ladder's b must
# be its high end, which is also the far one). A warning, since the game copes, but the file should say it.
$warnings = @()
foreach ($e in $edges) {
    if (-not $visitedClosed.ContainsKey($e.a) -or -not $visitedClosed.ContainsKey($e.b)) { continue }
    $da = $visitedClosed[$e.a]; $db = $visitedClosed[$e.b]
    $kind = "$($e.kind)".ToLower()
    if ($kind -eq 'ladder') {
        if ((Get-Elev $byId[$e.b]) -le (Get-Elev $byId[$e.a])) { $warnings += "Ladder '$($e.a)' - '$($e.b)': b should be the high end" }
        elseif ($db -le $da) { $warnings += "Ladder '$($e.a)' - '$($e.b)': the high end b is not farther from the start than a ($db vs $da), so it can be kicked down before the walk round" }
    } elseif ($db -lt $da) { $warnings += "$kind '$($e.a)' - '$($e.b)': b is nearer the start than a ($db vs $da); put the far end in b" }
}

$unreachable = @($nodes | Where-Object { -not (Is-Impassable $_) -and -not $visited.ContainsKey($_.id) })
foreach ($n in $unreachable) { $errors += "Unreachable passable node: '$($n.id)' at ($($n.q),$($n.r)) elev $(Get-Elev $n), even with every ladder, tree and door open" }

# Isolated scenery check: impassable tiles should touch at least one map node.
foreach ($n in ($nodes | Where-Object { Is-Impassable $_ })) {
    $touches = $false
    foreach ($off in $offsets) {
        if ($byCoord.ContainsKey("$($n.q + $off[0]),$($n.r + $off[1])")) { $touches = $true; break }
    }
    if (-not $touches) { $errors += "Floating scenery tile: '$($n.id)' touches nothing" }
}

# Loot: a known kind, a name for every key, and no souls on a fight hex (the fight already pays).
$lootKinds = @('estus_shard', 'treasure', 'key', 'souls', 'homeward_bone')
foreach ($n in $nodes) {
    $hasLoot = $null -ne $n.loot -and $n.loot -ne 'None'
    if ($hasLoot -and $lootKinds -notcontains $n.loot.ToString().ToLower()) { $errors += "Unknown loot '$($n.loot)' on '$($n.id)'" }
    if ($hasLoot -and $n.loot.ToString().ToLower() -eq 'key' -and [string]::IsNullOrEmpty($n.key)) { $errors += "Key loot on '$($n.id)' has no `"key`" name" }
    if (-not $hasLoot -and -not [string]::IsNullOrEmpty($n.key)) { $errors += "'$($n.id)' names a key but has no key loot" }
    if ($hasLoot -and $n.loot.ToString().ToLower() -eq 'souls' -and ($n.encounter -eq 'Encounter' -or $n.encounter -eq 'Boss')) { $errors += "Souls loot on fight hex '$($n.id)'" }
    if ($hasLoot -and (Is-Impassable $n)) { $errors += "Loot on impassable '$($n.id)'" }
}
# Structures: a known kind, and never on lava or water.
$structureKinds = @('none', 'castle', 'cathedral', 'library', 'aqueduct', 'tower', 'crypt', 'ruins', 'slum')
foreach ($n in $nodes) {
    $hasStructure = $null -ne $n.structure -and $n.structure -ne 'None'
    if ($hasStructure -and $structureKinds -notcontains $n.structure.ToString().ToLower()) { $errors += "Unknown structure '$($n.structure)' on '$($n.id)'" }
    if ($hasStructure -and (Is-Impassable $n)) { $errors += "Structure on impassable '$($n.id)'" }
}
$lootCount = @($nodes | Where-Object { $null -ne $_.loot -and $_.loot -ne 'None' }).Count

# Roamers: each loop is two or more plain hexes (no encounter, bonfire or boss, passable) of the roamer's
# section, and every step in order, the last back to the first included, is a legal step with every edge
# closed: neighbours, up at most 1, not through a door. A path walked there and back repeats hexes (A B C B).
function Is-Plain($n) { ($null -eq $n.encounter -or $n.encounter -eq 'None') -and -not (Is-Impassable $n) }
$roamers = @()
if ($null -ne $json.roamers) { $roamers = @($json.roamers) }
$roamerIds = @{}
foreach ($r in $roamers) {
    if ([string]::IsNullOrEmpty($r.id) -or $roamerIds.ContainsKey($r.id) -or $byId.ContainsKey($r.id)) { $errors += "Roamer id '$($r.id)' is missing, repeated, or also a node id"; continue }
    $roamerIds[$r.id] = $true
    $loop = @($r.loop)
    if ($loop.Count -lt 2) { $errors += "Roamer '$($r.id)' needs a loop of at least two hexes"; continue }
    $unknown = @($loop | Where-Object { -not $byId.ContainsKey($_) })
    if ($unknown.Count -gt 0) { $errors += "Roamer '$($r.id)' names a hex that does not exist: $($unknown -join ', ')"; continue }
    for ($i = 0; $i -lt $loop.Count; $i++) {
        $from = $byId[$loop[$i]]
        $to = $byId[$loop[($i + 1) % $loop.Count]]
        if (-not (Is-Plain $from)) { $errors += "Roamer '$($r.id)': '$($from.id)' is not plain ground (an encounter, bonfire, boss or impassable hex)" }
        if ([int]$from.section -ne [int]$r.section) { $errors += "Roamer '$($r.id)': '$($from.id)' is in section $([int]$from.section), not $([int]$r.section)" }
        if ((Hex-Distance $from $to) -ne 1) { $errors += "Roamer '$($r.id)': '$($from.id)' -> '$($to.id)' is not a step between neighbours"; continue }
        if ((Get-Elev $to) - (Get-Elev $from) -gt 1) { $errors += "Roamer '$($r.id)': '$($from.id)' -> '$($to.id)' climbs more than 1" }
        $between = $edgeByPair[(Pair-Key $from.id $to.id)]
        if ($null -ne $between -and "$($between.kind)".ToLower() -eq 'door') { $errors += "Roamer '$($r.id)': '$($from.id)' -> '$($to.id)' goes through a door" }
    }
}

$encounters = @($nodes | Where-Object { $_.encounter -eq 'Encounter' })
$levelCounts = $encounters | Group-Object level | Sort-Object Name | ForEach-Object { "L$($_.Name) x$($_.Count)" }
$bonfires = @($nodes | Where-Object { $_.encounter -eq 'Bonfire' }).Count

Write-Output "Nodes: $($nodes.Count)   Encounters: $($encounters.Count) ($($levelCounts -join ', '))   Bonfires: $bonfires   Loot: $lootCount   Roamers: $($roamers.Count)   Edges: $($edges.Count)   Reachable: $($visited.Count) with every edge open, $($visitedClosed.Count) with them closed"
$warnings | ForEach-Object { Write-Output "WARNING: $_" }
if ($errors.Count -eq 0) { Write-Output "VALID: all passable nodes reachable from '$($starts[0].id)'" }
else { $errors | ForEach-Object { Write-Output "ERROR: $_" } }
