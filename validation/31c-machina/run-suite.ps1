param([ValidateSet("stable","beta","both")][string]$Profile="both")
$ErrorActionPreference="Stop"
$projectRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "../..")).Path
$profiles=if($Profile -eq "both"){@("stable","beta")}else{@($Profile)}
foreach($selectedProfile in $profiles){
    $candidate=Get-Content -LiteralPath (Join-Path $projectRoot "outputs/31b-monsters-20261114/$selectedProfile/latest-candidate.json") -Raw|ConvertFrom-Json
    $profileName=if($selectedProfile -eq "stable"){"stable-0.107.1"}else{"beta-0.113.0"}
    $dependencies=Join-Path $projectRoot "outputs/game-update-compat-20261113/profiles/$profileName"
    $output=Join-Path $projectRoot "outputs/31c-machina-20261114/$selectedProfile"
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $buildArguments=@("build",(Join-Path $PSScriptRoot "Checks.csproj"),"-c","Release","-p:OutputPath=$output/checks/","-p:ModAssemblyPath=$($candidate.assembly)","-p:NativeAssemblyPath=$dependencies/sts2.dll","-p:NativeRitsuAssemblyPath=$dependencies/STS2-RitsuLib.dll","--nologo","-v","quiet")
    if($selectedProfile -eq "beta"){$buildArguments+="-p:BetaNativeChecks=true"}
    & dotnet @buildArguments *> (Join-Path $output "build.log")
    if($LASTEXITCODE -ne 0){throw "Validation build failed: $output/build.log"}
    $runtimeArguments=@((Join-Path $output "checks/Checks.dll"),$candidate.assembly,$candidate.sha256,$dependencies,"C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll",(Join-Path $output "report.json"))
    if($selectedProfile -eq "beta"){$runtimeArguments+="--isolate-telemetry"}
    & dotnet @runtimeArguments *> (Join-Path $output "run.log")
    if($LASTEXITCODE -ne 0){throw "Validation failed: $output/report.json"}
    $report=Get-Content -LiteralPath (Join-Path $output "report.json") -Raw|ConvertFrom-Json
    Write-Output "$selectedProfile PASS $($report.checks) $($report.modSha256)"
}
