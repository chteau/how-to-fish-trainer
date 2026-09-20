using HtfPatcher;

return Cli.Run(args);

namespace HtfPatcher
{
    internal static class Cli
    {
        internal static int Run(string[] args)
        {
            try
            {
                var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
                var gameDir = Option(args, "--game");
                var payloadDir = Option(args, "--payload") ?? AppContext.BaseDirectory;

                switch (command)
                {
                    case "patch": return Patch(gameDir, payloadDir);
                    case "unpatch": return Unpatch(gameDir);
                    case "status": return Status(gameDir, payloadDir);
                    case "refs": return Refs(gameDir, Option(args, "--out") ?? "lib");
                    case "help" or "--help" or "-h": Usage(); return 0;
                    default:
                        Console.Error.WriteLine($"unknown command: {command}");
                        Usage();
                        return 2;
                }
            }
            catch (PatchException e)
            {
                Console.Error.WriteLine("error: " + e.Message);
                return 1;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("unexpected failure: " + e);
                return 1;
            }
        }

        private static void Usage()
        {
            Console.WriteLine("""
                How to Fish - trainer patcher

                  patch     inject the trainer into Assembly-CSharp.dll (backs it up first)
                  unpatch   restore the original Assembly-CSharp.dll and remove trainer files
                  status    report whether the game is currently patched
                  refs      copy the assemblies the trainer builds against into a folder

                Options:
                  --game <dir>      game folder (default: found automatically via Steam)
                  --payload <dir>   folder holding HtfTrainer.dll (default: next to this program)
                  --out <dir>       destination for 'refs' (default: lib)

                Close the game before patching or unpatching.
                """);
        }

        private static int Patch(string? gameDir, string payloadDir)
        {
            var paths = Paths.Resolve(gameDir);
            Console.WriteLine($"game:    {paths.GameDir}");
            Console.WriteLine($"managed: {paths.ManagedDir}");

            var missing = Paths.Payload
                .Where(name => !File.Exists(Path.Combine(payloadDir, name)))
                .ToArray();

            if (missing.Length > 0)
                throw new PatchException(
                    $"missing trainer files in '{payloadDir}': {string.Join(", ", missing)}. Run build.sh first.");

            if (Injector.IsPatched(paths.Assembly))
            {
                Console.WriteLine("already patched - run 'unpatch' first if you want to re-apply.");
                return 0;
            }

            if (!File.Exists(paths.Backup))
            {
                File.Copy(paths.Assembly, paths.Backup);
                Console.WriteLine($"backup:  {Path.GetFileName(paths.Backup)}");
            }
            else
            {
                Console.WriteLine("backup:  already present, keeping the existing one");
            }

            foreach (var name in Paths.Payload)
                File.Copy(Path.Combine(payloadDir, name), Path.Combine(paths.ManagedDir, name), overwrite: true);
            Console.WriteLine($"copied:  {Paths.Payload.Length} trainer files into Managed");

            try
            {
                Injector.Inject(paths.Assembly, Path.Combine(paths.ManagedDir, Paths.TrainerAssembly));
            }
            catch
            {
                // Never leave a half-patched assembly behind.
                if (File.Exists(paths.Backup)) File.Copy(paths.Backup, paths.Assembly, overwrite: true);
                throw;
            }

            Console.WriteLine("patched: GameInfo.Awake now calls HtfTrainer.Loader.Init()");
            Console.WriteLine();
            Console.WriteLine("Launch the game and press F7 for the trainer menu.");
            return 0;
        }

        private static int Unpatch(string? gameDir)
        {
            var paths = Paths.Resolve(gameDir);
            var restored = false;

            if (File.Exists(paths.Backup))
            {
                File.Copy(paths.Backup, paths.Assembly, overwrite: true);
                File.Delete(paths.Backup);
                restored = true;
                Console.WriteLine("restored: original Assembly-CSharp.dll");
            }
            else if (Injector.IsPatched(paths.Assembly))
            {
                throw new PatchException(
                    "the assembly is patched but the backup is gone. Verify the game files through Steam to restore it.");
            }
            else
            {
                Console.WriteLine("nothing to restore: Assembly-CSharp.dll is not patched");
            }

            var removed = 0;
            foreach (var name in Paths.Payload)
            {
                var path = Path.Combine(paths.ManagedDir, name);
                if (!File.Exists(path)) continue;
                File.Delete(path);
                removed++;
            }

            if (removed > 0) Console.WriteLine($"removed:  {removed} trainer files from Managed");
            if (!restored && removed == 0) Console.WriteLine("the game is already clean");
            return 0;
        }

        private static int Status(string? gameDir, string payloadDir)
        {
            var paths = Paths.Resolve(gameDir);
            var patched = Injector.IsPatched(paths.Assembly);

            Console.WriteLine($"game:    {paths.GameDir}");
            Console.WriteLine($"patched: {(patched ? "yes" : "no")}");
            Console.WriteLine($"backup:  {(File.Exists(paths.Backup) ? "present" : "none")}");

            var installed = Paths.Payload.Count(n => File.Exists(Path.Combine(paths.ManagedDir, n)));
            Console.WriteLine($"files:   {installed}/{Paths.Payload.Length} trainer files in Managed");

            var built = Paths.Payload.Count(n => File.Exists(Path.Combine(payloadDir, n)));
            Console.WriteLine($"payload: {built}/{Paths.Payload.Length} available in {payloadDir}");
            return 0;
        }

        /// <summary>
        /// Vendors the game's assemblies so the trainer can compile against them. Prefers the pristine
        /// backup, because Roslyn refuses a dnlib-written assembly as a reference ("invalid public
        /// key") and building against a patched one would be wrong anyway.
        /// </summary>
        private static int Refs(string? gameDir, string outDir)
        {
            var paths = Paths.Resolve(gameDir);
            Directory.CreateDirectory(outDir);

            var copied = 0;
            var missing = new List<string>();

            foreach (var name in Paths.ReferenceAssemblies)
            {
                var source = Path.Combine(paths.ManagedDir, name);

                if (name == Paths.TargetAssembly && File.Exists(paths.Backup))
                    source = paths.Backup;

                if (!File.Exists(source)) { missing.Add(name); continue; }

                File.Copy(source, Path.Combine(outDir, name), overwrite: true);
                copied++;
            }

            if (missing.Count > 0)
                throw new PatchException($"missing game assemblies: {string.Join(", ", missing)}");

            Console.WriteLine($"game: {paths.GameDir}");
            Console.WriteLine($"refs: copied {copied} assemblies into {outDir}");
            return 0;
        }

        private static string? Option(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
