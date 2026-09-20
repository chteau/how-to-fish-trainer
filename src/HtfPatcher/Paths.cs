namespace HtfPatcher;

/// <summary>Resolves the game layout and the set of files the patcher owns.</summary>
internal sealed class Paths
{
    internal const string TargetAssembly = "Assembly-CSharp.dll";
    internal const string BackupSuffix = ".htfbak";
    internal const string TrainerAssembly = "HtfTrainer.dll";

    /// <summary>
    /// Copied next to the game's own assemblies. The trainer has no third-party dependencies: the
    /// two behavioural hooks are woven into the game assembly at patch time rather than applied by a
    /// runtime patching library, which would have needed System.Reflection.Emit facades this game
    /// does not ship.
    /// </summary>
    internal static readonly string[] Payload = { TrainerAssembly };

    internal string GameDir { get; }
    internal string ManagedDir { get; }
    internal string Assembly => Path.Combine(ManagedDir, TargetAssembly);
    internal string Backup => Assembly + BackupSuffix;

    private Paths(string gameDir, string managedDir)
    {
        GameDir = gameDir;
        ManagedDir = managedDir;
    }

    internal static string DefaultGameDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "How to Fish", "How to Fish");
    }

    /// <summary>
    /// Accepts either the folder holding the executable or the Steam folder one level above it,
    /// since both are natural things to point at.
    /// </summary>
    internal static Paths Resolve(string gameDir)
    {
        gameDir = Path.GetFullPath(gameDir.TrimEnd('/', '\\'));

        var managed = FindManaged(gameDir);
        if (managed is null)
        {
            var nested = Path.Combine(gameDir, "How to Fish");
            if (Directory.Exists(nested))
            {
                managed = FindManaged(nested);
                if (managed is not null) gameDir = nested;
            }
        }

        if (managed is null)
            throw new PatchException($"could not find a '*_Data/Managed' folder under: {gameDir}");

        return new Paths(gameDir, managed);
    }

    private static string? FindManaged(string dir)
    {
        if (!Directory.Exists(dir)) return null;
        foreach (var data in Directory.EnumerateDirectories(dir, "*_Data"))
        {
            var managed = Path.Combine(data, "Managed");
            if (File.Exists(Path.Combine(managed, TargetAssembly))) return managed;
        }
        return null;
    }
}

internal sealed class PatchException : Exception
{
    internal PatchException(string message) : base(message) { }
}
