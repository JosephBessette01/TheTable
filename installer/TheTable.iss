; The Table — per-user Windows installer.
; Compiled by GitHub Actions after publishing the standalone launcher.
#define AppName "The Table"
#define AppVersion "0.1.0"
#define AppPublisher "The Table"
[Setup]
AppId={{B0235BEE-683D-4B35-9746-472EED8277AB}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\TheTable
DefaultGroupName=The Table
PrivilegesRequired=lowest
OutputDir=..\installer-output
OutputBaseFilename=TheTableSetup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\TheTableLauncher.exe
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=no

[Files]
Source: "..\output\TheTableLauncher.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\The Table"; Filename: "{app}\TheTableLauncher.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\The Table"; Filename: "{app}\TheTableLauncher.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\TheTableLauncher.exe"; Description: "Launch The Table"; Flags: nowait postinstall skipifsilent
