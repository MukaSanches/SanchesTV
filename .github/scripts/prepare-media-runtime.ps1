param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

$ErrorActionPreference = "Stop"

$mpvUrl = "https://github.com/zhongfly/mpv-winbuild/releases/download/2026-09-20-e76a35ec95/mpv-dev-lgpl-x86_64-20260920-git-e76a35ec95.7z"
$mpvSha = "c8d52781ed8773bf414faf12aa74415ecdf4f2ecc7254a5d633d76266d131b9d"

$ffmpegUrl = "https://github.com/zhongfly/mpv-winbuild/releases/download/2026-09-20-e76a35ec95/ffmpeg-lgpl-x86_64-git-bf56c9459.7z"
$ffmpegSha = "c42179573d9d50cc22c547eba21690b5cd2d131e01f892fa6cb9432a774cf2a8"

# ffprobe separado e estático (LGPL) para diagnóstico Cinema OS.
# Mantemos o FFmpeg atual do mpv-winbuild para não alterar o runtime de gravação/player.
$ffprobeUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n9.0-latest-win64-lgpl-9.0.zip"
$ffprobeSha = "424298f2283c9cd090d63725b89c8f494dd4a377b322a4bb48ef500ee5cf3fd3"


$work = Join-Path $env:RUNNER_TEMP "sanchestv-media-runtime"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $work | Out-Null

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
SanchesTV 8.2 media runtime

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
