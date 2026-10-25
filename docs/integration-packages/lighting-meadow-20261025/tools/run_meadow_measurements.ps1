$ErrorActionPreference = 'Stop'
$lightingEvidence = 'E:\Games_Develop\Research\LightingMeadow_20261025'
$lightingEnvironment = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe' OR Name='Meadow.exe' OR Name='LittleCastle_LightingMeadow.exe'" | Select-Object ProcessId,Name,CommandLine)
ConvertTo-Json -InputObject $lightingEnvironment -Depth 4 | Set-Content -LiteralPath (Join-Path $lightingEvidence 'measurement-environment.json') -Encoding utf8
foreach ($lightingSpace in @('Linear','Gamma')) {
    $lightingHours = if ($lightingSpace -eq 'Linear') { @('12','18.5','1') } else { @('12') }
    foreach ($lightingHour in $lightingHours) {
        foreach ($lightingMode in @(0,1)) {
            $lightingFolder = Join-Path $lightingEvidence "Acceptance\$lightingSpace\Measurements\h$lightingHour-mode$lightingMode"
            New-Item -ItemType Directory -Path $lightingFolder -Force | Out-Null
            $lightingExe = Join-Path $lightingEvidence "Player_$lightingSpace\LittleCastle_LightingMeadow.exe"
            $lightingArguments = "-lmBenchmark -lmMode $lightingMode -lmHour $lightingHour -lmView 0 -lmOutput $lightingFolder -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile $lightingFolder\player.log"
            $lightingProcess = Start-Process -FilePath $lightingExe -ArgumentList $lightingArguments -WindowStyle Normal -Wait -PassThru
            if ($lightingProcess.ExitCode -ne 0) { throw "Measurement failed: $lightingSpace $lightingHour $lightingMode, exit $($lightingProcess.ExitCode)" }
            $lightingReport = Get-Content -LiteralPath (Join-Path $lightingFolder 'benchmark.json') -Raw | ConvertFrom-Json
            if ($lightingReport.status -ne 'PASS' -or $lightingReport.frame.samples -ne 600) { throw 'Insufficient ordinary camera-render samples.' }
            Write-Output "$lightingSpace hour=$lightingHour mode=$lightingMode samples=$($lightingReport.frame.samples) GPU=$($lightingReport.gpu.meanMs) frameP95=$($lightingReport.frame.p95Ms)"
        }
    }
}
