# End-to-end test of KombosSetup-<version>.exe on a CLEAN, DISPOSABLE Windows machine (a GitHub-hosted runner). It installs, upgrades, starts and uninstalls
# for real, so it REFUSES to run anywhere else (it would change the registry and Program Files of the machine it runs on).
#
#   -Scenario fresh          new setup on a clean machine
#   -Scenario from-per-user  an old per-user release (default v4.9.1), started and with "Start with Windows", then the new machine-wide setup
#   -Scenario from-latest    the newest published release, then the new setup (an update)
#
# Results: <Out>\report.md (also the job summary), the Inno logs, a screenshot. Exit code 1 on any failed check.
param(
    [Parameter(Mandatory)][ValidateSet('fresh', 'from-per-user', 'from-latest')][string]$Scenario,
    [Parameter(Mandatory)][string]$NewSetup,
    [string]$Repository = 'giorstergiopoulos-design/Kombos',
    [string]$PerUserTag = 'v4.9.1',
    [string]$Out = (Join-Path ([IO.Path]::GetTempPath()) 'kombos-e2e'),
    [switch]$IAmADisposableMachine
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -and -not $IAmADisposableMachine) {
    throw 'This script installs and uninstalls the program for real. Run it only on a GitHub-hosted runner or a disposable VM (-IAmADisposableMachine). Never on a PC that is in use.'
}
New-Item -ItemType Directory -Force $Out | Out-Null
$AppId = '{B3D8F2A1-6C4E-4A9F-9D2B-1E7F5C8A3B60}_is1'
$UninstallKey = "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId"
$RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run'
$MachineDir = Join-Path $env:ProgramFiles 'Kombos'
$UserDir = Join-Path $env:LOCALAPPDATA 'Programs\Kombos'
$SettingsDir = Join-Path $env:APPDATA 'Kombos'
# the Start menu group is called by the Greek name (Kombos, written with Greek letters); built from code points so that no script encoding can break it
$GreekName = -join ([char[]](0x039A, 0x03CC, 0x03BC, 0x03B2, 0x03BF, 0x03C2))
$report = New-Object System.Collections.Generic.List[string]
$failures = 0

