; See.Net 安装包脚本（Inno Setup 6+）
;
; 编译参数（均由 CI 注入，本地编译可用 ISCC /D 覆盖）：
;   SourceDir  —— dotnet publish 输出目录（含 See.exe 与 webassets\）
;   Version    —— 安装包版本号（如 1.0.0）
;   OutputDir  —— 安装包输出目录
;   RepoRoot   —— 仓库根目录（用于读取 NOTICE / 图标）
;
; 例：
;   ISCC setup.iss /DSourceDir=..\..\artifacts\publish /DVersion=1.0.0 /DOutputDir=..\..\artifacts

#define MyAppName "See.Net"
#define MyAppPublisher "Fireneb-炎序星图"
#define MyAppExeName "See.exe"
#define MyAppURL "https://github.com/yourname/See.Net"

; 编译参数默认值（CI 会覆盖）
#ifndef SourceDir
  #define SourceDir "..\..\artifacts\publish"
#endif
#ifndef Version
  #define Version "1.0.0"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts"
#endif
#ifndef RepoRoot
  #define RepoRoot "..\.."
#endif

[Setup]
AppId={{8F3A2B1C-9D4E-4F5A-8B6C-7D2E1A0F9C3B}
AppName={#MyAppName}
AppVersion={#Version}
AppVerName={#MyAppName} {#Version}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; 安装权限：非管理员也可安装（按用户），避免 Program Files 写入问题
PrivilegesRequiredOverridesAllowed=dialog
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=See-{#Version}-Setup
SetupIconFile={#RepoRoot}\packaging\assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
; 卸载时确认
UninstallDisplayName={#MyAppName}
; 不显示版本号在添加/删除程序
VersionInfoVersion={#Version}

[Languages]
Name: "chinesesimp"; MessagesFile: "{#RepoRoot}\packaging\inno\languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 发布目录全部内容（含 webassets\、runtimes\、*.dll）
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; 安装完成后可选立即启动（托盘常驻）
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 仅清理程序目录，用户数据（Documents\FNSoftware\See）保留
Type: filesandordirs; Name: "{app}"
