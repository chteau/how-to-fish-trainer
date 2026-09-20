using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Health and fullness are FishNet SyncVars written only on the server (PlayerVitals.TakeDamage,
    /// Heal and RestoreFullness all early-out unless IsServerInitialized). That makes these two
    /// options host-only: when someone else hosts the lobby there is no client-side path to them.
    /// </summary>
    internal static class Vitals
    {
        private const int Max = 100;

        internal static bool IsHost
        {
            get
            {
                var v = Local;
                return v != null && v.IsServerInitialized;
            }
        }

        private static PlayerVitals Local
        {
            get
            {
                var p = Player.LocalPlayer;
                return p != null ? p.Vitals : null;
            }
        }

        internal static void Tick()
        {
            if (!Cfg.UnlimitedHealth && !Cfg.UnlimitedFood) return;

            var v = Local;
            if (v == null || !v.IsServerInitialized) return;

            if (Cfg.UnlimitedHealth && v._syncedHealth.Value < Max) v._syncedHealth.Value = Max;
            if (Cfg.UnlimitedFood && v._syncedFullness.Value < Max) v._syncedFullness.Value = Max;
        }
    }
}
