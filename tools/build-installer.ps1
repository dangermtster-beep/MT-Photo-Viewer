<#
    MT Photo Viewer — kurulum dosyasını baştan sona üretir.

        csproj <Version>  ->  publish  ->  Inno Setup  ->  setup\output\
                                                         ├─ MT_Photo_Viewer_Setup_<sürüm>.exe
                                                         └─ version.json   (güncelleme bildirimi)

    YENİ SÜRÜM ÇIKARMAK
        1) MT.PhotoViewer\Assets\ReleaseNotes.json dosyasının EN ÜSTÜNE yeni sürümün
           notlarını ekleyin (kullanıcı "Yenilikler" penceresinde bunu görür).
        2) Sürümü artırıp derleyin:
               powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1 -Bump patch
           patch: 1.0.1 -> 1.0.2  (hata düzeltmesi)
           minor: 1.0.1 -> 1.1.0  (yeni özellik)
           major: 1.0.1 -> 2.0.0  (büyük değişiklik)
        3) setup\output içindeki .exe ve version.json'u güncelleme sunucusuna yükleyin
           (ya da -FeedDir ile doğrudan bir klasöre/paylaşıma kopyalatın).

    Script şu durumlarda DURUR:
        - ReleaseNotes.json'da bu sürümün notu yoksa,
        - sürüm, daha önce derlenmiş sürümden düşükse.

    Parametreler:
        -Bump patch|minor|major  csproj'daki sürümü artırır
        -FeedDir <klasör>        .exe ve version.json'u bu klasöre de kopyalar
        -FeedUrl <adres>         bu derleme için csproj'daki <UpdateFeedUrl>'i geçersiz kılar
        -FrameworkDependent      .NET çalışma zamanını paketlemez (~3 MB kurulum)
#>
param(
    [ValidateSet('patch', 'minor', 'major')]
    [string]$Bump,
    [string]$FeedDir,
    [string]$FeedUrl,
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'

$root      = Split-Path -Parent $PSScriptRoot
$project   = Join-Path $root 'MT.PhotoViewer\MT.PhotoViewer.csproj'
$notesPath = Join-Path $root 'MT.PhotoViewer\Assets\ReleaseNotes.json'
$publish   = Join-Path $root 'publish\win-x64'
$script    = Join-Path $root 'setup\MT.PhotoViewer.iss'
$outputDir = Join-Path $root 'setup\output'
$manifest  = Join-Path $outputDir 'version.json'
$changelog = Join-Path $root 'CHANGELOG.md'
$utf8      = New-Object System.Text.UTF8Encoding($false)   # BOM'suz

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 bulunamadı. https://jrsoftware.org/isdl.php adresinden kurun."
}

# --- 1) Sürüm (tek kaynak: csproj) ----------------------------------------
$csproj = [IO.File]::ReadAllText($project, $utf8)
$match  = [regex]::Match($csproj, '<Version>(\d+)\.(\d+)\.(\d+)</Version>')
if (-not $match.Success) { throw "csproj içinde <Version>X.Y.Z</Version> bulunamadı." }

$major = [int]$match.Groups[1].Value
$minor = [int]$match.Groups[2].Value
$patch = [int]$match.Groups[3].Value

if ($Bump) {
    switch ($Bump) {
        'major' { $major++; $minor = 0; $patch = 0 }
        'minor' { $minor++; $patch = 0 }
        'patch' { $patch++ }
    }
    $newTag = "<Version>$major.$minor.$patch</Version>"
    $csproj = $csproj.Remove($match.Index, $match.Length).Insert($match.Index, $newTag)
    [IO.File]::WriteAllText($project, $csproj, $utf8)
    Write-Host "==> Sürüm artırıldı: $($match.Groups[0].Value -replace '</?Version>','') -> $major.$minor.$patch" -ForegroundColor Cyan
}

$Version = "$major.$minor.$patch"
$semver  = [version]$Version

# --- 2) Sürüm notları zorunlu ---------------------------------------------
# PowerShell 5 ConvertFrom-Json diziyi tek nesne olarak aktarır; önce değişkene al.
$rawNotes = ConvertFrom-Json ([IO.File]::ReadAllText($notesPath, $utf8))
$notes = @($rawNotes | Sort-Object { [version]$_.version } -Descending)

$current = $notes | Where-Object { $_.version -eq $Version } | Select-Object -First 1
if (-not $current -or @($current.changes).Count -eq 0) {
    throw @"
v$Version için sürüm notu yok. Kullanıcıya ne değiştiğini söylemeden sürüm çıkarılamaz.
$notesPath dosyasının EN ÜSTÜNE şunu ekleyin:

  {
    "version": "$Version",
    "date": "$(Get-Date -Format 'yyyy-MM-dd')",
    "changes": [
      "Değişiklik 1",
      "Değişiklik 2"
    ]
  },
"@
}

