@echo off
setlocal
cd /d "%~dp0"
set "RESS=MarsSentinel_Data\sharedassets0.assets.resS"
if not exist "%RESS%" (
    echo [ORBITX] Unpacking MarsSentinel 3D textures and mesh assets for first launch...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$out = [System.IO.File]::Create('%RESS%'); foreach ($p in @('%RESS%.part1.gz','%RESS%.part2.gz')) { $in = [System.IO.File]::OpenRead($p); $gz = New-Object System.IO.Compression.GZipStream($in, [System.IO.Compression.CompressionMode]::Decompress); $gz.CopyTo($out); $gz.Close(); $in.Close() }; $out.Close()"
)
echo [ORBITX] Launching MarsSentinel.exe...
start "" "%~dp0MarsSentinel.exe"
