param(
    [switch]$Diagnose,
    [switch]$Pause,
    [switch]$NoDoc,
    [switch]$NoPdf,
    [switch]$NoXlsx
)

$ErrorActionPreference = "Stop"
$proj = Join-Path $PSScriptRoot "src\AIRename.csproj"
$publishDir = Join-Path $PSScriptRoot "publish\win-x64"
$targetDir = Join-Path $env:LOCALAPPDATA "AIRename"
$exePath = Join-Path $targetDir "AIRename.exe"

function Show-Info($msg){ Write-Host $msg -ForegroundColor Cyan }
function Show-Ok($msg){ Write-Host $msg -ForegroundColor Green }
function Show-Warn($msg){ Write-Host $msg -ForegroundColor Yellow }
function Show-Err($msg){ Write-Host $msg -ForegroundColor Red }

if ($Diagnose) {
    Show-Info "[DIAG] environment & registry check"
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) { Show-Err "dotnet not found. Install .NET 8 SDK." } else { Show-Ok "dotnet: $($dotnet.Source)" }

    Show-Info "Target dir: $targetDir"
    if (Test-Path $exePath) {
        $size = (Get-Item $exePath).Length
        Show-Ok "Found exe: $exePath ($size bytes)"
    } else {
        Show-Warn "Exe missing: $exePath (not published or copy failed)"
    }

    Show-Info "Registry: HKCU\Software\Classes\*\shell\AIRename"
    & reg.exe query "HKCU\Software\Classes\*\shell\AIRename" 2>$null | Out-Host
    & reg.exe query "HKCU\Software\Classes\*\shell\AIRename\command" 2>$null | Out-Host
    Show-Info "If missing: double-click assets\reg\install.reg"

    if ($Pause) { Read-Host "Press Enter to close (diagnose)" }
    return
}

Show-Info "[1/3] Build NativeAOT package"
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# MSBuild feature toggles
$props = @()
if ($NoDoc)  { $props += "-p:IncludeDocSupport=false" }
if ($NoPdf)  { $props += "-p:IncludePdfSupport=false" }
if ($NoXlsx) { $props += "-p:IncludeXlsxSupport=false" }

& dotnet publish $proj -c Release -r win-x64 --self-contained true -p:PublishAot=true -p:StripSymbols=true -p:IlcInvariantGlobalization=true -o $publishDir @props

$sw.Stop()
Show-Ok "Publish done in $([math]::Round($sw.Elapsed.TotalSeconds,2)) s"

Show-Info "[2/3] Copy single exe to local dir"
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
$publishedExe = Join-Path $publishDir "AIRename.exe"
if (!(Test-Path $publishedExe)) { throw "Publish failed: missing $publishedExe" }
Copy-Item -Force $publishedExe $exePath
if (!(Test-Path $exePath)) { throw "Copy failed: missing $exePath" }
Show-Ok "Copy ok: $exePath"

Show-Info "[3/3] Done. Next: double-click assets\reg\install.reg"
if ($Pause) { Read-Host "Press Enter to close" }