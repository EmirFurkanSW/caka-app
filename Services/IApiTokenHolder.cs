namespace CAKA.PerformanceApp.Services;

/// <summary>
/// Giriş sonrası JWT token'ı tutar; API isteklerinde Bearer olarak kullanılır.
/// </summary>
public interface IApiTokenHolder
{
    string? Token { get; set; }
}
