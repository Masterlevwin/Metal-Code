#define MyAppName "Metal-Code"
#define MyAppVersion "2.7.3"
#define MyAppExeName "Metal-Code.exe" ; Обратите внимание: в папке publish файл может называться просто Metal-Code
#define MyAppAssocName MyAppName + " File"
#define MyAppAssocExt ".mcm"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
AppId={{DA7DB734-1E3D-40EA-9046-D8CBAF91405A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\Metal-Code.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=C:\Users\User\Desktop
OutputBaseFilename=Setup_Metal-Code
SetupIconFile=C:\Users\User\source\repos\Masterlevwin\Metal-Code\app_logo.ico
Password=laserpro
Encryption=yes
SolidCompression=yes
WizardStyle=classic

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; ВАЖНО: Копируем ВСЁ из папки publish рекурсивно
Source: "C:\Users\User\source\repos\Masterlevwin\Metal-Code\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocExt}\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppAssocKey}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Metal-Code.exe,0"
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Metal-Code.exe"" ""%1"""

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\Metal-Code.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Metal-Code.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Metal-Code.exe"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent