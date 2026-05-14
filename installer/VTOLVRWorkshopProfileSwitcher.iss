#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef PublisherName
  #define PublisherName "B1progame | VTOLVR Workshop Tools"
#endif

#ifndef AppChannel
  #define AppChannel "Stable"
#endif

#ifndef MyAppName
  #define MyAppName "VTOL VR Switcher"
#endif

#ifndef MyAppId
  #define MyAppId "{{6AB2D1C3-8D31-45E8-8B3F-AC5C8C1A7E12}"
#endif

#ifndef MyOutputBaseFilename
  #define MyOutputBaseFilename "VTOLVRSwitcher-Setup"
#endif

#ifndef MyDefaultDirName
  #define MyDefaultDirName "{autopf}\VTOL VR Switcher"
#endif

#ifndef MyDefaultGroupName
  #define MyDefaultGroupName "VTOL VR Switcher"
#endif

#ifndef DefaultAutoInstallUpdates
  #define DefaultAutoInstallUpdates "false"
#endif

#ifndef DefaultIncludeBetaUpdates
  #define DefaultIncludeBetaUpdates "false"
#endif

#ifndef SourceDir
  #define SourceDir "publish\\win-x64"
#endif

#ifndef IconFile
  #define IconFile "src\\VTOLVRWorkshopProfileSwitcher\\Assets\\AppIcon.ico"
#endif

#ifndef LicenseFile
  #define LicenseFile "LICENSE"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#PublisherName}
AppPublisherURL=https://github.com/B1progame/Vtol-Vr-Mod
AppSupportURL=https://github.com/B1progame/Vtol-Vr-Mod/issues
AppUpdatesURL=https://github.com/B1progame/Vtol-Vr-Mod/releases
DefaultDirName={#MyDefaultDirName}
DefaultGroupName={#MyDefaultGroupName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename={#MyOutputBaseFilename}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\VTOLVRWorkshopProfileSwitcher.exe
SetupIconFile={#IconFile}
LicenseFile={#LicenseFile}
VersionInfoCompany={#PublisherName}
VersionInfoDescription={#MyAppName} Installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\VTOLVRWorkshopProfileSwitcher.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\VTOLVRWorkshopProfileSwitcher.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VTOLVRWorkshopProfileSwitcher.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  AutoUpdatePage: TInputOptionWizardPage;
  RemoveUserData: Boolean;

procedure InitializeWizard;
begin
  AutoUpdatePage :=
    CreateInputOptionPage(
      wpSelectTasks,
      'Update Preferences',
      'Automatic updates',
      'Choose whether VTOL VR Switcher should auto-install updates when available.',
      True,
      False);

  AutoUpdatePage.Add('Enable automatic updates');
  AutoUpdatePage.Values[0] := False;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  DataDir: string;
  SettingsPath: string;
  AutoInstallUpdatesValue: string;
  SettingsJson: string;
begin
  if CurStep <> ssInstall then
  begin
    exit;
  end;

  DataDir := ExpandConstant('{localappdata}\VTOLVR-WorkshopProfiles');
  if not DirExists(DataDir) then
  begin
    ForceDirectories(DataDir);
  end;

  SettingsPath := AddBackslash(DataDir) + 'settings.json';

  if FileExists(SettingsPath) then
  begin
    exit;
  end;

  if AutoUpdatePage.Values[0] then
  begin
    AutoInstallUpdatesValue := 'true';
  end
  else
  begin
    AutoInstallUpdatesValue := 'false';
  end;

  SettingsJson :=
    '{'#13#10 +
    '  "selectedDesign": "TACTICAL RED",'#13#10 +
    '  "openSteamPageAfterDelete": true,'#13#10 +
    '  "autoInstallUpdates": ' + AutoInstallUpdatesValue + ','#13#10 +
    '  "includeBetaUpdates": {#DefaultIncludeBetaUpdates},'#13#10 +
    '  "vrRuntime": "SteamVR"'#13#10 +
    '}'#13#10;

  SaveStringToFile(SettingsPath, SettingsJson, False);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    RemoveUserData :=
      MsgBox(
        'Also remove user data (profiles, backups, logs)?'#13#10#13#10 +
        'Path: ' + ExpandConstant('{localappdata}\VTOLVR-WorkshopProfiles'),
        mbConfirmation, MB_YESNO) = IDYES;

    if RemoveUserData then
    begin
      DataDir := ExpandConstant('{localappdata}\VTOLVR-WorkshopProfiles');
      if DirExists(DataDir) then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
