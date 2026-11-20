# Basic Capture'ı derler. Windows'un içindeki C# derleyicisini kullanır, ek bir şey kurmak gerekmez.
# Kullanım:  powershell -ExecutionPolicy Bypass -File build.ps1 [-Cikti yol\BasicCapture.exe]
param([string]$Cikti = 'BasicCapture.exe')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# derleyicisi bulunamadı: $csc (.NET Framework 4.x gerekli)" }

if (-not (Test-Path 'logo.ico')) { & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'logo.ps1') }

& $csc /nologo /target:winexe /optimize+ /win32icon:logo.ico "/out:$Cikti" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll Program.cs Akici.cs
if ($LASTEXITCODE -ne 0) { throw "Derleme başarısız" }
Get-Item $Cikti | Select-Object FullName, Length, LastWriteTime
