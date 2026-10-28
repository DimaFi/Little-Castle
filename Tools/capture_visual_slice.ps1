param([string]$OutputFolder = 'Logs/TerrainStarter/VisualSlice_v003',
      [string]$BaselineFolder = 'Logs/TerrainStarter/VisualSlice_v002')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$player = Join-Path $projectRoot 'Builds/CottageGarden/CottageGarden.exe'
$captureRoot = Join-Path $projectRoot 'Logs/TerrainStarter'
$outputRoot = Join-Path $projectRoot $OutputFolder
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$baselineRoot = Join-Path $projectRoot $BaselineFolder
if ([IO.Path]::GetFullPath($baselineRoot) -eq [IO.Path]::GetFullPath($outputRoot)) { throw 'Use a separate output folder to preserve the baseline.' }
Copy-Item -LiteralPath (Join-Path $baselineRoot 'gameplay-medium.png') -Destination (Join-Path $outputRoot 'gameplay-before.png')
Copy-Item -LiteralPath (Join-Path $baselineRoot 'close-a-medium.png') -Destination (Join-Path $outputRoot 'close-before.png')
Copy-Item -LiteralPath (Join-Path $baselineRoot 'performance-medium.json') -Destination (Join-Path $outputRoot 'performance-before.json')
$modes = [ordered]@{
    'medium' = ''; 'medium-stack-off' = '-starterStackOff';
    'medium-ao-off' = '-starterAOOff'; 'medium-dof-off' = '-starterDOFOff';
    'medium-filter-off' = '-starterFilterOff'; 'low' = '-starterLow';
    'medium-terrain-off' = '-starterTerrainOff';
    'high' = '-starterHigh'; 'ultra' = '-starterUltra'
}
foreach ($entry in $modes.GetEnumerator()) {
    $mode = $entry.Value; $label = $entry.Key
    # The visible preview is intentional: a hidden Windows player may stop presenting.
    $startedAt = [DateTime]::UtcNow
    $process = Start-Process -FilePath $player -ArgumentList "-starterQA $mode -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile $outputRoot/runtime-$label.log" -WorkingDirectory $projectRoot -WindowStyle Normal -Wait -PassThru
    $reportPath = Join-Path $captureRoot "player-performance-$label.json"
    if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $reportPath) -or (Get-Item -LiteralPath $reportPath).LastWriteTimeUtc -lt $startedAt) { throw "Missing or stale QA report: $label" }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($report.samples -ne 600) { throw "Incomplete benchmark: $label" }
    Copy-Item -LiteralPath $reportPath -Destination (Join-Path $outputRoot "performance-$label.json")
    Copy-Item -LiteralPath (Join-Path $captureRoot "player-$label.png") -Destination (Join-Path $outputRoot "gameplay-$label.png")
    Write-Output "Captured $label : GPU $([math]::Round($report.gpuMeanMs,3)) ms"
}
foreach ($mode in @('', '-starterStackOff', '-starterTerrainOff')) {
    $label = if ($mode -eq '-starterStackOff') { 'medium-stack-off' } elseif ($mode) { 'medium-terrain-off' } else { 'medium' }
    $startedAt = [DateTime]::UtcNow
    $process = Start-Process -FilePath $player -ArgumentList "-starterVisual $mode -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile $outputRoot/visual-$label.log" -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Visual capture failed: $label" }
    foreach ($view in @('close-a', 'close-b', 'far', 'ground')) {
        $imagePath = Join-Path $captureRoot "player-$label-$view.png"
        if (!(Test-Path -LiteralPath $imagePath) -or (Get-Item -LiteralPath $imagePath).LastWriteTimeUtc -lt $startedAt) {
            throw "Missing or stale visual capture: $label / $view"
        }
        Copy-Item -LiteralPath $imagePath -Destination (Join-Path $outputRoot "$view-$label.png")
    }
}
Get-ChildItem -LiteralPath (Split-Path $player -Parent) -File -Recurse | Get-FileHash -Algorithm SHA256 | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'build-sha256.json') -Encoding UTF8
Write-Output "Review captures saved: $outputRoot"
