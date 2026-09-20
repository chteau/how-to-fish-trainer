using System.Reflection;
using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Punch damage. PlayerPunching applies it through Item.LocalHit / PlayerVitals.LocalHit exactly
    /// like a gun does, so this is client-authoritative and works without hosting.
    ///
    /// Melee weapons are a separate path (Melee reads its damage from a shared sharpness-upgrade
    /// object rather than a field on the component), so they are deliberately left alone here.
    /// </summary>
    internal static class Hands
    {
        private static FieldInfo _damage;
        private static bool _resolved;
        private static int _stock = -1;

        private static PlayerPunching Local
        {
            get
            {
                var p = Player.LocalPlayer;
                return p != null ? p.Punching : null;
            }
        }

        internal static int StockDamage => _stock;

        internal static int CurrentDamage
        {
            get
            {
                var punching = Local;
                return punching == null ? 0 : Refl.Get(_damage, punching, 0);
            }
        }

        internal static void Tick()
        {
            var punching = Local;
            if (punching == null) return;

            if (!_resolved)
            {
                _resolved = true;
                _damage = Refl.Field(typeof(PlayerPunching), "_damage");
            }
            if (_damage == null) return;

            if (_stock < 0) _stock = Refl.Get(_damage, punching, 5);

            var wanted = Cfg.PunchDamageEnabled
                ? Mathf.Max(1, Mathf.RoundToInt(_stock * Mathf.Max(0.01f, Cfg.PunchDamageMult)))
                : _stock;

            if (Refl.Get(_damage, punching, -1) != wanted) Refl.Set(_damage, punching, wanted);
        }

        internal static void Restore()
        {
            var punching = Local;
            if (punching == null || _damage == null || _stock < 0) return;
            Refl.Set(_damage, punching, _stock);
        }
    }
}
