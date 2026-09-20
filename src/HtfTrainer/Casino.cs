using System.Reflection;
using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Roulette and slot machine rigging. Both run on the server, so both are host-only.
    ///
    /// The roulette is decided by physics rather than a dice roll — LocalCasino watches where the
    /// ball settles and hands the colour to CasinoManager.ServerRouletteResult. Rather than fight the
    /// ball, the patcher rewrites that method's argument, so only the payout decision changes and the
    /// wheel still looks and sounds exactly as it should.
    /// </summary>
    internal static class Casino
    {
        private static FieldInfo _curBetColor;

        internal static bool IsHost
        {
            get
            {
                var cm = CasinoManager.Instance;
                return cm != null && cm.IsServerInitialized;
            }
        }

        /// <summary>The colour the table is currently backing, or null when no bet is live.</summary>
        internal static BetColor? PlacedBet
        {
            get
            {
                if (_curBetColor == null)
                    _curBetColor = Refl.Field(typeof(CasinoManager), "_curBetColor");
                if (_curBetColor == null) return null;

                try { return (BetColor)_curBetColor.GetValue(null); }
                catch { return null; }
            }
        }

        /// <summary>
        /// Called from the injected hook with the colour the ball actually landed on.
        /// Returns the colour the payout should be settled against.
        /// </summary>
        internal static BetColor Resolve(BetColor rolled)
        {
            if (!Cfg.RigRoulette || !IsHost) return rolled;
            var bet = PlacedBet;
            return bet ?? rolled;
        }

        internal static int TotalWorth => CasinoManager.Instance != null ? CasinoManager.Instance.TotalWorth : 0;

        // ---- slot machine ----

        internal static string SlotRigStatus { get; private set; } = "not rigged";

        /// <summary>
        /// Arms the game's own slot cheat with a legendary skin, so the next pull is a guaranteed win.
        /// SlotMachineManager already supports this — it is how the dev "/slots" command works.
        /// </summary>
        internal static void RigSlots(Rarity rarity)
        {
            if (!IsHost) { SlotRigStatus = "host only"; return; }

            var items = SlotMachine.AvailableItems;
            if (items == null || items.Length == 0) { SlotRigStatus = "no slot items loaded"; return; }

            foreach (var item in items)
            {
                if (item == null) continue;
                var preset = item.SkinPreset;
                if (preset == null || preset.Skins == null) continue;

                for (byte i = 0; i < preset.Skins.Count; i++)
                {
                    if (preset.Skins[i].Rarity != rarity) continue;
                    SlotMachineManager.SetCheatSkin(item, i);
                    SlotRigStatus = $"next pull: {rarity} {SafeName(item)}";
                    return;
                }
            }

            SlotRigStatus = $"no {rarity} skin found";
        }

        internal static void ClearSlotRig()
        {
            SlotMachineManager.SetCheatSkin(null, byte.MaxValue);
            SlotRigStatus = "not rigged";
        }

        private static string SafeName(Item item)
        {
            try { return item.GetName(); } catch { return "item"; }
        }

        internal static void AddMoney(int amount)
        {
            if (!IsHost || Player.LocalPlayer == null) return;
            MoneyManager.AddMoney(amount, Player.LocalPlayer);
        }

        internal static int Money => MoneyManager.Money;
    }
}
