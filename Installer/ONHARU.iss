#define AppName "ONHARU"
#define AppVersion "2.1.0"
#ifdef TestBuild
#define SetupAppId "{{D61EF3C4-0B0D-44A9-B6CD-79F785C74E54}"
#define SetupDir "{localappdata}\Programs\ONHARU-InstallTest"
#define SetupOutputDir "..\Release\InstallTestInstaller"
#define SetupOutputName "ONHARU-2.1.0-InstallTest"
#else
#define SetupAppId "{{C43E8BF2-2B16-4CC7-A85B-D18C2AA7D706}"
#define SetupDir "{localappdata}\Programs\ONHARU"
#define SetupOutputDir "..\Release\Installer"
#define SetupOutputName "ONHARU-2.1.0-Setup"
#endif

[Setup]
AppId={#SetupAppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=JUAN.HJLEE
DefaultDirName={#SetupDir}
DefaultGroupName=ONHARU
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#SetupOutputDir}
OutputBaseFilename={#SetupOutputName}
SetupIconFile=..\App\Assets\onharu.ico
UninstallDisplayIcon={app}\ONHARU.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
CloseApplicationsFilter=ONHARU.exe,ONHARU-2.1-local-test.exe,OnharuV3.App.exe,OnharuV3.LayerHost.exe
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=JUAN.HJLEE
VersionInfoDescription=ONHARU installer
VersionInfoProductName=ONHARU
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕 화면에 바로가기 만들기"; GroupDescription: "바로가기:"; Flags: checkedonce
Name: "startup"; Description: "Windows 시작 시 ONHARU 실행"; GroupDescription: "자동 실행:"; Flags: checkedonce

[Files]
Source: "..\Release\ONHARU-2.1.0\ONHARU.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Release\ONHARU-2.1.0\OnharuV3.LayerHost.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Release\ONHARU-2.1.0\OnharuV3.DesktopHook.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Release\ONHARU-2.1.0\README.md"; DestDir: "{app}"; DestName: "ONHARU-사용설명서.md"; Flags: ignoreversion

[InstallDelete]
Type: files; Name: "{app}\layer-host.log"

[Icons]
Name: "{group}\ONHARU"; Filename: "{app}\ONHARU.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\ONHARU"; Filename: "{app}\ONHARU.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userstartup}\ONHARU"; Filename: "{app}\ONHARU.exe"; WorkingDir: "{app}"; Tasks: startup

[Run]
Filename: "{app}\ONHARU.exe"; Description: "ONHARU 실행"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\OnharuV3.LayerHost.exe"; Parameters: "--stop"; WorkingDir: "{app}"; Flags: runhidden waituntilterminated; RunOnceId: "StopLayerHost"
