using System;
using System.Collections.Generic;
using System.Reflection;

namespace HtfTrainer
{
    /// <summary>
    /// Cached access to the private members of the game assembly. Every lookup is resolved once and
    /// then reused, so the per-frame modules never pay for reflection in their hot paths.
    /// </summary>
    internal static class Refl
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static |
                                         BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<string, MethodInfo> Methods = new Dictionary<string, MethodInfo>();

        internal static FieldInfo Field(Type type, string name)
        {
            var key = type.FullName + "." + name;
            if (Fields.TryGetValue(key, out var cached)) return cached;

            FieldInfo found = null;
            for (var t = type; t != null && found == null; t = t.BaseType)
                found = t.GetField(name, Any);

            if (found == null) Log.Warn($"field not found: {key}");
            Fields[key] = found;
            return found;
        }

        internal static MethodInfo Method(Type type, string name, params Type[] args)
        {
            var key = type.FullName + "." + name + "(" + args.Length + ")";
            if (Methods.TryGetValue(key, out var cached)) return cached;

            MethodInfo found = null;
            for (var t = type; t != null && found == null; t = t.BaseType)
                found = args.Length == 0 ? t.GetMethod(name, Any) : t.GetMethod(name, Any, null, args, null);

            if (found == null) Log.Warn($"method not found: {key}");
            Methods[key] = found;
            return found;
        }

        internal static T Get<T>(FieldInfo field, object instance, T fallback = default)
        {
            if (field == null) return fallback;
            try { return (T)field.GetValue(instance); }
            catch (Exception e) { Log.Warn($"read {field.Name} failed: {e.Message}"); return fallback; }
        }

        internal static void Set(FieldInfo field, object instance, object value)
        {
            if (field == null) return;
            try { field.SetValue(instance, value); }
            catch (Exception e) { Log.Warn($"write {field.Name} failed: {e.Message}"); }
        }

        /// <summary>Binds a private, parameterless instance method as a fast reusable delegate.</summary>
        internal static Action Bind(MethodInfo method, object instance)
        {
            if (method == null || instance == null) return null;
            try { return (Action)Delegate.CreateDelegate(typeof(Action), instance, method); }
            catch (Exception e) { Log.Warn($"bind {method.Name} failed: {e.Message}"); return null; }
        }
    }
}
