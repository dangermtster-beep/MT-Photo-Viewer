; ============================================================================
;  MT Photo Viewer — Inno Setup kurulum betiği (Guide §43, §45, Faz 5)
;
;  Derleme:  tools\build-installer.ps1
;  Çıktı:    setup\output\MT_Photo_Viewer_Setup_<sürüm>.exe
; ============================================================================

#define AppName        "MT Photo Viewer"
; Sürüm tools\build-installer.ps1 tarafından csproj'dan okunup verilir:
;   ISCC /DAppVersion=1.0.1
#ifndef AppVersion
  #error AppVersion verilmedi. Kurulumu tools\build-installer.ps1 ile derleyin.
#endif
#define AppPublisher   "Metin TUNÇER"
#define AppExeName     "MT.PhotoViewer.exe"
#define ProgId         "MT.PhotoViewer.Image"
#define SourceDir      "..\publish\win-x64"

[Setup]
; AppId kurulumun kimliğidir — yükseltmelerde ASLA değiştirme.
AppId={{8F3A6C21-5D74-4E9B-9C0A-2B7E1F4D6A83}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} Kurulumu

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes

; Tüm kullanıcılar için kurulum — dosya ilişkilendirmeleri HKLM'e yazılır.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

OutputDir=output
OutputBaseFilename=MT_Photo_Viewer_Setup_{#AppVersion}
SetupIconFile=..\MT.PhotoViewer\Assets\Logo\MT_PhotoViewer_Logo.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; Kurulum bittiğinde Explorer'ın ikon/menü önbelleğini tazeler.
ChangesAssociations=yes

; Uygulama açıkken kurulum yapılmasını engelle.
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
AppMutex=MT.PhotoViewer.SingleInstance

[Languages]
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; \
    Description: "{cm:CreateDesktopIcon}"; \
    GroupDescription: "{cm:AdditionalIcons}"

Name: "fileassoc"; \
    Description: "Resim dosyalarını ""Birlikte Aç"" listesine ekle"; \
    GroupDescription: "Windows tümleştirmesi:"

Name: "contextmenu"; \
    Description: "Sağ tık menüsüne ""MT Photo Viewer ile Aç"" ekle"; \
    GroupDescription: "Windows tümleştirmesi:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs; \
    Excludes: "*.pdb"

[Icons]
Name: "{group}\{#AppName}";            Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";      Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; \
    Description: "{cm:LaunchProgram,{#AppName}}"; \
    Flags: nowait postinstall skipifsilent

; Uygulama içinden güncelleme (/SILENT /UPDATE=1): kurulum bitince uygulamayı
; yeniden aç. runasoriginaluser — yönetici olarak açılırsa Explorer'dan sürükle-bırak çalışmaz.
Filename: "{app}\{#AppExeName}"; \
    Flags: nowait runasoriginaluser; \
    Check: IsUpdateMode

[Registry]
; ---------------------------------------------------------------------------
;  1) ProgID — dosyanın MT Photo Viewer ile nasıl açılacağını tanımlar
; ---------------------------------------------------------------------------
Root: HKA; Subkey: "Software\Classes\{#ProgId}"; \
    ValueType: string; ValueName: ""; ValueData: "Fotoğraf"; \
    Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#ProgId}"; \
    ValueType: string; ValueName: "FriendlyTypeName"; ValueData: "Fotoğraf"
Root: HKA; Subkey: "Software\Classes\{#ProgId}\DefaultIcon"; \
    ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"
Root: HKA; Subkey: "Software\Classes\{#ProgId}\shell\open"; \
    ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"
Root: HKA; Subkey: "Software\Classes\{#ProgId}\shell\open\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""

; ---------------------------------------------------------------------------
;  2) Applications girdisi — "Birlikte Aç" listesinde görünmesini sağlar
; ---------------------------------------------------------------------------
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}"; \
    ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"; \
    Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\DefaultIcon"; \
    ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\shell\open\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""

Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".jpg";  ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".jpeg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".png";  ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".bmp";  ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".gif";  ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".webp"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".tif";  ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".tiff"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".ico";  ValueData: ""

; ---------------------------------------------------------------------------
;  3) OpenWithProgids — varsayılanı GASP ETMEDEN "Birlikte Aç"a ekler.
;     (Windows 10+ varsayılan uygulamayı yalnızca kullanıcı değiştirebilir.)
; ---------------------------------------------------------------------------
Root: HKA; Subkey: "Software\Classes\.jpg\OpenWithProgids";  ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.jpeg\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.png\OpenWithProgids";  ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.bmp\OpenWithProgids";  ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.gif\OpenWithProgids";  ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.webp\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.tif\OpenWithProgids";  ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.tiff\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.ico\OpenWithProgids";  ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc

