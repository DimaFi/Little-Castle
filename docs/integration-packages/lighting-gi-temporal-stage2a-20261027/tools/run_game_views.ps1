param([int]$Repeats=3,[int]$FirstRound=1,[string]$Prefix='Verified')
$ErrorActionPreference='Stop';$lightingRoot='E:\Games_Develop\Research\LightingGameViews_20261026';$lightingExe=Join-Path $lightingRoot 'Player\SpatialIndirectStudy.exe'
for($lightingRound=$FirstRound;$lightingRound -lt $FirstRound+$Repeats;$lightingRound++){
    $lightingModes=if($lightingRound%2 -eq 1){@('Base','GroundFog')}else{@('GroundFog','Base')}
    foreach($lightingMode in $lightingModes){
        $lightingName='{0:D2}{1}' -f $lightingRound,$lightingMode;$lightingName=$Prefix+$lightingName;$lightingDirectory=Join-Path $lightingRoot $lightingName
        if(Test-Path -LiteralPath $lightingDirectory){throw "Refusing overwrite: $lightingDirectory"}
        $lightingArguments=@('-giGameViewsStudy','-giOutput',$lightingDirectory,'-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',(Join-Path $lightingRoot ($lightingName+'.log')))
        if($lightingMode -eq 'GroundFog'){$lightingArguments+='-giGroundRelativeFog'}
        if($lightingRound -gt 1){$lightingArguments+='-giNoCapture'}
        $lightingRun=Start-Process -FilePath $lightingExe -ArgumentList $lightingArguments -WindowStyle Normal -PassThru;$lightingRun.WaitForExit()
        if($lightingRun.ExitCode -ne 0){throw "$lightingName failed: $($lightingRun.ExitCode)"}
        $lightingResult=Join-Path $lightingDirectory 'result.txt';$lightingErrors=Join-Path $lightingDirectory 'runtime-errors.txt'
        if(!(Test-Path -LiteralPath $lightingResult) -or !(Test-Path -LiteralPath $lightingErrors)){throw "$lightingName exited without completion evidence"}
        if(!(Get-Content -LiteralPath $lightingResult -Raw).StartsWith('PASS') -or (Get-Content -LiteralPath $lightingErrors -Raw)){throw "$lightingName evidence failed"}
        $lightingBench=Get-Content -LiteralPath (Join-Path $lightingDirectory 'benchmarks.json') -Raw | ConvertFrom-Json
        if($lightingBench.reports.Count -ne 30){throw "$lightingName expected30 benchmark reports"}
        Write-Output "$lightingName complete; exit0;30 reports verified"
    }
}
