# Installs Inno Setup 7 on a (GitHub-hosted) runner. Kombos.iss refuses to compile with anything older (guard `#if Ver < EncodeVer(7,0,0)`).
# ONE free source: the official GitHub release of jrsoftware/issrc (the project's own download location, linked from jrsoftware.org/isdl.php).
# The download is verified twice before it is run: its SHA-256 must equal the pinned value (the digest GitHub shows for that release asset) and
# its Authenticode signature must be valid and issued to "Pyrsys B.V." (the publisher named on the Inno Setup download page).
$ErrorActionPreference = 'Stop'
$version = '7.1.1'
$url = "https://github.com/jrsoftware/issrc/releases/download/is-$($version.Replace('.', '_'))/innosetup-$version-x64.exe"
$sha256 = '8a5a6711c3766a6e5fb74f280ba0e3551e719ce94538d901fdbd1f8f9920b3f0'

function Find-Iscc7 {
    @("$env:LOCALAPPDATA\Programs", $env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ -and (Test-Path $_) } |
        ForEach-Object { Get-ChildItem $_ -Directory -Filter 'Inno Setup 7*' -ErrorAction SilentlyContinue } |
        ForEach-Object { Join-Path $_.FullName 'ISCC.exe' } | Where-Object { Test-Path $_ } | Select-Object -First 1
}

$iscc = Find-Iscc7
if (-not $iscc) {
    $setup = Join-Path $env:RUNNER_TEMP "innosetup-$version-x64.exe"
    if (-not $env:RUNNER_TEMP) { $setup = Join-Path ([IO.Path]::GetTempPath()) "innosetup-$version-x64.exe" }
    Invoke-WebRequest $url -OutFile $setup -UseBasicParsing
    $got = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($got -ne $sha256) { throw "Inno Setup download has SHA-256 $got, expected $sha256" }
    $sig = Get-AuthenticodeSignature $setup
    if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notlike '*Pyrsys B.V.*') { throw "Inno Setup download signature is not valid for Pyrsys B.V. ($($sig.Status))" }
    $p = Start-Process $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-' -PassThru
    if (-not $p.WaitForExit(300000)) { try { $p.Kill() } catch { }; throw 'the Inno Setup installer did not finish in 5 minutes' }
    $iscc = Find-Iscc7
}
if (-not $iscc) { throw 'Inno Setup 7 is not installed and could not be installed' }
"Inno Setup 7: $iscc"
