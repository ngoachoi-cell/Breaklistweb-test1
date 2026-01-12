using System.Globalization;
using BreaklistWeb.Models;
using OfficeOpenXml;

namespace BreaklistWeb.Services;

public class ShiftReportParser
{
    public List<BreaklistRow> Parse(byte[] fileBytes, string fileName, out string? error)
    {
        error = null;

        using var ms = new MemoryStream(fileBytes);
        using var package = new ExcelPackage(ms);

        var ws = package.Workbook.Worksheets
            .FirstOrDefault(s => string.Equals(s.Name, "Report", StringComparison.OrdinalIgnoreCase))
            ?? package.Workbook.Worksheets.FirstOrDefault();

        if (ws == null)
        {
            error = "No worksheet found in this file.";
            return new List<BreaklistRow>();
        }

        int headerRow = -1;
        int maxScanRows = Math.Min(ws.Dimension?.End.Row ?? 1, 40);

        for (int r = 1; r <= maxScanRows; r++)
        {
            var v = ws.Cells[r, 1].Value?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(v) &&
                v.Equals("Employee Full Name", StringComparison.OrdinalIgnoreCase))
            {
                headerRow = r;
                break;
            }
        }

        if (headerRow == -1)
        {
            error = "Could not find header row. Expected 'Employee Full Name' in column A.";
            return new List<BreaklistRow>();
        }

        int nameCol = FindCol(ws, headerRow, new[] { "Employee Full Name", "Employee Name", "Name" }) ?? 1;
        int startCol = FindCol(ws, headerRow, new[] { "Start Time", "Start" }) ?? 10;
        int endCol = FindCol(ws, headerRow, new[] { "End Time", "End" }) ?? 14;

        var rows = new List<BreaklistRow>();
        int lastRow = ws.Dimension?.End.Row ?? headerRow;

        int dayStart = 360;
        int windowEnd = dayStart + 1500; // 07:00 next day = 1860

        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            var name = ws.Cells[r, nameCol].Value?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var startVal = ws.Cells[r, startCol].Value;
            var endVal = ws.Cells[r, endCol].Value;

            var startMinOfDay = ParseTimeToMinutes(startVal);
            if (startMinOfDay == null)
                continue;

            var endMinOfDay = ParseTimeToMinutes(endVal);
            if (endMinOfDay == null)
                endMinOfDay = (startMinOfDay.Value + 8 * 60) % 1440;

            int startAbs = NormalizeToWindow(startMinOfDay.Value, dayStart);
            int endAbs = NormalizeToWindow(endMinOfDay.Value, dayStart);
            if (endAbs <= startAbs) endAbs += 1440;

            if (endAbs > windowEnd) endAbs = windowEnd;

            rows.Add(new BreaklistRow
            {
                Name = name,
                StartAbsMin = startAbs,
                EndAbsMin = endAbs
            });
        }

        if (rows.Count == 0)
        {
            error = "Worksheet 'Report' was found, but no valid rows were parsed. Check that Start Time cells are real times/datetimes.";
            return rows;
        }

        rows = rows.OrderBy(x => x.StartAbsMin).ThenBy(x => x.Name).ToList();
        for (int i = 0; i < rows.Count; i++) rows[i].SortOrder = i;

        return rows;
    }

    private static int? FindCol(ExcelWorksheet ws, int headerRow, IEnumerable<string> names)
    {
        int endCol = ws.Dimension?.End.Column ?? 50;
        var wanted = new HashSet<string>(names.Select(n => n.Trim()), StringComparer.OrdinalIgnoreCase);

        for (int c = 1; c <= endCol; c++)
        {
            var v = ws.Cells[headerRow, c].Value?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(v) && wanted.Contains(v))
                return c;
        }
        return null;
    }

    private static int NormalizeToWindow(int minutesOfDay, int dayStart)
    {
        if (minutesOfDay < dayStart) minutesOfDay += 1440;
        return minutesOfDay;
    }

    private static int? ParseTimeToMinutes(object? value)
    {
        if (value == null) return null;

        if (value is DateTime dt)
            return dt.Hour * 60 + dt.Minute;

        if (value is TimeSpan ts)
            return (int)ts.TotalMinutes % 1440;

        if (value is double d)
        {
            var odt = DateTime.FromOADate(d);
            return odt.Hour * 60 + odt.Minute;
        }

        var s = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(s)) return null;

        if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var t1))
            return (int)t1.TotalMinutes % 1440;

        if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out var t2) ||
            DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out t2))
            return t2.Hour * 60 + t2.Minute;

        return null;
    }
}