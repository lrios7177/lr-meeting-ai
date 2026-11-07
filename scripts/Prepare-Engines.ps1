param([string]$Destination = '', [string]$OllamaModel = 'qwen3:1.7b')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$Destination) { $Destination = Join-Path $root 'artifacts/ai' }
$cache = Join-Path $root 'artifacts/engine-downloads'
New-Item $Destination,$cache -ItemType Directory -Force | Out-Null
function Download-Verified([string]$Url, [string]$Path, [string]$Sha256) {
    if ((Test-Path $Path) -and (Get-FileHash $Path -Algorithm SHA256).Hash -eq $Sha256) { return }
    $partial = $Path + '.partial'
    try {
        Invoke-WebRequest -Uri $Url -OutFile $partial -MaximumRetryCount 3 -RetryIntervalSec 5
        if ((Get-FileHash $partial -Algorithm SHA256).Hash -ne $Sha256) { throw "SHA256 mismatch: $Url" }
        Move-Item $partial $Path -Force
    } finally { if (Test-Path $partial) { Remove-Item $partial -Force } }
}
$whisperHash = '6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb'
$ollamaHash = '9ca74a7ab66ec86870b1eecac7df160df08dc4e3a291ab5d2d59bcf9310221c6'
$whisperZip = Join-Path $cache 'whisper-b5454-x64.zip'
Download-Verified 'https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-bin-x64.zip' $whisperZip $whisperHash
$expandedWhisper = Join-Path $cache 'whisper'
Expand-Archive $whisperZip $expandedWhisper -Force
$whisperDir = Join-Path $Destination 'whisper'
New-Item $whisperDir -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $expandedWhisper 'Release/whisper-cli.exe') $whisperDir -Force
Copy-Item (Join-Path $expandedWhisper 'Release/*.dll') $whisperDir -Force

# Use the published model's immutable repository revision and advertised LFS SHA256.
$metadata = Invoke-RestMethod 'https://huggingface.co/api/models/ggerganov/whisper.cpp?blobs=true'
$modelInfo = $metadata.siblings | Where-Object rfilename -eq 'ggml-base.bin' | Select-Object -First 1
if (!$modelInfo.lfs.sha256 -or !$metadata.sha) { throw 'Whisper model metadata lacks SHA256 or revision.' }
$modelDir = Join-Path $Destination 'models'
New-Item $modelDir -ItemType Directory -Force | Out-Null
Download-Verified ("https://huggingface.co/ggerganov/whisper.cpp/resolve/{0}/ggml-base.bin?download=true" -f $metadata.sha) (Join-Path $modelDir 'ggml-base.bin') $modelInfo.lfs.sha256

$ollamaZip = Join-Path $cache 'ollama-0.40.15-windows-amd64.zip'
Download-Verified 'https://github.com/ollama/ollama/releases/download/v0.40.15/ollama-windows-amd64.zip' $ollamaZip $ollamaHash
$ollamaDir = Join-Path $Destination 'ollama'
# Only CPU components: exclude GPU backends while extracting, avoiding several GB of unused libraries.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($ollamaZip)
try {
    $prefix = [IO.Path]::GetFullPath($ollamaDir) + [IO.Path]::DirectorySeparatorChar
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName -match '(?i)(cuda|cublas|cudart|nvrtc|nvidia|rocm|rocblas|vulkan|mlx)') { continue }
        if (!$entry.Name) { continue }
        $path = [IO.Path]::GetFullPath((Join-Path $ollamaDir $entry.FullName))
        if (!$path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe ZIP entry.' }
        New-Item (Split-Path $path -Parent) -ItemType Directory -Force | Out-Null
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $path, $true)
    }
} finally { $archive.Dispose() }
$ollamaExe = Get-ChildItem $ollamaDir -Filter ollama.exe -Recurse | Select-Object -First 1
if (!$ollamaExe) { throw 'Ollama executable missing.' }

# Microsoft permits app-local redistribution of these unmodified CRT DLLs with the application.
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs = & $vswhere -latest -property installationPath
$crt = Get-ChildItem (Join-Path $vs 'VC/Redist/MSVC') -Directory | Sort-Object Name -Descending | ForEach-Object { Get-ChildItem (Join-Path $_.FullName 'x64') -Directory -Filter 'Microsoft.VC*.CRT' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName } | Select-Object -First 1
if (!$crt) { throw 'Visual C++ app-local runtime was not found in the build environment.' }
Copy-Item (Join-Path $crt '*.dll') $whisperDir -Force
Copy-Item (Join-Path $crt '*.dll') $ollamaExe.DirectoryName -Force

