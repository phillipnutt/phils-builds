# Compiles src/Core.cs with Tools/CoreTests.cs (no KSP needed) and runs the checks.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$csc = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
if (-not (Test-Path $csc)) { $csc = (Get-Command csc.exe -ErrorAction Stop).Source }
$out = Join-Path ([System.IO.Path]::GetTempPath()) 'PhilsVesselNaming-CoreTests.exe'
& $csc /nologo /langversion:latest /out:$out (Join-Path $repo 'src\Core.cs') (Join-Path $PSScriptRoot 'CoreTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
& $out
exit $LASTEXITCODE
