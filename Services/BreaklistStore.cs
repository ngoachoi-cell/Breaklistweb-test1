using System.Text.Json;
using BreaklistWeb.Models;

namespace BreaklistWeb.Services;

public class BreaklistStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public BreaklistStore(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "breaklist.json");
    }

    public BreaklistState LoadOrNew()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
                return new BreaklistState();

            var json = File.ReadAllText(_path);
            var state = JsonSerializer.Deserialize<BreaklistState>(json);
            return state ?? new BreaklistState();
        }
    }

    public void Save(BreaklistState state)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_path, json);
        }
    }

    public void Reset(BreaklistState state) => Save(state);
}