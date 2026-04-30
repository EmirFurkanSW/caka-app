using System.Globalization;
using CAKA.PerformanceApp.Models;
using ClosedXML.Excel;

namespace CAKA.PerformanceApp.Services;

public class ReportExcelService : IReportExcelService
{
    private static readonly XLColor NavyHeader = XLColor.FromHtml("#1E2A38");
    private static readonly XLColor StripSub = XLColor.FromHtml("#F3F6FA");
    private static readonly XLColor CardBg = XLColor.FromHtml("#E8EEF5");
    private const double KontenjanCarpani = 1.05;
    private const double GenelGiderCarpani = 1.05;

    public void GenerateWeekReport(string filePath, DateTime weekStart, DateTime weekEnd,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName,
        WeekExcelLookups? lookups = null)
    {
        lookups ??= new WeekExcelLookups();

        using var wb = new XLWorkbook();
        FillWeekDetailWorksheet(wb.Worksheets.Add("Haftalık detay"),
            weekStart, weekEnd, entries, userNameToDisplayName, lookups);
        FillWeekPivotWorksheet(wb.Worksheets.Add("İş-aşama-çalışan"),
            entries, userNameToDisplayName, lookups);
        FillWeekEmployeeTotalsWorksheet(wb.Worksheets.Add("Çalışan özeti"),
            entries, userNameToDisplayName, lookups);
        var matrices = wb.Worksheets.Add("İş bazlı maliyet");
        var rowMat = 1;
        FillJobMatricesForLogs(matrices, ref rowMat, lookups, entries, userNameToDisplayName,
            $"Haftalık dönem: {weekStart:dd.MM.yyyy} – {weekEnd:dd.MM.yyyy}");

        matrices.Columns().AdjustToContents();
        foreach (IXLWorksheet ws in wb.Worksheets.Where(w => w.Name != "İş bazlı maliyet"))
            ws.Columns().AdjustToContents();

        wb.SaveAs(filePath);
    }

    public void GenerateJobPerformanceReport(string filePath, string jobCode, string jobDescription,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName,
        JobDetail? jobDetail = null,
        IReadOnlyList<string>? columnUserNames = null,
        Guid? explicitJobId = null)
    {
        using var wb = new XLWorkbook();
        var periodStart = entries.Count > 0 ? entries.Min(x => x.Date).Date : DateTime.Today;
        var periodEnd = entries.Count > 0 ? entries.Max(x => x.Date).Date : DateTime.Today;

        var jobLogs = entries.ToList();
        var jobIdResolved = explicitJobId
            ?? jobDetail?.Id
            ?? jobLogs.Select(e => e.JobId).FirstOrDefault(j => j.HasValue)
            ?? Guid.Empty;

        var usersPre = ResolveColumnUsernames(columnUserNames, userNameToDisplayName, jobLogs);
        var lastColTitle = Math.Max(8, usersPre.Count + 2);

        var ws = wb.Worksheets.Add("Maliyet ve aşama");
        ws.Cell(1, 1).Value = "İş maliyeti ve aşama dağılımı";
        StyleTitleMerge(ws.Range(1, 1, 1, lastColTitle));

        ws.Cell(2, 1).Value = $"{jobCode} — {jobDescription}";
        ws.Range(2, 1, 2, lastColTitle).Merge().Style.Fill.BackgroundColor = CardBg;

        ws.Cell(3, 1).Value =
            $"Kayıtların tarih aralığı: {periodStart:dd.MM.yyyy} – {periodEnd:dd.MM.yyyy}";
        ws.Range(3, 1, 3, lastColTitle).Merge();

        var lookups = BuildLookupsFromJobDetail(jobDetail, jobCode, jobDescription);

        var r = 5;
        FillJobLandscapePerformanceSheet(ws, ref r, lookups, jobIdResolved, jobLogs,
            userNameToDisplayName, columnUserNames);

        var noteRow = r + 1;
        ws.Cell(noteRow, 1).Value =
            "Not: Kontenjan ve genel gider sırasıyla %5 (×1,05); ‘Discount Amount’ ve ‘Grand Total / First Offer’ satırı düzenlenebilir. Farklı para birimleri aynı toplamdaki sayılarca toplanmıştır.";
        ws.Range(noteRow, 1, noteRow, lastColTitle).Merge().Style.Font.Italic = true;
        ws.Range(noteRow, 1, noteRow, lastColTitle).Style.Font.FontColor = XLColor.FromHtml("#5A6978");

        ws.Columns().AdjustToContents();

        if (jobDetail != null)
            AppendJobDefinitionWorksheet(wb, jobDetail, jobCode, jobDescription);

        wb.SaveAs(filePath);
    }

