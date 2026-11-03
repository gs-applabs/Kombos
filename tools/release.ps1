# Builds the Kombos setup the same way on a developer PC and on a CI runner:
#   dotnet publish (framework-dependent, win-x64) -> Inno Setup (installer\Kombos.iss) -> installer\output\KombosSetup-<version>.exe + .sha256
# Needs the .NET 10 SDK and Inno Setup (ISCC.exe) - on a runner see tools\ci-install-inno.ps1.
param(
    [string]$Iscc = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

if (-not $Iscc) {
    $candidates = @("$env:LOCALAPPDATA\Programs", $env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ -and (Test-Path $_) } |
        ForEach-Object { Get-ChildItem $_ -Directory -Filter 'Inno Setup*' -ErrorAction SilentlyContinue } | Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'ISCC.exe' } | Where-Object { Test-Path $_ }
    $Iscc = $candidates | Select-Object -First 1
    if (-not $Iscc) { $Iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
}
if (-not $Iscc) { throw 'ISCC.exe (Inno Setup) was not found' }

$publish = Join-Path $root 'publish'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
& dotnet publish (Join-Path $root 'Kombos.csproj') -c Release -r win-x64 --self-contained false -o $publish -m:1 -p:UseSharedCompilation=false -nodeReuse:false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

$out = Join-Path $root 'installer\output'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
& $Iscc (Join-Path $root 'installer\Kombos.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

$setup = Get-ChildItem $out -Filter 'KombosSetup-*.exe' | Select-Object -First 1
if (-not $setup) { throw 'the setup was not produced' }
$hash = (Get-FileHash $setup.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $($setup.Name)" | Set-Content "$($setup.FullName).sha256" -Encoding ascii
"Setup: $($setup.FullName)"
"SHA-256: $hash"
