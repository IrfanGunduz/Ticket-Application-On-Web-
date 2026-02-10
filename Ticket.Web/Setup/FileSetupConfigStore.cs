using System.Text.Json;

namespace Ticket.Web.Setup;

public sealed class FileSetupConfigStore : ISetupConfigStore
{
    private readonly string _filePath;

    public FileSetupConfigStore()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dir = Path.Combine(root, "Ticket");
        Directory.CreateDirectory(dir);

        _filePath = Path.Combine(dir, "setup.json");
    }

    public bool HasConfig() => File.Exists(_filePath);

    public SetupConfig? TryLoad()
    {
        if (!File.Exists(_filePath)) return null;

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<SetupConfig>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Save(SetupConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

        // Basit ve yeterli. İstersen atomic save’i sonra ekleriz.
        File.WriteAllText(_filePath, json);
    }
}
