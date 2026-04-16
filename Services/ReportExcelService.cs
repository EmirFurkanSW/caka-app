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
        IReadOnlyDictionary<string, string> userNameToDisplayName)
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

        ws.Cell(5, 1).Value =
            "D: çalışan için hedef (maks.) saat — elle. F: saatlik ücret (USD) — elle. E (verim): 2−(Gerçekleşen÷Hedef), %100 = hedef sürede. G (tahmini maliyet): Hedef saat × Saatlik ücret (D×F), D ve F dolunca otomatik.";
        ws.Range(5, 1, 5, 12).Merge();
        ws.Range(5, 1, 5, 12).Style.Font.Italic = true;
        ws.Range(5, 1, 5, 12).Style.Font.FontColor = XLColor.FromHtml("#6B7B8C");

        var headerRow = 6;
        var headers = new[]
        {
            "Çalışan",
            "Kullanıcı Adı",
            "Gerçekleşen Saat (bu iş)",
            "Maksimum Hedef Saat",
            "Verim (100% = hedefe uygun)",
            "Saatlik Ücret (USD) — elle",
            "Tahmini Maliyet (USD) — D×F",
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

                return new
                {
                    UserName = g.Key,
                    DisplayName = displayName,
                    Actual = actual,
                    LogCount = logs.Count,
                    WorkDayCount = workDayCount,
                    FirstDate = firstDate,
                    LastDate = lastDate
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
            ws.Cell(row, 4).Clear();
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.0";
            // Verim: 100% = hedef sürede; örn. D=10 C=8 → 2-C/D = 1,2 → %120
            ws.Cell(row, 5).FormulaA1 = $"IF(OR(ISBLANK(D{row}),C{row}=0,D{row}=0),\"\",2-C{row}/D{row})";
            ws.Cell(row, 6).Clear();
            ws.Cell(row, 6).Style.NumberFormat.Format = "$#,##0.00";
            // Tahmini maliyet = hedef saat (D) × saatlik ücret (F)
            ws.Cell(row, 7).FormulaA1 = $"IF(OR(ISBLANK(D{row}),ISBLANK(F{row})),\"\",D{row}*F{row})";
            ws.Cell(row, 8).Value = item.LogCount;
            ws.Cell(row, 9).Value = item.WorkDayCount;
            ws.Cell(row, 10).Value = item.FirstDate;
            ws.Cell(row, 11).Value = item.LastDate;
            ws.Cell(row, 12).Value = "—";

            ws.Cell(row, 3).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, 5).Style.NumberFormat.Format = "0.0%";
            ws.Cell(row, 6).Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell(row, 7).Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell(row, 10).Style.DateFormat.Format = "dd.MM.yyyy";
            ws.Cell(row, 11).Style.DateFormat.Format = "dd.MM.yyyy";
            ws.Cell(row, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6FA");

            row++;
        }

        if (grouped.Count > 0)
        {
            var firstDataRow = headerRow + 1;
            var lastDataRow = headerRow + grouped.Count;

            ws.Cell(row, 1).Value = "GENEL TOPLAM";
            ws.Range(row, 1, row, 2).Merge();
            ws.Cell(row, 3).FormulaA1 = $"SUM(C{firstDataRow}:C{lastDataRow})";
            ws.Cell(row, 4).FormulaA1 = $"SUM(D{firstDataRow}:D{lastDataRow})";
            ws.Cell(row, 5).Clear();
            ws.Cell(row, 6).Clear();
            ws.Cell(row, 7).FormulaA1 = $"SUM(G{firstDataRow}:G{lastDataRow})";
            ws.Cell(row, 8).FormulaA1 = $"SUM(H{firstDataRow}:H{lastDataRow})";
            ws.Cell(row, 9).FormulaA1 = $"SUM(I{firstDataRow}:I{lastDataRow})";
            ws.Cell(row, 10).Clear();
            ws.Cell(row, 11).Clear();
            ws.Cell(row, 12).Value = "G sütunu tahmini maliyet toplamı (satır formülleri).";

            ws.Cell(row, 3).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, 7).Style.NumberFormat.Format = "$#,##0.00";
            ws.Range(row, 1, row, 12).Style.Font.Bold = true;
            ws.Range(row, 1, row, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF5");
        }

        var dataEndRow = Math.Max(headerRow + 1, row);
        var tableRange = ws.Range(headerRow, 1, dataEndRow, headers.Length);
        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        ws.SheetView.FreezeRows(headerRow);
        ws.Range(headerRow, 1, headerRow, headers.Length).SetAutoFilter();

        // Üst birleşik başlıklar AdjustToContents ile A sütununu şişirmesin diye tablo için sabit genişlikler
        var colWidths = new[] { 26d, 22d, 34d, 28d, 30d, 32d, 34d, 18d, 18d, 14d, 14d, 42d };
        for (var c = 1; c <= 12; c++)
            ws.Column(c).Width = colWidths[c - 1];
        ws.Range(headerRow, 1, headerRow, 12).Style.Alignment.WrapText = true;
        ws.Row(headerRow).Height = 36;

        wb.SaveAs(filePath);
    }
}
