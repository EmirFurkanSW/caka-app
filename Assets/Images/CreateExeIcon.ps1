# Projedeki ExeLogo.png'den app.ico olusturur (EXE ikonu - gomulu).
# Build/publish oncesi calistirilir; EXE bu logoyu gosterir.
Add-Type -AssemblyName System.Drawing
$scriptDir = $PSScriptRoot
$base = Split-Path -Parent (Split-Path -Parent $scriptDir)
$pngPath = Join-Path $scriptDir "ExeLogo.png"
$icoPath = Join-Path $scriptDir "app.ico"
if (-not (Test-Path $pngPath)) { Write-Error "ExeLogo.png bulunamadi: $pngPath"; exit 1 }
$img = [System.Drawing.Image]::FromFile($pngPath)
$bmp = New-Object System.Drawing.Bitmap($img)
$icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$fs = [System.IO.File]::Create($icoPath)
$icon.Save($fs)
$fs.Close()
$img.Dispose(); $bmp.Dispose(); $icon.Dispose()
Write-Host "EXE ikonu guncellendi: $icoPath"
