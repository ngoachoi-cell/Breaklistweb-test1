namespace BreaklistWeb.Models;

public class BreaklistRow
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int SortOrder { get; set; }
    public string Name { get; set; } = "";
    public int StartAbsMin { get; set; }
    public int EndAbsMin { get; set; }
}