function Check([bool]$ok, [string]$what) {
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    if (-not $ok) { $script:failures++ }
    $script:report.Add("- **$mark** $what")
    Write-Host "$mark  $what"
}
function Note([string]$text) { $script:report.Add("- _NOT VERIFIED_ $text"); Write-Host "NOT VERIFIED  $text" }
function Run-Setup([string]$exe, [string[]]$arguments, [string]$logName) {
    $log = Join-Path $Out $logName
    $p = Start-Process -FilePath $exe -ArgumentList ($arguments + "/LOG=`"$log`"") -PassThru
    # a setup or uninstaller that hangs must fail the step, never hang the job
    if (-not $p.WaitForExit(300000)) {
        try { $p.Kill() } catch { }
        Write-Host "::error::$logName did not finish in 5 minutes"
        return -1
    }
    return $p.ExitCode
}
function Reg-Value([string]$hive, [string]$path, [string]$name) {
    try { (Get-ItemProperty -Path "${hive}:\$path" -Name $name -ErrorAction Stop).$name } catch { $null }
}
function Reg-Key([string]$hive, [string]$path) { Test-Path "${hive}:\$path" }
function Programs-Copies { @($MachineDir, $UserDir) | Where-Object { Test-Path $_ } | ForEach-Object { Get-ChildItem $_ -Filter 'Kombos.exe' -Recurse -ErrorAction SilentlyContinue } }
function Screenshot([string]$name) {
    try {
        Add-Type -AssemblyName System.Windows.Forms, System.Drawing
        $b = [Windows.Forms.SystemInformation]::VirtualScreen
        $bmp = New-Object Drawing.Bitmap $b.Width, $b.Height
        [Drawing.Graphics]::FromImage($bmp).CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
        $bmp.Save((Join-Path $Out $name)); $bmp.Dispose()
    } catch { Write-Host "screenshot not possible: $($_.Exception.Message)" }
}
function Expected-Version {
    $v = (Get-Item $NewSetup).VersionInfo.ProductVersion
    if ($v) { return $v.Split('+')[0].Trim() }
    return $null
}

$report.Add("# Installer end-to-end: $Scenario on $([Environment]::OSVersion.VersionString), $([Environment]::ProcessorCount) logical processors")
$report.Add('')

# ---- 1. the old version ----------------------------------------------------------------------------------------------------------------
$oldIsPerUser = $false
if ($Scenario -ne 'fresh') {
    $tag = if ($Scenario -eq 'from-per-user') { $PerUserTag } else { (& gh release view --repo $Repository --json tagName --jq .tagName) }
    $oldDir = Join-Path $Out 'old'
    New-Item -ItemType Directory -Force $oldDir | Out-Null
    & gh release download $tag --repo $Repository --pattern 'KombosSetup-*.exe*' --dir $oldDir --clobber 2>&1 | Out-Host
    $oldSetup = Get-ChildItem $oldDir -Filter 'KombosSetup-*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $oldSetup) {
        $report.Add("- skipped: release $tag has no KombosSetup-*.exe (nothing to upgrade from)")
        Write-Host "::notice::release $tag has no setup asset: scenario $Scenario skipped"
        $report | Set-Content (Join-Path $Out 'report.md'); if ($env:GITHUB_STEP_SUMMARY) { $report | Add-Content $env:GITHUB_STEP_SUMMARY }
        exit 0
    }
    $sumFile = "$($oldSetup.FullName).sha256"
    if (Test-Path $sumFile) {
        $expected = ((Get-Content $sumFile -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
        Check ((Get-FileHash $oldSetup.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -eq $expected) "the downloaded $tag setup matches its published SHA-256"
    }
    # the user's own settings must survive every upgrade
    New-Item -ItemType Directory -Force $SettingsDir | Out-Null
    Set-Content (Join-Path $SettingsDir 'settings.json') '{"Language":"DE","ThemeMode":"Dark","OnboardingSeen":true}' -Encoding utf8

    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
    if ($Scenario -eq 'from-per-user') { $arguments += '/TASKS="startupicon"' }
    $code = Run-Setup $oldSetup.FullName $arguments 'old-setup.log'
    Check ($code -eq 0) "the old setup ($tag) installed (exit code $code)"
    $oldIsPerUser = Test-Path (Join-Path $UserDir 'Kombos.exe')
    Check ((Programs-Copies).Count -ge 1) "the old version is installed ($(if ($oldIsPerUser) { 'per user' } else { 'machine-wide' }))"
    if ($Scenario -eq 'from-per-user') {
        Check $oldIsPerUser "$tag installs per user (%LocalAppData%\Programs)"
        $startupLnk = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\$GreekName.lnk"
        Check ((Test-Path $startupLnk) -or ($null -ne (Reg-Value 'HKCU' $RunKey 'Kombos'))) 'the old per-user copy has its start-up entry'
        # the old program is RUNNING while the new setup removes it (an isolated data folder, never real settings)
        $env:KOMBOS_DATA_DIR = Join-Path $Out 'old-data'
        $old = Start-Process -FilePath (Join-Path $UserDir 'Kombos.exe') -PassThru
        Remove-Item Env:\KOMBOS_DATA_DIR -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 8
        Check (-not $old.HasExited) 'the old program is running before the upgrade'
    }
}

# ---- 2. the new setup ------------------------------------------------------------------------------------------------------------------
$newArgs = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
if ($Scenario -eq 'fresh') { $newArgs += '/TASKS="startupicon"' }
$code = Run-Setup $NewSetup $newArgs 'new-setup.log'
Check ($code -eq 0) "the new setup finished (exit code $code)"
Start-Sleep -Seconds 2

$version = Expected-Version
Check (Test-Path (Join-Path $MachineDir 'Kombos.exe')) 'the program is in Program Files'
if ($version) { Check (((Get-Item (Join-Path $MachineDir 'Kombos.exe')).VersionInfo.ProductVersion) -like "$version*") "the installed program is version $version" }
Check (Reg-Key 'HKLM' $UninstallKey) 'the machine-wide uninstall entry exists (HKLM)'
Check (-not (Reg-Key 'HKCU' $UninstallKey)) 'no per-user uninstall entry is left (HKCU)'
Check (-not (Test-Path $UserDir)) 'the old per-user folder is gone'
Check (@(Programs-Copies).Count -eq 1) "exactly one copy of Kombos.exe is installed (found $(@(Programs-Copies).Count))"
Check ($null -eq (Reg-Value 'HKLM' $RunKey 'Kombos')) 'no machine-wide start-up value (HKLM Run)'
$stray = @(Get-Process Kombos -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path -like "$UserDir*" })
Check ($stray.Count -eq 0) 'no program of the old per-user folder is still running'
$menu = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\$GreekName"
Check ((Test-Path $menu) -and @(Get-ChildItem $menu -Filter '*.lnk' -ErrorAction SilentlyContinue).Count -ge 1) 'the all-users Start menu shortcut exists'
Check (-not (Test-Path (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$GreekName"))) 'no per-user Start menu folder is left'
Check (-not (Test-Path (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\$GreekName.lnk"))) 'no old Startup shortcut is left'
if ($Scenario -ne 'fresh') {
    $config = Get-Content (Join-Path $SettingsDir 'settings.json') -Raw -ErrorAction SilentlyContinue
    Check ($config -like '*"Language":"DE"*') 'the settings (language) survived the upgrade'
}
$runValue = Reg-Value 'HKCU' $RunKey 'Kombos'
if ($Scenario -eq 'fresh' -or $Scenario -eq 'from-per-user') {
    Check ($runValue -and $runValue -like '*Program Files*') "the wish 'start with Windows' ends as a Run value pointing into Program Files ($runValue)"
}

# ---- 3. start it ------------------------------------------------------------------------------------------------------------------------
$exe = Join-Path $MachineDir 'Kombos.exe'
$env:KOMBOS_DATA_DIR = Join-Path $Out 'smoke-data'        # isolated data: the smoke start never writes into the settings checked above
$smoke = Start-Process -FilePath $exe -PassThru
Remove-Item Env:\KOMBOS_DATA_DIR -ErrorAction SilentlyContinue
# a slow runner needs longer than a fixed pause: poll for the window (at most 90 s), then keep the program running a little longer
for ($i = 0; $i -lt 90 -and -not $smoke.HasExited; $i++) { $smoke.Refresh(); if ($smoke.MainWindowHandle -ne 0) { break }; Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 5
Check (-not $smoke.HasExited) 'the program starts and keeps running'
$smoke.Refresh()
Check ($smoke.MainWindowHandle -ne 0) 'the main window is shown'
Check ($smoke.Responding) 'the window is responding'
Screenshot 'running.png'
Stop-Process -Id $smoke.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# ---- 4. uninstall -----------------------------------------------------------------------------------------------------------------------
$uninstaller = Join-Path $MachineDir 'unins000.exe'
Check (Test-Path $uninstaller) 'the uninstaller is there'
$code = Run-Setup $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') 'uninstall.log'
# the uninstaller hands over to a helper and returns early: wait until the whole folder (unins000.* included) is gone, at most 90 s
for ($i = 0; $i -lt 90 -and (Test-Path $MachineDir); $i++) { Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 3                                                                                      # the helper's last registry clean-up
Check (-not (Test-Path (Join-Path $MachineDir 'Kombos.exe'))) 'the program file is gone'
$left = if (Test-Path $MachineDir) { @(Get-ChildItem $MachineDir -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName.Substring($MachineDir.Length) }) } else { @() }
Check ($left.Count -eq 0) "no files are left in the Program Files folder$(if ($left.Count) { ' (left: ' + (($left | Select-Object -First 8) -join ', ') + ')' })"
Check (-not (Reg-Key 'HKLM' $UninstallKey)) 'the uninstall entry is gone (HKLM)'
Check (-not (Test-Path $menu)) 'the Start menu folder is gone'
Check ($null -eq (Reg-Value 'HKLM' $RunKey 'Kombos') -and $null -eq (Reg-Value 'HKCU' $RunKey 'Kombos')) 'no start-up value is left (HKLM / HKCU Run)'
Check (@(Programs-Copies).Count -eq 0) 'no Kombos.exe is left anywhere in the program folders'
if ($Scenario -ne 'fresh') { Check (Test-Path $SettingsDir) 'the settings in %AppData% are kept by a silent uninstall (by design)' }

# ---- limits -----------------------------------------------------------------------------------------------------------------------------
Note 'the UAC prompt (the runner is an administrator without prompts), SmartScreen, several monitors and per-monitor DPI cannot be tested on a runner'
$report.Add('')
$report.Add("**Result: $(if ($failures -eq 0) { 'all checks passed' } else { "$failures check(s) FAILED" })**")
$report | Set-Content (Join-Path $Out 'report.md')
if ($env:GITHUB_STEP_SUMMARY) { $report | Add-Content $env:GITHUB_STEP_SUMMARY }
if ($failures -gt 0) { exit 1 }
