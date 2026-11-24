; Inno Setup Script for Dynamic Web Panel (.NET 10 + WebView2)
; Installs the native application files and registers desktop/start menu shortcuts.
; Cleans up running processes (including legacy Electron installs), startup entries, and user data on uninstall.

[Setup]
AppId=com.dynamicwebpanel.app
AppName=Dynamic Web Panel
AppVersion=2.0.0
AppPublisher=Dynamic Web Panel
DefaultDirName={localappdata}\Programs\DynamicWebPanel
DisableProgramGroupPage=yes
DefaultGroupName=Dynamic Web Panel
OutputDir=dist
OutputBaseFilename=DynamicWebPanelSetup
SetupIconFile=Assets\icon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\icon.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "dist\win-unpacked\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Assets\icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Dynamic Web Panel"; Filename: "{app}\DynamicWebPanel.exe"; IconFilename: "{app}\icon.ico"
Name: "{autodesktop}\Dynamic Web Panel"; Filename: "{app}\DynamicWebPanel.exe"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon
Name: "{group}\Uninstall Dynamic Web Panel"; Filename: "{uninstallexe}"

[Code]
function RemoveQuotes(const S: String): String;
begin
  Result := S;
  if (Length(Result) >= 2) and (Result[1] = '"') and (Result[Length(Result)] = '"') then
  begin
    Result := Copy(Result, 2, Length(Result) - 2);
  end;
end;

function GetUninstallString(): String;
var
  UninstallPath: String;
begin
  Result := '';
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\com.dynamicwebpanel.app_is1', 'UninstallString', UninstallPath) then
    Result := UninstallPath
  else if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{com.dynamicwebpanel.app}_is1', 'UninstallString', UninstallPath) then
    Result := UninstallPath
  else if RegQueryStringValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\com.dynamicwebpanel.app_is1', 'UninstallString', UninstallPath) then
    Result := UninstallPath
  else if RegQueryStringValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{com.dynamicwebpanel.app}_is1', 'UninstallString', UninstallPath) then
    Result := UninstallPath;
end;

procedure KillRunningProcesses();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im DynamicWebPanel.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im dynamic-web-panel.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
  UninstallPath: String;
  MsgResult: Integer;
  ButtonLabels: TArrayOfString;
begin
  Result := True;
  KillRunningProcesses();

  UninstallPath := GetUninstallString();
  if UninstallPath <> '' then
  begin
    UninstallPath := RemoveQuotes(UninstallPath);

    SetArrayLength(ButtonLabels, 3);
    ButtonLabels[0] := 'Uninstall and Exit';
    ButtonLabels[1] := 'Uninstall and Reinstall';
    ButtonLabels[2] := 'Cancel';

    MsgResult := TaskDialogMsgBox(
      'Dynamic Web Panel is already installed.',
      'An existing version of Dynamic Web Panel was found on your system. Please choose whether you want to completely uninstall and clean out the application, perform a clean reinstall, or cancel the setup.',
      mbConfirmation,
      MB_YESNOCANCEL,
      ButtonLabels,
      0
    );

    if MsgResult = idYes then
    begin
      if Exec(UninstallPath, '/SILENT', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
      begin
        MsgBox('Dynamic Web Panel has been successfully uninstalled and all data cleared.', mbInformation, MB_OK);
      end
      else
      begin
        MsgBox('Failed to run the uninstaller.', mbError, MB_OK);
      end;
      Result := False;
    end
    else if MsgResult = idNo then
    begin
      if Exec(UninstallPath, '/SILENT', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
      begin
        Result := True;
      end
      else
      begin
        if MsgBox('Failed to run the uninstaller. Do you want to proceed with the installation anyway?', mbError, MB_YESNO) = idYes then
          Result := True
        else
          Result := False;
      end;
    end
    else
    begin
      Result := False;
    end;
  end;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  KillRunningProcesses();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'DynamicWebPanel');
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'dynamic-web-panel');

    DelTree(ExpandConstant('{userappdata}\dynamic-web-panel'), True, True, True);
    DelTree(ExpandConstant('{localappdata}\DynamicWebPanel'), True, True, True);
    DelTree(ExpandConstant('{app}'), True, True, True);
  end;
end;

[Run]
Filename: "{app}\DynamicWebPanel.exe"; Description: "{cm:LaunchProgram,Dynamic Web Panel}"; Flags: nowait postinstall skipifsilent
