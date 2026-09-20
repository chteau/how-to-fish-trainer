using System.Reflection;
using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Catch rate and item value. All server-side, so host-only.
    ///
    /// Item.TotalWorth is _worth * randomWeight * CooknessCurve(cookness) * bettingMultiplier *
    /// killScoreMultiplier, and the first three are plain SyncVars — so inflating weight and parking
    /// cookness on the curve's peak is the whole money cheat.
    /// </summary>
    internal static class Fishing
    {
        private const float ApplyInterval = 0.5f;

        private static FieldInfo _catchTime;
        private static float _nextApply;
        private static float _bestCookness = -1f;

        internal static bool IsHost =>
            Server.Instance != null && Server.Instance.IsServerInitialized;

        /// <summary>Cookness that maximises worth, found by sampling the game's own curve.</summary>
        internal static float BestCookness
        {
            get
            {
                if (_bestCookness >= 0f) return _bestCookness;

                var curve = GameInfo.CooknessWorthCurve;
                if (curve == null) return 1f;

                var best = 0f;
                var bestValue = float.MinValue;
                for (var t = 0f; t <= 2f; t += 0.02f)
                {
                    var v = curve.Evaluate(t);
                    if (v <= bestValue) continue;
                    bestValue = v;
                    best = t;
                }

                _bestCookness = best;
                return best;
            }
        }

        internal static void Tick()
        {
            if (!IsHost) return;
            if (Cfg.InstantBite) ApplyInstantBite();

            // Worth edits are SyncVar writes, so they are throttled rather than run every frame.
            if (Time.time < _nextApply) return;
            _nextApply = Time.time + ApplyInterval;

            if (Cfg.MaxWeight || Cfg.PerfectCook) ApplyWorth();
        }

        private static void ApplyInstantBite()
        {
            if (_catchTime == null)
                _catchTime = Refl.Field(typeof(Bait), "<RandomizedCatchTime>k__BackingField");
            if (_catchTime == null) return;

            // Copy first: the game mutates this set as baits enter and leave the water.
            var baits = new Bait[Bait.BaitsUnderWater.Count];
            Bait.BaitsUnderWater.CopyTo(baits);

            foreach (var bait in baits)
            {
                if (bait == null) continue;
                if (Refl.Get(_catchTime, bait, 0f) > 0.01f) Refl.Set(_catchTime, bait, 0f);
            }
        }

        private static void ApplyWorth()
        {
            var weight = Mathf.Max(0.1f, Cfg.WeightMult);
            var cook = BestCookness;

            foreach (var item in ItemManager.Items.Values)
            {
                if (item == null || item.Creature == null) continue;

                if (Cfg.MaxWeight && !Mathf.Approximately(item._syncedRandomWeight.Value, weight))
                    item._syncedRandomWeight.Value = weight;

                if (Cfg.PerfectCook && !Mathf.Approximately(item._cookness.Value, cook))
                    item._cookness.Value = cook;
            }
        }

        internal static string UnlockAllSkins()
        {
            var items = GameInfo.ItemWithSkinsforCommands;
            if (items == null) return "no skin list available";

            var unlocked = 0;
            foreach (var item in items)
            {
                if (item == null || item.SkinPreset == null || item.SkinPreset.Skins == null) continue;
                for (byte i = 0; i < item.SkinPreset.Skins.Count; i++)
                {
                    SaveManager.UnlockSkin(item.ID, i);
                    unlocked++;
                }
            }

            return $"unlocked {unlocked} skin(s)";
        }
    }
}
