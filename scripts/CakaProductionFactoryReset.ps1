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

$response = Invoke-RestMethod `
    -Method Post `
    -Uri $base `
    -Headers @{ "X-CAKA-FACTORY-KEY" = $Secret } `
    -ContentType "application/json"

$response | ConvertTo-Json -Depth 5 | Write-Host
Write-Host "`nHatırlatma: Render ortamından CAKA_FACTORY_RESET_SECRET'i kaldırın." -ForegroundColor Yellow
