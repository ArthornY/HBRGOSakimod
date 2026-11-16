param(
    [string]$CandidateManifest = 'outputs/garden-scene-distribution-20261115/stable/latest-candidate.json',
    [string]$BetaProfile = 'outputs/startup-1131-fix-20261115/profiles/beta-0.113.1',
    [string]$OutputDirectory = 'outputs/garden-scene-distribution-20261115',
    [string]$ReportName = 'points-final-report.json'
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repository
try {
    $candidate = Get-Content -LiteralPath $CandidateManifest -Raw | ConvertFrom-Json
    $assembly = [IO.Path]::GetFullPath($candidate.assembly)
    if ((Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -ne $candidate.sha256) { throw 'Candidate hash mismatch' }
    $godot = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
    # Only this independent host is compiled. No main-mod build or delivered
    # DLL/PCK output is requested by this script.
    & dotnet build validation/memory-garden-points/Checks.csproj -c Release --nologo -v minimal "-p:ModAssemblyPath=$assembly" "-p:NativeAssemblyPath=$($candidate.nativeProfile)/sts2.dll" "-p:NativeRitsuAssemblyPath=$($candidate.nativeProfile)/STS2-RitsuLib.dll"
    if ($LASTEXITCODE -ne 0) { throw 'Points host build failed' }
    $rows = @(@{ Name = 'stable'; Profile = $candidate.nativeProfile }, @{ Name = 'beta'; Profile = $BetaProfile })
    foreach ($row in $rows) {
        $report = Join-Path (Join-Path $OutputDirectory $row.Name) $ReportName
        & dotnet validation/memory-garden-points/bin/Release/net9.0/Checks.dll $assembly $candidate.sha256 $row.Profile $godot $report
        if ($LASTEXITCODE -ne 0) { throw "Points host failed for $($row.Name)" }
    }
    if ((Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash -ne $candidate.sha256) { throw 'Candidate changed during validation' }
}
finally { Pop-Location }
