using System.Globalization;
using System.IO;
using CAKA.PerformanceApp.Models;
using PdfSharp.Pdf;
using PdfSharp.Drawing;

namespace CAKA.PerformanceApp.Services;

public class ReportPdfService : IReportPdfService
{
    private const double Margin = 40;
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double ContentWidth = PageWidth - 2 * Margin;
    private const double BottomMargin = 60;
    private const double RowHeight = 20;
    private const double SectionGap = 16;
    private const double ColDateWidth = 74;
    private const double ColHoursWidth = 52;
    private const double GapBetweenDescAndHours = 20;
    private const int MaxDescChars = 35;

    public void GenerateWeekReport(string filePath, DateTime weekStart, DateTime weekEnd,
        IReadOnlyList<WorkLog> entries,
        IReadOnlyDictionary<string, string> userNameToDisplayName)
    {
        var document = new PdfDocument();
        document.Info.Title = $"Haftalık rapor {weekStart:dd.MM.yyyy} - {weekEnd:dd.MM.yyyy}";

        var fontTitle = new XFont("Arial", 14, XFontStyleEx.Bold);
        var fontSection = new XFont("Arial", 11, XFontStyleEx.Bold);
        var fontHeader = new XFont("Arial", 9, XFontStyleEx.Bold);
        var fontCell = new XFont("Arial", 9, XFontStyleEx.Regular);
        var fontTotal = new XFont("Arial", 9, XFontStyleEx.Bold);
        var pen = new XPen(XColors.Black, 0.5);

        PdfPage page = document.AddPage();
        page.Width = XUnit.FromPoint(PageWidth);
        page.Height = XUnit.FromPoint(PageHeight);
        XGraphics gfx = XGraphics.FromPdfPage(page);
        double y = Margin;

        void EnsureSpace(double needed)
        {
            if (y + needed > PageHeight - BottomMargin)
            {
                gfx.Dispose();
                page = document.AddPage();
                page.Width = XUnit.FromPoint(PageWidth);
                page.Height = XUnit.FromPoint(PageHeight);
                gfx = XGraphics.FromPdfPage(page);
                y = Margin;
            }
        }

        gfx.DrawString($"Haftalık iş raporu: {weekStart:dd.MM.yyyy} - {weekEnd:dd.MM.yyyy}", fontTitle, XBrushes.Black, Margin, y);
        y += RowHeight + SectionGap;

        var colDescWidth = ContentWidth - ColDateWidth - ColHoursWidth - GapBetweenDescAndHours;
        var xDate = Margin;
        var xDesc = Margin + ColDateWidth + 6;
        var xHours = Margin + ColDateWidth + colDescWidth + GapBetweenDescAndHours;

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

            var rowCount = userEntries.Count > 0 ? userEntries.Count : 1;
            var sectionHeight = RowHeight + RowHeight + (rowCount + 1) * RowHeight + RowHeight + SectionGap;
            EnsureSpace(sectionHeight);

            gfx.DrawString($"Çalışan: {displayName}", fontSection, XBrushes.Black, Margin, y);
            y += RowHeight;

            gfx.DrawString("Tarih", fontHeader, XBrushes.Black, new XRect(xDate, y, ColDateWidth, RowHeight + 4), XStringFormats.TopLeft);
            gfx.DrawString("İş açıklaması", fontHeader, XBrushes.Black, new XRect(xDesc, y, colDescWidth, RowHeight + 4), XStringFormats.TopLeft);
            gfx.DrawString("Saat", fontHeader, XBrushes.Black, new XRect(xHours, y, ColHoursWidth, RowHeight + 4), XStringFormats.TopLeft);
            y += RowHeight;
            gfx.DrawLine(pen, Margin, y, Margin + ContentWidth, y);
            y += 8;

            if (userEntries.Count == 0)
            {
                gfx.DrawString("Bu hafta iş kaydı yok.", fontCell, XBrushes.Gray, new XRect(xDesc, y, colDescWidth, RowHeight + 4), XStringFormats.TopLeft);
                y += RowHeight;
            }
            else
            {
                foreach (var log in userEntries)
                {
                    var desc = log.Description.Length > MaxDescChars
                        ? log.Description.Substring(0, MaxDescChars - 3) + "..."
                        : log.Description;
                    gfx.DrawString(log.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), fontCell, XBrushes.Black, new XRect(xDate, y, ColDateWidth, RowHeight + 4), XStringFormats.TopLeft);
                    gfx.DrawString(desc, fontCell, XBrushes.Black, new XRect(xDesc, y, colDescWidth, RowHeight + 4), XStringFormats.TopLeft);
                    gfx.DrawString(log.Hours.ToString("N1", CultureInfo.GetCultureInfo("tr-TR")), fontCell, XBrushes.Black, new XRect(xHours, y, ColHoursWidth, RowHeight + 4), XStringFormats.TopLeft);
                    y += RowHeight;
                }
            }

            y += 8;
            gfx.DrawLine(pen, Margin, y, Margin + ContentWidth, y);
            y += RowHeight;
            gfx.DrawString($"Toplam: {totalHours:N1} saat", fontTotal, XBrushes.Black, Margin, y);
            y += RowHeight + SectionGap;
        }

        var summaryHeight = RowHeight * 2 + (summaryList.Count + 2) * RowHeight + SectionGap;
        EnsureSpace(summaryHeight);

        y += SectionGap;
        gfx.DrawString("Haftalık özet", fontSection, XBrushes.Black, Margin, y);
        y += RowHeight + 4;
        gfx.DrawLine(pen, Margin, y, Margin + 250, y);
        y += RowHeight;

        var grandTotal = summaryList.Sum(x => x.Hours);
        foreach (var (name, hours) in summaryList)
        {
            gfx.DrawString(name, fontCell, XBrushes.Black, Margin, y);
            gfx.DrawString($"{hours:N1} saat", fontCell, XBrushes.Black, Margin + 200, y);
            y += RowHeight;
        }

        y += 4;
        gfx.DrawLine(pen, Margin, y, Margin + 250, y);
        y += RowHeight;
        gfx.DrawString($"Genel toplam: {grandTotal:N1} saat", fontTotal, XBrushes.Black, Margin, y);

        gfx.Dispose();
        using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            document.Save(stream, false);
    }
}
