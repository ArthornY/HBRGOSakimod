param(
    [Parameter(Mandatory = $true)][string]$ModAssemblyPath,
    [ValidateSet('stable', 'beta', 'both')][string]$Profile = 'both',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'outputs/hotaru-numeric-leaks-20261116/potion/final' }
$ModAssemblyPath = (Resolve-Path -LiteralPath $ModAssemblyPath).Path
$candidateHash = (Get-FileHash -LiteralPath $ModAssemblyPath).Hash
$godotSharp = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
$profiles = if ($Profile -eq 'both') { @('stable', 'beta') } else { @($Profile) }
foreach ($profileName in $profiles) {
    $relative = if ($profileName -eq 'stable') { 'outputs/game-update-compat-20261113/profiles/stable-0.107.1' }
        else { 'outputs/startup-1131-fix-20261115/profiles/beta-0.113.1' }
    $nativeProfile = Join-Path $repo $relative
    $output = Join-Path $OutputDirectory $profileName
    $null = New-Item -ItemType Directory -Path (Join-Path $output 'host') -Force
    & dotnet build (Join-Path $PSScriptRoot 'Checks.csproj') -c Release -o (Join-Path $output 'host') --nologo -warnaserror `
        -p:SkipModOutputCopy=true -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true `
        ('-p:BetaNativeChecks=' + ($profileName -eq 'beta').ToString().ToLowerInvariant()) `
        ('-p:ModAssemblyPath=' + $ModAssemblyPath) ('-p:NativeAssemblyPath=' + (Join-Path $nativeProfile 'sts2.dll')) `
        ('-p:NativeRitsuAssemblyPath=' + (Join-Path $nativeProfile 'STS2-RitsuLib.dll')) *> (Join-Path $output 'build.log')
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath (Join-Path $output 'build.log'); exit $LASTEXITCODE }
    $runPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & dotnet (Join-Path $output 'host/Checks.dll') $ModAssemblyPath $candidateHash $nativeProfile $godotSharp (Join-Path $output 'report.json') *> (Join-Path $output 'run.log')
    $runExit = $LASTEXITCODE
    $ErrorActionPreference = $runPreference
    if ($runExit -ne 0) { Get-Content -LiteralPath (Join-Path $output 'run.log'); exit $runExit }
    Get-Content -LiteralPath (Join-Path $output 'run.log')
}
