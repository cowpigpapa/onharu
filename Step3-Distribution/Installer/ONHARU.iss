#define AppName "ONHARU"
#define AppVersion "1.0.0"
#define AppPublisher "JUAN.HJLEE"
#define AppExeName "ONHARU.exe"

[Setup]
AppId={{C43E8BF2-2B16-4CC7-A85B-D18C2AA7D706}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\ONHARU
DefaultGroupName=ONHARU
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=ONHARU-Setup-{#AppVersion}
SetupIconFile=..\Source\Assets\onharu.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면 바로가기 만들기"; GroupDescription: "추가 바로가기:"; Flags: unchecked
Name: "startup"; Description: "Windows 시작 시 온하루 자동 실행"; GroupDescription: "자동 실행:"; Flags: unchecked

[Files]
Source: "..\ONHARU-ver1.0.0-category-fix-preview.exe"; DestDir: "{app}"; DestName: "{#AppExeName}"; Flags: ignoreversion

[Icons]
Name: "{group}\ONHARU"; Filename: "{app}\{#AppExeName}"
Name: "{userdesktop}\ONHARU"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userstartup}\ONHARU"; Filename: "{app}\{#AppExeName}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Description: "ONHARU 실행"; Flags: nowait postinstall skipifsilent
