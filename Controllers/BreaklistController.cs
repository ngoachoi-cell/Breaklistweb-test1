using BreaklistWeb.Models;
using BreaklistWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace BreaklistWeb.Controllers;

public class BreaklistController : Controller
{
    private readonly BreaklistStore _store;
    private readonly ShiftReportParser _parser;

    public BreaklistController(BreaklistStore store, ShiftReportParser parser)
    {
        _store = store;
        _parser = parser;
    }

    [HttpGet]
    public IActionResult Upload()
    {
        var state = _store.LoadOrNew();
        ViewBag.LastError = TempData["Error"];
        return View(state);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please choose a file.";
            return RedirectToAction(nameof(Upload));
        }

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        var parsed = _parser.Parse(bytes, file.FileName, out var error);
        if (!string.IsNullOrEmpty(error))
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Upload));
        }

        if (parsed.Count == 0)
        {
            TempData["Error"] = "No valid staff found in the file.";
            return RedirectToAction(nameof(Upload));
        }

        var state = _store.LoadOrNew();
        int nextOrder = state.Rows.Count == 0 ? 0 : state.Rows.Max(r => r.SortOrder) + 1;
        foreach (var row in parsed)
        {
            row.SortOrder = nextOrder++;
            state.Rows.Add(row);
        }

        _store.Save(state);
        return RedirectToAction(nameof(ViewBreaklist));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearAll()
    {
        var state = new BreaklistState();
        _store.Reset(state);
        return RedirectToAction(nameof(Upload));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SortByStartTime()
    {
        var state = _store.LoadOrNew();
        // Sort by StartAbsMin, then Name
        var sorted = state.Rows.OrderBy(r => r.StartAbsMin).ThenBy(r => r.Name).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].SortOrder = i;
        }
        state.Rows = sorted;
        _store.Save(state);
        return RedirectToAction(nameof(ViewBreaklist));
    }

    [HttpGet]
    public IActionResult ViewBreaklist()
    {
        var state = _store.LoadOrNew();
        if (state.Rows.Count == 0)
            return RedirectToAction(nameof(Upload));

        state.Rows = state.Rows.OrderBy(r => r.SortOrder).ToList();
        _store.Save(state);
        return View("View", state);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateCell(string rowId, int slot, string value)
    {
        var state = _store.LoadOrNew();
        var key = $"{rowId}:{slot}";
        value = (value ?? "").Trim();

        if (string.IsNullOrEmpty(value))
            state.Cells.Remove(key);
        else
            state.Cells[key] = value.Length > 6 ? value.Substring(0, 6) : value;

        _store.Save(state);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateRow(string rowId, string name, string start, string end)
    {
        var state = _store.LoadOrNew();
        var row = state.Rows.FirstOrDefault(r => r.Id == rowId);
        if (row == null) return NotFound();

        row.Name = (name ?? "").Trim();

        if (TryParseHHmm(start, out var startMin))
            row.StartAbsMin = NormalizeToWindow(startMin, state.DayStartMin);

        if (TryParseHHmm(end, out var endMin))
        {
            var endAbs = NormalizeToWindow(endMin, state.DayStartMin);
            if (endAbs <= row.StartAbsMin) endAbs += 1440;
            row.EndAbsMin = Math.Min(endAbs, state.DayStartMin + state.WindowMinutes);
        }

        _store.Save(state);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddRow()
    {
        var state = _store.LoadOrNew();
        var maxOrder = state.Rows.Count == 0 ? 0 : state.Rows.Max(r => r.SortOrder) + 1;
        state.Rows.Add(new BreaklistRow
        {
            SortOrder = maxOrder,
            Name = "New Staff",
            StartAbsMin = state.DayStartMin,
            EndAbsMin = state.DayStartMin + 8 * 60
        });
        _store.Save(state);
        return RedirectToAction(nameof(ViewBreaklist));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteRow(string rowId)
    {
        var state = _store.LoadOrNew();
        state.Rows.RemoveAll(r => r.Id == rowId);

        var prefix = rowId + ":";
        foreach (var k in state.Cells.Keys.Where(k => k.StartsWith(prefix)).ToList())
            state.Cells.Remove(k);

        state.Rows = state.Rows.OrderBy(r => r.SortOrder).ToList();
        for (int i = 0; i < state.Rows.Count; i++) state.Rows[i].SortOrder = i;

        _store.Save(state);
        return RedirectToAction(nameof(ViewBreaklist));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Reorder(string orderedIds)
    {
        var state = _store.LoadOrNew();
        var ids = (orderedIds ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var map = state.Rows.ToDictionary(r => r.Id);
        var newRows = new List<BreaklistRow>();

        for (int i = 0; i < ids.Count; i++)
        {
            if (map.TryGetValue(ids[i], out var row))
            {
                row.SortOrder = i;
                newRows.Add(row);
            }
        }

        foreach (var row in state.Rows.Where(r => !ids.Contains(r.Id)))
        {
            row.SortOrder = newRows.Count;
            newRows.Add(row);
        }

        state.Rows = newRows;
        _store.Save(state);
        return Ok();
    }

    private static bool TryParseHHmm(string s, out int minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;

        if (TimeSpan.TryParse(s, out var ts))
        {
            minutes = (int)ts.TotalMinutes % 1440;
            return true;
        }
        return false;
    }

    private static int NormalizeToWindow(int minutesOfDay, int dayStart)
    {
        if (minutesOfDay < dayStart) minutesOfDay += 1440;
        return minutesOfDay;
    }
}