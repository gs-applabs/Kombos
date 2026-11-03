# Makes sure a free Inno Setup is on the (GitHub-hosted) runner. Kombos.iss needs only the stock languages (English, French, German) plus its own
# installer\Languages\Greek.isl, so the compiler of the runner image is enough; Chocolatey installs it when an image does not have it.
$ErrorActionPreference = 'Stop'
function Find-Iscc {
    # The ISCC.exe on the PATH of a runner can be a Chocolatey shim: the real install folders come first.
    $real = @("$env:LOCALAPPDATA\Programs", $env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ -and (Test-Path $_) } |
        ForEach-Object { Get-ChildItem $_ -Directory -Filter 'Inno Setup*' -ErrorAction SilentlyContinue } | Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'ISCC.exe' } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($real) { return $real }
    $found = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
    if ($found -and $found -notlike '*\Chocolatey\bin\*') { return $found }
    return $null
}
$iscc = Find-Iscc
if (-not $iscc) { choco install innosetup -y --no-progress; $iscc = Find-Iscc }
if (-not $iscc) { throw 'Inno Setup is not installed and could not be installed' }
$dir = Join-Path (Split-Path $iscc) 'Languages'
foreach ($file in 'French.isl', 'German.isl') {
    if (-not (Test-Path (Join-Path $dir $file))) { throw "Inno Setup at $iscc lacks $file" }
}
"Inno Setup: $iscc"
