<#
    MT Photo Viewer — derlenmiş sürümü GitHub Releases'a yayınlar.

    Önce kurulumu derleyin:
        powershell -ExecutionPolicy Bypass -File tools\build-installer.ps1 -Bump patch
    Sonra yayınlayın:
        powershell -ExecutionPolicy Bypass -File tools\publish-release.ps1

    Yayınlanan sürüm "Latest" olarak işaretlenir; kurulu uygulamalar bir sonraki
    açılışta "Güncelleme var" rozetini gösterir.

    Gereksinimler:
        - GitHub CLI:  winget install GitHub.cli   ardından   gh auth login
        - csproj içinde <GitHubRepo>sahip/depo</GitHubRepo> (depo herkese açık olmalı)
#>
param(
    # Taslak olarak oluşturur: kullanıcılar görmez, GitHub'da kontrol edip elle yayınlarsınız.
    [switch]$Draft
)

$ErrorActionPreference = 'Stop'

$root      = Split-Path -Parent $PSScriptRoot
$project   = Join-Path $root 'MT.PhotoViewer\MT.PhotoViewer.csproj'
$outputDir = Join-Path $root 'setup\output'
$manifest  = Join-Path $outputDir 'version.json'
$utf8      = New-Object System.Text.UTF8Encoding($false)

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) bulunamadı. Kurun: winget install GitHub.cli   ardından: gh auth login"
}

& gh auth status *> $null
if ($LASTEXITCODE -ne 0) { throw "GitHub'a giriş yapılmamış. Çalıştırın: gh auth login" }

$csproj  = [IO.File]::ReadAllText($project, $utf8)
$version = [regex]::Match($csproj, '<Version>([^<]+)</Version>').Groups[1].Value
$repo    = [regex]::Match($csproj, '<GitHubRepo>([^<]*)</GitHubRepo>').Groups[1].Value.Trim()

if (-not $repo) { throw "csproj içinde <GitHubRepo>sahip/depo</GitHubRepo> boş." }

$setupName = "MT_Photo_Viewer_Setup_$version.exe"
$setupPath = Join-Path $outputDir $setupName

if (-not (Test-Path $setupPath) -or -not (Test-Path $manifest)) {
    throw "v$version kurulumu derlenmemiş. Önce tools\build-installer.ps1 çalıştırın."
}

$feed = ConvertFrom-Json ([IO.File]::ReadAllText($manifest, $utf8))
if ($feed.version -ne $version) {
    throw "version.json v$($feed.version) diyor ama csproj v$version. Kurulumu yeniden derleyin."
}
if ((Get-FileHash $setupPath -Algorithm SHA256).Hash -ne $feed.sha256) {
    throw "Kurulum dosyası version.json'daki SHA-256 ile eşleşmiyor. Kurulumu yeniden derleyin."
}

$tag = "v$version"

& gh release view $tag --repo $repo *> $null
if ($LASTEXITCODE -eq 0) {
    throw "$repo deposunda $tag zaten yayınlanmış. Yeni sürüm için: build-installer.ps1 -Bump patch"
}

# Sürüm sayfasında görünen açıklama: bu sürümün notları.
$current = $feed.notes | Where-Object { $_.version -eq $version } | Select-Object -First 1
$body = ($current.changes | ForEach-Object { "- $_" }) -join "`n"
$notesFile = Join-Path $env:TEMP "mtpv_release_notes_$version.md"
[IO.File]::WriteAllText($notesFile, $body, $utf8)

$ghArgs = @(
    'release', 'create', $tag, $setupPath, $manifest,
    '--repo', $repo,
    '--title', "MT Photo Viewer $tag",
    '--notes-file', $notesFile
)
if ($Draft) { $ghArgs += '--draft' } else { $ghArgs += '--latest' }

Write-Host "==> $repo deposuna $tag yayınlanıyor..." -ForegroundColor Cyan
& gh @ghArgs
if ($LASTEXITCODE -ne 0) { throw "GitHub sürümü oluşturulamadı." }

Remove-Item $notesFile -ErrorAction SilentlyContinue

Write-Host ""
if ($Draft) {
    Write-Host "Taslak oluşturuldu: https://github.com/$repo/releases — kontrol edip 'Publish' deyin." -ForegroundColor Yellow
} else {
    Write-Host "Yayınlandı: https://github.com/$repo/releases/tag/$tag" -ForegroundColor Green
    Write-Host "Kurulu uygulamalar bir sonraki açılışta güncellemeyi görecek."
}
