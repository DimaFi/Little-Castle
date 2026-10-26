param([int]$DurationSeconds=420,[string]$Prefix='Final')
$ErrorActionPreference='Stop'
$lightingExpected='E:\Games_Develop\Research\SpatialIndirectStage1_20261026\Player\SpatialIndirectStudy.exe'
$lightingFound=@{}
$lightingWatch=[Diagnostics.Stopwatch]::StartNew()
while($lightingWatch.Elapsed.TotalSeconds -lt $DurationSeconds){
    foreach($lightingProcess in @(Get-Process -Name SpatialIndirectStudy -ErrorAction SilentlyContinue)){
        try{
            if($lightingProcess.Path -ne $lightingExpected){continue}
            $lightingIdentity=[string]$lightingProcess.Id
            if(-not $lightingFound.ContainsKey($lightingIdentity)){
                $lightingInfo=Get-CimInstance Win32_Process -Filter "ProcessId=$($lightingProcess.Id)"
                if($lightingInfo.CommandLine -notlike ('*'+$Prefix+'*')){continue}
                $lightingFound[$lightingIdentity]=[ordered]@{pid=$lightingProcess.Id;commandLine=$lightingInfo.CommandLine;peakWorkingSetBytes=0L;peakPrivateBytes=0L;samples=0}
            }
            $lightingProcess.Refresh()
            $lightingEntry=$lightingFound[$lightingIdentity]
            $lightingEntry.peakWorkingSetBytes=[Math]::Max($lightingEntry.peakWorkingSetBytes,$lightingProcess.PeakWorkingSet64)
            $lightingEntry.peakPrivateBytes=[Math]::Max($lightingEntry.peakPrivateBytes,$lightingProcess.PrivateMemorySize64)
            $lightingEntry.samples++
        }catch [System.InvalidOperationException]{}
    }
    Start-Sleep -Milliseconds 250
}
@($lightingFound.Values)|ConvertTo-Json -Depth 4|Set-Content -LiteralPath 'E:\Games_Develop\Research\SpatialIndirectStage1_20261026\os-memory-samples-final.json' -Encoding utf8
Write-Output "Recorded OS memory for $($lightingFound.Count) owned measurement Players."
