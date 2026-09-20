using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Ammo, reload, rate of fire, spread and knockback on the weapon in hand.
    ///
    /// Two related things are handled elsewhere because they are not weapon fields: damage is scaled
    /// at Attachments.Damage, and the camera kick is zeroed at PlayerCamera.Recoil - both by injected
    /// hooks, since the projectile reads its damage at spawn and the kick is applied to the camera
    /// rather than stored on the gun.
    /// </summary>
    internal static class Weapons
    {
        private sealed class Stock
        {
            internal float FireDelay;
            internal float Spread;
            internal int Knockback;
        }

        /// <summary>Stock values per weapon instance, captured before any override touches them.</summary>
        private static readonly Dictionary<EntityId, Stock> Stocks = new Dictionary<EntityId, Stock>();

        private static FieldInfo _spread, _knockback;
        private static bool _resolved;

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            _spread = Refl.Field(typeof(Weapon), "_spread");
            _knockback = Refl.Field(typeof(Weapon), "_recoilKnockback");
        }

        internal static void Tick()
        {
            var weapon = Held.Weapon;
            if (weapon == null) return;

            Resolve();

            var id = weapon.GetEntityId();
            if (!Stocks.TryGetValue(id, out var stock))
            {
                stock = new Stock
                {
                    FireDelay = Held.BaseTimeBetweenShots,
                    Spread = Refl.Get(_spread, weapon, 0f),
                    Knockback = Refl.Get(_knockback, weapon, 0)
                };
                Stocks[id] = stock;
            }

            Held.SetTimeBetweenShots(Cfg.FireRateEnabled
                ? stock.FireDelay / Mathf.Max(0.05f, Cfg.FireRateMult)
                : stock.FireDelay);

            Refl.Set(_spread, weapon, Cfg.NoSpread ? 0f : stock.Spread);
            Refl.Set(_knockback, weapon, Cfg.NoRecoil ? 0 : stock.Knockback);

            var perMag = Held.AmmoPerMag;
            if (perMag <= 0) return;

            if (Cfg.UnlimitedAmmo && weapon.Ammo < perMag)
            {
                Held.SetAmmo(perMag);
                Held.CancelReload();
                Held.ClearCooldown();
            }
            else if (Cfg.NoReload && (Held.IsReloading || Held.ReloadQueued))
            {
                Held.SetAmmo(perMag);
                Held.CancelReload();
            }
        }

        /// <summary>Puts the weapon we touched back to its stock handling.</summary>
        internal static void RestoreAll()
        {
            var weapon = Held.Weapon;
            if (weapon != null && Stocks.TryGetValue(weapon.GetEntityId(), out var stock))
            {
                Held.SetTimeBetweenShots(stock.FireDelay);
                Refl.Set(_spread, weapon, stock.Spread);
                Refl.Set(_knockback, weapon, stock.Knockback);
            }
            Stocks.Clear();
        }
    }
}
