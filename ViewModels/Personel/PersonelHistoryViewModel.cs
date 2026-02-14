using System.Collections.ObjectModel;
using System.Windows.Input;
using CAKA.PerformanceApp.Core;
using CAKA.PerformanceApp.Models;
using CAKA.PerformanceApp.Services;

namespace CAKA.PerformanceApp.ViewModels.Personel;

public class PersonelHistoryViewModel : ViewModelBase
{
    public PersonelHistoryViewModel(IAuthService authService, IWorkLogService workLogService)
    {
        _authService = authService;
        _workLogService = workLogService;
        WeekGroups = new ObservableCollection<WeekWorkLogGroup>();
        _pendingDeleteIds = new List<Guid>();
        RefreshCommand = new RelayCommand(_ => Refresh());
        SaveWeekEditsCommand = new RelayCommand(param =>
        {
            if (param is WeekWorkLogGroup group)
                SaveWeekEdits(group);
        });
        DeleteEntryCommand = new RelayCommand(param =>
        {
            if (param is WorkLog log)
                DeleteEntry(log);
        });
        Refresh();
    }

    private readonly IAuthService _authService;
    private readonly IWorkLogService _workLogService;
    private readonly List<Guid> _pendingDeleteIds;

    public ObservableCollection<WeekWorkLogGroup> WeekGroups { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SaveWeekEditsCommand { get; }
    public ICommand DeleteEntryCommand { get; }

    private static DateTime GetMonday(DateTime date)
    {
        var d = date.Date;
        var daysToMonday = d.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)d.DayOfWeek - 1;
        return d.AddDays(-daysToMonday);
    }

    public void Refresh()
    {
        WeekGroups.Clear();
        var userName = _authService.CurrentUser?.UserName;
        var list = _workLogService.GetByUser(userName)
            .Where(log => !_pendingDeleteIds.Contains(log.Id))
            .ToList();
        var byWeek = list
            .GroupBy(log => GetMonday(log.Date))
            .OrderByDescending(g => g.Key)
            .ToList();

        var today = DateTime.Today;
        var currentWeekStart = GetMonday(today);

        foreach (var group in byWeek)
        {
            var weekStart = group.Key;
            var weekEnd = weekStart.AddDays(6);
            var isCurrentWeek = (weekStart == currentWeekStart);
            var wg = new WeekWorkLogGroup { WeekStart = weekStart, WeekEnd = weekEnd, IsCurrentWeek = isCurrentWeek };
            foreach (var log in group.OrderBy(l => l.Date).ThenBy(l => l.CreatedAt))
                wg.Entries.Add(log);
            WeekGroups.Add(wg);
        }
    }

    private void DeleteEntry(WorkLog log)
    {
        var currentWeekGroup = WeekGroups.FirstOrDefault(g => g.IsCurrentWeek);
        if (currentWeekGroup == null) return;
        var found = currentWeekGroup.Entries.FirstOrDefault(e => e.Id == log.Id);
        if (found == null) return;
        currentWeekGroup.Entries.Remove(found);
        _pendingDeleteIds.Add(log.Id);
    }

    private void SaveWeekEdits(WeekWorkLogGroup group)
    {
        if (!group.IsCurrentWeek) return;
        foreach (var log in group.Entries)
        {
            if (log.Hours < 0 || log.Hours > 24) continue;
            _workLogService.Update(log);
        }
        foreach (var id in _pendingDeleteIds)
            _workLogService.Delete(id);
        _pendingDeleteIds.Clear();
    }
}
