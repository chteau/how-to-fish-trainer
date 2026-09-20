using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace HtfPatcher;

/// <summary>
/// Weaves three calls into the game assembly. Everything the trainer needs at runtime is reached
/// through these, which is why the payload is a single DLL with no third-party dependencies.
///
/// A runtime patching library was the obvious alternative, but Harmony pulls in MonoMod and
/// Mono.Cecil, and those reference System.Reflection.Emit facades that this game's Managed folder
/// does not ship and its netstandard.dll does not forward — Harmony throws a TypeLoadException
/// before it can build a single patch. Injecting the calls up front sidesteps that entirely.
/// </summary>
internal static class Injector
{
    private const string TrainerAssemblyName = "HtfTrainer";
    private const string TrainerNamespace = "HtfTrainer";

    internal static bool IsPatched(string assemblyPath)
    {
        using var module = ModuleDefMD.Load(File.ReadAllBytes(assemblyPath));
        foreach (var reference in module.GetAssemblyRefs())
            if (string.Equals(reference.Name, TrainerAssemblyName, StringComparison.Ordinal))
                return true;
        return false;
    }

    internal static void Inject(string assemblyPath, string trainerPath)
    {
        var temp = assemblyPath + ".htftmp";

        {
            using var module = ModuleDefMD.Load(File.ReadAllBytes(assemblyPath));
            var refs = new TrainerRefs(module, trainerPath);

            InjectLoader(module, refs);
            InjectAimHook(module, refs);
            InjectReturnHook(module, "Attachments", "get_Damage", refs.ScaleDamage);
            InjectReturnHook(module, "Player", "get_BlockInputs", refs.BlockInputs);
            InjectArgumentHook(module, "CasinoManager", "ServerRouletteResult", 1, refs.RigRoulette);
            InjectArgumentHook(module, "PlayerCamera", "Recoil", 1, refs.ScaleRecoil);
            InjectReturnHook(module, "KillScoreCalculator", "GetMultiplier", refs.ScoreMultiplier);

            var options = new ModuleWriterOptions(module);
            // Preserve every token: other assemblies in the game reference this one, and FishNet's
            // generated serializers are sensitive to metadata being reshuffled.
            options.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
            module.Write(temp, options);
        }

        File.Move(temp, assemblyPath, overwrite: true);
    }

    /// <summary>GameInfo.Awake runs once, early, on every launch — the natural place to start up.</summary>
    private static void InjectLoader(ModuleDefMD module, TrainerRefs refs)
    {
        var body = RequireMethod(module, "GameInfo", "Awake").Body;
        body.Instructions.Insert(0, OpCodes.Call.ToInstruction(refs.LoaderInit));
        body.UpdateInstructionOffsets();
    }

    /// <summary>
    /// Gives the trainer first refusal on the aim-assist result:
    ///
    ///     if (Hooks.TryGetAim(this, cameraPosition, cameraEuler, out var delta)) return delta;
    ///     ... original body ...
    /// </summary>
    private static void InjectAimHook(ModuleDefMD module, TrainerRefs refs)
    {
        var method = RequireMethod(module, "PlayerAimAssist", "GetRotationDelta");
        if (method.MethodSig.Params.Count != 3)
            throw new PatchException(
                $"PlayerAimAssist.GetRotationDelta has {method.MethodSig.Params.Count} parameters, expected 3 — the game version may have changed.");

        var body = method.Body;
        var delta = new Local(refs.Vector2Sig);
        body.Variables.Add(delta);

        var resume = body.Instructions[0];
        var prologue = new[]
        {
            OpCodes.Ldarg_0.ToInstruction(),        // this
            OpCodes.Ldarg_1.ToInstruction(),        // cameraPosition
            OpCodes.Ldarg_2.ToInstruction(),        // cameraEuler
            OpCodes.Ldloca.ToInstruction(delta),    // out delta
            OpCodes.Call.ToInstruction(refs.TryGetAim),
            OpCodes.Brfalse.ToInstruction(resume),
            OpCodes.Ldloc.ToInstruction(delta),
            OpCodes.Ret.ToInstruction()
        };

        for (var i = 0; i < prologue.Length; i++) body.Instructions.Insert(i, prologue[i]);
        body.UpdateInstructionOffsets();
    }

    /// <summary>
    /// Passes a method's return value through a hook of the same type. Each existing 'ret' is
    /// rewritten in place into the call and a fresh 'ret' is put after it, so any branch already
    /// targeting that instruction still goes through the hook rather than jumping over it.
    /// </summary>
    private static void InjectReturnHook(ModuleDefMD module, string typeName, string methodName,
                                         IMethod hook)
    {
        var method = RequireMethod(module, typeName, methodName);
        var instructions = method.Body.Instructions;
        var patched = 0;

        for (var i = 0; i < instructions.Count; i++)
        {
            if (instructions[i].OpCode != OpCodes.Ret) continue;

            var ret = instructions[i];
            ret.OpCode = OpCodes.Call;
            ret.Operand = hook;
            instructions.Insert(i + 1, OpCodes.Ret.ToInstruction());
            patched++;
            i++;
        }

        if (patched == 0)
            throw new PatchException($"{typeName}.{methodName} has no return to hook.");

        method.Body.UpdateInstructionOffsets();
    }

