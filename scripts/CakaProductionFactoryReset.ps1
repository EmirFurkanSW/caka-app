#requires -Version 5.1
<#
.SYNOPSIS
    Canlı API veritabanını sıfırlar (admin hariç tüm kullanıcı ve iş verisi silinir).

.DESCRIPTION
    1) Render Dashboard → Environment: CAKA_FACTORY_RESET_SECRET değişkenini (uzun/rastgele) ekleyin.
    2) Son Git push deploy edilmiş olsun (main).
    3) Bu scripti -Secret ile aynı değerde çalıştırın.
    4) Render'dan CAKA_FACTORY_RESET_SECRET satırını SILİN ve yeniden deploy edin.

    Render paneline kod erişiminiz olmadığından bu adımı yalnızca siz yapabilirsiniz.
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $Secret,

    [string] $ApiBaseUrl
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not $ApiBaseUrl) {
    $cfgPath = Join-Path $repoRoot "CAKA.config.json"
    if (-not (Test-Path $cfgPath)) { throw "CAKA.config.json bulunamadı: $cfgPath" }
    $cfg = Get-Content $cfgPath -Encoding UTF8 | ConvertFrom-Json
    $ApiBaseUrl = $cfg.ApiBaseUrl
}
$base = ($ApiBaseUrl.TrimEnd('/') + "/api/maintenance/factory-reset")

Write-Host "Hedef: $base" -ForegroundColor Cyan

try {
    $resp = Invoke-WebRequest -Method Post -Uri $base `
        -Headers @{ "X-CAKA-FACTORY-KEY" = $Secret } `
        -ContentType "application/json" `
        -UseBasicParsing `
        -ErrorAction Stop
    $payload = $resp.Content | ConvertFrom-Json
    $payload | ConvertTo-Json -Depth 5 | Write-Host
}
catch {
    $code = $null
    $body = $null
    if ($_.Exception.Response) {
        $code = [int]$_.Exception.Response.StatusCode
        $stream = $_.Exception.Response.GetResponseStream()
        if ($stream) {
            $reader = New-Object System.IO.StreamReader($stream)
            $body = $reader.ReadToEnd()
        }
    }
    Write-Host "`nHTTP kodu: $code" -ForegroundColor Red
    if ($body) { Write-Host $body }
    elseif ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message }

    if ($code -eq 404 -and $body -match "Sıfırlama kapalı|kapalı") {
        Write-Host "`n--- Ne yapmalı? ---" -ForegroundColor Yellow
        Write-Host "1) Render.com → Web Service (caka-api) → Environment"
        Write-Host "2) Ortam değişkeni EXACT isim: CAKA_FACTORY_RESET_SECRET"
        Write-Host "   Değer: bu betiğe verdiğiniz -Secret ile BIREBIR aynı anahtar olmalı."
        Write-Host "3) Save Changes → Manual Deploy (Deploy latest commit)."
        Write-Host "4) Deploy bitince Logs'ta api'nin ayağa kalktığını doğrula, sonra scripti yeniden çalıştır."
    }
    exit 1
}

Write-Host "`nHatırlatma: Render ortamından CAKA_FACTORY_RESET_SECRET'i kaldırın." -ForegroundColor Yellow
