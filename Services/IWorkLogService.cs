using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// İş kayıtları veri erişim arayüzü.
/// Şu an in-memory; ileride veritabanı/API repository ile değiştirilebilir.
/// </summary>
public interface IWorkLogService
{
    IReadOnlyList<WorkLog> GetByUser(string? userName);
    IReadOnlyList<WorkLog> GetAll();
    void Add(WorkLog workLog);
    void Update(WorkLog workLog);
    void Delete(Guid id);
    decimal GetTotalHoursForUser(string? userName, DateTime from, DateTime to);
    decimal GetTotalHoursAll(DateTime from, DateTime to);
}
