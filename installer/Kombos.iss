; Κόμβος (Kombos) - Inno Setup installer script.
; ΣΗΜΕΙΩΣΗ: το #define MyAppVersion παρακάτω πρέπει να ενημερώνεται χειροκίνητα ώστε να ταιριάζει με το
; Kombos.Localization.L.AppVersion σε κάθε νέα έκδοση - δεν διαβάζεται αυτόματα από τον πηγαίο κώδικα.
;
; Πώς χτίζεται:
;   1. dotnet publish ..\Kombos.csproj -c Release -r win-x64 --self-contained false -o ..\publish
;      (framework-dependent - χρειάζεται .NET 10 Desktop Runtime εγκατεστημένο στον υπολογιστή-στόχο)
;   2. Άνοιξε αυτό το αρχείο με τον Inno Setup Compiler (ISCC.exe Kombos.iss) ή πάτησε Compile.
;   3. Το KombosSetup-<version>.exe βγαίνει στο installer\output\.

#define MyAppName "Κόμβος"
#define MyAppNameAscii "Kombos"
#define MyAppVersion "4.9.5"
#define MyAppPublisher "Kombos"
#define MyAppExeName "Kombos.exe"
#define MyPublishDir "..\publish"

[Setup]
AppId={{B3D8F2A1-6C4E-4A9F-9D2B-1E7F5C8A3B60}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppNameAscii}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=output
OutputBaseFilename=KombosSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Εικονίδιο του ίδιου του installer + εικόνες των σελίδων του οδηγού (100/150/200 % DPI) με το εικονίδιο του Κόμβου.
SetupIconFile=..\kombos.ico
WizardImageFile=wizard-large-100.bmp,wizard-large-150.bmp,wizard-large-200.bmp
WizardSmallImageFile=wizard-small-100.bmp,wizard-small-150.bmp,wizard-small-200.bmp
DisableProgramGroupPage=yes
; v4.9.4: εγκατάσταση για όλους τους χρήστες στο Program Files (το εκτελέσιμο δεν είναι εγγράψιμο από απλό χρήστη). Οι ρυθμίσεις
; μένουν στο %AppData%\Kombos. Παλιά per-user εγκατάσταση (%LocalAppData%\Programs\Kombos) αφαιρείται πρώτα, βλ. RemovePerUserInstall.
PrivilegesRequired=admin
; Άδεια MIT (αρχείο LICENSE στη ρίζα του repo): εμφανίζεται ως σελίδα στον οδηγό εγκατάστασης.
LicenseFile=..\LICENSE
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
UninstallDisplayName={#MyAppName}
; Ο κώδικας διαβάζει/καθαρίζει ΣΚΟΠΙΜΑ τα per-user (HKCU) στοιχεία μιας παλιάς per-user εγκατάστασης.
UsedUserAreasWarning=no
; Σύντομη περιγραφή εφαρμογής - βλ. README.md για την πλήρη λίστα λειτουργιών.
AppComments=Τοπικός hub 39+ εργαλείων AI με τον Claude ως κεντρικό agent.

; Το Greek.isl ΔΕΝ είναι από τις "επίσημες" γλώσσες που έρχονται προεγκατεστημένες με το Inno Setup -
; είναι το unofficial community translation από το ίδιο το επίσημο repo του Inno Setup
; (jrsoftware/issrc, Files/Languages/Unofficial/Greek.isl). Μπαίνει ΠΡΩΤΟ ώστε να είναι η προεπιλογή -
; η ίδια η εφαρμογή είναι ούτως ή άλλως πλήρως ελληνική στο πρώτο άνοιγμά της, οπότε το βήμα
; εγκατάστασης έπρεπε να ταιριάζει. Το αρχείο είναι αντιγραμμένο εδώ (Languages\Greek.isl) ώστε το
; script να μεταγλωττίζεται ίδιο σε οποιοδήποτε μηχάνημα, χωρίς να χρειάζεται να υπάρχει ήδη
; εγκατεστημένο στο τοπικό Inno Setup.
[Languages]
Name: "greek"; MessagesFile: "Languages\Greek.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
greek.StartWithWindowsTask=Εκκίνηση του %1 μαζί με τα Windows
english.StartWithWindowsTask=Start %1 with Windows
french.StartWithWindowsTask=Démarrer %1 avec Windows
german.StartWithWindowsTask=%1 mit Windows starten
greek.OldInstallRemains=Δεν ήταν δυνατή η αφαίρεση του παλιού αντιγράφου του Κόμβου από τον φάκελο χρήστη (τρέχει ακόμη;). Κλείστε το και ξανατρέξτε την εγκατάσταση.
english.OldInstallRemains=An earlier per-user copy of Kombos could not be removed (is it running?). Close it and run setup again.
french.OldInstallRemains=Une ancienne copie de Kombos installée pour l'utilisateur n'a pas pu être supprimée (est-elle en cours d'exécution ?). Fermez-la et relancez l'installation.
german.OldInstallRemains=Eine frühere benutzerbezogene Kopie von Kombos konnte nicht entfernt werden (läuft sie noch?). Schließen Sie sie und starten Sie das Setup erneut.
greek.DeleteSettings=Να διαγραφούν και οι ρυθμίσεις και οι συζητήσεις του Κόμβου (%AppData%\Kombos);
english.DeleteSettings=Also delete the Kombos settings and conversations (%AppData%\Kombos)?
french.DeleteSettings=Supprimer aussi les paramètres et les conversations de Kombos (%AppData%\Kombos) ?
german.DeleteSettings=Auch die Kombos-Einstellungen und -Unterhaltungen (%AppData%\Kombos) löschen?

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "{cm:StartWithWindowsTask,{#MyAppName}}"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; "Εκκίνηση με τα Windows": ίδια τιμή Run του χρήστη που γράφει και η ίδια η εφαρμογή (Ρυθμίσεις), όχι συντόμευση στον φάκελο Startup
; (στην εγκατάσταση admin αυτός θα ήταν του λογαριασμού που ανέβηκε με UAC). Σβήνεται στην απεγκατάσταση.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppNameAscii}"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startupicon; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Παλιότερες εκδόσεις εγκαθίσταντο ανά χρήστη (%LocalAppData%\Programs\Kombos, καταχώριση απεγκατάστασης και εκκίνηση στο HKCU).
// Το setup εγκαθιστά πλέον για όλους στο Program Files, άρα το per-user αντίγραφο αφαιρείται ΠΡΩΤΑ και το αποτέλεσμα ελέγχεται:
//   1. τερματίζεται το πρόγραμμά του (μόνο εικόνα μέσα σε αυτόν ακριβώς τον φάκελο, ποτέ κάτι άλλο),
//   2. τρέχει ο δικός του uninstaller σιωπηλά (οι ρυθμίσεις στο %AppData%\Kombos μένουν),
//   3. η καταχώριση και ο φάκελος πρέπει να έχουν φύγει (λίγες επαναλήψεις)· ό,τι μένει από το ΔΙΚΟ μας παλιό αντίγραφο καθαρίζεται,
//   4. σβήνονται η τιμή HKCU Run και η συντόμευση Startup· η επιθυμία "εκκίνηση με τα Windows" μεταφέρεται στη νέα εγκατάσταση.
// Αν κάτι μείνει, το setup σταματά με μήνυμα αντί να αφήσει δύο αντίγραφα.
const
  // ΜΙΑ αγκύλη: ένα string της Pascal δεν είναι τιμή του [Setup] - το "{{" θα έμενε δύο αγκύλες και δεν θα ταίριαζε ποτέ.
  OldUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B3D8F2A1-6C4E-4A9F-9D2B-1E7F5C8A3B60}_is1';
  RunKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run';

var
  HadAutostart: Boolean;

function StartupShortcut: String;
begin
  Result := ExpandConstant('{userstartup}\{#MyAppName}.lnk');
end;

function PerUserCopyThere(Dir: String): Boolean;
begin
  Result := RegKeyExists(HKCU, OldUninstallKey) or ((Dir <> '') and DirExists(Dir));
end;

function RunValueSet(Root: Integer; Name: String): Boolean;
var
  Value: String;
begin
  Result := RegQueryStringValue(Root, RunKeyPath, Name, Value) and (Value <> '');
end;

// Τερματίζει τα προγράμματα της οποίας η εικόνα βρίσκεται μέσα στον παλιό per-user φάκελο (ακριβής διαδρομή).
procedure EndProgramsIn(Dir: String);
var
  ResultCode: Integer;
  Quoted, Params: String;
begin
  Quoted := Dir;
  StringChangeEx(Quoted, '''', '''''', True);
  Params := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "Get-Process Kombos -ErrorAction SilentlyContinue | ' +
            'Where-Object { $_.Path -and $_.Path.StartsWith(''' + Quoted + '\'', ''OrdinalIgnoreCase'') } | Stop-Process -Force"';
  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Επιστρέφει '' όταν το per-user αντίγραφο έφυγε, αλλιώς τον λόγο.
function RemovePerUserInstall: String;
var
  Cmd, Uninstaller, Dir, Programs: String;
  ResultCode, Attempt: Integer;
begin
  Result := '';
  HadAutostart := RunValueSet(HKCU, '{#MyAppNameAscii}') or RunValueSet(HKLM, '{#MyAppNameAscii}') or FileExists(StartupShortcut);
  Dir := '';
  Uninstaller := '';
  if RegQueryStringValue(HKCU, OldUninstallKey, 'InstallLocation', Dir) then Dir := RemoveBackslashUnlessRoot(Dir);
  if RegQueryStringValue(HKCU, OldUninstallKey, 'UninstallString', Cmd) then
  begin
    Uninstaller := RemoveQuotes(Cmd);
    if Dir = '' then Dir := ExtractFileDir(Uninstaller);
  end;

  Programs := Lowercase(ExpandConstant('{localappdata}\Programs\'));
  if (Dir <> '') and (Pos(Programs, Lowercase(AddBackslash(Dir))) = 1) then
  begin
    EndProgramsIn(Dir);
    // ο uninstaller πρέπει να βρίσκεται μέσα σε εκείνον τον φάκελο: τιμή μητρώου δεν εκτελείται ποτέ από αλλού
    if (Uninstaller <> '') and FileExists(Uninstaller) and (Pos(Lowercase(AddBackslash(Dir)), Lowercase(Uninstaller)) = 1) then
      Exec(Uninstaller, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Attempt := 0;
    while PerUserCopyThere(Dir) and (Attempt < 5) do
    begin
      Sleep(1000);
      Attempt := Attempt + 1;
    end;
    if PerUserCopyThere(Dir) then
    begin
      // ό,τι έμεινε από το δικό μας παλιό αντίγραφο: η καταχώριση και ο φάκελος (οι ρυθμίσεις στο %AppData% δεν αγγίζονται)
      RegDeleteKeyIncludingSubkeys(HKCU, OldUninstallKey);
      if DirExists(Dir) then DelTree(Dir, True, True, True);
    end;
    if PerUserCopyThere(Dir) then Result := Dir;
  end
  else if RegKeyExists(HKCU, OldUninstallKey) then
  begin
    // δική μας καταχώριση εκτός %LocalAppData%\Programs: δεν αγγίζεται, αλλά αναφέρεται (ποτέ δύο αντίγραφα σιωπηλά)
    Result := OldUninstallKey;
  end;
  RegDeleteValue(HKCU, RunKeyPath, '{#MyAppNameAscii}');
  DeleteFile(StartupShortcut);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Left: String;
begin
  Result := '';
  Left := RemovePerUserInstall;
  if Left <> '' then
    Result := CustomMessage('OldInstallRemains') + #13#10 + Left;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  // το παλιό αντίγραφο ξεκινούσε με τα Windows: η ίδια επιθυμία προεπιλέγεται για το νέο
  if (CurPageID = wpSelectTasks) and HadAutostart then
    WizardSelectTasks('startupicon');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // σιωπηλή αναβάθμιση από per-user με εκκίνηση Windows: δεν υπάρχει σελίδα επιλογών, η επιθυμία μεταφέρεται ρητά
  if (CurStep = ssPostInstall) and WizardSilent and HadAutostart and not WizardIsTaskSelected('startupicon') then
    RegWriteStringValue(HKCU, RunKeyPath, '{#MyAppNameAscii}', '"' + ExpandConstant('{app}\{#MyAppExeName}') + '"');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Ό,τι δείχνει στο αφαιρούμενο πρόγραμμα φεύγει (η εφαρμογή γράφει η ίδια τιμή Run όταν ο χρήστης ζητήσει "Εκκίνηση με τα Windows").
    RegDeleteValue(HKCU, RunKeyPath, '{#MyAppNameAscii}');
    RegDeleteValue(HKLM, RunKeyPath, '{#MyAppNameAscii}');
    DataDir := ExpandConstant('{userappdata}\{#MyAppNameAscii}');
    // Οι ρυθμίσεις διαγράφονται ΜΟΝΟ αν το πει ο χρήστης (ποτέ σε σιωπηλή απεγκατάσταση)
    if DirExists(DataDir) and (not UninstallSilent) then
      if MsgBox(CustomMessage('DeleteSettings'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
  end;
end;
