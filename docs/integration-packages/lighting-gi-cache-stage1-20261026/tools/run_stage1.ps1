param([int]$Repeats=3,[string]$Prefix='Final')
$ErrorActionPreference='Stop'
$lightingRoot='E:\Games_Develop\Research\SpatialIndirectStage1_20261026'
$lightingExe=Join-Path $lightingRoot 'Player\SpatialIndirectStudy.exe'
if(-not(Test-Path -LiteralPath $lightingExe)){throw 'Build the isolated Stage1 Release Player first.'}
for($lightingRound=1;$lightingRound -le $Repeats;$lightingRound++){
    $lightingModes=if($lightingRound%2 -eq 1){@('Reference','Cached')}else{@('Cached','Reference')}
    foreach($lightingMode in $lightingModes){
        $lightingName=$Prefix+('{0:D2}{1}' -f $lightingRound,$lightingMode)
        $lightingDirectory=Join-Path $lightingRoot $lightingName
        if(Test-Path -LiteralPath $lightingDirectory){throw "Refusing to overwrite measured output: $lightingDirectory"}
        $lightingArguments=@('-giStudy','-giDump','-giOutput',$lightingDirectory,'-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',(Join-Path $lightingRoot ($lightingName+'.log')))
        if($lightingMode -eq 'Reference'){$lightingArguments+='-giReference'}
        if($lightingRound -gt 1){$lightingArguments+='-giNoCapture'}
        $lightingProcess=Start-Process -FilePath $lightingExe -ArgumentList $lightingArguments -WindowStyle Normal -PassThru
        $lightingProcess.WaitForExit()
        if($lightingProcess.ExitCode -ne 0){throw "$lightingName failed: exit $($lightingProcess.ExitCode)"}
        if(-not(Test-Path -LiteralPath (Join-Path $lightingDirectory 'result.txt'))){throw "$lightingName produced no result"}
        Write-Output "$lightingName complete; exit0"
    }
}