$models = Join-Path $modelDir 'ollama'
$variables = @('OLLAMA_HOST','OLLAMA_MODELS','OLLAMA_NO_CLOUD','CUDA_VISIBLE_DEVICES','OLLAMA_VULKAN')
$previous = @{}
foreach ($key in $variables) { $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
$server = $null
try {
    $env:OLLAMA_HOST = '127.0.0.1:11435'
    $env:OLLAMA_MODELS = $models
    $env:OLLAMA_NO_CLOUD = '1'
    $env:CUDA_VISIBLE_DEVICES = '-1'
    $env:OLLAMA_VULKAN = '0'
    $server = Start-Process $ollamaExe.FullName -ArgumentList 'serve' -PassThru -RedirectStandardOutput (Join-Path $cache 'ollama-build.out.log') -RedirectStandardError (Join-Path $cache 'ollama-build.err.log')
    $ready = $false
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        if ($server.HasExited) { Get-Content (Join-Path $cache 'ollama-build.err.log'); throw 'Ollama stopped during preparation.' }
        try { $null = Invoke-RestMethod 'http://127.0.0.1:11435/api/version' -TimeoutSec 2; $ready=$true; break } catch { Start-Sleep -Seconds 1 }
    }
    if (!$ready) { throw 'Ollama local startup timed out.' }
    & $ollamaExe.FullName pull $OllamaModel
    if ($LASTEXITCODE -ne 0) { throw 'Ollama model download failed.' }
    $info = Invoke-RestMethod 'http://127.0.0.1:11435/api/show' -Method Post -ContentType 'application/json' -Body (@{model=$OllamaModel} | ConvertTo-Json)
    if ($info.remote_host -or $info.remote_model) { throw 'Remote models are not supported.' }
    $tags = Invoke-RestMethod 'http://127.0.0.1:11435/api/tags'
    $ollamaDigest = ($tags.models | Where-Object name -eq $OllamaModel | Select-Object -First 1).digest
    if (!$ollamaDigest) { throw 'The local model digest is missing.' }
    # Basic real-model startup check, not a quality evaluation of meeting reports.
    $probe = @{model=$OllamaModel;stream=$false;think=$false;prompt='Responde solo OK.';options=@{num_predict=16;num_ctx=1024}}
    $result = Invoke-RestMethod 'http://127.0.0.1:11435/api/generate' -Method Post -ContentType 'application/json' -Body ($probe | ConvertTo-Json -Depth 5) -TimeoutSec 180
    if (!$result.done -or !$result.response) { throw 'The bundled Ollama model did not respond.' }
} finally {
    if ($server -and !$server.HasExited) { & taskkill.exe /PID $server.Id /T /F | Write-Output; $server.WaitForExit() }
    foreach ($key in $variables) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}
# Keep upstream license files with distributed engines and models.
$licenses = Join-Path $Destination 'licenses'; New-Item $licenses -ItemType Directory -Force | Out-Null
Invoke-WebRequest 'https://raw.githubusercontent.com/ggml-org/whisper.cpp/b5454/LICENSE' -OutFile (Join-Path $licenses 'whisper-MIT.txt')
Invoke-WebRequest 'https://raw.githubusercontent.com/ollama/ollama/v0.40.15/LICENSE' -OutFile (Join-Path $licenses 'Ollama-MIT.txt')
Invoke-WebRequest 'https://raw.githubusercontent.com/openai/whisper/main/LICENSE' -OutFile (Join-Path $licenses 'Whisper-model-MIT.txt')
Invoke-WebRequest 'https://huggingface.co/Qwen/Qwen3-1.7B/raw/main/LICENSE' -OutFile (Join-Path $licenses 'Qwen3-model-Apache-2.0.txt')
@{schemaVersion=1;profile='CPU';whisperVersion='b5454';whisperSha256=$whisperHash;whisperModel='ggml-base.bin';whisperModelRevision=$metadata.sha;whisperModelSha256=$modelInfo.lfs.sha256;ollamaVersion='0.40.15';ollamaSha256=$ollamaHash;ollamaModel=$OllamaModel;ollamaModelDigest=$ollamaDigest;createdUtc=[DateTimeOffset]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content (Join-Path $Destination 'bundle.json') -Encoding utf8
Write-Output 'All local AI engines, model weights and app-local runtime files are prepared.'
