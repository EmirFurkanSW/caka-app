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

    public void GenerateJobPerformanceReport(string filePath, string jobCode, string jobDescription,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName,
        IReadOnlyDictionary<string, decimal> hourlyRateByUser,
        decimal patronTargetHoursPerEmployee)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("İş performansı");

        var periodStart = entries.Count > 0 ? entries.Min(x => x.Date).Date : DateTime.Today;
        var periodEnd = entries.Count > 0 ? entries.Max(x => x.Date).Date : DateTime.Today;

        ws.Cell(1, 1).Value = "İş Performans Raporu (Yönetici)";
        ws.Range(1, 1, 1, 12).Merge();
        ws.Range(1, 1, 1, 12).Style.Font.Bold = true;
        ws.Range(1, 1, 1, 12).Style.Font.FontSize = 18;
        ws.Range(1, 1, 1, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E2A38");
        ws.Range(1, 1, 1, 12).Style.Font.FontColor = XLColor.White;
        ws.Range(1, 1, 1, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(2, 1).Value = $"İş: {jobCode} - {jobDescription}";
        ws.Range(2, 1, 2, 12).Merge();
        ws.Range(2, 1, 2, 12).Style.Font.Bold = true;
        ws.Range(2, 1, 2, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF5");

        ws.Cell(3, 1).Value = $"Rapor dönemi (kayıtlar): {periodStart:dd.MM.yyyy} - {periodEnd:dd.MM.yyyy}";
        ws.Range(3, 1, 3, 12).Merge();
        ws.Range(3, 1, 3, 12).Style.Font.FontColor = XLColor.FromHtml("#3A4653");

        ws.Cell(4, 1).Value = "Not: Bu rapor yalnızca seçilen işe ait kayıtları içerir. Diğer işler dahil edilmez.";
        ws.Range(4, 1, 4, 12).Merge();
        ws.Range(4, 1, 4, 12).Style.Font.Italic = true;
        ws.Range(4, 1, 4, 12).Style.Font.FontColor = XLColor.FromHtml("#6B7B8C");

        ws.Cell(5, 1).Value = "Patron hedefi (çalışan başına maks. saat)";
        ws.Cell(5, 2).Value = (double)patronTargetHoursPerEmployee;
        ws.Cell(5, 2).Style.NumberFormat.Format = "0.0";
        ws.Range(5, 1, 5, 2).Style.Font.Bold = true;
        ws.Range(5, 1, 5, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6FA");

        ws.Cell(6, 1).Value = "Verim sütunu: hedefe göre yüzde sapma = (Hedef ÷ Gerçekleşen − 1) × 100. Hedefe eşitse %0; hedeften hızlıysa pozitif; yavaşsa negatif.";
        ws.Range(6, 1, 6, 12).Merge();
        ws.Range(6, 1, 6, 12).Style.Font.Italic = true;
        ws.Range(6, 1, 6, 12).Style.Font.FontColor = XLColor.FromHtml("#6B7B8C");

        var headerRow = 8;
        var headers = new[]
        {
            "Çalışan",
            "Kullanıcı Adı",
            "Gerçekleşen Saat (bu iş)",
            "Maksimum Hedef Saat",
            "Verim (hedefe göre %)",
            "Saatlik Ücret (TRY)",
            "Tahmini Maliyet (TRY)",
            "Kayıt Sayısı",
            "Çalışılan Gün",
            "İlk Kayıt",
            "Son Kayıt",
            "Durum"
        };

        for (var i = 0; i < headers.Length; i++)
            ws.Cell(headerRow, i + 1).Value = headers[i];

        var headerRange = ws.Range(headerRow, 1, headerRow, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E2A38");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var grouped = entries
            .Where(x => !string.IsNullOrWhiteSpace(x.UserName))
            .GroupBy(x => x.UserName!)
            .Select(g =>
            {
                var logs = g.OrderBy(x => x.Date).ThenBy(x => x.CreatedAt).ToList();
                var displayName = userNameToDisplayName.GetValueOrDefault(g.Key, g.Key);
                var actual = logs.Sum(x => x.Hours);
                var workDayCount = logs.Select(x => x.Date.Date).Distinct().Count();
                var firstDate = logs.First().Date.Date;
                var lastDate = logs.Last().Date.Date;

                // Verim: hedefe göre % sapma — hedefe eşitse 0; örn. hedef 40, gerçek 20 → (40/20-1)*100 = %100
                decimal efficiencyPercent;
                if (actual <= 0m)
                    efficiencyPercent = 0m;
                else
                    efficiencyPercent = (patronTargetHoursPerEmployee / actual - 1m) * 100m;

                var rate = hourlyRateByUser.GetValueOrDefault(g.Key, 0m);
                if (rate < 0) rate = 0m;
                var cost = actual * rate;

                string status;
                if (actual <= 0m)
                    status = "Kayıt yok / 0 saat";
                else if (efficiencyPercent > 0.0001m)
                    status = "Hedeften hızlı (pozitif verim)";
                else if (efficiencyPercent < -0.0001m)
                    status = "Hedeften yavaş (negatif verim)";
                else
                    status = "Hedefte (verim %0)";

                return new
                {
                    UserName = g.Key,
                    DisplayName = displayName,
                    Actual = actual,
                    Target = patronTargetHoursPerEmployee,
                    EfficiencyPercent = efficiencyPercent,
                    Rate = rate,
                    Cost = cost,
                    LogCount = logs.Count,
                    WorkDayCount = workDayCount,
                    FirstDate = firstDate,
                    LastDate = lastDate,
                    Status = status
                };
            })
            .OrderByDescending(x => x.Actual)
            .ToList();

        var row = headerRow + 1;
        foreach (var item in grouped)
        {
            ws.Cell(row, 1).Value = item.DisplayName;
            ws.Cell(row, 2).Value = item.UserName;
            ws.Cell(row, 3).Value = (double)item.Actual;
            ws.Cell(row, 4).Value = (double)item.Target;
            ws.Cell(row, 5).Value = (double)(item.EfficiencyPercent / 100m);
            ws.Cell(row, 6).Value = (double)item.Rate;
            ws.Cell(row, 7).Value = (double)item.Cost;
            ws.Cell(row, 8).Value = item.LogCount;
            ws.Cell(row, 9).Value = item.WorkDayCount;
            ws.Cell(row, 10).Value = item.FirstDate;
            ws.Cell(row, 11).Value = item.LastDate;
            ws.Cell(row, 12).Value = item.Status;

            ws.Cell(row, 3).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.0%";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 10).Style.DateFormat.Format = "dd.MM.yyyy";
            ws.Cell(row, 11).Style.DateFormat.Format = "dd.MM.yyyy";

            if (item.EfficiencyPercent < -0.0001m)
                ws.Cell(row, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8D7DA");
            else if (item.EfficiencyPercent > 0.0001m)
                ws.Cell(row, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#D4EDDA");
            else
                ws.Cell(row, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF5");

            row++;
        }

        if (grouped.Count > 0)
        {
            var totalActual = grouped.Sum(x => x.Actual);
            var totalTarget = patronTargetHoursPerEmployee * grouped.Count;
            var totalCost = grouped.Sum(x => x.Cost);

            ws.Cell(row, 1).Value = "GENEL TOPLAM";
            ws.Range(row, 1, row, 2).Merge();
            ws.Cell(row, 3).Value = (double)totalActual;
            ws.Cell(row, 4).Value = (double)totalTarget;
            ws.Cell(row, 5).Value = "";
            ws.Cell(row, 6).Value = "";
            ws.Cell(row, 7).Value = (double)totalCost;
            ws.Cell(row, 8).Value = grouped.Sum(x => x.LogCount);
            ws.Cell(row, 9).Value = grouped.Sum(x => x.WorkDayCount);
            ws.Cell(row, 10).Value = "";
            ws.Cell(row, 11).Value = "";
            ws.Cell(row, 12).Value = "Özet: verim yüzdeleri satır bazlıdır; toplanmaz.";

            ws.Cell(row, 3).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Range(row, 1, row, 12).Style.Font.Bold = true;
            ws.Range(row, 1, row, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF5");
        }

        var dataEndRow = Math.Max(headerRow + 1, row);
        var tableRange = ws.Range(headerRow, 1, dataEndRow, headers.Length);
        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        ws.SheetView.FreezeRows(headerRow);
        ws.Range(headerRow, 1, headerRow, headers.Length).SetAutoFilter();

        ws.Column(1).Width = 24;
        ws.Column(2).Width = 18;
        ws.Column(3).Width = 18;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 22;
        ws.Column(6).Width = 18;
        ws.Column(7).Width = 20;
        ws.Column(8).Width = 12;
        ws.Column(9).Width = 12;
        ws.Column(10).Width = 12;
        ws.Column(11).Width = 12;
        ws.Column(12).Width = 26;

        wb.SaveAs(filePath);
    }
}
