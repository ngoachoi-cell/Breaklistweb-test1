namespace BreaklistWeb.Models;

public class BreaklistState
{
    public int DayStartMin { get; set; } = 360;      // 06:00
    public int WindowMinutes { get; set; } = 1500;   // 25 hours = 1500 minutes
    public int SlotStepMin { get; set; } = 20;       // 20-minute intervals

    public string SourceFileName { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public List<BreaklistRow> Rows { get; set; } = new();
    public Dictionary<string, string> Cells { get; set; } = new();

    public static string ToHHmm(int minutes)
    {
        var m = minutes % 1440;
        if (m < 0) m += 1440;
        var ts = TimeSpan.FromMinutes(m);
        return ts.ToString(@"hh\:mm");
    }
}