# Rastgele güvenli sıfırlama anahtarı üretir (Render'a CAKA_FACTORY_RESET_SECRET olarak yapıştırın).
[System.Guid]::NewGuid().ToString("N") + [System.Guid]::NewGuid().ToString("N")