    /// <summary>
    /// Passes one argument through a hook before the method body sees it:
    ///     arg = Hook(arg);
    /// Used where the interesting decision is an input rather than a return value.
    /// </summary>
    private static void InjectArgumentHook(ModuleDefMD module, string typeName, string methodName,
                                           ushort argIndex, IMethod hook)
    {
        var method = RequireMethod(module, typeName, methodName);
        var body = method.Body;
        var resume = body.Instructions[0];

        var prologue = new[]
        {
            OpCodes.Ldarg.ToInstruction(method.Parameters[argIndex]),
            OpCodes.Call.ToInstruction(hook),
            OpCodes.Starg.ToInstruction(method.Parameters[argIndex])
        };

        for (var i = 0; i < prologue.Length; i++) body.Instructions.Insert(i, prologue[i]);
        _ = resume;
        body.UpdateInstructionOffsets();
    }

    private static MethodDef RequireMethod(ModuleDefMD module, string typeName, string methodName)
    {
        TypeDef? type = null;
        foreach (var candidate in module.Types)
            if (string.Equals(candidate.FullName, typeName, StringComparison.Ordinal)) { type = candidate; break; }

        if (type is null)
            throw new PatchException($"'{typeName}' not found — is this the right Assembly-CSharp.dll?");

        var method = type.FindMethod(methodName)
                     ?? throw new PatchException($"'{typeName}.{methodName}' not found.");

        if (!method.HasBody)
            throw new PatchException($"'{typeName}.{methodName}' has no body to patch.");

        return method;
    }

    /// <summary>References into the trainer assembly, resolved once per patch run.</summary>
    private sealed class TrainerRefs
    {
        internal readonly IMethod LoaderInit;
        internal readonly IMethod TryGetAim;
        internal readonly IMethod ScaleDamage;
        internal readonly IMethod BlockInputs;
        internal readonly IMethod RigRoulette;
        internal readonly IMethod ScaleRecoil;
        internal readonly IMethod ScoreMultiplier;
        internal readonly TypeSig Vector2Sig;

        internal TrainerRefs(ModuleDefMD module, string trainerPath)
        {
            using var trainer = ModuleDefMD.Load(File.ReadAllBytes(trainerPath));
            var identity = trainer.Assembly
                           ?? throw new PatchException($"'{trainerPath}' has no assembly identity.");

            var assemblyRef = module.UpdateRowId(new AssemblyRefUser(identity));

            // Vector2/Vector3 are structs, so these must be ValueTypeSig. ToTypeSig() on a TypeRef
            // falls back to ClassSig when it cannot resolve the target, and Mono then rejects the
            // call site with "Expected reference type but got type kind 17" every single frame.
            Vector2Sig = new ValueTypeSig(UnityType(module, "Vector2"));
            var vector3 = new ValueTypeSig(UnityType(module, "Vector3"));
            var aimAssist = new ClassSig(FindTypeDef(module, "PlayerAimAssist"));

            LoaderInit = Method(module, assemblyRef, "Loader", "Init",
                MethodSig.CreateStatic(module.CorLibTypes.Void));

            TryGetAim = Method(module, assemblyRef, "Hooks", "TryGetAim",
                MethodSig.CreateStatic(module.CorLibTypes.Boolean,
                    aimAssist, vector3, vector3, new ByRefSig(Vector2Sig)));

            ScaleDamage = Method(module, assemblyRef, "Hooks", "ScaleDamage",
                MethodSig.CreateStatic(module.CorLibTypes.Int32, module.CorLibTypes.Int32));

            BlockInputs = Method(module, assemblyRef, "Hooks", "BlockInputs",
                MethodSig.CreateStatic(module.CorLibTypes.Boolean, module.CorLibTypes.Boolean));

            // BetColor is an enum, so it is a value type like Vector2/Vector3.
            var betColor = new ValueTypeSig(FindTypeDef(module, "BetColor"));
            RigRoulette = Method(module, assemblyRef, "Hooks", "RigRoulette",
                MethodSig.CreateStatic(betColor, betColor));

            ScaleRecoil = Method(module, assemblyRef, "Hooks", "ScaleRecoil",
                MethodSig.CreateStatic(Vector2Sig, Vector2Sig));

            ScoreMultiplier = Method(module, assemblyRef, "Hooks", "ScoreMultiplier",
                MethodSig.CreateStatic(module.CorLibTypes.Single, module.CorLibTypes.Single));
        }

        private static IMethod Method(ModuleDef module, AssemblyRef assemblyRef,
                                      string typeName, string methodName, MethodSig signature)
        {
            var typeRef = new TypeRefUser(module, TrainerNamespace, typeName, assemblyRef);
            return new MemberRefUser(module, methodName, signature, typeRef);
        }

        /// <summary>Reuses the UnityEngine type reference the game assembly already carries.</summary>
        private static ITypeDefOrRef UnityType(ModuleDefMD module, string name)
        {
            foreach (var typeRef in module.GetTypeRefs())
                if (typeRef.Namespace == "UnityEngine" && typeRef.Name == name)
                    return typeRef;

            throw new PatchException($"UnityEngine.{name} is not referenced by the game assembly.");
        }

        private static TypeDef FindTypeDef(ModuleDefMD module, string fullName)
        {
            foreach (var type in module.Types)
                if (string.Equals(type.FullName, fullName, StringComparison.Ordinal)) return type;

            throw new PatchException($"'{fullName}' not found in the game assembly.");
        }
    }
}
