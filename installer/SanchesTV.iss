#define MyAppName "SanchesTV"
#define MyAppVersion "7.0.0"
#define MyAppPublisher "SanchesTV"
#define MyAppExeName "SanchesTV.exe"

[Setup]
AppId={{4E44E9A4-01E2-4A3A-A4CD-DFD7F1A812C0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\SanchesTV
DefaultGroupName=SanchesTV
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=SanchesTV-Setup-7.0.0-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\SanchesTV"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\SanchesTV"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir SanchesTV"; Flags: nowait postinstall skipifsilent
