# Operator-run rebuild after saving/closing Unity. This script has not been run
# by the agent while the approval service is unavailable.
param([string]$Unity = 'E:/Unity/Unity_6.5/6000.5.5f1/Editor/Unity.exe',
      [switch]$Capture)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $project
if (!(Test-Path -LiteralPath $Unity)) { throw "Unity not found: $Unity" }
if (Test-Path -LiteralPath 'Temp/UnityLockfile') { throw 'Save and close this Unity project before running the rebuild.' }
$python = 'C:/Program Files/Blender Foundation/Blender 4.4/4.4/python/bin/python.exe'
& $python Tools/import_terrain_starter.py
if ($LASTEXITCODE -ne 0) { throw 'Asset import failed.' }
foreach ($step in @('BuildArtStudy','BuildStudyPlayer')) {
    $startedAt = [DateTime]::UtcNow
    $log = Join-Path $project "Logs/garden-plants-$step.log"
    $arguments = @('-batchmode','-quit','-projectPath',('"'+$project+'"'),
        '-executeMethod',"LittleCastle.Editor.TerrainStarterSceneBuilder.$step",
        '-logFile',('"'+$log+'"'))
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Unity failed at $step; inspect $log" }
    if (Select-String -LiteralPath $log -Pattern 'error CS\d+|Shader error|BuildFailedException|Aborting batchmode|\bException:') {
        throw "Unity reported errors at $step; inspect $log"
    }
    if ($step -eq 'BuildStudyPlayer') {
        $summary = Join-Path $project 'Logs/cottage-garden-player.txt'
        if (!(Test-Path -LiteralPath $summary) -or (Get-Item -LiteralPath $summary).LastWriteTimeUtc -lt $startedAt -or
            (Get-Content -LiteralPath $summary -Raw).Trim() -ne 'Succeeded 0') {
            throw 'No fresh successful player build report. Captures were not started.'
        }
    }
}
if ($Capture) { & (Join-Path $PSScriptRoot 'capture_visual_slice.ps1') }
Write-Output 'Rebuilt CottageGarden. Review plants, shadows, close/far LOD and ground before accepting.'
