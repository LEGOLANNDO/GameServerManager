[Setup]
AppName=Game Server Manager
AppVersion=1.0.5
AppPublisher=GameServerManager Contributors
DefaultDirName={autopf}\GameServerManager
DefaultGroupName=Game Server Manager
UninstallDisplayIcon={app}\GameServerManager.exe
Compression=lzma2
SolidCompression=yes
OutputDir=dist
OutputBaseFilename=GameServerManager-v1.0.5-Setup
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "dist\portable\GameServerManager\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Game Server Manager"; Filename: "{app}\GameServerManager.exe"
Name: "{autodesktop}\Game Server Manager"; Filename: "{app}\GameServerManager.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\GameServerManager.exe"; Description: "Launch Game Server Manager"; Flags: nowait postinstall skipifsilent
