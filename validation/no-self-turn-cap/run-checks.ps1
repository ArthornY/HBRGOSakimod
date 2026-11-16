param([Parameter(Mandatory=$true)][string]$Candidate,[Parameter(Mandatory=$true)][string]$OutputRoot)
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_UI_LANGUAGE='en'
$repo=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$candidatePath=(Resolve-Path -LiteralPath $Candidate).Path
$candidateSha=(Get-FileHash -LiteralPath $candidatePath -Algorithm SHA256).Hash
$null=New-Item -ItemType Directory -Path $OutputRoot -Force
$out=(Resolve-Path -LiteralPath $OutputRoot).Path
$godot='C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
foreach($profile in @('stable','beta')){
    $native=Join-Path $repo $(if($profile -eq 'stable'){'outputs/game-update-compat-20261113/profiles/stable-0.107.1'}else{'outputs/startup-1131-fix-20261115/profiles/beta-0.113.1'})
    $native=(Resolve-Path -LiteralPath $native).Path
    $log=Join-Path $out ($profile+'-build.log')
    & dotnet build (Join-Path $PSScriptRoot 'Checks.csproj') -c Release --nologo -warnaserror -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true `
        ('-p:ModAssemblyPath='+$candidatePath) ('-p:NativeAssemblyPath='+$native+'/sts2.dll') `
        ('-p:NativeRitsuAssemblyPath='+$native+'/STS2-RitsuLib.dll') ('-p:NativeBaseLibAssemblyPath='+$native+'/BaseLib.dll') `
        ('-p:BetaNativeChecks='+$(if($profile -eq 'beta'){'true'}else{'false'})) *> $log
    if($LASTEXITCODE -ne 0){Get-Content -LiteralPath $log -Tail 35;exit $LASTEXITCODE}
    $fixture=Join-Path $PSScriptRoot 'bin/Release/net9.0/Checks.dll'
    $report=Join-Path $out ($profile+'-report.json')
    $runArgs=@($fixture,$candidatePath,$candidateSha,$native,$godot,$report)
    if($profile -eq 'beta'){$runArgs+='--isolate-telemetry'}
    & dotnet @runArgs *> (Join-Path $out ($profile+'-run.log'))
    if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $out ($profile+'-run.log')) -Tail 40;exit $LASTEXITCODE}
    $r=Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
    if(!$r.ok){throw 'No Self turn cap failed'}
    [PSCustomObject]@{profile=$profile;checks=$r.checks;ok=$r.ok;candidate=$candidateSha}|ConvertTo-Json -Compress
}
