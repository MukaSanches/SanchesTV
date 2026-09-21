param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

$ErrorActionPreference = "Stop"
$work = Join-Path $env:RUNNER_TEMP "sanchestv-v7-tools"
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $work | Out-Null

function Get-VerifiedFile([string]$url, [string]$sha, [string]$name) {
    $path = Join-Path $work $name
    Invoke-WebRequest -Uri $url -OutFile $path
    $actual = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $sha.ToLowerInvariant()) {
        throw "SHA-256 mismatch for $name. Expected $sha, got $actual"
    }
    return $path
}

function Expand-ZipTo([string]$archive, [string]$target) {
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Expand-Archive -Path $archive -DestinationPath $target -Force
}

# MediaMTX: multi-protocol local media router.
$mtxUrl = "https://github.com/bluenviron/mediamtx/releases/download/v1.21.1/mediamtx_v1.21.1_windows_amd64.zip"
$mtxSha = "faa97974861eb75a68b5aa326c78e7e7a6f670b5ef191bace78e715130381f23"
$mtxZip = Get-VerifiedFile $mtxUrl $mtxSha "mediamtx.zip"
$mtxExtract = Join-Path $work "mediamtx"
Expand-ZipTo $mtxZip $mtxExtract
$mtxExe = Get-ChildItem $mtxExtract -Recurse -Filter "mediamtx.exe" | Select-Object -First 1
if (-not $mtxExe) { throw "mediamtx.exe not found" }
$mtxRuntime = Join-Path $PublishDir "runtime\mediamtx"
New-Item -ItemType Directory -Force -Path $mtxRuntime | Out-Null
Copy-Item $mtxExe.FullName (Join-Path $mtxRuntime "mediamtx.exe") -Force

# TSDuck: MPEG-TS / broadcast diagnostics.
$tsUrl = "https://github.com/tsduck/tsduck/releases/download/v3.45-4798/TSDuck-Win64-3.45-4798-Portable.zip"
$tsSha = "12976d2d3a55beaade5298399c720be26a85f57dc7d6b5399c5c660c4855afbc"
$tsZip = Get-VerifiedFile $tsUrl $tsSha "tsduck.zip"
$tsRuntime = Join-Path $PublishDir "runtime\tsduck"
Expand-ZipTo $tsZip $tsRuntime
$tsAnalyze = Get-ChildItem $tsRuntime -Recurse -Filter "tsanalyze.exe" | Select-Object -First 1
if (-not $tsAnalyze) { throw "tsanalyze.exe not found" }

# CCExtractor: broadcast closed-caption extraction.
$ccUrl = "https://github.com/CCExtractor/ccextractor/releases/download/v0.96.6/CCExtractor.0.96.6_win_portable.zip"
$ccSha = "c9b14cd356c7ada66b294426ba25c52d7654396a31b8b115b789e4d5e73f62bc"
$ccZip = Get-VerifiedFile $ccUrl $ccSha "ccextractor.zip"
$ccRuntime = Join-Path $PublishDir "runtime\ccextractor"
Expand-ZipTo $ccZip $ccRuntime
$ccExe = Get-ChildItem $ccRuntime -Recurse -Filter "ccextractorwinfull.exe" | Select-Object -First 1
if (-not $ccExe) { throw "ccextractorwinfull.exe not found" }

# whisper.cpp: local/offline transcription. Build an exact commit and bundle the multilingual tiny model.
$whisperCommit = "927cfce34f31707e17f2bff35c349632fb9e2c3a"
$whisperSrc = Join-Path $work "whisper.cpp"
git clone --filter=blob:none https://github.com/ggml-org/whisper.cpp.git $whisperSrc
if ($LASTEXITCODE -ne 0) { throw "Failed to clone whisper.cpp" }
Push-Location $whisperSrc
git checkout --detach $whisperCommit
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Failed to checkout whisper.cpp commit" }
$actualCommit = (git rev-parse HEAD).Trim()
if ($actualCommit -ne $whisperCommit) { Pop-Location; throw "whisper.cpp commit mismatch" }

$whisperBuild = Join-Path $whisperSrc "build-sanchestv"
cmake -S . -B $whisperBuild -A x64 -DWHISPER_BUILD_TESTS=OFF -DWHISPER_BUILD_SERVER=OFF -DWHISPER_BUILD_EXAMPLES=ON -DBUILD_SHARED_LIBS=OFF
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "whisper.cpp cmake configure failed" }
cmake --build $whisperBuild --config Release --target whisper-cli --parallel 2
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "whisper.cpp build failed" }
Pop-Location

$whisperExe = Get-ChildItem $whisperBuild -Recurse -Filter "whisper-cli.exe" | Select-Object -First 1
if (-not $whisperExe) { throw "whisper-cli.exe not found" }
$whisperRuntime = Join-Path $PublishDir "runtime\whisper"
New-Item -ItemType Directory -Force -Path $whisperRuntime | Out-Null
Copy-Item $whisperExe.FullName (Join-Path $whisperRuntime "whisper-cli.exe") -Force

$modelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin"
$modelSha = "be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21"
$model = Get-VerifiedFile $modelUrl $modelSha "ggml-tiny.bin"
Copy-Item $model (Join-Path $whisperRuntime "ggml-tiny.bin") -Force


# Real-ESRGAN ncnn Vulkan: optional offline super-resolution.
# Archive hash is pinned to the official v0.2.0 Windows asset bytes.
$esrUrl = "https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan/releases/download/v0.2.0/realesrgan-ncnn-vulkan-v0.2.0-windows.zip"
$esrSha = "1bbbdb12d470af80b035c773682e144c6c2f6ece9210832a289af0a48ce3fa9a"
$esrZip = Get-VerifiedFile $esrUrl $esrSha "realesrgan.zip"
$esrRuntime = Join-Path $PublishDir "runtime\realesrgan"
Expand-ZipTo $esrZip $esrRuntime
$esrExe = Get-ChildItem $esrRuntime -Recurse -Filter "realesrgan-ncnn-vulkan.exe" | Select-Object -First 1
if (-not $esrExe) { throw "realesrgan-ncnn-vulkan.exe not found" }

# RIFE ncnn Vulkan: portable 2x frame interpolation, no CUDA/PyTorch dependency.
$rifeUrl = "https://github.com/nihui/rife-ncnn-vulkan/releases/download/20221029/rife-ncnn-vulkan-20221029-windows.zip"
$rifeSha = "d8e4d772d26cd8006ef0ad0bc82eb191b53c68677d1ae2f42506d74cbbbea606"
$rifeZip = Get-VerifiedFile $rifeUrl $rifeSha "rife.zip"
$rifeRuntime = Join-Path $PublishDir "runtime\rife"
Expand-ZipTo $rifeZip $rifeRuntime
$rifeExe = Get-ChildItem $rifeRuntime -Recurse -Filter "rife-ncnn-vulkan.exe" | Select-Object -First 1
if (-not $rifeExe) { throw "rife-ncnn-vulkan.exe not found" }

# Hyperion.NG: ambient-light server/UI. Official GitHub asset provides SHA-256.
$hyperionUrl = "https://github.com/hyperion-project/hyperion.ng/releases/download/2.2.1/Hyperion-2.2.1-Windows-x64.zip"
$hyperionSha = "958c406ee15a4ee654433d82ebb396363cc7089e638b1a7241f4164df2c05c0b"
$hyperionZip = Get-VerifiedFile $hyperionUrl $hyperionSha "hyperion.zip"
$hyperionRuntime = Join-Path $PublishDir "runtime\hyperion"
Expand-ZipTo $hyperionZip $hyperionRuntime
$hyperionExe = Get-ChildItem $hyperionRuntime -Recurse -Include "hyperiond.exe","Hyperion.exe" | Select-Object -First 1
if (-not $hyperionExe) { throw "Hyperion executable not found" }

$licenseDir = Join-Path $PublishDir "licenses\v7-tools"
New-Item -ItemType Directory -Force -Path $licenseDir | Out-Null
@"
SanchesTV 7 external runtime provenance

MediaMTX v1.21.1
$mtxUrl
SHA-256: $mtxSha

TSDuck v3.45-4798 portable
$tsUrl
SHA-256: $tsSha

CCExtractor v0.96.6 portable
$ccUrl
SHA-256: $ccSha

whisper.cpp v1.9.4 source commit
https://github.com/ggml-org/whisper.cpp/commit/$whisperCommit
Commit: $whisperCommit

Whisper multilingual tiny model
$modelUrl
SHA-256: $modelSha

Real-ESRGAN ncnn Vulkan v0.2.0
$esrUrl
SHA-256: $esrSha

RIFE ncnn Vulkan 20221029
$rifeUrl
SHA-256: $rifeSha

Hyperion.NG 2.2.1 Windows x64
$hyperionUrl
SHA-256: $hyperionSha

These tools remain separate executables invoked by SanchesTV. Their upstream licenses apply.
"@ | Out-File (Join-Path $licenseDir "V7-RUNTIME-PROVENANCE.txt") -Encoding utf8

Write-Host "MediaMTX: $($mtxExe.FullName)"
Write-Host "TSDuck tsanalyze: $($tsAnalyze.FullName)"
Write-Host "CCExtractor: $($ccExe.FullName)"
Write-Host "whisper.cpp: $($whisperExe.FullName)"
Write-Host "Real-ESRGAN: $($esrExe.FullName)"
Write-Host "RIFE: $($rifeExe.FullName)"
Write-Host "Hyperion: $($hyperionExe.FullName)"
Write-Host "V7 tools prepared."
