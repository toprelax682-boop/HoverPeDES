; HoverPeDES installer (Inno Setup 6)
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{B6E2D0A4-6C1E-4B7B-9A55-4E7D3C1F0A21}
AppName=HoverPeDES
AppVersion={#AppVersion}
AppPublisher=HoverPeDES
DefaultDirName={autopf}\HoverPeDES
DefaultGroupName=HoverPeDES
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=HoverPeDES-Setup-{#AppVersion}
SetupIconFile=..\src\HoverPeDES\Assets\hoverpedes.ico
UninstallDisplayIcon={app}\HoverPeDES.exe
LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
AppMutex=Local\HoverPeDES.SingleInstance
ShowLanguageDialog=auto

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "vi"; MessagesFile: "Languages\Vietnamese.isl"

[CustomMessages]
en.Autostart=Start HoverPeDES when Windows starts
vi.Autostart=Khởi động HoverPeDES cùng Windows
en.LaunchApp=Launch HoverPeDES now
vi.LaunchApp=Chạy HoverPeDES ngay
en.ExtHint=After setup, right-click the HoverPeDES tray icon and choose "Install browser extension" to preview videos and images on websites.
vi.ExtHint=Sau khi cài, bấm chuột phải vào biểu tượng HoverPeDES ở khay hệ thống và chọn "Cài tiện ích trình duyệt" để xem trước video và ảnh trên web.

[Tasks]
Name: "autostart"; Description: "{cm:Autostart}"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\extension\*"; DestDir: "{app}\extension"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\HoverPeDES"; Filename: "{app}\HoverPeDES.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "HoverPeDES"; ValueData: """{app}\HoverPeDES.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\HoverPeDES.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im HoverPeDES.exe"; Flags: runhidden; RunOnceId: "KillHoverPeDES"

[Code]
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
    WizardForm.FinishedLabel.Caption := WizardForm.FinishedLabel.Caption + #13#10#13#10 + CustomMessage('ExtHint');
end;
