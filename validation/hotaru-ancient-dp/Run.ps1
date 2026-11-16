param(
    [string]$ModAssemblyPath,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
if (!$ModAssemblyPath) { $ModAssemblyPath = Join-Path $repo 'outputs/hotaru-ancient-dp-20261116/candidates/main/HBRGOSakiMod.dll' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'outputs/hotaru-ancient-dp-20261116/final' }
$ModAssemblyPath = (Resolve-Path -LiteralPath $ModAssemblyPath).Path
$godotSharp = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
$candidateHash = (Get-FileHash -LiteralPath $ModAssemblyPath).Hash
foreach ($profileName in @('stable', 'beta')) {
    $profileRelative = if ($profileName -eq 'stable') { 'outputs/game-update-compat-20261113/profiles/stable-0.107.1' }
        else { 'outputs/startup-1131-fix-20261115/profiles/beta-0.113.1' }
    $profile = Join-Path $repo $profileRelative
    $output = Join-Path $OutputDirectory $profileName
    $hostDirectory = Join-Path $output 'host'
    $null = New-Item -ItemType Directory -Path $hostDirectory -Force
    & dotnet build (Join-Path $PSScriptRoot 'Checks.csproj') -c Release -o $hostDirectory --nologo -warnaserror `
        -p:SkipModOutputCopy=true -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true `
        ('-p:BetaNativeChecks=' + ($profileName -eq 'beta').ToString().ToLowerInvariant()) `
        ('-p:ModAssemblyPath=' + $ModAssemblyPath) ('-p:NativeAssemblyPath=' + (Join-Path $profile 'sts2.dll')) `
        ('-p:NativeRitsuAssemblyPath=' + (Join-Path $profile 'STS2-RitsuLib.dll')) *> (Join-Path $output 'build.log')
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath (Join-Path $output 'build.log'); exit $LASTEXITCODE }
    $runArgs = @((Join-Path $hostDirectory 'Checks.dll'), $ModAssemblyPath, $candidateHash, $profile, $godotSharp, (Join-Path $output 'report.json'))
    if ($profileName -eq 'beta') { $runArgs += '--isolate-telemetry' }
    & dotnet @runArgs *> (Join-Path $output 'run.log')
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath (Join-Path $output 'run.log'); exit $LASTEXITCODE }
    Get-Content -LiteralPath (Join-Path $output 'run.log')
}

