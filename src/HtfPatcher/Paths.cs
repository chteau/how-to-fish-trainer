using System.Text.RegularExpressions;

namespace HtfPatcher;

/// <summary>Resolves the game layout and the set of files the patcher owns.</summary>
internal sealed class Paths
{
    internal const string TargetAssembly = "Assembly-CSharp.dll";
    internal const string BackupSuffix = ".htfbak";
    internal const string TrainerAssembly = "HtfTrainer.dll";

    /// <summary>
    /// Copied next to the game's own assemblies. The trainer has no third-party dependencies: the
    /// behavioural hooks are woven into the game assembly at patch time rather than applied by a
    /// runtime patching library, which would have needed System.Reflection.Emit facades this game
    /// does not ship.
    /// </summary>
    internal static readonly string[] Payload = { TrainerAssembly };

    /// <summary>Assemblies the trainer compiles against, vendored into lib/ by the build scripts.</summary>
    internal static readonly string[] ReferenceAssemblies =
    {
        "Assembly-CSharp.dll", "FishNet.Runtime.dll", "GameKit.Dependencies.dll",
        "UnityEngine.dll", "UnityEngine.CoreModule.dll", "UnityEngine.PhysicsModule.dll",
        "UnityEngine.IMGUIModule.dll", "UnityEngine.InputLegacyModule.dll",
        "UnityEngine.TextRenderingModule.dll", "UnityEngine.AnimationModule.dll",
        "Unity.InputSystem.dll", "netstandard.dll"
    };

    internal string GameDir { get; }
    internal string ManagedDir { get; }
    internal string Assembly => Path.Combine(ManagedDir, TargetAssembly);
    internal string Backup => Assembly + BackupSuffix;

    private Paths(string gameDir, string managedDir)
    {
        GameDir = gameDir;
        ManagedDir = managedDir;
    }

    /// <summary>
    /// Accepts either the folder holding the executable or the Steam folder one level above it, since
    /// both are natural things to point at. Passing null auto-detects across Windows, Linux and macOS.
    /// </summary>
    internal static Paths Resolve(string? gameDir)
    {
        if (!string.IsNullOrWhiteSpace(gameDir)) return ResolveExplicit(gameDir!);

        var searched = new List<string>();
        foreach (var candidate in CandidateGameDirs())
        {
            searched.Add(candidate);
            var managed = FindManaged(candidate);
            if (managed is not null) return new Paths(candidate, managed);
        }

        throw new PatchException(
            "could not find the game automatically. Pass --game \"<path to How to Fish>\".\nLooked in:\n  " +
            string.Join("\n  ", searched.Distinct()));
    }

    private static Paths ResolveExplicit(string gameDir)
    {
        gameDir = Path.GetFullPath(gameDir.Trim().Trim('"').TrimEnd('/', '\\'));

        var managed = FindManaged(gameDir);
        if (managed is null)
        {
            // Allow pointing at the Steam folder that contains the game folder.
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

    internal static string DefaultGameDir() => CandidateGameDirs().FirstOrDefault() ?? "";

    /// <summary>Every place the game plausibly lives, across all three desktop platforms.</summary>
    private static IEnumerable<string> CandidateGameDirs()
    {
        foreach (var library in SteamLibraries())
        {
            var path = Path.Combine(library, "steamapps", "common", "How to Fish", "How to Fish");
            yield return path;
        }
    }

    private static IEnumerable<string> SteamLibraries()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in SteamRoots())
        {
            if (!seen.Add(root)) continue;
            yield return root;

            // Steam records extra library drives here, which is how it finds games on D: and friends.
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;

            string text;
            try { text = File.ReadAllText(vdf); }
            catch { continue; }

            foreach (Match match in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase))
            {
                var extra = match.Groups[1].Value.Replace("\\\\", "\\");
                if (seen.Add(extra)) yield return extra;
            }
        }
    }

    private static IEnumerable<string> SteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            foreach (var env in new[] { "ProgramFiles(x86)", "ProgramFiles" })
            {
                var baseDir = Environment.GetEnvironmentVariable(env);
                if (!string.IsNullOrEmpty(baseDir)) yield return Path.Combine(baseDir, "Steam");
            }
            yield return @"C:\Program Files (x86)\Steam";
            yield return @"C:\Program Files\Steam";
            yield return Path.Combine(home, "Steam");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine(home, "Library", "Application Support", "Steam");
        }
        else
        {
            yield return Path.Combine(home, ".local", "share", "Steam");
            yield return Path.Combine(home, ".steam", "steam");
            yield return Path.Combine(home, ".steam", "root");
            yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam",
                                      ".local", "share", "Steam"); // flatpak
        }
    }
}

internal sealed class PatchException : Exception
{
    internal PatchException(string message) : base(message) { }
}
