#define AppName "ONHARU"
#define AppVersion "2.2.3"
#ifdef TestBuild
#define SetupAppId "{{D61EF3C4-0B0D-44A9-B6CD-79F785C74E54}"
#define SetupDir "{localappdata}\Programs\ONHARU-InstallTest"
#define SetupOutputDir "..\Release\InstallTestInstaller"
#define SetupOutputName "ONHARU-2.2.3-InstallTest"
#else
#define SetupAppId "{{C43E8BF2-2B16-4CC7-A85B-D18C2AA7D706}"
#define SetupDir "{localappdata}\Programs\ONHARU"
#define SetupOutputDir "..\Release\Installer"
#define SetupOutputName "ONHARU-2.2.3-Setup"
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
CloseApplicationsFilter=ONHARU.exe,ONHARU-2.1-local-test.exe,Onharu.App.exe,Onharu.LayerHost.exe
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=JUAN.HJLEE
VersionInfoDescription=ONHARU installer
VersionInfoProductName=ONHARU
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
korean.DesktopIcon=바탕 화면에 바로가기 만들기
english.DesktopIcon=Create a desktop shortcut
korean.Shortcuts=바로가기:
english.Shortcuts=Shortcuts:
korean.StartWithWindows=Windows 시작 시 ONHARU 실행
english.StartWithWindows=Start ONHARU with Windows
korean.AutomaticStart=자동 실행:
english.AutomaticStart=Startup:
korean.LaunchOnharu=ONHARU 실행
english.LaunchOnharu=Launch ONHARU
korean.KeepUserDataTitle=사용자 데이터 보관
english.KeepUserDataTitle=Keep user data
korean.KeepUserDataQuestion=일정, 설정, 일기, 시간표, 백업과 연결 정보를 보관하시겠습니까?%n%n예: 재설치를 위해 보관 (권장)%n아니요: 모든 사용자 데이터 삭제%n취소: 제거 중단
english.KeepUserDataQuestion=Keep schedules, settings, diary, timetable, backups, and connection data?%n%nYes: Keep for reinstallation (recommended)%nNo: Delete all user data%nCancel: Stop uninstalling
korean.DeleteUserDataWarning=모든 ONHARU 사용자 데이터를 삭제합니다.%n이 작업은 되돌릴 수 없습니다. 계속하시겠습니까?
english.DeleteUserDataWarning=All ONHARU user data will be deleted.%nThis cannot be undone. Continue?

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:Shortcuts}"; Flags: checkedonce
Name: "startup"; Description: "{cm:StartWithWindows}"; GroupDescription: "{cm:AutomaticStart}"; Flags: checkedonce

[Files]
Source: "..\Release\ONHARU-2.2.3\ONHARU.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Release\ONHARU-2.2.3\ONHARU.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Release\ONHARU-2.2.3\Onharu.LayerHost.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Release\ONHARU-2.2.3\Onharu.DesktopHook.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Release\ONHARU-2.2.3\README.md"; DestDir: "{app}"; DestName: "ONHARU-사용설명서.md"; Flags: ignoreversion

[InstallDelete]
Type: files; Name: "{app}\layer-host.log"

[Icons]
Name: "{group}\ONHARU"; Filename: "{app}\ONHARU.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\ONHARU"; Filename: "{app}\ONHARU.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userstartup}\ONHARU"; Filename: "{app}\ONHARU.exe"; WorkingDir: "{app}"; Tasks: startup

[Run]
Filename: "{app}\ONHARU.exe"; Description: "{cm:LaunchOnharu}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\Onharu.LayerHost.exe"; Parameters: "--stop"; WorkingDir: "{app}"; Flags: runhidden waituntilterminated; RunOnceId: "StopLayerHost"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Onharu\logs"
Type: filesandordirs; Name: "{localappdata}\Onharu\kbo-team-logos"
Type: files; Name: "{localappdata}\Onharu\sports-kbo-parse-*.json"

[Code]
var
  DeleteOnharuUserData: Boolean;

function InitializeUninstall(): Boolean;
var
  Answer: Integer;
begin
  DeleteOnharuUserData := False;
  if UninstallSilent then
  begin
    Result := True;
    exit;
  end;

  Answer := MsgBox(ExpandConstant('{cm:KeepUserDataQuestion}'), mbConfirmation, MB_YESNOCANCEL);
  if Answer = IDCANCEL then
  begin
    Result := False;
    exit;
  end;

  if Answer = IDNO then
  begin
    if MsgBox(ExpandConstant('{cm:DeleteUserDataWarning}'), mbError, MB_YESNO) <> IDYES then
    begin
      Result := False;
      exit;
    end;
    DeleteOnharuUserData := True;
  end;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and DeleteOnharuUserData then
    DelTree(ExpandConstant('{localappdata}\Onharu'), True, True, True);
end;
