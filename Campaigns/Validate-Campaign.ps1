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

# BFS from start over passable nodes with |elevation delta| <= 1.
$visited = @{}
$queue = New-Object System.Collections.Queue
$queue.Enqueue($starts[0])
$visited[$starts[0].id] = $true
while ($queue.Count -gt 0) {
    $cur = $queue.Dequeue()
    foreach ($off in $offsets) {
        $nk = "$($cur.q + $off[0]),$($cur.r + $off[1])"
        if (-not $byCoord.ContainsKey($nk)) { continue }
        $nb = $byCoord[$nk]
        if ($visited.ContainsKey($nb.id)) { continue }
        if (Is-Impassable $nb) { continue }
        if ([Math]::Abs((Get-Elev $nb) - (Get-Elev $cur)) -gt 1) { continue }
        $visited[$nb.id] = $true
        $queue.Enqueue($nb)
    }
}

$unreachable = @($nodes | Where-Object { -not (Is-Impassable $_) -and -not $visited.ContainsKey($_.id) })
foreach ($n in $unreachable) { $errors += "Unreachable passable node: '$($n.id)' at ($($n.q),$($n.r)) elev $(Get-Elev $n)" }

# Isolated scenery check: impassable tiles should touch at least one map node.
foreach ($n in ($nodes | Where-Object { Is-Impassable $_ })) {
    $touches = $false
    foreach ($off in $offsets) {
        if ($byCoord.ContainsKey("$($n.q + $off[0]),$($n.r + $off[1])")) { $touches = $true; break }
    }
    if (-not $touches) { $errors += "Floating scenery tile: '$($n.id)' touches nothing" }
}

$encounters = @($nodes | Where-Object { $_.encounter -eq 'Encounter' })
$levelCounts = $encounters | Group-Object level | Sort-Object Name | ForEach-Object { "L$($_.Name) x$($_.Count)" }
$bonfires = @($nodes | Where-Object { $_.encounter -eq 'Bonfire' }).Count

Write-Output "Nodes: $($nodes.Count)   Encounters: $($encounters.Count) ($($levelCounts -join ', '))   Bonfires: $bonfires   Reachable: $($visited.Count)"
if ($errors.Count -eq 0) { Write-Output "VALID: all passable nodes reachable from '$($starts[0].id)'" }
else { $errors | ForEach-Object { Write-Output "ERROR: $_" } }
