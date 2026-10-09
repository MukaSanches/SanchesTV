param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

$ErrorActionPreference = "Stop"

$mpvUrl = "https://github.com/zhongfly/mpv-winbuild/releases/download/2026-10-05-5d85ba5fb3/mpv-dev-lgpl-x86_64-20261005-git-5d85ba5fb3.7z"
$mpvSha = "dce88bc3c7b8702415af3ec718dea14c797dfc6eaa77ca3a8b193f9f5e593c6a"

$ffmpegUrl = "https://github.com/zhongfly/mpv-winbuild/releases/download/2026-10-05-5d85ba5fb3/ffmpeg-lgpl-x86_64-git-2da55bf59.7z"
$ffmpegSha = "da1afa8d9069c309a07bf2adbd5234cc191bde9b056e953bf3831f9d9c702e3f"

# ffprobe separado e estático (LGPL) para diagnóstico Cinema OS.
# Mantemos o FFmpeg atual do mpv-winbuild para não alterar o runtime de gravação/player.
$ffprobeUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-10-08-13-05/ffmpeg-n9.0.2-23-g27b46f0fbc-win64-lgpl-9.0.zip"
$ffprobeSha = "74823968af824fddced214fbfa3aa47c73c0cb15fd225db8d73ee3212e9757f7"


$work = Join-Path $env:RUNNER_TEMP "sanchestv-media-runtime"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $work | Out-Null

function Download-GitHubAsset([string]$url, [string]$target) {
    $downloaded = $false
    $assetUri = [Uri]$url
    $segments = $assetUri.AbsolutePath.Trim("/").Split("/")
    if ($assetUri.Host -eq "github.com" -and $segments.Length -eq 6 -and
        $segments[2] -eq "releases" -and $segments[3] -eq "download") {
        $owner = $segments[0]
        $repository = $segments[1]
        $tag = [Uri]::UnescapeDataString($segments[4])
        $asset = [Uri]::UnescapeDataString($segments[5])
        $assetPath = Join-Path (Split-Path $target -Parent) $asset
        try {
            & gh release download $tag --repo "$owner/$repository" --pattern $asset --dir (Split-Path $target -Parent) --clobber
            if ($LASTEXITCODE -eq 0 -and (Test-Path $assetPath)) {
                Move-Item $assetPath $target -Force
                $downloaded = $true
            }
        } catch {
            Write-Warning "GitHub CLI could not retrieve $asset; using direct URL."
        }
    }
    if (-not $downloaded) {
        Invoke-WebRequest -Uri $url -OutFile $target -MaximumRetryCount 2 -RetryIntervalSec 4
    }
}
function Get-VerifiedArchive([string]$url, [string]$sha, [string]$name) {
    $path = Join-Path $work $name
    Download-GitHubAsset $url $path
    $actual = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $sha.ToLowerInvariant()) {
        throw "SHA-256 mismatch for $name. Expected $sha, got $actual"
    }
    return $path
}

$mpvArchive = Get-VerifiedArchive $mpvUrl $mpvSha "libmpv.7z"
$ffmpegArchive = Get-VerifiedArchive $ffmpegUrl $ffmpegSha "ffmpeg.7z"
$ffprobeArchive = Get-VerifiedArchive $ffprobeUrl $ffprobeSha "ffprobe.zip"

$mpvDir = Join-Path $work "mpv"
$ffmpegDir = Join-Path $work "ffmpeg"
$ffprobeDir = Join-Path $work "ffprobe"
New-Item -ItemType Directory -Force -Path $mpvDir,$ffmpegDir,$ffprobeDir | Out-Null

& 7z x $mpvArchive "-o$mpvDir" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Failed to extract libmpv." }

& 7z x $ffmpegArchive "-o$ffmpegDir" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Failed to extract FFmpeg." }

Expand-Archive -Path $ffprobeArchive -DestinationPath $ffprobeDir -Force

$mpvDll = Get-ChildItem $mpvDir -Recurse -Filter "libmpv-2.dll" | Select-Object -First 1
if (-not $mpvDll) { throw "libmpv-2.dll not found in archive." }

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
Get-ChildItem $mpvDll.Directory.FullName -Filter "*.dll" | ForEach-Object {
    Copy-Item $_.FullName -Destination $PublishDir -Force
}

$ffmpegExe = Get-ChildItem $ffmpegDir -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
if (-not $ffmpegExe) { throw "ffmpeg.exe not found in archive." }

$runtimeFfmpeg = Join-Path $PublishDir "runtime\ffmpeg"
New-Item -ItemType Directory -Force -Path $runtimeFfmpeg | Out-Null
Copy-Item $ffmpegExe.FullName -Destination (Join-Path $runtimeFfmpeg "ffmpeg.exe") -Force

$ffprobeExe = Get-ChildItem $ffprobeDir -Recurse -Filter "ffprobe.exe" | Select-Object -First 1
if (-not $ffprobeExe) { throw "ffprobe.exe not found in verified diagnostic archive." }
Copy-Item $ffprobeExe.FullName -Destination (Join-Path $runtimeFfmpeg "ffprobe.exe") -Force

$licenseDir = Join-Path $PublishDir "licenses\media-runtime"
New-Item -ItemType Directory -Force -Path $licenseDir | Out-Null

$provenance = @"
SanchesTV 8.4 media runtime

libmpv package:
$mpvUrl
SHA-256: $mpvSha
Package: mpv-dev-lgpl, LGPLv2.1+ build, FFmpeg linked under LGPLv3.

