#define AppVersion "0.2.0"
[Setup]
AppId={{707E61AF-6732-41AB-A8A6-5822454D8367}
AppName=LR Meeting AI
AppVersion={#AppVersion}
AppPublisher=Ing. Luis Ríos, MBA
DefaultDirName={localappdata}\Programs\LR Meeting AI
DefaultGroupName=LR Meeting AI
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir=..\artifacts\installer
OutputBaseFilename=LRMeetingAI-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\LRMeetingAI.exe
[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\LR Meeting AI"; Filename: "{app}\LRMeetingAI.exe"
[Run]
Filename: "{app}\LRMeetingAI.exe"; Description: "Abrir LR Meeting AI"; Flags: nowait postinstall skipifsilent
