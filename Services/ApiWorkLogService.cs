using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

/// <summary>
/// İş kayıtları web API üzerinden okunur ve yazılır.
/// </summary>
public class ApiWorkLogService : IWorkLogService
{
    private readonly BackendApiClient _api;

    public ApiWorkLogService(BackendApiClient api)
    {
        _api = api;
    }

    public IReadOnlyList<WorkLog> GetByUser(string? userName) => _api.GetWorkLogs(userName);

    public IReadOnlyList<WorkLog> GetAll() => _api.GetAllWorkLogs();

    public void Add(WorkLog workLog) => _api.AddWorkLog(workLog);

    public void Update(WorkLog workLog) => _api.UpdateWorkLog(workLog);

    public void Delete(Guid id) => _api.DeleteWorkLog(id);

    public decimal GetTotalHoursForUser(string? userName, DateTime from, DateTime to)
        => _api.GetTotalHoursForUser(userName, from, to);

    public decimal GetTotalHoursAll(DateTime from, DateTime to)
        => _api.GetTotalHoursAll(from, to);
}