; ---------------------------------------------------------------------------
;  4) Varsayılan Uygulamalar kaydı — Ayarlar > Varsayılan uygulamalar'da görünür
; ---------------------------------------------------------------------------
Root: HKLM; Subkey: "Software\{#AppName}\Capabilities"; \
    ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"; \
    Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\{#AppName}\Capabilities"; \
    ValueType: string; ValueName: "ApplicationDescription"; \
    ValueData: "Hızlı, sade ve modern fotoğraf görüntüleyici"
Root: HKLM; Subkey: "Software\{#AppName}\Capabilities"; \
    ValueType: string; ValueName: "ApplicationIcon"; ValueData: "{app}\{#AppExeName},0"

Root: HKLM; Subkey: "Software\{#AppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpg";  ValueData: "{#ProgId}"
Root: HKLM; Subkey: "Software\{#AppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpeg"; ValueData: "{#ProgId}"
Root: HKLM; Subkey: "Software\{#AppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".png";  ValueData: "{#ProgId}"
Root: HKLM; Subkey: "Software\{#AppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".bmp";  ValueData: "{#ProgId}"
Root: HKLM; Subkey: "Software\{#AppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".gif";  ValueData: "{#ProgId}"
Root: HKLM; Subkey: "Software\{#AppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webp"; ValueData: "{#ProgId}"
Root: HKLM; Subkey: "Software\{#AppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tif";  ValueData: "{#ProgId}"
Root: HKLM; Subkey: "Software\{#AppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tiff"; ValueData: "{#ProgId}"
Root: HKLM; Subkey: "Software\{#AppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ico";  ValueData: "{#ProgId}"

Root: HKLM; Subkey: "Software\RegisteredApplications"; \
    ValueType: string; ValueName: "{#AppName}"; \
    ValueData: "Software\{#AppName}\Capabilities"; \
    Flags: uninsdeletevalue

; ---------------------------------------------------------------------------
;  5) Sağ tık menüsü — resim dosyaları (PerceivedType = image)
; ---------------------------------------------------------------------------
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\image\shell\MTPhotoViewer"; \
    ValueType: string; ValueName: ""; ValueData: "MT Photo Viewer ile Aç"; \
    Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\image\shell\MTPhotoViewer"; \
    ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName},0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\image\shell\MTPhotoViewer\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: contextmenu

; PerceivedType tanımlı olmayan uzantılar (ör. .webp, .ico) için açık kayıt
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webp\shell\MTPhotoViewer"; ValueType: string; ValueName: ""; ValueData: "MT Photo Viewer ile Aç"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webp\shell\MTPhotoViewer"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName},0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webp\shell\MTPhotoViewer\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: contextmenu

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ico\shell\MTPhotoViewer"; ValueType: string; ValueName: ""; ValueData: "MT Photo Viewer ile Aç"; Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ico\shell\MTPhotoViewer"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName},0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ico\shell\MTPhotoViewer\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: contextmenu

; ---------------------------------------------------------------------------
;  6) Sağ tık menüsü — klasörler (klasördeki ilk fotoğrafı açar)
; ---------------------------------------------------------------------------
Root: HKA; Subkey: "Software\Classes\Directory\shell\MTPhotoViewer"; \
    ValueType: string; ValueName: ""; ValueData: "MT Photo Viewer ile Görüntüle"; \
    Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\MTPhotoViewer"; \
    ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName},0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\MTPhotoViewer\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%V"""; Tasks: contextmenu

Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\MTPhotoViewer"; \
    ValueType: string; ValueName: ""; ValueData: "MT Photo Viewer ile Görüntüle"; \
    Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\MTPhotoViewer"; \
    ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName},0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\MTPhotoViewer\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%V"""; Tasks: contextmenu

; ---------------------------------------------------------------------------
;  7) App Paths — Çalıştır (Win+R) kutusundan "MT.PhotoViewer" ile açılır
; ---------------------------------------------------------------------------
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExeName}"; \
    ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName}"; \
    Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExeName}"; \
    ValueType: string; ValueName: "Path"; ValueData: "{app}"

; Not: Kullanıcı ayarları (%AppData%\MT.PhotoViewer\settings.json) kaldırma
; sırasında silinmez. Kurulum yönetici modunda çalıştığı için oradan yazılan
; per-user yollar yanlış profile denk gelir; ayrıca yeniden kurulumda
; tercihlerin korunması beklenen davranıştır.

[Code]
function IsUpdateMode: Boolean;
begin
  Result := ExpandConstant('{param:UPDATE|0}') = '1';
end;

[CustomMessages]
tr.LaunchProgram=%1 uygulamasını çalıştır
tr.CreateDesktopIcon=Masaüstü kısayolu oluştur
tr.AdditionalIcons=Kısayollar:
