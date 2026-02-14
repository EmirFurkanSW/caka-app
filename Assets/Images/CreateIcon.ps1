# Masaustundeki logodan app.ico olusturur (sadece EXE ikonu icin).
# Giris ekrani logosu projedeki Assets\Images\Logo.png kullanir; buna dokunulmaz.
Add-Type -AssemblyName System.Drawing
$desktop = [Environment]::GetFolderPath("Desktop")
$pngPath = $null
foreach ($name in @("logo.png", "Logo.png", "logo.jpg", "Logo.jpg")) {
    $p = Join-Path $desktop $name
    if (Test-Path $p) { $pngPath = $p; break }
}
if (-not $pngPath) { Write-Error "Masaustunde logo.png/Logo.png bulunamadi: $desktop"; exit 1 }
$base = Split-Path -Parent $PSScriptRoot
$base = Split-Path -Parent $base
$icoPath = Join-Path $base "Assets\Images\app.ico"
$img = [System.Drawing.Image]::FromFile($pngPath)
$bmp = New-Object System.Drawing.Bitmap($img)
$icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$fs = [System.IO.File]::Create($icoPath)
$icon.Save($fs)
$fs.Close()
$img.Dispose(); $bmp.Dispose(); $icon.Dispose()
Write-Host "EXE ikonu olusturuldu (masaustu logodan): $icoPath"
