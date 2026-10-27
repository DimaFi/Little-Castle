param([int]$Repeats=3,[string]$Prefix='Final',[switch]$SkipCycles)
$ErrorActionPreference='Stop'
$lightingRoot='E:\Games_Develop\Research\SpatialIndirectStage2A_20261026'
$lightingExe=Join-Path $lightingRoot 'Player\SpatialIndirectStudy.exe'
function Invoke-LightingStudy([string]$Name,[string[]]$Extra){
    $lightingDirectory=Join-Path $lightingRoot $Name
    if(Test-Path -LiteralPath $lightingDirectory){throw "Refusing overwrite: $lightingDirectory"}
    $lightingArguments=@('-giStudy','-giTemporalStudy','-giDump','-giOutput',$lightingDirectory,'-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',(Join-Path $lightingRoot ($Name+'.log')))+$Extra
    $lightingProcess=Start-Process -FilePath $lightingExe -ArgumentList $lightingArguments -WindowStyle Normal -PassThru
    $lightingProcess.WaitForExit()
    if($lightingProcess.ExitCode -ne 0){throw "$Name failed, exit $($lightingProcess.ExitCode)"}
    if(-not(Test-Path -LiteralPath (Join-Path $lightingDirectory 'result.txt'))){throw "$Name produced no result"}
    Write-Output "$Name complete; exit0"
}
for($lightingRound=1;$lightingRound -le $Repeats;$lightingRound++){
    $lightingModes=if($lightingRound%2 -eq 1){@('Immediate','Temporal')}else{@('Temporal','Immediate')}
    foreach($lightingMode in $lightingModes){
        $lightingExtra=@();if($lightingMode -eq 'Immediate'){$lightingExtra+='-giImmediate'}
        if($lightingRound -gt 1){$lightingExtra+='-giNoCapture'}
        Invoke-LightingStudy ($Prefix+('{0:D2}{1}' -f $lightingRound,$lightingMode)) $lightingExtra
    }
}
if(-not $SkipCycles){
    Invoke-LightingStudy ($Prefix+'Cycle1x') @('-giCycleSpeed','1')
    Invoke-LightingStudy ($Prefix+'Cycle8x') @('-giCycleSpeed','8')
    Invoke-LightingStudy ($Prefix+'Progress') @('-giProgressCapture')
    Invoke-LightingStudy ($Prefix+'Cycle8xImmediate') @('-giCycleSpeed','8','-giImmediate')
}