if ([version]$notes[0].version -gt $semver) {
    throw "ReleaseNotes.json'daki en yeni sürüm (v$($notes[0].version)) csproj'daki sürümden (v$Version) yüksek. -Bump kullanmayı mı unuttunuz?"
}

# --- 3) Sürüm geriye gidemez ----------------------------------------------
if (Test-Path $manifest) {
    $previous = [version](([IO.File]::ReadAllText($manifest, $utf8) | ConvertFrom-Json).version)
    if ($semver -lt $previous) {
        throw "v$Version, daha önce derlenmiş v$previous sürümünden düşük. Sürüm yalnızca artabilir."
    }
    if ($semver -eq $previous) {
        Write-Warning ("v$Version zaten derlenmişti; aynı sürüm yeniden derleniyor. " +
            "Kullanıcılara dağıtılacak bir değişiklikse -Bump patch kullanın, yoksa kimse güncelleme bildirimi almaz.")
    }
}

$repoMatch = [regex]::Match($csproj, '<GitHubRepo>([^<]*)</GitHubRepo>')
if (-not $PSBoundParameters.ContainsKey('FeedUrl') -and -not ($repoMatch.Success -and $repoMatch.Groups[1].Value.Trim())) {
    Write-Warning ("csproj'da <GitHubRepo> boş: bu sürüm güncelleme kontrolü YAPMAYACAK. " +
        "Kullanıcılarının sonraki sürümü görebilmesi için depo adını şimdi girin.")
}

# --- 4) Uygulamayı yayınla -------------------------------------------------
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

$selfContained = (-not $FrameworkDependent).ToString().ToLower()
Write-Host "==> v$Version yayınlanıyor (self-contained: $selfContained)..." -ForegroundColor Cyan

$publishArgs = @(
    'publish', $project,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', $selfContained,
    '-p:PublishReadyToRun=true',
    "-p:Version=$Version",
    '-o', $publish,
    '--nologo'
)
if ($PSBoundParameters.ContainsKey('FeedUrl')) { $publishArgs += "-p:UpdateFeedUrl=$FeedUrl" }

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız oldu." }

# --- 5) Kurulum dosyasını derle -------------------------------------------
Write-Host "==> Kurulum dosyası derleniyor..." -ForegroundColor Cyan

& $iscc "/DAppVersion=$Version" $script
if ($LASTEXITCODE -ne 0) { throw "Inno Setup derlemesi başarısız oldu." }

$setupName = "MT_Photo_Viewer_Setup_$Version.exe"
$setupPath = Join-Path $outputDir $setupName
$setupItem = Get-Item $setupPath
$sha256    = (Get-FileHash $setupPath -Algorithm SHA256).Hash

# --- 6) version.json (uygulamanın okuduğu güncelleme bildirimi) -----------
$feed = [ordered]@{
    version = $Version
    date    = $current.date
    file    = $setupName
    sha256  = $sha256
    size    = $setupItem.Length
    notes   = @($notes | ForEach-Object {
        [ordered]@{ version = $_.version; date = $_.date; changes = @($_.changes) }
    })
}
[IO.File]::WriteAllText($manifest, (ConvertTo-Json -InputObject $feed -Depth 6), $utf8)

# --- 7) CHANGELOG.md (depo için okunabilir kopya) -------------------------
$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine('# Sürüm Notları')
[void]$md.AppendLine()
[void]$md.AppendLine('> Bu dosya `tools/build-installer.ps1` tarafından `MT.PhotoViewer/Assets/ReleaseNotes.json` kaynağından üretilir; elle düzenlemeyin.')
foreach ($n in $notes) {
    [void]$md.AppendLine()
    [void]$md.AppendLine("## v$($n.version) — $($n.date)")
    [void]$md.AppendLine()
    foreach ($c in $n.changes) { [void]$md.AppendLine("- $c") }
}
[IO.File]::WriteAllText($changelog, $md.ToString(), $utf8)

# --- 8) İsteğe bağlı: güncelleme klasörüne yayınla ------------------------
if ($FeedDir) {
    New-Item -ItemType Directory -Force -Path $FeedDir | Out-Null
    # Önce kurulum, sonra version.json: istemci, dosya yerinde olmadan yeni sürümü görmesin.
    Copy-Item $setupPath (Join-Path $FeedDir $setupName) -Force
    Copy-Item $manifest  (Join-Path $FeedDir 'version.json') -Force
    Write-Host "==> Güncelleme klasörüne kopyalandı: $FeedDir" -ForegroundColor Cyan
}

$size = [Math]::Round($setupItem.Length / 1MB, 1)
Write-Host ""
Write-Host "Hazır: v$Version" -ForegroundColor Green
Write-Host "  $setupPath ($size MB)"
Write-Host "  $manifest"
Write-Host "  SHA-256: $sha256"
