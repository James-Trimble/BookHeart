; Installer for StoryHeart, built with Inno Setup 6 (https://jrsoftware.org/isinfo.php).
; The build passes the version and the folder of published files:
;   iscc /DAppVersion=0.1.0 /DSourceDir=..\publish\installer StoryHeart.iss

#define AppName "StoryHeart"
#define AppExe "StoryHeart.exe"
#define AppPublisher "James Trimble"
#define ProgId "StoryHeart.Audiobook"
; StoryHeart used to be called BookHeart. Updating from BookHeart moves it to a StoryHeart folder and tidies up after it.
#define OldName "BookHeart"
#define OldProgId "BookHeart.Audiobook"
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
; Windows file versions must be plain numbers, so test versions like 0.2.0-beta.1 use 0.2.0 here.
#ifndef AppNumericVersion
  #define AppNumericVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\installer"
#endif

[Setup]
; Never change AppId: Windows uses it to recognise StoryHeart for upgrades and uninstalling. It was set when
; StoryHeart was called BookHeart, which is why updating from BookHeart replaces it instead of adding a second program.
AppId={{09591A69-6CE2-4ECC-9B84-D050C7013A01}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/James-Trimble/StoryHeart
AppSupportURL=https://github.com/James-Trimble/StoryHeart/issues
AppCopyright=Copyright © 2026 {#AppPublisher}
VersionInfoVersion={#AppNumericVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=Copyright © 2026 {#AppPublisher}
VersionInfoDescription={#AppName} Setup
; Install for just you (no admin needed) or for everyone; the installer asks.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DisableDirPage=no
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
WizardStyle=modern
ShowLanguageDialog=yes
UsePreviousLanguage=no
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=no
LicenseFile=..\legal\EULA.txt
SetupIconFile=..\src\StoryHeart\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=..\release
OutputBaseFilename={#AppName}-{#AppVersion}-Setup
Compression=lzma2/max
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "czech"; MessagesFile: "compiler:Languages\Czech.isl"
Name: "danish"; MessagesFile: "compiler:Languages\Danish.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "finnish"; MessagesFile: "compiler:Languages\Finnish.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "norwegian"; MessagesFile: "compiler:Languages\Norwegian.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[CustomMessages]
AssocGroup=File types:
AssocM4b=Open audiobook files (.m4b) with {#AppName}
AssocAudio=Also open other audio files (.mp3, .m4a, .aac, .flac, .ogg, .opus, .wav, .wma) with {#AppName}
AudiobookFile=Audiobook

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "assocm4b"; Description: "{cm:AssocM4b}"; GroupDescription: "{cm:AssocGroup}"
Name: "assocaudio"; Description: "{cm:AssocAudio}"; GroupDescription: "{cm:AssocGroup}"; Flags: unchecked

[InstallDelete]
; BookHeart's shortcuts. The StoryHeart ones replace them (including the desktop one, if you had it).
Type: files; Name: "{autoprograms}\{#OldName}.lnk"
Type: files; Name: "{autoprograms}\{#OldName} Manual.lnk"
Type: files; Name: "{autodesktop}\{#OldName}.lnk"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{autoprograms}\{#AppName} Manual"; Filename: "{app}\{#AppName} Manual.html"

[Registry]
; Remove BookHeart's file types before adding StoryHeart's.
Root: HKA; Subkey: "Software\Classes\{#OldProgId}"; Flags: deletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#OldName}.exe"; Flags: deletekey
Root: HKA; Subkey: "Software\Classes\.m4b\OpenWithProgids"; ValueType: none; ValueName: "{#OldProgId}"; Flags: deletevalue
Root: HKA; Subkey: "Software\Classes\.mp3\OpenWithProgids"; ValueType: none; ValueName: "{#OldProgId}"; Flags: deletevalue
Root: HKA; Subkey: "Software\Classes\.m4a\OpenWithProgids"; ValueType: none; ValueName: "{#OldProgId}"; Flags: deletevalue
Root: HKA; Subkey: "Software\Classes\.aac\OpenWithProgids"; ValueType: none; ValueName: "{#OldProgId}"; Flags: deletevalue
Root: HKA; Subkey: "Software\Classes\.flac\OpenWithProgids"; ValueType: none; ValueName: "{#OldProgId}"; Flags: deletevalue
Root: HKA; Subkey: "Software\Classes\.ogg\OpenWithProgids"; ValueType: none; ValueName: "{#OldProgId}"; Flags: deletevalue
Root: HKA; Subkey: "Software\Classes\.opus\OpenWithProgids"; ValueType: none; ValueName: "{#OldProgId}"; Flags: deletevalue
Root: HKA; Subkey: "Software\Classes\.wav\OpenWithProgids"; ValueType: none; ValueName: "{#OldProgId}"; Flags: deletevalue
Root: HKA; Subkey: "Software\Classes\.wma\OpenWithProgids"; ValueType: none; ValueName: "{#OldProgId}"; Flags: deletevalue

; The file type StoryHeart opens. Listing it under OpenWithProgids puts StoryHeart in every
; "Open with" menu for these files, even when it isn't the default.
Root: HKA; Subkey: "Software\Classes\{#ProgId}"; ValueType: string; ValueName: ""; ValueData: "{cm:AudiobookFile}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#ProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExe},0"
Root: HKA; Subkey: "Software\Classes\{#ProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""

; .m4b
Root: HKA; Subkey: "Software\Classes\.m4b\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.m4b"; ValueType: string; ValueName: ""; ValueData: "{#ProgId}"; Flags: uninsdeletevalue; Tasks: assocm4b
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".m4b"; ValueData: ""

; Other audio types: always in "Open with"; the default only if chosen.
Root: HKA; Subkey: "Software\Classes\.mp3\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.mp3"; ValueType: string; ValueName: ""; ValueData: "{#ProgId}"; Flags: uninsdeletevalue; Tasks: assocaudio
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".mp3"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.m4a\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.m4a"; ValueType: string; ValueName: ""; ValueData: "{#ProgId}"; Flags: uninsdeletevalue; Tasks: assocaudio
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".m4a"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.aac\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.aac"; ValueType: string; ValueName: ""; ValueData: "{#ProgId}"; Flags: uninsdeletevalue; Tasks: assocaudio
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".aac"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.flac\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.flac"; ValueType: string; ValueName: ""; ValueData: "{#ProgId}"; Flags: uninsdeletevalue; Tasks: assocaudio
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".flac"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.ogg\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.ogg"; ValueType: string; ValueName: ""; ValueData: "{#ProgId}"; Flags: uninsdeletevalue; Tasks: assocaudio
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".ogg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.opus\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.opus"; ValueType: string; ValueName: ""; ValueData: "{#ProgId}"; Flags: uninsdeletevalue; Tasks: assocaudio
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".opus"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.wav\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.wav"; ValueType: string; ValueName: ""; ValueData: "{#ProgId}"; Flags: uninsdeletevalue; Tasks: assocaudio
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".wav"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.wma\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.wma"; ValueType: string; ValueName: ""; ValueData: "{#ProgId}"; Flags: uninsdeletevalue; Tasks: assocaudio
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".wma"; ValueData: ""

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; When StoryHeart updates itself, the installer runs silently. StoryHeart starts a helper that reopens it
; afterwards; this is a backup. Only one copy of StoryHeart runs, so it never opens twice.
Filename: "{app}\{#AppExe}"; Flags: nowait runasoriginaluser; Check: ReopenAfterUpdate

[Code]
var
  OldAppDir: String;

// An update that installs because StoryHeart was closed passes /NOLAUNCH, so StoryHeart stays closed.
function ReopenAfterUpdate: Boolean;
begin
  Result := WizardSilent and (Pos('/NOLAUNCH', UpperCase(GetCmdTail)) = 0);
end;

// Updating from BookHeart: install into a StoryHeart folder next to the old BookHeart one, wherever that was.
procedure InitializeWizard;
var
  Prev: String;
begin
  Prev := RemoveBackslashUnlessRoot(WizardForm.PrevAppDir);
  if (Prev <> '') and (CompareText(ExtractFileName(Prev), '{#OldName}') = 0) and FileExists(AddBackslash(Prev) + '{#OldName}.exe') then
  begin
    OldAppDir := Prev;
    WizardForm.DirEdit.Text := AddBackslash(ExtractFileDir(Prev)) + '{#AppName}';
  end;
end;

// Then remove the old BookHeart folder. Its library isn't in there: StoryHeart moves that itself the first time it opens.
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and (OldAppDir <> '') and (CompareText(OldAppDir, ExpandConstant('{app}')) <> 0) then
    DelTree(OldAppDir, True, True, True);
end;
