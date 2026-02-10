using System;
using System.IO;

namespace Ticket.Web.Setup;

public static class SetupPaths
{
    // Öncelik: ProgramData\Ticket (server-wide, backup kolay)
    // Olmazsa: LocalAppData\Ticket (fallback)
    public static string RootDir { get; } = ResolveRootDir();

    public static string SetupJsonPath => Path.Combine(RootDir, "setup.json");
    public static string KeysDir => Path.Combine(RootDir, "keys");

    private static string ResolveRootDir()
    {
        var programData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Ticket");

        if (TryEnsureDir(programData))
            return programData;

        var localAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ticket");

        Directory.CreateDirectory(localAppData);
        return localAppData;
    }

    private static bool TryEnsureDir(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
