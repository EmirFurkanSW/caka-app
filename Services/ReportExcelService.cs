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
        FillWeekDetailWorksheet(wb.Worksheets.Add("Weekly detail"),
            weekStart, weekEnd, entries, userNameToDisplayName, lookups);
        FillWeekPivotWorksheet(wb.Worksheets.Add("Job–stage–person"),
            entries, userNameToDisplayName, lookups);
        FillWeekEmployeeTotalsWorksheet(wb.Worksheets.Add("Person summary"),
            entries, userNameToDisplayName, lookups);
        var matrices = wb.Worksheets.Add("Job-based cost");
        var rowMat = 1;
        FillJobMatricesForLogs(matrices, ref rowMat, lookups, entries, userNameToDisplayName,
            $"Weekly period: {weekStart:dd.MM.yyyy} – {weekEnd:dd.MM.yyyy}");

        matrices.Columns().AdjustToContents();
        foreach (IXLWorksheet ws in wb.Worksheets.Where(w => w.Name != "Job-based cost"))
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

        var usersPre = ResolvePlannedColumnUsernames(columnUserNames, userNameToDisplayName, jobDetail);
        var lastColTitle = Math.Max(8, usersPre.Count + 2);

        var lookups = BuildLookupsFromJobDetail(jobDetail, jobCode, jobDescription);

        var wsPlanned = wb.Worksheets.Add("Cost & stages");
        wsPlanned.Cell(1, 1).Value = "Work cost & stage breakdown (planned budget)";
        StyleTitleMerge(wsPlanned.Range(1, 1, 1, lastColTitle));

        wsPlanned.Cell(2, 1).Value = $"{jobCode} — {jobDescription}";
        wsPlanned.Range(2, 1, 2, lastColTitle).Merge().Style.Fill.BackgroundColor = CardBg;

        wsPlanned.Cell(3, 1).Value =
            "Based on job definition only (planned hours × hourly rates). Logged work is not included.";
        wsPlanned.Range(3, 1, 3, lastColTitle).Merge();

        var rPlanned = 5;
        FillJobLandscapePerformanceSheet(wsPlanned, ref rPlanned, lookups, jobIdResolved, jobLogs,
            userNameToDisplayName, columnUserNames, JobExcelCostMode.PlannedBudget);

        wsPlanned.Columns().AdjustToContents();

        var wsActual = wb.Worksheets.Add("Planned vs actual");
        wsActual.Cell(1, 1).Value = "Work cost — planned vs actual";
        StyleTitleMerge(wsActual.Range(1, 1, 1, lastColTitle));

        wsActual.Cell(2, 1).Value = $"{jobCode} — {jobDescription}";
        wsActual.Range(2, 1, 2, lastColTitle).Merge().Style.Fill.BackgroundColor = CardBg;

        var periodNote = entries.Count > 0
            ? $"Logged work through: {periodStart:dd.MM.yyyy} – {periodEnd:dd.MM.yyyy}"
            : "No work logs recorded for this job yet.";
        wsActual.Cell(3, 1).Value = periodNote;
        wsActual.Range(3, 1, 3, lastColTitle).Merge();

        var rActual = 5;
        FillJobLandscapePerformanceSheet(wsActual, ref rActual, lookups, jobIdResolved, jobLogs,
            userNameToDisplayName, columnUserNames, JobExcelCostMode.PlannedVsActual);

        wsActual.Columns().AdjustToContents();

        wb.SaveAs(filePath);
    }

    /// <summary>Job-facing layout with people as columns, stage summary, Grand Total / discount / first offer.</summary>
    private static void FillJobLandscapePerformanceSheet(IXLWorksheet ws, ref int startRow,
        WeekExcelLookups lookups, Guid jobId, List<WorkLog> jobLogs,
        IReadOnlyDictionary<string, string> display,
        IReadOnlyList<string>? columnUserPreset,
        JobExcelCostMode mode)
    {
        lookups.JobDetails.TryGetValue(jobId, out var detail);
        var userCols = mode == JobExcelCostMode.PlannedBudget
            ? ResolvePlannedColumnUsernames(columnUserPreset, display, detail)
            : ResolvePlannedColumnUsernames(columnUserPreset, display, detail);
        var (code, jdesc) = lookups.ResolveJob(jobId);

        if (userCols.Count == 0)
        {
            ws.Cell(startRow, 1).Value = "No participants in job definition (assign employees on Job Management).";
            startRow += 3;
            return;
        }

        var lastCol = userCols.Count + 2;
        var includeActual = mode == JobExcelCostMode.PlannedVsActual;
        var plannedStageList = OrderedStagesForPlanned(detail);
        var actualStageList = includeActual ? OrderedStagesForJobPerformance(detail, jobLogs) : plannedStageList;

        ws.Cell(startRow, 1).Value = $"{code} — {jdesc}".Trim().TrimEnd('—', ' ');
        ws.Range(startRow, 1, startRow, lastCol).Merge();
        ws.Row(startRow).Style.Font.Bold = true;
        ws.Row(startRow).Style.Font.FontSize = 13;
        startRow++;

        ws.Cell(startRow, 1).Value = "";
        for (var i = 0; i < userCols.Count; i++)
            ws.Cell(startRow, i + 2).Value = display.GetValueOrDefault(userCols[i], userCols[i]);

        ws.Cell(startRow, lastCol).Value = "Total";
        StyleHeader(ws.Range(startRow, 1, startRow, lastCol));
        var headerExcelRow = startRow;
        startRow++;

        var matrixTopRow = startRow;

        // Hourly rates
        ws.Cell(startRow, 1).Value = "Hourly rate (job definition)";
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

            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Cell(startRow, lastCol).Clear();
        startRow++;

        // Planned hours
        var plannedHoursRow = startRow;
        WriteHoursMatrixRow(ws, ref startRow, lastCol, userCols, "Planned hours",
            u => detail != null ? SumPlannedHoursForUser(detail, u) : 0);

        int? actualHoursRow = null;
        int? hoursVarianceRow = null;
        if (includeActual)
        {
            actualHoursRow = startRow;
            WriteHoursMatrixRow(ws, ref startRow, lastCol, userCols, "Actual hours logged",
                u => SumHoursForUser(jobLogs, u, HourFilter.All));

            hoursVarianceRow = startRow;
            WriteVarianceHoursRow(ws, ref startRow, lastCol, userCols, plannedHoursRow, actualHoursRow.Value);
        }

        // Man-days
        var manDaysSourceRow = includeActual ? actualHoursRow!.Value : plannedHoursRow;
        ws.Cell(startRow, 1).Value = includeActual ? "Man-days — actual (hours÷8)" : "Man-days (hours÷8)";
        for (var i = 0; i < userCols.Count; i++)
        {
            ws.Cell(startRow, i + 2).FormulaA1 = $"{ColLetter(i + 2)}{manDaysSourceRow}/8";
            ws.Cell(startRow, i + 2).Style.NumberFormat.Format = "0.00";
            ws.Cell(startRow, i + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Cell(startRow, lastCol).FormulaA1 = $"{ColLetter(lastCol)}{manDaysSourceRow}/8";
        ws.Cell(startRow, lastCol).Style.NumberFormat.Format = "0.00";
        startRow++;

        if (includeActual)
        {
            ws.Cell(startRow, 1).Value = "Man-days — planned (hours÷8)";
            for (var i = 0; i < userCols.Count; i++)
            {
                ws.Cell(startRow, i + 2).FormulaA1 = $"{ColLetter(i + 2)}{plannedHoursRow}/8";
                ws.Cell(startRow, i + 2).Style.NumberFormat.Format = "0.00";
                ws.Cell(startRow, i + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            ws.Cell(startRow, lastCol).FormulaA1 = $"{ColLetter(lastCol)}{plannedHoursRow}/8";
            ws.Cell(startRow, lastCol).Style.NumberFormat.Format = "0.00";
            startRow++;
        }

        // Costs
        var plannedCostRow = startRow;
        WriteCostMatrixRow(ws, ref startRow, lastCol, userCols, jobId, lookups, "Planned cost",
            u => detail != null ? SumPlannedHoursForUser(detail, u) : 0);

        int? actualCostRow = null;
        int? costOverloadRow = null;
        if (includeActual)
        {
            actualCostRow = startRow;
            WriteCostMatrixRow(ws, ref startRow, lastCol, userCols, jobId, lookups, "Actual cost",
                u => SumHoursForUser(jobLogs, u, HourFilter.All));

            costOverloadRow = startRow;
            WriteCostVarianceRow(ws, ref startRow, lastCol, userCols, plannedCostRow, actualCostRow.Value,
                "Cost overload (actual − planned)");
        }

        ws.Row(startRow).Height = 8;
        startRow++;

        // Stage rows — planned
        foreach (var sid in plannedStageList)
        {
            var lbl = lookups.ResolveStageEnglish(jobId, sid);
            if (includeActual)
                lbl += " (planned)";
            WriteStageHoursRow(ws, ref startRow, lastCol, userCols, lbl, matrixTopRow,
                u => detail != null ? PlannedHoursFor(detail, sid, u) : 0);
        }

        // Stage rows — actual (sheet 2 only)
        if (includeActual)
        {
            foreach (var sid in actualStageList)
            {
                var lbl = sid == Guid.Empty
                    ? "Unassigned / general (actual)"
                    : lookups.ResolveStageEnglish(jobId, sid) + " (actual)";
                WriteStageHoursRow(ws, ref startRow, lastCol, userCols, lbl, matrixTopRow,
                    u => sid == Guid.Empty
                        ? SumHoursForUser(jobLogs, u, HourFilter.UnassignedStage, detail: detail)
                        : SumHoursForUser(jobLogs, u, HourFilter.ExactStage, sid, detail));
            }
        }

        BorderRange(ws.Range(matrixTopRow - 1, 1, startRow - 1, lastCol));
        ws.SheetView.FreezeRows(headerExcelRow);
        startRow++;

        // Stage cost summary — planned
        var summaryHeaderRow = startRow;
        WriteStageSummaryHeader(ws, ref startRow);
        var summaryFirstDataRow = startRow;
        FillStageSummaryData(ws, ref startRow, plannedStageList, userCols, jobId, lookups, detail, jobLogs,
            usePlannedHours: true);
        var plannedSummaryLastRow = startRow - 1;
        BorderRange(ws.Range(summaryHeaderRow, 1, plannedSummaryLastRow, 5));

        int? actualSummaryHeaderRow = null;
        int? actualSummaryFirstRow = null;
        int? actualSummaryLastRow = null;
        if (includeActual)
        {
            startRow += 2;
            actualSummaryHeaderRow = startRow;
            ws.Cell(startRow, 1).Value = "Stage summary — actual logged work";
            ws.Range(startRow, 1, startRow, 5).Merge();
            ws.Row(startRow).Style.Font.Bold = true;
            ws.Row(startRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF5");
            startRow++;

            WriteStageSummaryHeader(ws, ref startRow);
            actualSummaryFirstRow = startRow;
            FillStageSummaryData(ws, ref startRow, actualStageList, userCols, jobId, lookups, detail, jobLogs,
                usePlannedHours: false);
            actualSummaryLastRow = startRow - 1;
            BorderRange(ws.Range(actualSummaryHeaderRow!.Value + 1, 1, actualSummaryLastRow.Value, 5));
        }

        startRow += 2;

        // Grand total footer
        var grandHdrRow = startRow;
        if (includeActual)
        {
            ws.Cell(grandHdrRow, 1).Value = "Grand Total (planned G&A)";
            ws.Cell(grandHdrRow, 2).Value = "Grand Total (actual G&A)";
            ws.Cell(grandHdrRow, 3).Value = "Overload (actual − planned)";
            ws.Cell(grandHdrRow, 4).Value = "Discount Amount";
            ws.Cell(grandHdrRow, 5).Value = "First Offer (planned)";
            StyleHeader(ws.Range(grandHdrRow, 1, grandHdrRow, 5));
            var grandValRow = startRow + 1;
            ws.Cell(grandValRow, 1).FormulaA1 = $"SUM(E{summaryFirstDataRow}:E{plannedSummaryLastRow})";
            ws.Cell(grandValRow, 2).FormulaA1 =
                $"SUM(E{actualSummaryFirstRow}:E{actualSummaryLastRow})";
            ws.Cell(grandValRow, 3).FormulaA1 = $"{ColLetter(2)}{grandValRow}-{ColLetter(1)}{grandValRow}";
            ws.Cell(grandValRow, 4).Value = 0d;
            ws.Cell(grandValRow, 5).FormulaA1 = $"{ColLetter(1)}{grandValRow}-{ColLetter(4)}{grandValRow}";
            for (var c = 1; c <= 5; c++)
            {
                ws.Cell(grandValRow, c).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(grandValRow, c).Style.Font.FontColor = XLColor.FromHtml("#0563C1");
                ws.Cell(grandValRow, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            BorderRange(ws.Range(grandHdrRow, 1, grandValRow, 5));
            startRow = grandValRow + 2;
        }
        else
        {
            ws.Cell(grandHdrRow, 1).Value = "Grand Total";
            ws.Cell(grandHdrRow, 2).Value = "Discount Amount";
            ws.Cell(grandHdrRow, 3).Value = "First Offer";
            var hdrFooter = ws.Range(grandHdrRow, 1, grandHdrRow, 3);
            hdrFooter.Style.Font.Bold = true;
            hdrFooter.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
            hdrFooter.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            BorderRange(hdrFooter);

            var grandValRow = startRow + 1;
            ws.Cell(grandValRow, 1).FormulaA1 = $"SUM(E{summaryFirstDataRow}:E{plannedSummaryLastRow})";
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
    }

    private static List<string> ResolvePlannedColumnUsernames(
        IReadOnlyList<string>? presetOrdered,
        IReadOnlyDictionary<string, string> display,
        JobDetail? detail)
    {
        if (presetOrdered is { Count: > 0 })
        {
            return presetOrdered
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (detail?.Participants is { Count: > 0 })
        {
            return detail.Participants
                .Select(p => p.UserName.Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(u => display.GetValueOrDefault(u, u), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (detail?.StagePlans is { Count: > 0 })
        {
            return detail.StagePlans
                .Where(p => p.PlannedHours > 0 && !string.IsNullOrWhiteSpace(p.UserName))
                .Select(p => p.UserName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(u => display.GetValueOrDefault(u, u), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return new List<string>();
    }

    private static List<Guid> OrderedStagesForPlanned(JobDetail? detail)
    {
        if (detail?.Stages is not { Count: > 0 })
            return new List<Guid>();
        return OrderedStageItems(detail).Select(s => s.Id).ToList();
    }

    private static decimal SumPlannedHoursForUser(JobDetail detail, string username) =>
        OrderedStageItems(detail).Sum(st => PlannedHoursFor(detail, st.Id, username));

    private static void WriteHoursMatrixRow(IXLWorksheet ws, ref int row, int lastCol,
        IReadOnlyList<string> userCols, string label, Func<string, decimal> hoursForUser)
    {
        ws.Cell(row, 1).Value = label;
        for (var i = 0; i < userCols.Count; i++)
        {
            ws.Cell(row, i + 2).Value = (double)hoursForUser(userCols[i]);
            ws.Cell(row, i + 2).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, i + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Cell(row, lastCol).FormulaA1 = $"SUM({ColLetter(2)}{row}:{ColLetter(lastCol - 1)}{row})";
        ws.Cell(row, lastCol).Style.NumberFormat.Format = "0.0";
        row++;
    }

    private static void WriteVarianceHoursRow(IXLWorksheet ws, ref int row, int lastCol,
        IReadOnlyList<string> userCols, int plannedRow, int actualRow)
    {
        ws.Cell(row, 1).Value = "Hours variance (actual − planned)";
        ws.Row(row).Style.Font.Italic = true;
        for (var i = 0; i < userCols.Count; i++)
        {
            ws.Cell(row, i + 2).FormulaA1 = $"{ColLetter(i + 2)}{actualRow}-{ColLetter(i + 2)}{plannedRow}";
            ws.Cell(row, i + 2).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, i + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Cell(row, lastCol).FormulaA1 = $"{ColLetter(lastCol)}{actualRow}-{ColLetter(lastCol)}{plannedRow}";
        ws.Cell(row, lastCol).Style.NumberFormat.Format = "0.0";
        row++;
    }

    private static void WriteCostMatrixRow(IXLWorksheet ws, ref int row, int lastCol,
        IReadOnlyList<string> userCols, Guid jobId, WeekExcelLookups lookups, string label,
        Func<string, decimal> hoursForUser)
    {
        ws.Cell(row, 1).Value = label;
        for (var i = 0; i < userCols.Count; i++)
        {
            var hrs = hoursForUser(userCols[i]);
            var isUsd = false;
            decimal? rateOpt = jobId == Guid.Empty ? null : lookups.ParticipantHourly(userCols[i], jobId, out isUsd);
            var cell = ws.Cell(row, i + 2);
            if (rateOpt.HasValue)
            {
                cell.Value = (double)(hrs * rateOpt.Value);
                cell.Style.NumberFormat.Format = isUsd ? "$#,##0.00" : "#,##0.00 \"₺\"";
            }
            else
                cell.Value = 0d;

            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Cell(row, lastCol).FormulaA1 = $"SUM({ColLetter(2)}{row}:{ColLetter(lastCol - 1)}{row})";
        ws.Cell(row, lastCol).Style.NumberFormat.Format = "#,##0.00";
        row++;
    }

    private static void WriteCostVarianceRow(IXLWorksheet ws, ref int row, int lastCol,
        IReadOnlyList<string> userCols, int plannedCostRow, int actualCostRow, string label)
    {
        ws.Cell(row, 1).Value = label;
        ws.Row(row).Style.Font.Bold = true;
        for (var i = 0; i < userCols.Count; i++)
        {
            ws.Cell(row, i + 2).FormulaA1 = $"{ColLetter(i + 2)}{actualCostRow}-{ColLetter(i + 2)}{plannedCostRow}";
            ws.Cell(row, i + 2).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, i + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Cell(row, lastCol).FormulaA1 = $"{ColLetter(lastCol)}{actualCostRow}-{ColLetter(lastCol)}{plannedCostRow}";
        ws.Cell(row, lastCol).Style.NumberFormat.Format = "#,##0.00";
        row++;
    }

    private static void WriteStageHoursRow(IXLWorksheet ws, ref int row, int lastCol,
        IReadOnlyList<string> userCols, string label, int matrixTopRow, Func<string, decimal> hoursForUser)
    {
        ws.Cell(row, 1).Value = label;
        for (var i = 0; i < userCols.Count; i++)
        {
            ws.Cell(row, i + 2).Value = (double)hoursForUser(userCols[i]);
            ws.Cell(row, i + 2).Style.NumberFormat.Format = "0.0";
            ws.Cell(row, i + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            if ((row - matrixTopRow) % 2 == 1)
                ws.Cell(row, i + 2).Style.Fill.BackgroundColor = StripSub;
        }

        ws.Cell(row, lastCol).FormulaA1 = $"SUM({ColLetter(2)}{row}:{ColLetter(lastCol - 1)}{row})";
        ws.Cell(row, lastCol).Style.NumberFormat.Format = "0.0";
        if ((row - matrixTopRow) % 2 == 1)
            ws.Cell(row, lastCol).Style.Fill.BackgroundColor = StripSub;
        row++;
    }

    private static void WriteStageSummaryHeader(IXLWorksheet ws, ref int row)
    {
        ws.Cell(row, 1).Value = "Stage and Description";
        ws.Range(row, 1, row, 5).Style.Font.Bold = true;
        ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#5A5A5A");
        ws.Range(row, 1, row, 5).Style.Font.FontColor = XLColor.White;
        ws.Cell(row, 2).Value = "Total";
        ws.Cell(row, 3).Value = "Total Hours";
        ws.Cell(row, 4).Value = "Price With Contingency (5%)";
        ws.Cell(row, 5).Value = "Price With G&A Cost (5%)";
        BorderRange(ws.Range(row, 1, row, 5));
        row++;
    }

    private static void FillStageSummaryData(IXLWorksheet ws, ref int row,
        IReadOnlyList<Guid> stageList, IReadOnlyList<string> userCols, Guid jobId,
        WeekExcelLookups lookups, JobDetail? detail, List<WorkLog> jobLogs, bool usePlannedHours)
    {
        if (stageList.Count == 0)
        {
            ws.Cell(row, 1).Value = "— No stages defined —";
            for (var c = 2; c <= 5; c++)
                ws.Cell(row, c).Value = 0d;
            row++;
            return;
        }

        foreach (var sid in stageList)
        {
            var lbl = sid == Guid.Empty
                ? "Unassigned / general"
                : lookups.ResolveStageEnglish(jobId, sid);
            decimal raw = 0;
            decimal hrs = 0;
            foreach (var u in userCols)
            {
                decimal uh;
                if (usePlannedHours)
                    uh = detail != null ? PlannedHoursFor(detail, sid, u) : 0;
                else
                    uh = sid == Guid.Empty
                        ? SumHoursForUser(jobLogs, u, HourFilter.UnassignedStage, detail: detail)
                        : SumHoursForUser(jobLogs, u, HourFilter.ExactStage, sid, detail);

                hrs += uh;
                var rateOpt = jobId == Guid.Empty ? null : lookups.ParticipantHourly(u, jobId, out _);
                if (rateOpt.HasValue)
                    raw += uh * rateOpt.Value;
            }

            var cont = raw * (decimal)KontenjanCarpani;
            var ga = cont * (decimal)GenelGiderCarpani;

            ws.Cell(row, 1).Value = lbl;
            ws.Cell(row, 2).Value = (double)raw;
            ws.Cell(row, 3).Value = (double)hrs;
            ws.Cell(row, 4).Value = (double)cont;
            ws.Cell(row, 5).Value = (double)ga;
            for (var c = 2; c <= 5; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.0";
            row++;
        }
    }

    private static List<string> ResolveColumnUsernames(
        IReadOnlyList<string>? presetOrdered,
        IReadOnlyDictionary<string, string> display,
        List<WorkLog> jobLogs,
        JobDetail? jobDetail = null)
    {
        if (presetOrdered is { Count: > 0 })
        {
            return presetOrdered
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return ResolveJobReportUsernames(jobDetail, jobLogs, display);
    }

    /// <summary>İş tanımındaki çalışanlar + dönemde kaydı olanlar (tüm sistem kullanıcıları değil).</summary>
    private static List<string> ResolveJobReportUsernames(
        JobDetail? detail,
        List<WorkLog> jobLogs,
        IReadOnlyDictionary<string, string> display)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (detail?.Participants is { Count: > 0 })
        {
            foreach (var p in detail.Participants)
            {
                if (!string.IsNullOrWhiteSpace(p.UserName))
                    set.Add(p.UserName.Trim());
            }
        }

        foreach (var l in jobLogs)
        {
            if (!string.IsNullOrWhiteSpace(l.UserName))
                set.Add(l.UserName!.Trim());
        }

        if (set.Count == 0 && detail != null)
            return UsersFromPlansAndLogs(detail, jobLogs).OrderBy(u => display.GetValueOrDefault(u, u), StringComparer.OrdinalIgnoreCase).ToList();

        return set.OrderBy(u => display.GetValueOrDefault(u, u), StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Job definition stages; unassigned row last when any log could not be mapped to a stage.</summary>
    private static List<Guid> OrderedStagesForJobPerformance(JobDetail? detail, List<WorkLog> jobLogs)
    {
        var ordered = new List<Guid>();

        if (detail?.Stages is { Count: > 0 })
        {
            foreach (var s in OrderedStageItems(detail))
                ordered.Add(s.Id);
        }

        if (jobLogs.Any(l => ResolveLogStageId(l, detail) == Guid.Empty))
            ordered.Add(Guid.Empty);

        return ordered;
    }

    private static List<JobStageItem> OrderedStageItems(JobDetail detail) =>
        detail.Stages.OrderBy(x => x.SortOrder).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

    private static bool IsJobParticipant(JobDetail detail, string username)
    {
        if (detail.Participants.Count == 0)
            return true;
        return detail.Participants.Any(p => SameUser(p.UserName, username));
    }

    private static decimal PlannedHoursFor(JobDetail detail, Guid stageId, string username)
    {
        if (stageId == Guid.Empty || string.IsNullOrWhiteSpace(username))
            return 0;
        if (!IsJobParticipant(detail, username))
            return 0;

        var byStageId = detail.StagePlans
            .Where(p => SameUser(p.UserName, username))
            .Where(p => p.StageId.HasValue && p.StageId.Value == stageId)
            .Sum(p => p.PlannedHours);
        if (detail.StagePlans.Any(p =>
                SameUser(p.UserName, username) && p.StageId.HasValue && p.StageId.Value == stageId))
            return byStageId;

        var ordered = OrderedStageItems(detail);
        return detail.StagePlans
            .Where(p => SameUser(p.UserName, username))
            .Where(p => p.StageIndex >= 0 && p.StageIndex < ordered.Count && ordered[p.StageIndex].Id == stageId)
            .Sum(p => p.PlannedHours);
    }

    private static HashSet<string> UsersFromPlansAndLogs(JobDetail? detail, List<WorkLog> jobLogs)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (detail?.Participants is { Count: > 0 })
        {
            foreach (var p in detail.Participants)
            {
                if (!string.IsNullOrWhiteSpace(p.UserName))
                    set.Add(p.UserName.Trim());
            }
        }
        else if (detail?.StagePlans != null)
        {
            foreach (var p in detail.StagePlans.Where(x => x.PlannedHours > 0))
            {
                if (!string.IsNullOrWhiteSpace(p.UserName))
                    set.Add(p.UserName.Trim());
            }
        }

        foreach (var l in jobLogs)
        {
            if (!string.IsNullOrWhiteSpace(l.UserName))
                set.Add(l.UserName.Trim());
        }

        return set;
    }

    private static void AppendEmployeePerformanceWorksheet(
        XLWorkbook wb,
        WeekExcelLookups lookups,
        Guid jobId,
        JobDetail? jobDetail,
        List<WorkLog> jobLogs,
        IReadOnlyDictionary<string, string> display,
        DateTime periodStart,
        DateTime periodEnd,
        string jobCode,
        string jobDescription,
        int mergeCols)
    {
        var ws = wb.Worksheets.Add("Employee performance");
        var row = 1;
        ws.Cell(row, 1).Value = "Employee performance (planned vs actual)";
        StyleTitleMerge(ws.Range(row, 1, row, mergeCols));

        row++;
        ws.Cell(row, 1).Value = $"{jobCode} — {jobDescription}";
        ws.Range(row, 1, row, mergeCols).Merge().Style.Fill.BackgroundColor = CardBg;

        row++;
        ws.Cell(row, 1).Value =
            $"Records date range: {periodStart:dd.MM.yyyy} – {periodEnd:dd.MM.yyyy}";
        ws.Range(row, 1, row, mergeCols).Merge();

        row += 2;
        if (jobDetail == null)
        {
            ws.Cell(row, 1).Value =
                "Job definition was not loaded; planned hours are unavailable. Only actual hours from logs are shown.";
            ws.Range(row, 1, row, mergeCols).Merge().Style.Font.Italic = true;
            row += 2;
        }

        var orderedStages = OrderedStagesForJobPerformance(jobDetail, jobLogs);
        var users = ResolveJobReportUsernames(jobDetail, jobLogs, display);

        var plannedActualRows = new List<(Guid StageId, string UserName, decimal Planned, decimal Actual)>();
        foreach (var sid in orderedStages)
        {
            foreach (var u in users)
            {
                var planned = jobDetail != null ? PlannedHoursFor(jobDetail, sid, u) : 0;
                var actual = sid == Guid.Empty
                    ? SumHoursForUser(jobLogs, u, HourFilter.UnassignedStage, detail: jobDetail)
                    : SumHoursForUser(jobLogs, u, HourFilter.ExactStage, sid, jobDetail);
                if (planned == 0 && actual == 0)
                    continue;
                plannedActualRows.Add((sid, u, planned, actual));
            }
        }

        var stageOrdinal = orderedStages.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);

        plannedActualRows = plannedActualRows
            .OrderBy(x => display.GetValueOrDefault(x.UserName, x.UserName), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => stageOrdinal.TryGetValue(x.StageId, out var ix) ? ix : int.MaxValue)
            .ToList();

        var headerRowNum = row;
        ws.Cell(row, 1).Value = "Stage";
        ws.Cell(row, 2).Value = "Employee";
        ws.Cell(row, 3).Value = "Planned hours";
        ws.Cell(row, 4).Value = "Actual hours";
        ws.Cell(row, 5).Value = "Variance (actual − planned)";
        ws.Cell(row, 6).Value = "% vs planned";
        StyleHeader(ws.Range(row, 1, row, 6));
        row++;

        if (plannedActualRows.Count == 0)
        {
            ws.Cell(row, 1).Value =
                "— No planned rows and no logged hours for this period (per stage × employee). —";
            ws.Range(row, 1, row, mergeCols).Merge().Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
            row++;
        }
        else
        {
            var dataFirstRowNum = row;
            foreach (var pr in plannedActualRows)
            {
                var lbl = pr.StageId == Guid.Empty
                    ? "Unassigned / general"
                    : lookups.ResolveStageEnglish(jobId, pr.StageId);

                ws.Cell(row, 1).Value = lbl;
                ws.Cell(row, 2).Value = display.GetValueOrDefault(pr.UserName, pr.UserName);

                if (jobDetail != null)
                {
                    ws.Cell(row, 3).Value = (double)pr.Planned;
                    ws.Cell(row, 3).Style.NumberFormat.Format = "0.0";
                }
                else
                {
                    ws.Cell(row, 3).Value = "—";
                }

                ws.Cell(row, 4).Value = (double)pr.Actual;

                if (jobDetail != null)
                {
                    ws.Cell(row, 5).Value = (double)(pr.Actual - pr.Planned);
                    ws.Cell(row, 5).Style.NumberFormat.Format = "0.0";
                }
                else
                    ws.Cell(row, 5).Value = "—";

                ws.Cell(row, 4).Style.NumberFormat.Format = "0.0";
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                if (jobDetail != null && pr.Planned > 0)
                {
                    var ratio = (double)((pr.Actual - pr.Planned) / pr.Planned);
                    ws.Cell(row, 6).Value = ratio;
                    ws.Cell(row, 6).Style.NumberFormat.Format = "0.0%";
                }
                else
                    ws.Cell(row, 6).Value = "—";

                if ((row - dataFirstRowNum) % 2 == 1)
                    ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = StripSub;

                row++;
            }

            var dataLastRowNum = row - 1;

            ws.Cell(row, 1).Value = "Total";
            ws.Cell(row, 1).Style.Font.Bold = true;
            if (jobDetail != null)
            {
                ws.Cell(row, 3).FormulaA1 = $"SUM(C{dataFirstRowNum}:C{dataLastRowNum})";
                ws.Cell(row, 5).FormulaA1 = $"SUM(E{dataFirstRowNum}:E{dataLastRowNum})";
                ws.Cell(row, 3).Style.NumberFormat.Format = "0.0";
                ws.Cell(row, 5).Style.NumberFormat.Format = "0.0";
            }
            else
            {
                ws.Cell(row, 3).Value = "—";
                ws.Cell(row, 5).Value = "—";
            }

            ws.Cell(row, 4).FormulaA1 = $"SUM(D{dataFirstRowNum}:D{dataLastRowNum})";
            ws.Cell(row, 4).Style.NumberFormat.Format = "0.0";

            ws.Cell(row, 6).Value = "—";
            ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = CardBg;

            BorderRange(ws.Range(headerRowNum, 1, row, 6));
            row++;

            ws.Cell(row, 1).Value =
                "Variance: negative = finished faster than planned; positive = more hours logged than planned.";
            ws.Range(row, 1, row, mergeCols).Merge().Style.Font.Italic = true;
            ws.Range(row, 1, row, mergeCols).Style.Font.FontColor = XLColor.FromHtml("#5A6978");
            row++;
        }

        ws.SheetView.FreezeRows(headerRowNum);
        ws.Columns().AdjustToContents();
    }

    private enum HourFilter { All, UnassignedStage, ExactStage }

    /// <summary>Sayfa 1: yalnızca iş tanımındaki plan; Sayfa 2: plan + gerçekleşen karşılaştırması.</summary>
    private enum JobExcelCostMode { PlannedBudget, PlannedVsActual }

    private static decimal SumHoursForUser(List<WorkLog> logs, string userColumn, HourFilter mode,
        Guid stageId = default, JobDetail? detail = null)
    {
        var q = logs.Where(l => SameUser(l.UserName, userColumn));
        if (detail is { Stages.Count: > 0 } && mode is HourFilter.UnassignedStage or HourFilter.ExactStage)
        {
            return mode switch
            {
                HourFilter.UnassignedStage => q.Where(l => ResolveLogStageId(l, detail) == Guid.Empty)
                    .Sum(l => l.Hours),
                HourFilter.ExactStage => q.Where(l => ResolveLogStageId(l, detail) == stageId).Sum(l => l.Hours),
                _ => 0
            };
        }

        return mode switch
        {
            HourFilter.All => q.Sum(l => l.Hours),
            HourFilter.UnassignedStage => q.Where(l => l.JobStageId == null || l.JobStageId == Guid.Empty)
                .Sum(l => l.Hours),
            HourFilter.ExactStage => q.Where(l => l.JobStageId == stageId).Sum(l => l.Hours),
            _ => 0
        };
    }

    /// <summary>
    /// Gerçek aşama: JobStageId; yoksa kayıt metninden (API: "kod - açıklama · Stage 1"); tek aşamalı işte otomatik eşleme.
    /// </summary>
    private static Guid ResolveLogStageId(WorkLog log, JobDetail? detail)
    {
        if (detail is not { Stages.Count: > 0 })
            return Guid.Empty;

        if (log.JobId.HasValue && log.JobId.Value != detail.Id)
            return Guid.Empty;

        var ordered = OrderedStageItems(detail);

        if (log.JobStageId is Guid rawId && rawId != Guid.Empty)
        {
            if (ordered.Any(s => s.Id == rawId))
                return rawId;
        }

        var token = ExtractStageTokenFromDescription(log.Description);
        if (!string.IsNullOrWhiteSpace(token))
        {
            for (var i = 0; i < ordered.Count; i++)
            {
                var s = ordered[i];
                var orderNum = i + 1;
                var name = string.IsNullOrWhiteSpace(s.Name) ? $"Stage {orderNum}" : s.Name.Trim();
                var label = StagePickerLabel(s, orderNum);
                var descOnly = (s.Description ?? "").Trim();

                if (token.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    token.Equals(label, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(descOnly) &&
                     token.Equals(descOnly, StringComparison.OrdinalIgnoreCase)))
                    return s.Id;
            }
        }

        if (ordered.Count == 1)
            return ordered[0].Id;

        return Guid.Empty;
    }

    private static string StagePickerLabel(JobStageItem s, int orderNum)
    {
        var name = string.IsNullOrWhiteSpace(s.Name) ? $"Stage {orderNum}" : s.Name.Trim();
        var desc = (s.Description ?? "").Trim();
        return string.IsNullOrEmpty(desc) ? name : $"{name} - {desc}";
    }

    private static string? ExtractStageTokenFromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var text = description.Trim();
        var dot = text.LastIndexOf('·');
        if (dot >= 0 && dot < text.Length - 1)
            return text[(dot + 1)..].Trim();

        var pipe = text.LastIndexOf('|');
        if (pipe >= 0 && pipe < text.Length - 1)
            return text[(pipe + 1)..].Trim();

        return null;
    }

    private static bool SameUser(string? logUser, string colUser) =>
        string.Equals(logUser?.Trim(), colUser, StringComparison.OrdinalIgnoreCase);

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
                "No work logs linked to a job (JobId) in this period. See the Weekly detail sheet for legacy entries.";
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
            ws.Cell(startRow, 1).Value += " — no work logs under a username.";
            startRow += 2;
            return;
        }

        var lastCol = userCols.Count + 2;
        ws.Range(startRow, 1, startRow, lastCol).Merge();
        ws.Row(startRow).Style.Font.Bold = true;
        ws.Row(startRow).Style.Font.FontSize = 13;
        startRow++;

        var stages = OrderedStagesForJobPerformance(detail, jobLogs);

        ws.Cell(startRow, 1).Value = "";
        for (var i = 0; i < userCols.Count; i++)
            ws.Cell(startRow, i + 2).Value = userNameToDisplay.GetValueOrDefault(userCols[i], userCols[i]);

        ws.Cell(startRow, lastCol).Value = "Total";
        StyleHeader(ws.Range(startRow, 1, startRow, lastCol));
        startRow++;

        var matrixTopRow = startRow;

        ws.Cell(startRow, 1).Value = "Hourly rate (job definition)";
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
                cell.Value = "(not defined)";

            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        startRow++;

        ws.Cell(startRow, 1).Value = "Total hours";
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
        var blockTotalHoursExcelRow = startRow;
        startRow++;

        ws.Cell(startRow, 1).Value = "Man-days (hours÷8)";
        for (var i = 0; i < userCols.Count; i++)
        {
            ws.Cell(startRow, i + 2).FormulaA1 =
                $"{ColLetter(i + 2)}{blockTotalHoursExcelRow}/8";
            ws.Cell(startRow, i + 2).Style.NumberFormat.Format = "0.00";
            ws.Cell(startRow, i + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Cell(startRow, lastCol).FormulaA1 =
            $"{ColLetter(lastCol)}{blockTotalHoursExcelRow}/8";
        ws.Cell(startRow, lastCol).Style.NumberFormat.Format = "0.00";
        startRow++;

        ws.Cell(startRow, 1).Value = "Total cost";
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
                ? "Unassigned / general"
                : lookups.ResolveStageEnglish(jobId, sid);

            ws.Cell(startRow, 1).Value = lbl;
            for (var i = 0; i < userCols.Count; i++)
            {
                var h = sid == Guid.Empty
                    ? SumHoursForUser(jobLogs, userCols[i], HourFilter.UnassignedStage, detail: detail)
                    : SumHoursForUser(jobLogs, userCols[i], HourFilter.ExactStage, sid, detail);
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
        ws.Cell(startRow, 1).Value = "Stage summary (cost + margin)";
        ws.Cell(startRow, 1).Style.Font.Bold = true;
        startRow++;

        ws.Cell(startRow, 1).Value = "Stage";
        ws.Cell(startRow, 2).Value = "Raw cost (Σ)";
        ws.Cell(startRow, 3).Value = "Total hours";
        ws.Cell(startRow, 4).Value = "After contingency (×1.05)";
        ws.Cell(startRow, 5).Value = "After G&A (×1.05)";
        StyleHeader(ws.Range(startRow, 1, startRow, 5));
        var summaryFirstRow = startRow + 1;
        startRow++;

        foreach (var sid in stages)
        {
            var lbl = sid == Guid.Empty
                ? "Unassigned / general"
                : lookups.ResolveStageEnglish(jobId, sid);

            decimal raw = 0;
            decimal hrs = 0;
            foreach (var u in userCols)
            {
                var uh = sid == Guid.Empty
                    ? SumHoursForUser(jobLogs, u, HourFilter.UnassignedStage, detail: detail)
                    : SumHoursForUser(jobLogs, u, HourFilter.ExactStage, sid, detail);

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
        ws.Cell(startRow, 1).Value = "Grand total";
        ws.Cell(startRow, 2).FormulaA1 = $"SUM(B{summaryFirstRow}:B{summaryLastRow})";
        ws.Cell(startRow, 3).FormulaA1 = $"SUM(C{summaryFirstRow}:C{summaryLastRow})";
        ws.Cell(startRow, 4).FormulaA1 = $"SUM(D{summaryFirstRow}:D{summaryLastRow})";
        ws.Cell(startRow, 5).FormulaA1 = $"SUM(E{summaryFirstRow}:E{summaryLastRow})";
        ws.Range(startRow, 1, startRow, 5).Style.Font.Bold = true;
        ws.Range(startRow, 1, startRow, 5).Style.Fill.BackgroundColor = CardBg;
        var grandGaRowNum = startRow;
        BorderRange(ws.Range(summaryHdr - 1, 1, startRow, 5));

        startRow += 2;
        ws.Cell(startRow, 1).Value = "Discount amount:";
        ws.Cell(startRow, 2).Value = 0;
        ws.Cell(startRow, 2).Style.NumberFormat.Format = "#,##0.00";
        var discountRowNum = startRow;
        BorderRange(ws.Range(startRow, 1, startRow, 2));
        startRow++;

        ws.Cell(startRow, 1).Value = "First offer (G&A − discount):";
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
        ws.Cell(row, 1).Value = $"Weekly work logs: {weekStart:dd.MM.yyyy} — {weekEnd:dd.MM.yyyy}";
        StyleTitleMerge(ws.Range(row, 1, row, 12));
        row += 2;

        ws.Cell(row, 1).Value = "Date";
        ws.Cell(row, 2).Value = "Day";
        ws.Cell(row, 3).Value = "Job code";
        ws.Cell(row, 4).Value = "Job description";
        ws.Cell(row, 5).Value = "Stage";
        ws.Cell(row, 6).Value = "Log text";
        ws.Cell(row, 7).Value = "Username";
        ws.Cell(row, 8).Value = "Full name";
        ws.Cell(row, 9).Value = "Hourly rate";
        ws.Cell(row, 10).Value = "CCY";
        ws.Cell(row, 11).Value = "Est. amount";
        ws.Cell(row, 12).Value = "Hours";
        StyleHeader(ws.Range(row, 1, row, 12));
        ws.SheetView.FreezeRows(row);
        row++;

        foreach (var log in entries.OrderBy(e => userNameToDisplayName.GetValueOrDefault(e.UserName ?? "", e.UserName ?? ""))
                     .ThenBy(e => e.Date).ThenBy(e => e.CreatedAt))
        {
            string code = "", jdesc = "", stageLbl = "";

            ws.Cell(row, 1).Value = log.Date;
            ws.Cell(row, 1).Style.DateFormat.Format = "dd.MM.yyyy";
            ws.Cell(row, 2).Value = log.Date.ToString("dddd", new CultureInfo("en-US"));
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
                stageLbl = lookups.ResolveStageEnglish(jid, log.JobStageId);
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
                ws.Cell(row, 3).Value = "(legacy / no job selected)";
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
        ws.Cell(row, 1).Value = "Job × stage × person — hours pivot";
        StyleTitleMerge(ws.Range(row, 1, row, 6));
        row += 2;

        ws.Cell(row, 1).Value = "Job code";
        ws.Cell(row, 2).Value = "Job description";
        ws.Cell(row, 3).Value = "Stage";
        ws.Cell(row, 4).Value = "Username";
        ws.Cell(row, 5).Value = "Person";
        ws.Cell(row, 6).Value = "Hours";
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
            ws.Cell(row, 3).Value = lookups.ResolveStageEnglish(g.Key.Job, g.Key.Stage);
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
        ws.Cell(row, 1).Value = "Weekly person summary";
        StyleTitleMerge(ws.Range(row, 1, row, 4));
        row += 2;

        ws.Cell(row, 1).Value = "Person";
        ws.Cell(row, 2).Value = "Total hours";
        ws.Cell(row, 3).Value = "Est. amount Σ*";
        ws.Cell(row, 4).Value = "Log count";
        StyleHeader(ws.Range(row, 1, row, 4));
        ws.SheetView.FreezeRows(row);
        row++;

        ws.Cell(row, 1).Value =
            "* TRY and USD amounts may be mixed in one cell; use the Job-based cost sheet for currency-specific totals.";
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

    /// <summary>One-page executive view: catalogue of stages, rates, rollup planned vs actual, then per-person tables.</summary>
    private static void AppendProjectOverviewWorksheet(
        XLWorkbook wb,
        WeekExcelLookups lookups,
        Guid jobId,
        JobDetail detail,
        List<WorkLog> jobLogs,
        IReadOnlyDictionary<string, string> display,
        DateTime periodStart,
        DateTime periodEnd,
        string jobCode,
        string jobDescription,
        int mergeCols)
    {
        const int KpiCols = 5;

        static string VarianceComment(decimal variance, decimal planned, decimal actual)
        {
            if (Math.Abs(planned) < 0.0001m && Math.Abs(actual) < 0.0001m)
                return "—";
            if (Math.Abs(planned) < 0.0001m && actual > 0)
                return "Time logged (no planned baseline in job plan)";
            if (variance <= -0.05m)
                return "Under plan (less effort than budgeted)";
            if (variance >= 0.05m)
                return "Over plan (more effort than budgeted)";
            return "On plan (≈)";
        }

        var ws = wb.Worksheets.Add("Project overview");
        var r = 1;
        ws.Cell(r, 1).Value = "Project overview";
        StyleTitleMerge(ws.Range(r, 1, r, mergeCols));
        r++;
        ws.Cell(r, 1).Value = $"{jobCode} — {jobDescription}";
        ws.Range(r, 1, r, mergeCols).Merge().Style.Fill.BackgroundColor = CardBg;
        r++;
        ws.Cell(r, 1).Value =
            $"Report window (logged work): {periodStart:dd.MM.yyyy} – {periodEnd:dd.MM.yyyy}";
        ws.Range(r, 1, r, mergeCols).Merge();
        r += 2;

        var orderedStages = OrderedStageItems(detail);
        var allUsers = AllUsernamesForOverview(detail, jobLogs)
            .OrderBy(u => display.GetValueOrDefault(u, u), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalPlannedInPlan = detail.StagePlans
            .Where(p => IsJobParticipant(detail, p.UserName))
            .Sum(p => p.PlannedHours);
        var totalActualInWindow = jobLogs.Sum(l => l.Hours);
        var netVariance = totalActualInWindow - totalPlannedInPlan;

        ws.Cell(r, 1).Value =
            $"Summary: {totalPlannedInPlan:0.0} h total planned in the job definition " +
            $"vs {totalActualInWindow:0.0} h actually logged in the date range above " +
            $"(net {(netVariance >= 0 ? "+" : "")}{netVariance:0.0} h). " +
            "Per-stage and per-person tables below reconcile to the same rules as the “Employee performance” sheet.";
        ws.Range(r, 1, r, mergeCols).Merge();
        ws.Range(r, 1, r, mergeCols).Style.Font.Italic = true;
        ws.Range(r, 1, r, mergeCols).Style.Font.FontColor = XLColor.FromHtml("#5A6978");
        r += 2;

        void SectionBanner(string title)
        {
            ws.Cell(r, 1).Value = title;
            ws.Range(r, 1, r, mergeCols).Merge();
            ws.Row(r).Style.Font.Bold = true;
            ws.Row(r).Style.Font.FontSize = 12;
            ws.Row(r).Style.Fill.BackgroundColor = CardBg;
            r++;
        }

        SectionBanner("1. Stages (job definition)");
        var stageHdr = r;
        ws.Cell(r, 1).Value = "#";
        ws.Cell(r, 2).Value = "Stage";
        ws.Cell(r, 3).Value = "Description";
        StyleHeader(ws.Range(r, 1, r, 3));
        r++;
        if (orderedStages.Count == 0)
        {
            ws.Cell(r, 1).Value = "— No stages configured —";
            ws.Range(r, 1, r, 3).Merge();
            r++;
        }
        else
        {
            for (var i = 0; i < orderedStages.Count; i++)
            {
                ws.Cell(r, 1).Value = i + 1;
                ws.Cell(r, 2).Value = orderedStages[i].Name;
                ws.Cell(r, 3).Value = orderedStages[i].Description;
                if ((r - stageHdr) % 2 == 0)
                    ws.Range(r, 1, r, 3).Style.Fill.BackgroundColor = StripSub;
                r++;
            }
        }

        BorderRange(ws.Range(stageHdr, 1, r - 1, 3));
        r++;
        ws.Row(r).Height = 6;
        r++;

        SectionBanner("2. Participant billing rates");
        var rateHdr = r;
        ws.Cell(r, 1).Value = "Username";
        ws.Cell(r, 2).Value = "Person";
        ws.Cell(r, 3).Value = "Hourly rate";
        ws.Cell(r, 4).Value = "CCY";
        StyleHeader(ws.Range(r, 1, r, 4));
        r++;
        if (detail.Participants.Count == 0)
        {
            ws.Cell(r, 1).Value = "— No participants on file —";
            ws.Range(r, 1, r, 4).Merge();
            r++;
        }
        else
        {
            foreach (var p in detail.Participants.OrderBy(x => x.UserName, StringComparer.OrdinalIgnoreCase))
            {
                ws.Cell(r, 1).Value = p.UserName;
                ws.Cell(r, 2).Value = display.GetValueOrDefault(p.UserName, p.UserName);
                ws.Cell(r, 3).Value = (double)p.HourlyRate;
                var ccy =
                    string.IsNullOrWhiteSpace(p.HourlyRateCurrency)
                        ? "TRY"
                        : p.HourlyRateCurrency.Trim().ToUpperInvariant();
                ws.Cell(r, 4).Value = ccy;
                ws.Cell(r, 3).Style.NumberFormat.Format =
                    string.Equals(ccy, "USD", StringComparison.OrdinalIgnoreCase)
                        ? "$#,##0.00"
                        : "#,##0.00 \"₺\"";
                if ((r - rateHdr) % 2 == 0)
                    ws.Range(r, 1, r, 4).Style.Fill.BackgroundColor = StripSub;
                r++;
            }
        }

        BorderRange(ws.Range(rateHdr, 1, r - 1, 4));
        r++;
        ws.Row(r).Height = 6;
        r++;

        SectionBanner("3. Stage rollup — planned vs actual (everyone combined)");
        var rollHdr = r;
        ws.Cell(r, 1).Value = "Stage";
        ws.Cell(r, 2).Value = "Planned h (Σ)";
        ws.Cell(r, 3).Value = "Actual h (Σ)";
        ws.Cell(r, 4).Value = "Variance";
        ws.Cell(r, 5).Value = "Comment";
        StyleHeader(ws.Range(r, 1, r, KpiCols));
        r++;
        var rollFirstData = r;

        var idxRoll = 0;
        foreach (var st in orderedStages)
        {
            var pSum = allUsers.Sum(user => PlannedHoursFor(detail, st.Id, user));
            var aSum = allUsers.Sum(user =>
                SumHoursForUser(jobLogs, user, HourFilter.ExactStage, st.Id, detail));
            var variance = aSum - pSum;

            ws.Cell(r, 1).Value = lookups.ResolveStageEnglish(jobId, st.Id);
            ws.Cell(r, 2).Value = (double)pSum;
            ws.Cell(r, 3).Value = (double)aSum;
            ws.Cell(r, 4).Value = (double)variance;
            ws.Cell(r, 5).Value = VarianceComment(variance, pSum, aSum);
            for (var c = 2; c <= 4; c++)
            {
                ws.Cell(r, c).Style.NumberFormat.Format = "0.0";
                ws.Cell(r, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            if (idxRoll++ % 2 == 1)
                ws.Range(r, 1, r, KpiCols).Style.Fill.BackgroundColor = StripSub;

            r++;
        }

        var unassignedTotal = jobLogs
            .Where(l => ResolveLogStageId(l, detail) == Guid.Empty)
            .Sum(l => l.Hours);
        if (unassignedTotal > 0)
        {
            var varianceU = unassignedTotal;
            ws.Cell(r, 1).Value = "Unassigned / general";
            ws.Cell(r, 2).Value = 0d;
            ws.Cell(r, 3).Value = (double)unassignedTotal;
            ws.Cell(r, 4).Value = (double)varianceU;
            ws.Cell(r, 5).Value =
                VarianceComment(varianceU, 0, unassignedTotal);
            for (var c = 2; c <= 4; c++)
            {
                ws.Cell(r, c).Style.NumberFormat.Format = "0.0";
                ws.Cell(r, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            if (idxRoll++ % 2 == 1)
                ws.Range(r, 1, r, KpiCols).Style.Fill.BackgroundColor = StripSub;

            r++;
        }

        var rollLastData = r - 1;
        ws.Cell(r, 1).Value = "Total";
        ws.Cell(r, 1).Style.Font.Bold = true;
        ws.Cell(r, 2).FormulaA1 = $"SUM(B{rollFirstData}:B{rollLastData})";
        ws.Cell(r, 3).FormulaA1 = $"SUM(C{rollFirstData}:C{rollLastData})";
        ws.Cell(r, 4).FormulaA1 = $"SUM(D{rollFirstData}:D{rollLastData})";

        decimal plannedRollupSum =
            orderedStages.Sum(st => allUsers.Sum(user => PlannedHoursFor(detail, st.Id, user)));
        ws.Cell(r, 5).Value =
            VarianceComment(totalActualInWindow - plannedRollupSum, plannedRollupSum, totalActualInWindow);
        for (var c = 2; c <= 4; c++)
        {
            ws.Cell(r, c).Style.NumberFormat.Format = "0.0";
            ws.Cell(r, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(r, c).Style.Font.Bold = true;
        }

        ws.Range(r, 1, r, KpiCols).Style.Fill.BackgroundColor = CardBg;

        BorderRange(ws.Range(rollHdr, 1, r, KpiCols));
        r++;
        ws.Row(r).Height = 6;
        r++;

        SectionBanner("4. Per person — planned vs actual by stage");
        r++;

        foreach (var u in allUsers)
        {
            var dn = display.GetValueOrDefault(u, u);
            var headline = string.Equals(dn.Trim(), u, StringComparison.OrdinalIgnoreCase)
                ? dn
                : $"{dn} ({u})";
            ws.Cell(r, 1).Value = headline;
            ws.Range(r, 1, r, mergeCols).Merge();
            ws.Row(r).Style.Font.Bold = true;
            ws.Row(r).Style.Font.FontSize = 11;
            ws.Row(r).Style.Fill.BackgroundColor = XLColor.FromHtml("#DDE4EE");
            r++;

            var tblTop = r;
            ws.Cell(r, 1).Value = "Stage";
            ws.Cell(r, 2).Value = "Planned h";
            ws.Cell(r, 3).Value = "Actual h";
            ws.Cell(r, 4).Value = "Variance";
            ws.Cell(r, 5).Value = "Comment";
            StyleHeader(ws.Range(r, 1, r, KpiCols));
            r++;
            var pFirstData = r;
            var idxRow = 0;
            foreach (var st in orderedStages)
            {
                var phx = PlannedHoursFor(detail, st.Id, u);
                var act = SumHoursForUser(jobLogs, u, HourFilter.ExactStage, st.Id, detail);
                if (phx == 0 && act == 0)
                    continue;

                var v = act - phx;
                ws.Cell(r, 1).Value = lookups.ResolveStageEnglish(jobId, st.Id);
                ws.Cell(r, 2).Value = (double)phx;
                ws.Cell(r, 3).Value = (double)act;
                ws.Cell(r, 4).Value = (double)v;
                ws.Cell(r, 5).Value = VarianceComment(v, phx, act);
                for (var c = 2; c <= 4; c++)
                {
                    ws.Cell(r, c).Style.NumberFormat.Format = "0.0";
                    ws.Cell(r, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }

                if (idxRow++ % 2 == 1)
                    ws.Range(r, 1, r, KpiCols).Style.Fill.BackgroundColor = StripSub;

                r++;
            }

            var uUn = SumHoursForUser(jobLogs, u, HourFilter.UnassignedStage, detail: detail);
            if (uUn > 0)
            {
                var vv = uUn;
                ws.Cell(r, 1).Value = "Unassigned / general";
                ws.Cell(r, 2).Value = 0d;
                ws.Cell(r, 3).Value = (double)uUn;
                ws.Cell(r, 4).Value = (double)vv;
                ws.Cell(r, 5).Value = VarianceComment(vv, 0, uUn);
                for (var c = 2; c <= 4; c++)
                {
                    ws.Cell(r, c).Style.NumberFormat.Format = "0.0";
                    ws.Cell(r, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }

                if (idxRow++ % 2 == 1)
                    ws.Range(r, 1, r, KpiCols).Style.Fill.BackgroundColor = StripSub;

                r++;
            }

            if (r == pFirstData)
            {
                ws.Cell(r, 1).Value = "No planned rows and no logged hours for this window.";
                ws.Range(r, 1, r, KpiCols).Merge();
                BorderRange(ws.Range(tblTop, 1, r, KpiCols));
                r += 2;
                continue;
            }

            var pLastData = r - 1;
            var totalPlannedUser = PlannedHoursForUserTotal(detail, u);
            var totalActualUser = SumHoursForUser(jobLogs, u, HourFilter.All);
            ws.Cell(r, 1).Value = $"{headline} — total";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 2).FormulaA1 = $"SUM(B{pFirstData}:B{pLastData})";
            ws.Cell(r, 3).FormulaA1 = $"SUM(C{pFirstData}:C{pLastData})";
            ws.Cell(r, 4).FormulaA1 = $"SUM(D{pFirstData}:D{pLastData})";
            ws.Cell(r, 5).Value =
                VarianceComment(totalActualUser - totalPlannedUser, totalPlannedUser, totalActualUser);
            for (var c = 2; c <= 4; c++)
            {
                ws.Cell(r, c).Style.NumberFormat.Format = "0.0";
                ws.Cell(r, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Cell(r, c).Style.Font.Bold = true;
            }

            ws.Range(r, 1, r, KpiCols).Style.Fill.BackgroundColor = CardBg;
            BorderRange(ws.Range(tblTop, 1, r, KpiCols));
            r += 2;
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static HashSet<string> AllUsernamesForOverview(JobDetail detail, List<WorkLog> jobLogs) =>
        UsersFromPlansAndLogs(detail, jobLogs);

    private static decimal PlannedHoursForUserTotal(JobDetail detail, string username) =>
        OrderedStageItems(detail).Sum(st => PlannedHoursFor(detail, st.Id, username));
}