    /// <summary>İş bazlı profesyonel layout: çalışanlar sütununda, Stage tablosu, Grand Total / İndirim / First Offer.</summary>
    private static void FillJobLandscapePerformanceSheet(IXLWorksheet ws, ref int startRow,
        WeekExcelLookups lookups, Guid jobId, List<WorkLog> jobLogs,
        IReadOnlyDictionary<string, string> display,
        IReadOnlyList<string>? columnUserPreset)
    {
        var userCols = ResolveColumnUsernames(columnUserPreset, display, jobLogs);
        lookups.JobDetails.TryGetValue(jobId, out var detail);
        var (code, jdesc) = lookups.ResolveJob(jobId);

        if (userCols.Count == 0)
        {
            ws.Cell(startRow, 1).Value = "Sütunda gösterilecek kullanıcı yok (Çalışanlar listesi boş).";
            startRow += 3;
            return;
        }

        var lastCol = userCols.Count + 2;

        ws.Cell(startRow, 1).Value = $"{code} — {jdesc}".Trim().TrimEnd('—', ' ');
        ws.Range(startRow, 1, startRow, lastCol).Merge();
        ws.Row(startRow).Style.Font.Bold = true;
        ws.Row(startRow).Style.Font.FontSize = 13;
        startRow++;

        ws.Cell(startRow, 1).Value = "";
        for (var i = 0; i < userCols.Count; i++)
            ws.Cell(startRow, i + 2).Value = display.GetValueOrDefault(userCols[i], userCols[i]);

        ws.Cell(startRow, lastCol).Value = "Toplam";
        StyleHeader(ws.Range(startRow, 1, startRow, lastCol));
        var headerExcelRow = startRow;
        startRow++;

        var matrixTopRow = startRow;

        ws.Cell(startRow, 1).Value = "Saatlik ücret (iş tanımı)";
        for (var i = 0; i < userCols.Count; i++)
        {
            if (jobId == Guid.Empty)
            {
                ws.Cell(startRow, i + 2).Value = "—";
                continue;
            }

            var rate = lookups.ParticipantHourly(userCols[i], jobId, out var usd);
            var cell = ws.Cell(startRow, i + 2);
            if (rate.HasValue)
            {
                cell.Value = (double)rate.Value;
                cell.Style.NumberFormat.Format = usd ? "$#,##0.00" : "#,##0.00 \"₺\"";
            }
            else
                cell.Value = 0;

            ws.Cell(startRow, i + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Cell(startRow, lastCol).Clear();
        startRow++;

        ws.Cell(startRow, 1).Value = "Mandayı sayısı (saat÷8)";
        for (var i = 0; i < userCols.Count; i++)
        {
            var h = SumHoursForUser(jobLogs, userCols[i], HourFilter.All);
            ws.Cell(startRow, i + 2).Value = (double)(h / 8m);
            ws.Cell(startRow, i + 2).Style.NumberFormat.Format = "0.00";
            ws.Cell(startRow, i + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Cell(startRow, lastCol).FormulaA1 =
            $"SUM({ColLetter(2)}{startRow}:{ColLetter(lastCol - 1)}{startRow})";
        ws.Cell(startRow, lastCol).Style.NumberFormat.Format = "0.00";
        startRow++;

        ws.Cell(startRow, 1).Value = "Toplam saat";
        for (var i = 0; i < userCols.Count; i++)
        {
            var h = SumHoursForUser(jobLogs, userCols[i], HourFilter.All);
            ws.Cell(startRow, i + 2).Value = (double)h;
            ws.Cell(startRow, i + 2).Style.NumberFormat.Format = "0.0";
            ws.Cell(startRow, i + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Cell(startRow, lastCol).FormulaA1 =
            $"SUM({ColLetter(2)}{startRow}:{ColLetter(lastCol - 1)}{startRow})";
        ws.Cell(startRow, lastCol).Style.NumberFormat.Format = "0.0";
        startRow++;

        ws.Cell(startRow, 1).Value = "Toplam maliyet";
        for (var i = 0; i < userCols.Count; i++)
        {
            var hrs = SumHoursForUser(jobLogs, userCols[i], HourFilter.All);
            decimal? rateOpt = null;
            var isUsd = false;
            if (jobId != Guid.Empty)
                rateOpt = lookups.ParticipantHourly(userCols[i], jobId, out isUsd);

            var cell = ws.Cell(startRow, i + 2);
            if (rateOpt.HasValue)
            {
                cell.Value = (double)(hrs * rateOpt.Value);
                cell.Style.NumberFormat.Format = isUsd ? "$#,##0.00" : "#,##0.00 \"₺\"";
            }
            else
                cell.Value = 0d;

            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Cell(startRow, lastCol).FormulaA1 =
            $"SUM({ColLetter(2)}{startRow}:{ColLetter(lastCol - 1)}{startRow})";
        ws.Cell(startRow, lastCol).Style.NumberFormat.Format = "#,##0.00";
        startRow++;

        ws.Row(startRow).Height = 8;
        startRow++;

        var stageList = OrderedStagesForJobPerformance(detail, jobLogs);
        if (stageList.Count == 0 && detail?.Stages is { Count: > 0 })
            stageList.AddRange(detail.Stages.OrderBy(s => s.SortOrder).Select(s => s.Id));

        foreach (var sid in stageList)
        {
            var lbl = sid == Guid.Empty ? "General / none" : lookups.ResolveStage(jobId, sid);
            ws.Cell(startRow, 1).Value = lbl;
            ws.Row(startRow).Style.Font.Bold = false;
            for (var i = 0; i < userCols.Count; i++)
            {
                var h = sid == Guid.Empty
                    ? SumHoursForUser(jobLogs, userCols[i], HourFilter.UnassignedStage)
                    : SumHoursForUser(jobLogs, userCols[i], HourFilter.ExactStage, sid);
                ws.Cell(startRow, i + 2).Value = (double)h;
                ws.Cell(startRow, i + 2).Style.NumberFormat.Format = "0.0";
                ws.Cell(startRow, i + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                if ((startRow - matrixTopRow) % 2 == 1)
                    ws.Cell(startRow, i + 2).Style.Fill.BackgroundColor = StripSub;
            }

            ws.Cell(startRow, lastCol).FormulaA1 =
                $"SUM({ColLetter(2)}{startRow}:{ColLetter(lastCol - 1)}{startRow})";
            ws.Cell(startRow, lastCol).Style.NumberFormat.Format = "0.0";
            if ((startRow - matrixTopRow) % 2 == 1)
                ws.Cell(startRow, lastCol).Style.Fill.BackgroundColor = StripSub;

            startRow++;
        }

        BorderRange(ws.Range(matrixTopRow - 1, 1, startRow - 1, lastCol));
        ws.SheetView.FreezeRows(headerExcelRow);

        startRow++;

        ws.Cell(startRow, 1).Value = "Stage and Description";
        ws.Range(startRow, 1, startRow, 5).Style.Font.Bold = true;
        ws.Range(startRow, 1, startRow, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#5A5A5A");
        ws.Range(startRow, 1, startRow, 5).Style.Font.FontColor = XLColor.White;
        ws.Cell(startRow, 2).Value = "Total";
        ws.Cell(startRow, 3).Value = "Total Hours";
        ws.Cell(startRow, 4).Value = "Price With Contingency (5%)";
        ws.Cell(startRow, 5).Value = "Price With G&A Cost (5%)";
        BorderRange(ws.Range(startRow, 1, startRow, 5));
        var summaryHeaderExcelRow = startRow;
        startRow++;

        var summaryFirstDataRow = startRow;

        if (stageList.Count == 0)
        {
            ws.Cell(startRow, 1).Value = "— Tanımlı aşama yok —";
            ws.Cell(startRow, 2).Value = 0d;
            ws.Cell(startRow, 3).Value = 0d;
            ws.Cell(startRow, 4).Value = 0d;
            ws.Cell(startRow, 5).Value = 0d;
            startRow++;
        }
        else
        {
            foreach (var sid in stageList)
            {
                var lbl = sid == Guid.Empty ? "General / none" : lookups.ResolveStage(jobId, sid);
                decimal raw = 0;
                decimal hrs = 0;
                foreach (var u in userCols)
                {
                    var uh = sid == Guid.Empty
                        ? SumHoursForUser(jobLogs, u, HourFilter.UnassignedStage)
                        : SumHoursForUser(jobLogs, u, HourFilter.ExactStage, sid);
                    hrs += uh;
                    var rateOpt = jobId == Guid.Empty ? null : lookups.ParticipantHourly(u, jobId, out _);
                    if (rateOpt.HasValue)
                        raw += uh * rateOpt.Value;
                }

                var cont = raw * (decimal)KontenjanCarpani;
                var ga = cont * (decimal)GenelGiderCarpani;

                ws.Cell(startRow, 1).Value = lbl;
                ws.Cell(startRow, 2).Value = (double)raw;
                ws.Cell(startRow, 3).Value = (double)hrs;
                ws.Cell(startRow, 4).Value = (double)cont;
                ws.Cell(startRow, 5).Value = (double)ga;
                for (var c = 2; c <= 5; c++)
                    ws.Cell(startRow, c).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(startRow, 3).Style.NumberFormat.Format = "0.0";
                startRow++;
            }
        }

        var summaryLastDataRow = startRow - 1;
        BorderRange(ws.Range(summaryHeaderExcelRow, 1, summaryLastDataRow, 5));

        startRow += 2;

        var grandHdrRow = startRow;
        ws.Cell(grandHdrRow, 1).Value = "Grand Total";
        ws.Cell(grandHdrRow, 2).Value = "Discount Amount";
        ws.Cell(grandHdrRow, 3).Value = "First Offer";
        var hdrFooter = ws.Range(grandHdrRow, 1, grandHdrRow, 3);
        hdrFooter.Style.Font.Bold = true;
        hdrFooter.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
        hdrFooter.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        BorderRange(hdrFooter);

        var grandValRow = startRow + 1;
        ws.Cell(grandValRow, 1).FormulaA1 = $"SUM(E{summaryFirstDataRow}:E{summaryLastDataRow})";
        ws.Cell(grandValRow, 2).Value = 0d;
        ws.Cell(grandValRow, 3).FormulaA1 = $"{ColLetter(1)}{grandValRow}-{ColLetter(2)}{grandValRow}";
        for (var c = 1; c <= 3; c++)
        {
            ws.Cell(grandValRow, c).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(grandValRow, c).Style.Font.FontColor = XLColor.FromHtml("#0563C1");
            ws.Cell(grandValRow, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        BorderRange(ws.Range(grandHdrRow, 1, grandValRow, 3));
        startRow = grandValRow + 2;
    }

    private static List<string> ResolveColumnUsernames(
        IReadOnlyList<string>? presetOrdered,
        IReadOnlyDictionary<string, string> display,
        List<WorkLog> jobLogs)
    {
        if (presetOrdered is { Count: > 0 })
        {
            return presetOrdered
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return jobLogs
            .Where(l => !string.IsNullOrWhiteSpace(l.UserName))
            .Select(l => l.UserName!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(u => display.GetValueOrDefault(u, u), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>İş tanımındaki tüm aşamalar + kayıtlarda geçen ek aşamalar + (varsa) genel satırı.</summary>
    private static List<Guid> OrderedStagesForJobPerformance(JobDetail? detail, List<WorkLog> jobLogs)
    {
        var ordered = new List<Guid>();
        var hasGeneral = jobLogs.Any(l => l.JobStageId == null || l.JobStageId == Guid.Empty);

        if (hasGeneral)
            ordered.Add(Guid.Empty);

        if (detail?.Stages is { Count: > 0 })
        {
            foreach (var s in detail.Stages.OrderBy(x => x.SortOrder))
                if (!ordered.Contains(s.Id))
                    ordered.Add(s.Id);
        }

        foreach (var id in jobLogs
                     .Where(l => l.JobStageId.HasValue && l.JobStageId != Guid.Empty)
                     .Select(l => l.JobStageId!.Value)
                     .Distinct())
            if (!ordered.Contains(id))
                ordered.Add(id);

        return ordered;
    }

    private enum HourFilter { All, UnassignedStage, ExactStage }

    private static decimal SumHoursForUser(List<WorkLog> logs, string userColumn, HourFilter mode, Guid stageId = default)
    {
        var q = logs.Where(l => SameUser(l.UserName, userColumn));
        return mode switch
        {
            HourFilter.All => q.Sum(l => l.Hours),
            HourFilter.UnassignedStage => q.Where(l => l.JobStageId == null || l.JobStageId == Guid.Empty)
                .Sum(l => l.Hours),
            HourFilter.ExactStage => q.Where(l => l.JobStageId == stageId).Sum(l => l.Hours),
            _ => 0
        };
    }

    private static bool SameUser(string? logUser, string colUser) =>
        string.Equals(logUser?.Trim(), colUser, StringComparison.OrdinalIgnoreCase);

    private static List<Guid> OrderedStageIdsForJob(JobDetail? d, List<WorkLog> jobLogs)
    {
        var logged = jobLogs.Where(l => l.JobStageId.HasValue && l.JobStageId != Guid.Empty)
            .Select(l => l.JobStageId!.Value).Distinct().ToList();
        var hasUnassigned = jobLogs.Any(l => l.JobStageId == null || l.JobStageId == Guid.Empty);
        var order = new List<Guid>();

        if (d?.Stages.Count > 0)
        {
            foreach (var s in d.Stages.OrderBy(x => x.SortOrder))
                if (logged.Contains(s.Id))
                    order.Add(s.Id);
            foreach (var id in logged.OrderBy(id => id))
                if (!order.Contains(id))
                    order.Add(id);
        }
        else
        {
            order.AddRange(logged.OrderBy(id => id));
        }

        if (hasUnassigned)
            order.Insert(0, Guid.Empty);

        return order;
    }

    private static WeekExcelLookups BuildLookupsFromJobDetail(JobDetail? jobDetail, string code, string desc)
    {
        var lookups = new WeekExcelLookups();
        if (jobDetail == null || jobDetail.Id == Guid.Empty)
            return lookups;

        lookups.JobBasics[jobDetail.Id] = (code ?? "?", desc ?? "");
        lookups.JobDetails[jobDetail.Id] = jobDetail;
        return lookups;
    }

    private static void FillJobMatricesForLogs(IXLWorksheet ws, ref int startRow,
        WeekExcelLookups lookups,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplay,
        string? periodNote)
    {
        var distinctJobs = entries
            .Where(e => e.JobId.HasValue && e.JobId.Value != Guid.Empty)
            .Select(e => e.JobId!.Value)
            .Distinct()
            .OrderBy(id => lookups.ResolveJob(id).Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(periodNote))
        {
            ws.Cell(startRow, 1).Value = periodNote;
            ws.Range(startRow, 1, startRow, 12).Merge();
            ws.Cell(startRow, 1).Style.Font.Bold = true;
            startRow += 2;
        }

        if (distinctJobs.Count == 0)
        {
            ws.Cell(startRow, 1).Value =
                "Bu dönemde JobId içeren iş kaydı yok. Eski kayıtlar için 'Haftalık detay' sayfasına bakın.";
            startRow += 3;
            return;
        }

        foreach (var jobId in distinctJobs)
        {
            var jobLogs = entries.Where(e => e.JobId == jobId).ToList();
            WriteSingleJobEconomicsBlock(ws, ref startRow, lookups, jobId, jobLogs, userNameToDisplay);
            startRow += 3;
        }
    }

    private static string ColLetter(int columnIndex1Based)
    {
        var n = columnIndex1Based;
        var s = "";
        while (n > 0)
        {
            n--;
            s = (char)('A' + n % 26) + s;
            n /= 26;
        }

        return string.IsNullOrEmpty(s) ? "A" : s;
    }

    private static void WriteSingleJobEconomicsBlock(IXLWorksheet ws, ref int startRow,
        WeekExcelLookups lookups, Guid jobId, List<WorkLog> jobLogs,
        IReadOnlyDictionary<string, string> userNameToDisplay)
    {
        var (code, jdesc) = lookups.ResolveJob(jobId);
        lookups.JobDetails.TryGetValue(jobId, out var detail);

        var userCols = jobLogs.Where(l => !string.IsNullOrWhiteSpace(l.UserName))
            .Select(l => l.UserName!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(u => userNameToDisplay.GetValueOrDefault(u, u), StringComparer.OrdinalIgnoreCase)
            .ToList();

        ws.Cell(startRow, 1).Value = $"{code} — {jdesc}".Trim().TrimEnd('—', ' ');

        if (userCols.Count == 0)
        {
            ws.Cell(startRow, 1).Value += " — kullanıcı adlı kayıt yok.";
            startRow += 2;
            return;
        }

        var lastCol = userCols.Count + 2;
        ws.Range(startRow, 1, startRow, lastCol).Merge();
        ws.Row(startRow).Style.Font.Bold = true;
        ws.Row(startRow).Style.Font.FontSize = 13;
        startRow++;

        var stages = OrderedStageIdsForJob(detail, jobLogs);

        ws.Cell(startRow, 1).Value = "";
        for (var i = 0; i < userCols.Count; i++)
            ws.Cell(startRow, i + 2).Value = userNameToDisplay.GetValueOrDefault(userCols[i], userCols[i]);

        ws.Cell(startRow, lastCol).Value = "Toplam";
        StyleHeader(ws.Range(startRow, 1, startRow, lastCol));
        startRow++;

        var matrixTopRow = startRow;

        ws.Cell(startRow, 1).Value = "Saatlik ücret (iş tanımı)";
        for (var i = 0; i < userCols.Count; i++)
        {
            var rate = lookups.ParticipantHourly(userCols[i], jobId, out var usd);
            var cell = ws.Cell(startRow, i + 2);
            if (rate.HasValue)
            {
                cell.Value = (double)rate.Value;
                cell.Style.NumberFormat.Format = usd ? "$#,##0.00" : "#,##0.00 \"₺\"";
            }
            else
                cell.Value = "(tanımlı değil)";
        }

        startRow++;

        ws.Cell(startRow, 1).Value = "Mandayı sayısı (saat÷8)";
        for (var i = 0; i < userCols.Count; i++)
        {
            var h = SumHoursForUser(jobLogs, userCols[i], HourFilter.All);
            ws.Cell(startRow, i + 2).Value = (double)(h / 8m);
            ws.Cell(startRow, i + 2).Style.NumberFormat.Format = "0.00";
        }

        ws.Cell(startRow, lastCol).FormulaA1 =
            $"SUM({ColLetter(2)}{startRow}:{ColLetter(lastCol - 1)}{startRow})";
        ws.Cell(startRow, lastCol).Style.NumberFormat.Format = "0.00";
        startRow++;

        ws.Cell(startRow, 1).Value = "Toplam saat";
        for (var i = 0; i < userCols.Count; i++)
        {
            var h = SumHoursForUser(jobLogs, userCols[i], HourFilter.All);
            ws.Cell(startRow, i + 2).Value = (double)h;
            ws.Cell(startRow, i + 2).Style.NumberFormat.Format = "0.0";
        }

        ws.Cell(startRow, lastCol).FormulaA1 =
            $"SUM({ColLetter(2)}{startRow}:{ColLetter(lastCol - 1)}{startRow})";
        ws.Cell(startRow, lastCol).Style.NumberFormat.Format = "0.0";
        startRow++;

        ws.Cell(startRow, 1).Value = "Toplam maliyet";
        for (var i = 0; i < userCols.Count; i++)
        {
            var hrs = SumHoursForUser(jobLogs, userCols[i], HourFilter.All);
            var rateOpt = lookups.ParticipantHourly(userCols[i], jobId, out var usd);
            var cell = ws.Cell(startRow, i + 2);
            if (rateOpt.HasValue)
            {
                cell.Value = (double)(hrs * rateOpt.Value);
                cell.Style.NumberFormat.Format = usd ? "$#,##0.00" : "#,##0.00 \"₺\"";
            }
            else
                cell.Value = "—";
        }

        ws.Cell(startRow, lastCol).FormulaA1 =
            $"SUM({ColLetter(2)}{startRow}:{ColLetter(lastCol - 1)}{startRow})";
        startRow++;

        ws.Row(startRow).Height = 10;
        startRow++;

        foreach (var sid in stages)
        {
            var lbl = sid == Guid.Empty
                ? "Aşamasız / genel"
                : lookups.ResolveStage(jobId, sid);

            ws.Cell(startRow, 1).Value = lbl;
            for (var i = 0; i < userCols.Count; i++)
            {
                var h = sid == Guid.Empty
                    ? SumHoursForUser(jobLogs, userCols[i], HourFilter.UnassignedStage)
                    : SumHoursForUser(jobLogs, userCols[i], HourFilter.ExactStage, sid);
                ws.Cell(startRow, i + 2).Value = (double)h;
                ws.Cell(startRow, i + 2).Style.NumberFormat.Format = "0.0";
            }

            ws.Cell(startRow, lastCol).FormulaA1 =
                $"SUM({ColLetter(2)}{startRow}:{ColLetter(lastCol - 1)}{startRow})";
            ws.Cell(startRow, lastCol).Style.NumberFormat.Format = "0.0";
            startRow++;
        }

        BorderRange(ws.Range(matrixTopRow - 1, 1, startRow - 1, lastCol));

        var summaryHdr = startRow + 1;
        ws.Cell(startRow, 1).Value = "Aşama bazında özet (maliyet + marj)";
        ws.Cell(startRow, 1).Style.Font.Bold = true;
        startRow++;

        ws.Cell(startRow, 1).Value = "Aşama";
        ws.Cell(startRow, 2).Value = "Ham maliyet (Σ)";
        ws.Cell(startRow, 3).Value = "Toplam saat";
        ws.Cell(startRow, 4).Value = "Kontenjan sonrası (×1,05)";
        ws.Cell(startRow, 5).Value = "G&A sonrası (×1,05)";
        StyleHeader(ws.Range(startRow, 1, startRow, 5));
        var summaryFirstRow = startRow + 1;
        startRow++;

        foreach (var sid in stages)
        {
            var lbl = sid == Guid.Empty
                ? "Aşamasız / genel"
                : lookups.ResolveStage(jobId, sid);

            decimal raw = 0;
            decimal hrs = 0;
            foreach (var u in userCols)
            {
                var uh = sid == Guid.Empty
                    ? SumHoursForUser(jobLogs, u, HourFilter.UnassignedStage)
                    : SumHoursForUser(jobLogs, u, HourFilter.ExactStage, sid);

                hrs += uh;
                var rateOpt = lookups.ParticipantHourly(u, jobId, out _);
                if (rateOpt.HasValue)
                    raw += uh * rateOpt.Value;
            }

            var cont = raw * (decimal)KontenjanCarpani;
            var ga = cont * (decimal)GenelGiderCarpani;

            ws.Cell(startRow, 1).Value = lbl;
            ws.Cell(startRow, 2).Value = (double)raw;
            ws.Cell(startRow, 3).Value = (double)hrs;
            ws.Cell(startRow, 4).Value = (double)cont;
            ws.Cell(startRow, 5).Value = (double)ga;
            ws.Cell(startRow, 2).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(startRow, 3).Style.NumberFormat.Format = "0.0";
            ws.Cell(startRow, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(startRow, 5).Style.NumberFormat.Format = "#,##0.00";
            startRow++;
        }

        var summaryLastRow = startRow - 1;
        ws.Cell(startRow, 1).Value = "Genel toplam";
        ws.Cell(startRow, 2).FormulaA1 = $"SUM(B{summaryFirstRow}:B{summaryLastRow})";
        ws.Cell(startRow, 3).FormulaA1 = $"SUM(C{summaryFirstRow}:C{summaryLastRow})";
        ws.Cell(startRow, 4).FormulaA1 = $"SUM(D{summaryFirstRow}:D{summaryLastRow})";
        ws.Cell(startRow, 5).FormulaA1 = $"SUM(E{summaryFirstRow}:E{summaryLastRow})";
        ws.Range(startRow, 1, startRow, 5).Style.Font.Bold = true;
        ws.Range(startRow, 1, startRow, 5).Style.Fill.BackgroundColor = CardBg;
        var grandGaRowNum = startRow;
        BorderRange(ws.Range(summaryHdr - 1, 1, startRow, 5));

        startRow += 2;
        ws.Cell(startRow, 1).Value = "İndirim tutarı:";
        ws.Cell(startRow, 2).Value = 0;
        ws.Cell(startRow, 2).Style.NumberFormat.Format = "#,##0.00";
        var discountRowNum = startRow;
        BorderRange(ws.Range(startRow, 1, startRow, 2));
        startRow++;

        ws.Cell(startRow, 1).Value = "Net (G&A − indirim):";
        ws.Cell(startRow, 2).FormulaA1 =
            $"{ColLetter(5)}{grandGaRowNum}-{ColLetter(2)}{discountRowNum}";
        ws.Cell(startRow, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(startRow, 1).Style.Font.Bold = true;
        BorderRange(ws.Range(startRow, 1, startRow, 2));

        startRow += 2;
    }

    private static void BorderRange(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
    }

    private static void StyleTitleMerge(IXLRange rg)
    {
        rg.Merge();
        rg.Style.Font.Bold = true;
        rg.Style.Font.FontSize = 16;
        rg.Style.Fill.BackgroundColor = NavyHeader;
        rg.Style.Font.FontColor = XLColor.White;
        rg.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = NavyHeader;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    }

    private static void FillWeekDetailWorksheet(IXLWorksheet ws, DateTime weekStart, DateTime weekEnd,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName,
        WeekExcelLookups lookups)
    {
        var row = 1;
        ws.Cell(row, 1).Value = $"Haftalık iş kayıtları: {weekStart:dd.MM.yyyy} — {weekEnd:dd.MM.yyyy}";
        StyleTitleMerge(ws.Range(row, 1, row, 12));
        row += 2;

        ws.Cell(row, 1).Value = "Tarih";
        ws.Cell(row, 2).Value = "Gün";
        ws.Cell(row, 3).Value = "İş kodu";
        ws.Cell(row, 4).Value = "İş açıklaması";
        ws.Cell(row, 5).Value = "Aşama";
        ws.Cell(row, 6).Value = "Kayıt metni";
        ws.Cell(row, 7).Value = "Kullanıcı adı";
        ws.Cell(row, 8).Value = "Ad Soyad";
        ws.Cell(row, 9).Value = "Saatlik ücret";
        ws.Cell(row, 10).Value = "Pb";
        ws.Cell(row, 11).Value = "Tahmini tutar";
        ws.Cell(row, 12).Value = "Saat";
        StyleHeader(ws.Range(row, 1, row, 12));
        ws.SheetView.FreezeRows(row);
        row++;

        foreach (var log in entries.OrderBy(e => userNameToDisplayName.GetValueOrDefault(e.UserName ?? "", e.UserName ?? ""))
                     .ThenBy(e => e.Date).ThenBy(e => e.CreatedAt))
        {
            string code = "", jdesc = "", stageLbl = "";

            ws.Cell(row, 1).Value = log.Date;
            ws.Cell(row, 1).Style.DateFormat.Format = "dd.MM.yyyy";
            ws.Cell(row, 2).Value = log.Date.ToString("dddd", new CultureInfo("tr-TR"));
            ws.Cell(row, 6).Value = log.Description ?? "";
            ws.Cell(row, 7).Value = log.UserName ?? "";

            ws.Cell(row, 8).Value =
                string.IsNullOrWhiteSpace(log.UserName)
                    ? ""
                    : userNameToDisplayName.GetValueOrDefault(log.UserName.Trim(), log.UserName);

            double? rate = null;
            var pb = "";
            decimal? amt = null;
            string moneyFmt = "#,##0.00 \"₺\"";

            if (log.JobId is { } jid && jid != Guid.Empty)
            {
                (code, jdesc) = lookups.ResolveJob(jid);
                stageLbl = lookups.ResolveStage(jid, log.JobStageId);
                var rr = lookups.ParticipantHourly(log.UserName, jid, out var isUsd);
                if (rr.HasValue)
                {
                    rate = (double)rr.Value;
                    pb = isUsd ? "USD" : "TRY";
                    moneyFmt = isUsd ? "$#,##0.00" : "#,##0.00 \"₺\"";
                    amt = log.Hours * rr.Value;
                }
                else
                    stageLbl = string.IsNullOrEmpty(stageLbl) ? "" : stageLbl;

                ws.Cell(row, 3).Value = string.IsNullOrEmpty(code)
                    ? (log.JobId.HasValue ? log.JobId.Value.ToString("N")[..8] + "…" : "")
                    : code;
                ws.Cell(row, 4).Value = jdesc;
                ws.Cell(row, 5).Value =
                    string.IsNullOrEmpty(stageLbl) &&
                    log.JobStageId is { } st && st != Guid.Empty
                        ? "?"
                        : stageLbl;

                if (rate.HasValue)
                {
                    ws.Cell(row, 9).Value = rate.Value;
                    ws.Cell(row, 9).Style.NumberFormat.Format = moneyFmt;
                }
                else
                    ws.Cell(row, 9).Value = "—";

                ws.Cell(row, 10).Value = pb;
                if (amt.HasValue)
                {
                    ws.Cell(row, 11).Value = (double)amt.Value;
                    ws.Cell(row, 11).Style.NumberFormat.Format = moneyFmt;
                }
                else
                    ws.Cell(row, 11).Value = "—";
            }
            else
            {
                ws.Cell(row, 3).Value = "(eski kayıt / iş seçilmemiş)";
                ws.Cell(row, 4).Value = "";
                ws.Cell(row, 5).Value = "";
                ws.Cell(row, 9).Value = "—";
                ws.Cell(row, 10).Value = "";
                ws.Cell(row, 11).Value = "—";
            }

            ws.Cell(row, 12).Value = (double)log.Hours;
            ws.Cell(row, 12).Style.NumberFormat.Format = "0.0";

            if (row % 2 == 0)
                ws.Range(row, 1, row, 12).Style.Fill.BackgroundColor = StripSub;
            ws.Range(row, 1, row, 12).Style.Border.OutsideBorder = XLBorderStyleValues.Dotted;
            row++;
        }
    }

    private static void FillWeekPivotWorksheet(IXLWorksheet ws,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplay,
        WeekExcelLookups lookups)
    {
        var row = 1;
        ws.Cell(row, 1).Value = "İş × aşama × çalışan — saat pivotu";
        StyleTitleMerge(ws.Range(row, 1, row, 6));
        row += 2;

        ws.Cell(row, 1).Value = "İş kodu";
        ws.Cell(row, 2).Value = "İş açıklaması";
        ws.Cell(row, 3).Value = "Aşama";
        ws.Cell(row, 4).Value = "Kullanıcı";
        ws.Cell(row, 5).Value = "Çalışan";
        ws.Cell(row, 6).Value = "Saat";
        StyleHeader(ws.Range(row, 1, row, 6));
        ws.SheetView.FreezeRows(row);
        row++;

        var grouped = entries
            .Where(e => e.JobId.HasValue)
            .GroupBy(e => (
                Job: e.JobId!.Value,
                Stage: e.JobStageId,
                User: (e.UserName ?? "").Trim()))
            .OrderByDescending(g =>
                lookups.ResolveJob(g.Key.Job).Code ?? "")
            .ThenBy(g => g.Key.User, StringComparer.OrdinalIgnoreCase);

        foreach (var g in grouped)
        {
            var (c, desc) = lookups.ResolveJob(g.Key.Job);
            ws.Cell(row, 1).Value = c;
            ws.Cell(row, 2).Value = desc;
            ws.Cell(row, 3).Value = lookups.ResolveStage(g.Key.Job, g.Key.Stage);
            ws.Cell(row, 4).Value = g.Key.User;
            ws.Cell(row, 5).Value = userNameToDisplay.GetValueOrDefault(g.Key.User, g.Key.User);
            ws.Cell(row, 6).Value = (double)g.Sum(e => e.Hours);
            ws.Cell(row, 6).Style.NumberFormat.Format = "0.0";
            row++;
        }

        BorderRange(ws.Range(3, 1, Math.Max(3, row - 1), 6));
    }

    private static void FillWeekEmployeeTotalsWorksheet(IXLWorksheet ws,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> display,
        WeekExcelLookups lookups)
    {
        var row = 1;
        ws.Cell(row, 1).Value = "Çalışan bazında haftalık özet";
        StyleTitleMerge(ws.Range(row, 1, row, 4));
        row += 2;

        ws.Cell(row, 1).Value = "Çalışan";
        ws.Cell(row, 2).Value = "Toplam saat";
        ws.Cell(row, 3).Value = "Tahmini tutar Σ*";
        ws.Cell(row, 4).Value = "Kayıt adedi";
        StyleHeader(ws.Range(row, 1, row, 4));
        ws.SheetView.FreezeRows(row);
        row++;

        ws.Cell(row, 1).Value =
            "* TRY/USD aynı hücrede toplanmış olabilir; kesin rakam için iş bazlı maliyet sayfasını kullanın.";
        ws.Range(row, 1, row, 4).Merge().Style.Font.Italic = true;
        ws.Range(row, 1, row, 4).Style.Font.FontColor = XLColor.FromHtml("#5A6978");
        row++;

        foreach (var g in entries.Where(e => !string.IsNullOrWhiteSpace(e.UserName))
                     .GroupBy(e => e.UserName!, StringComparer.OrdinalIgnoreCase))
        {
            var name = display.GetValueOrDefault(g.Key.Trim(), g.Key.Trim());
            var hrs = g.Sum(x => x.Hours);
            ws.Cell(row, 1).Value = name;
            ws.Cell(row, 2).Value = (double)hrs;
            ws.Cell(row, 2).Style.NumberFormat.Format = "0.0";

            decimal costSum = 0;
            foreach (var e in g)
            {
                if (!e.JobId.HasValue) continue;
                var rr = lookups.ParticipantHourly(e.UserName, e.JobId.Value, out _);
                if (rr.HasValue)
                    costSum += e.Hours * rr.Value;
            }

            if (Math.Abs(costSum) < 0.0001m)
                ws.Cell(row, 3).Value = "—";
            else
            {
                ws.Cell(row, 3).Value = (double)costSum;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            }
            ws.Cell(row, 4).Value = g.Count();
            row++;
        }
    }

    private static void AppendJobDefinitionWorksheet(XLWorkbook wb, JobDetail detail, string jobCode, string jobDescription)
    {
        var ws = wb.Worksheets.Add("İş planı");
        var r = 1;
        ws.Cell(r, 1).Value = "İş tanımı özeti (API ile uyumlu)";
        ws.Range(r, 1, r, 6).Merge();
        ws.Row(r).Style.Font.Bold = true;
        ws.Row(r).Style.Font.FontSize = 14;
        r++;
        ws.Cell(r, 1).Value = $"{jobCode} — {jobDescription}";
        ws.Range(r, 1, r, 6).Merge();
        r += 2;

        ws.Cell(r, 1).Value = "Aşamalar";
        ws.Row(r).Style.Font.Bold = true;
        r++;
        ws.Cell(r, 1).Value = "Sıra";
        ws.Cell(r, 2).Value = "Ad";
        ws.Cell(r, 3).Value = "Açıklama";
        ws.Row(r).Style.Font.Bold = true;
        r++;
        var orderedStages = detail.Stages.OrderBy(x => x.SortOrder).ToList();
        if (orderedStages.Count == 0)
        {
            ws.Cell(r, 2).Value = "— Kayıtlı aşama yok —";
            r++;
        }
        else
        {
            for (var i = 0; i < orderedStages.Count; i++)
            {
                ws.Cell(r, 1).Value = i + 1;
                ws.Cell(r, 2).Value = orderedStages[i].Name;
                ws.Cell(r, 3).Value = orderedStages[i].Description;
                r++;
            }
        }

        r++;
        ws.Cell(r, 1).Value = "Çalışan ücretleri (işe özel)";
        ws.Row(r).Style.Font.Bold = true;
        r++;
        ws.Cell(r, 1).Value = "Kullanıcı adı";
        ws.Cell(r, 2).Value = "Saatlik ücret";
        ws.Cell(r, 3).Value = "Para birimi";
        ws.Row(r).Style.Font.Bold = true;
        r++;
        if (detail.Participants.Count == 0)
        {
            ws.Cell(r, 2).Value = "— Tanımlı çalışan yok —";
            r++;
        }
        else
        {
            foreach (var p in detail.Participants.OrderBy(x => x.UserName, StringComparer.OrdinalIgnoreCase))
            {
                ws.Cell(r, 1).Value = p.UserName;
                ws.Cell(r, 2).Value = (double)p.HourlyRate;
                ws.Cell(r, 3).Value =
                    string.IsNullOrWhiteSpace(p.HourlyRateCurrency) ? "TRY" : p.HourlyRateCurrency.ToUpperInvariant();
                ws.Cell(r, 2).Style.NumberFormat.Format =
                    string.Equals(p.HourlyRateCurrency?.Trim(), "USD", StringComparison.OrdinalIgnoreCase)
                        ? "$#,##0.00"
                        : "#,##0.00 \"₺\"";
                r++;
            }
        }

        r++;
        ws.Cell(r, 1).Value = "Planlanan saatler (aşama × çalışan)";
        ws.Row(r).Style.Font.Bold = true;
        r++;
        ws.Cell(r, 1).Value = "Aşama";
        ws.Cell(r, 2).Value = "Kullanıcı";
        ws.Cell(r, 3).Value = "Plan saat";
        ws.Row(r).Style.Font.Bold = true;
        r++;

        static string StageNameAt(IReadOnlyList<JobStageItem> orderedStagesArg, int stageIndex)
        {
            if (stageIndex >= 0 && stageIndex < orderedStagesArg.Count)
                return orderedStagesArg[stageIndex].Name;
            return stageIndex >= 0 ? $"Aşama #{stageIndex + 1}" : "?";
        }

        var plans = detail.StagePlans.OrderBy(x => x.StageIndex).ThenBy(x => x.UserName).ToList();
        if (plans.Count == 0)
        {
            ws.Cell(r, 2).Value = "— Plan satırı yok —";
            r++;
        }
        else
        {
            foreach (var pl in plans)
            {
                ws.Cell(r, 1).Value = StageNameAt(orderedStages, pl.StageIndex);
                ws.Cell(r, 2).Value = pl.UserName;
                ws.Cell(r, 3).Value = (double)pl.PlannedHours;
                ws.Cell(r, 3).Style.NumberFormat.Format = "0.0";
                r++;
            }
        }

        ws.Columns().AdjustToContents();
    }
}
