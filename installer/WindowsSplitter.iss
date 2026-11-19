#define MyAppName "Windows Splitter"
#ifndef MyAppVersion
  #define MyAppVersion "1.1.0"
#endif
#define MyAppPublisher "LoiCao (Thanhloi-lab)"
#define MyAppURL "https://github.com/Thanhloi-lab/Window_Splitter"
#define MyAppExeName "WindowsSplitter.exe"

[Setup]
AppId={{8174DB58-6070-4C37-A235-49BB6B6EB801}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
LicenseFile=License.txt
DefaultDirName={autopf}\Windows Splitter
DefaultGroupName={#MyAppName}
OutputDir=output
OutputBaseFilename=WindowsSplitter-Setup-{#MyAppVersion}-x64
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "License.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Windows Splitter"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Windows Splitter"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Windows Splitter"; Verb: "runas"; Flags: shellexec postinstall skipifsilent

[Code]
var
  AdminConsentPage: TInputOptionWizardPage;

procedure InitializeWizard;
begin
  AdminConsentPage := CreateInputOptionPage(wpLicense,
    'Administrator privileges required',
    'Windows Splitter must run as an administrator.',
    'Allow Windows Splitter Setup to use administrator privileges to install and run the application:',
    False, False);
  AdminConsentPage.Add('I allow Windows Splitter to run with administrator privileges');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (AdminConsentPage <> nil) and (CurPageID = AdminConsentPage.ID) and
     (not AdminConsentPage.Values[0]) then
  begin
    MsgBox('Select the administrator permission checkbox to continue.', mbError, MB_OK);
    Result := False;
  end;
end;
