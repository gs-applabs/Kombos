# Upgrading from a per-user Kombos (≤ 4.9.3) to the machine-wide install (4.9.4)

Older setups installed per user into `%LocalAppData%\Programs\Kombos`. From 4.9.4 the setup installs for all users into `%ProgramFiles%\Kombos` and needs administrator approval.

What the setup does, in order (`installer\Kombos.iss`, function `RemovePerUserInstall`):

1. Ends a running `Kombos.exe` **only if its image lies inside the old per-user folder**.
2. Runs the old uninstaller silently — only when it lies inside that folder (a registry value is never executed from anywhere else). Settings in `%AppData%\Kombos` are kept.
3. Waits (up to 5 s) until the old uninstall entry (`HKCU`) and folder are gone; if our own old copy is still partly there it is cleaned by hand. If something still remains, setup stops with a message (English, Greek, French, German) instead of leaving two copies.
4. Removes the old `HKCU\...\Run\Kombos` value and the old Startup shortcut; if either existed, "Start with Windows" is pre-selected (interactive) or re-created pointing into Program Files (silent).

Checked end to end on clean GitHub runners by `.github/workflows/installer-e2e.yml` (scenarios `fresh`, `from-per-user`, `from-latest`). UAC cannot be tested there.
