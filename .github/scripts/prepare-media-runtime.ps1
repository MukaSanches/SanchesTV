param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

$ErrorActionPreference = "Stop"

$mpvUrl = "https://github.com/zhongfly/mpv-winbuild/releases/download/2026-09-20-e76a35ec95/mpv-dev-lgpl-x86_64-20260920-git-e76a35ec95.7z"
$mpvSha = "c8d52781ed8773bf414faf12aa74415ecdf4f2ecc7254a5d633d76266d131b9d"

$ffmpegUrl = "https://github.com/zhongfly/mpv-winbuild/releases/download/2026-09-20-e76a35ec95/ffmpeg-lgpl-x86_64-git-bf56c9459.7z"
$ffmpegSha = "c42179573d9d50cc22c547eba21690b5cd2d131e01f892fa6cb9432a774cf2a8"

$p2pUrl = "https://github.com/stremio-native/stream-server/releases/download/v0.1.8/stream-server-windows-amd64.exe"
$p2pSha = "90b7a14b282a9e649fba5adb6112c2c191dba884ec762f8650f45ccf06e17e09"

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
$p2pExe = Get-VerifiedArchive $p2pUrl $p2pSha "stream-server.exe"

$mpvDir = Join-Path $work "mpv"
$ffmpegDir = Join-Path $work "ffmpeg"
New-Item -ItemType Directory -Force -Path $mpvDir,$ffmpegDir | Out-Null

& 7z x $mpvArchive "-o$mpvDir" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Failed to extract libmpv." }

& 7z x $ffmpegArchive "-o$ffmpegDir" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Failed to extract FFmpeg." }

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

$ffprobeExe = Get-ChildItem $ffmpegDir -Recurse -Filter "ffprobe.exe" | Select-Object -First 1
if ($ffprobeExe) {
    Copy-Item $ffprobeExe.FullName -Destination (Join-Path $runtimeFfmpeg "ffprobe.exe") -Force
}

$p2pRuntime = Join-Path $PublishDir "runtime\p2p"
New-Item -ItemType Directory -Force -Path $p2pRuntime | Out-Null
Copy-Item $p2pExe -Destination (Join-Path $p2pRuntime "stream-server.exe") -Force

$licenseDir = Join-Path $PublishDir "licenses\media-runtime"
New-Item -ItemType Directory -Force -Path $licenseDir | Out-Null

$provenance = @"
SanchesTV 6.0 media runtime

libmpv package:
$mpvUrl
SHA-256: $mpvSha
Package: mpv-dev-lgpl, LGPLv2.1+ build, FFmpeg linked under LGPLv3.

FFmpeg package:
$ffmpegUrl
SHA-256: $ffmpegSha
Package: ffmpeg-lgpl x86_64.

P2P streaming sidecar:
$p2pUrl
SHA-256: $p2pSha
Project: stremio-native/stream-server v0.1.8.
Used only for user-supplied magnet/.torrent playback; SanchesTV does not bundle torrent indexers.

The mpv-winbuild project documents FFmpeg, libplacebo, libass and dav1d among the integrated components.
"@
$provenance | Out-File (Join-Path $licenseDir "RUNTIME-PROVENANCE.txt") -Encoding utf8

Write-Host "libmpv: $($mpvDll.FullName)"
Write-Host "ffmpeg: $($ffmpegExe.FullName)"
Write-Host "Media runtime prepared in $PublishDir"
