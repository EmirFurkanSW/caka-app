namespace CAKA.PerformanceApp.Core;

/// <summary>
/// Menüden bir sayfa seçildiğinde o ekranın her sefer ilk açılış gibi sıfırlanıp güncellenmesi.
/// </summary>
public interface INavigationRefresh
{
    void RefreshOnNavigate();
}
