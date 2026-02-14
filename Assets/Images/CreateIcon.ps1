# Logo.png'den app.ico olusturur (EXE ikonu icin)
Add-Type -AssemblyName System.Drawing
$base = Split-Path -Parent $PSScriptRoot
$base = Split-Path -Parent $base
$pngPath = Join-Path $base "Assets\Images\Logo.png"
$icoPath = Join-Path $base "Assets\Images\app.ico"
$png = [System.Drawing.Image]::FromFile($pngPath)
$bmp = New-Object System.Drawing.Bitmap($png)
$icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$fs = [System.IO.File]::Create($icoPath)
$icon.Save($fs)
$fs.Close()
$png.Dispose(); $bmp.Dispose(); $icon.Dispose()
Write-Host "app.ico olusturuldu: $icoPath"