FFmpeg package:
$ffmpegUrl
SHA-256: $ffmpegSha
Package: ffmpeg-lgpl x86_64.

FFprobe diagnostic package:
$ffprobeUrl
SHA-256: $ffprobeSha
Package: BtbN FFmpeg n9.0 win64 LGPL; only ffprobe.exe is copied into the SanchesTV runtime.


The mpv-winbuild project documents FFmpeg, libplacebo, libass and dav1d among the integrated components.
"@
$provenance | Out-File (Join-Path $licenseDir "RUNTIME-PROVENANCE.txt") -Encoding utf8

Write-Host "libmpv: $($mpvDll.FullName)"
Write-Host "ffmpeg: $($ffmpegExe.FullName)"
Write-Host "ffprobe: $($ffprobeExe.FullName)"
Write-Host "Media runtime prepared in $PublishDir"
) {
        $owner = $Matches[1]
        $repository = $Matches[2]
        $tag = $Matches[3]
        $asset = [Uri]::UnescapeDataString($Matches[4])
        $directory = Split-Path $target -Parent
        $assetPath = Join-Path $directory $asset
        try {
            & gh release download $tag --repo "$owner/$repository" --pattern $asset --dir $directory --clobber
            if ($LASTEXITCODE -eq 0 -and (Test-Path $assetPath)) {
                Move-Item $assetPath $target -Force
                $downloaded = $true
            }
        } catch {
            Write-Warning "GitHub CLI download failed for $asset; trying direct URL."
        }
    }
    if (-not $downloaded) {
        Invoke-WebRequest -Uri $url -OutFile $target -MaximumRetryCount 2 -RetryIntervalSec 4
    }
}

function Get-VerifiedArchive([string]$url, [string]$sha, [string]$name) {
    $path = Join-Path $work $name
    Invoke-WebRequest -Uri $url -OutFile $path
    $actual = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $sha.ToLowerInvariant()) {
        throw "SHA-256 mismatch for $name. Expected $sha, got $actual"
    }
    return $path
}

$mpvArchive = Get-VerifiedArchive $mpvUrl $mpvSha "libmpv.7z"
$ffmpegArchive = Get-VerifiedArchive $ffmpegUrl $ffmpegSha "ffmpeg.7z"
$ffprobeArchive = Get-VerifiedArchive $ffprobeUrl $ffprobeSha "ffprobe.zip"

$mpvDir = Join-Path $work "mpv"
$ffmpegDir = Join-Path $work "ffmpeg"
$ffprobeDir = Join-Path $work "ffprobe"
New-Item -ItemType Directory -Force -Path $mpvDir,$ffmpegDir,$ffprobeDir | Out-Null

& 7z x $mpvArchive "-o$mpvDir" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Failed to extract libmpv." }

& 7z x $ffmpegArchive "-o$ffmpegDir" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Failed to extract FFmpeg." }

Expand-Archive -Path $ffprobeArchive -DestinationPath $ffprobeDir -Force

$mpvDll = Get-ChildItem $mpvDir -Recurse -Filter "libmpv-2.dll" | Select-Object -First 1
if (-not $mpvDll) { throw "libmpv-2.dll not found in archive." }

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
Get-ChildItem $mpvDll.Directory.FullName -Filter "*.dll" | ForEach-Object {
    Copy-Item $_.FullName -Destination $PublishDir -Force
}

$ffmpegExe = Get-ChildItem $ffmpegDir -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
if (-not $ffmpegExe) { throw "ffmpeg.exe not found in archive." }

$runtimeFfmpeg = Join-Path $PublishDir "runtime\ffmpeg"
New-Item -ItemType Directory -Force -Path $runtimeFfmpeg | Out-Null
Copy-Item $ffmpegExe.FullName -Destination (Join-Path $runtimeFfmpeg "ffmpeg.exe") -Force

$ffprobeExe = Get-ChildItem $ffprobeDir -Recurse -Filter "ffprobe.exe" | Select-Object -First 1
if (-not $ffprobeExe) { throw "ffprobe.exe not found in verified diagnostic archive." }
Copy-Item $ffprobeExe.FullName -Destination (Join-Path $runtimeFfmpeg "ffprobe.exe") -Force

$licenseDir = Join-Path $PublishDir "licenses\media-runtime"
New-Item -ItemType Directory -Force -Path $licenseDir | Out-Null

$provenance = @"
SanchesTV 8.3 media runtime

libmpv package:
$mpvUrl
SHA-256: $mpvSha
Package: mpv-dev-lgpl, LGPLv2.1+ build, FFmpeg linked under LGPLv3.

FFmpeg package:
$ffmpegUrl
SHA-256: $ffmpegSha
Package: ffmpeg-lgpl x86_64.

FFprobe diagnostic package:
$ffprobeUrl
SHA-256: $ffprobeSha
Package: BtbN FFmpeg n9.0 win64 LGPL; only ffprobe.exe is copied into the SanchesTV runtime.


The mpv-winbuild project documents FFmpeg, libplacebo, libass and dav1d among the integrated components.
"@
$provenance | Out-File (Join-Path $licenseDir "RUNTIME-PROVENANCE.txt") -Encoding utf8

Write-Host "libmpv: $($mpvDll.FullName)"
Write-Host "ffmpeg: $($ffmpegExe.FullName)"
Write-Host "ffprobe: $($ffprobeExe.FullName)"
Write-Host "Media runtime prepared in $PublishDir"
