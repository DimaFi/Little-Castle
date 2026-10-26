param([int]$Repeats=3,[string]$Prefix='Final')
$ErrorActionPreference='Stop'
$lightingRoot='E:\Games_Develop\Research\SpatialIndirectStage1B_20261026'
$lightingPlayers=@{
    Before='E:\Games_Develop\Research\SpatialIndirectStage1_20261026\Player\SpatialIndirectStudy.exe'
    After=(Join-Path $lightingRoot 'Player\SpatialIndirectStudy.exe')
}
foreach($lightingExe in $lightingPlayers.Values){if(-not(Test-Path -LiteralPath $lightingExe)){throw "Missing frozen Player: $lightingExe"}}
for($lightingRound=1;$lightingRound -le $Repeats;$lightingRound++){
    $lightingModes=if($lightingRound%2 -eq 1){@('Before','After')}else{@('After','Before')}
    foreach($lightingMode in $lightingModes){
        $lightingName=$Prefix+('{0:D2}{1}' -f $lightingRound,$lightingMode)
        $lightingDirectory=Join-Path $lightingRoot $lightingName
        if(Test-Path -LiteralPath $lightingDirectory){throw "Refusing to overwrite measured output: $lightingDirectory"}
        $lightingArguments=@('-giStudy','-giDump','-giOutput',$lightingDirectory,'-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',(Join-Path $lightingRoot ($lightingName+'.log')))
        if($lightingRound -gt 1){$lightingArguments+='-giNoCapture'}
        $lightingProcess=Start-Process -FilePath $lightingPlayers[$lightingMode] -ArgumentList $lightingArguments -WindowStyle Normal -PassThru
        $lightingProcess.WaitForExit()
        if($lightingProcess.ExitCode -ne 0){throw "$lightingName failed: exit $($lightingProcess.ExitCode)"}
        if(-not(Test-Path -LiteralPath (Join-Path $lightingDirectory 'result.txt'))){throw "$lightingName produced no result"}
        Write-Output "$lightingName complete; exit0"
    }
}
