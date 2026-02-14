using System.Globalization;
using CAKA.PerformanceApp.Models;
using ClosedXML.Excel;

namespace CAKA.PerformanceApp.Services;

public class ReportExcelService : IReportExcelService
{
    public void GenerateWeekReport(string filePath, DateTime weekStart, DateTime weekEnd,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Haftalık rapor");

        var row = 1;
        ws.Cell(row, 1).Value = $"Haftalık iş raporu: {weekStart:dd.MM.yyyy} - {weekEnd:dd.MM.yyyy}";
        ws.Range(row, 1, row, 5).Merge();
        ws.Row(row).Style.Font.Bold = true;
        ws.Row(row).Style.Font.FontSize = 14;
        row += 2;

        var allUserNames = userNameToDisplayName
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
            .OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .Select(kv => kv.Key)
            .ToList();

        var summaryList = new List<(string DisplayName, decimal Hours)>();

        foreach (var userName in allUserNames)
        {
            var displayName = userNameToDisplayName.GetValueOrDefault(userName, userName);
            var userEntries = entries
                .Where(e => string.Equals(e.UserName, userName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Date).ThenBy(e => e.CreatedAt)
                .ToList();
            var totalHours = userEntries.Sum(e => e.Hours);
            summaryList.Add((displayName, totalHours));

            ws.Cell(row, 1).Value = $"Çalışan: {displayName}";
            ws.Range(row, 1, row, 5).Merge();
            ws.Row(row).Style.Font.Bold = true;
            row++;

            ws.Cell(row, 1).Value = "Tarih";
            ws.Cell(row, 2).Value = "Gün";
            ws.Cell(row, 3).Value = "İş açıklaması";
            ws.Cell(row, 4).Value = "Saat";
            ws.Row(row).Style.Font.Bold = true;
            row++;

            if (userEntries.Count == 0)
            {
                ws.Cell(row, 3).Value = "Bu hafta iş kaydı yok.";
                row++;
            }
            else
            {
                foreach (var log in userEntries)
                {
                    ws.Cell(row, 1).Value = log.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                    ws.Cell(row, 2).Value = log.Date.ToString("dddd", new CultureInfo("tr-TR"));
                    ws.Cell(row, 3).Value = log.Description;
                    ws.Cell(row, 4).Value = (double)log.Hours;
                    ws.Cell(row, 4).Style.NumberFormat.Format = "0.0";
                    row++;
                }
            }

            ws.Cell(row, 1).Value = "Toplam:";
            ws.Cell(row, 4).Value = (double)totalHours;
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.0";
            ws.Row(row).Style.Font.Bold = true;
            row += 2;
        }

        ws.Cell(row, 1).Value = "Haftalık özet";
        ws.Row(row).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Çalışan";
        ws.Cell(row, 2).Value = "Toplam saat";
        ws.Row(row).Style.Font.Bold = true;
        row++;
        var grandTotal = summaryList.Sum(x => x.Hours);
        foreach (var (name, hours) in summaryList)
        {
            ws.Cell(row, 1).Value = name;
            ws.Cell(row, 2).Value = (double)hours;
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.0";
            row++;
        }
        ws.Cell(row, 1).Value = "Genel toplam:";
        ws.Cell(row, 2).Value = (double)grandTotal;
        ws.Cell(row, 2).Style.NumberFormat.Format = "0.0";
        ws.Row(row).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
        wb.SaveAs(filePath);
    }
}
