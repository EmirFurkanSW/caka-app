using CAKA.PerformanceApp.Models;

namespace CAKA.PerformanceApp.Services;

public static class WeekExcelLookupBuilder
{
    /// <summary>İş kayıtlarında geçen işler için temel liste + detayı doldurur.</summary>
    public static WeekExcelLookups Build(IReadOnlyCollection<WorkLog> entries, BackendApiClient api)
    {
        var lookups = new WeekExcelLookups();
        Job[] allJobs = Array.Empty<Job>();
        try
        {
            allJobs = api.GetJobs(activeOnly: false).ToArray();
        }
        catch
        {
            /* API yanıt vermezse sadece detay ile devam etmeyi dene */
        }

        foreach (var job in allJobs)
            lookups.JobBasics[job.Id] = (job.Code ?? "?", job.Description ?? "");

        var wanted = entries.Where(e => e.JobId.HasValue).Select(e => e.JobId!.Value).Distinct().ToList();

        foreach (var jobId in wanted)
        {
            try
            {
                var detail = api.GetJobDetail(jobId);
                if (detail != null)
                    lookups.JobDetails[jobId] = detail;
            }
            catch
            {
                /* yoksa JobBasics yeterli */
            }
        }

        return lookups;
    }
}
