using System.Reflection;
using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Drip odds live in CreatureManager._shinyCreatureChance and are rolled in HookItem as
    /// Random.Range(0, 100) &lt; chance, on the server only. Setting it to 50 therefore gives a true
    /// 50/50 between a normal and a drip catch — and, like the vitals options, only works when hosting.
    /// </summary>
    internal static class Drip
    {
        private static FieldInfo _field;
        private static int _stock = -1;

        internal static bool IsHost
        {
            get
            {
                var cm = CreatureManager.Instance;
                return cm != null && cm.IsServerInitialized;
            }
        }

        internal static int CurrentChance
        {
            get
            {
                var cm = CreatureManager.Instance;
                if (cm == null) return -1;
                Resolve();
                return Refl.Get(_field, cm, -1);
            }
        }

        private static void Resolve()
        {
            if (_field == null) _field = Refl.Field(typeof(CreatureManager), "_shinyCreatureChance");
        }

        internal static void Tick()
        {
            var cm = CreatureManager.Instance;
            if (cm == null || !cm.IsServerInitialized) return;

            Resolve();
            if (_field == null) return;

            if (_stock < 0) _stock = Refl.Get(_field, cm, 1);

            var wanted = Cfg.DripEnabled ? Mathf.Clamp(Cfg.DripChance, 0, 100) : _stock;
            if (Refl.Get(_field, cm, -1) != wanted) Refl.Set(_field, cm, wanted);
        }

        internal static void Restore()
        {
            var cm = CreatureManager.Instance;
            if (cm == null || _field == null || _stock < 0) return;
            Refl.Set(_field, cm, _stock);
        }
    }
}
