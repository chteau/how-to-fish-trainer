using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Call targets injected directly into the game assembly by the patcher. These are public and
    /// deliberately simple, because they are invoked from hand-written IL.
    ///
    /// Using IL injection rather than a runtime patching library keeps the trainer dependency-free:
    /// Harmony drags in MonoMod and Mono.Cecil, which in turn reference System.Reflection.Emit
    /// facades that this game's Managed folder does not ship and its netstandard.dll does not
    /// forward - so Harmony cannot construct a patch here at all.
    /// </summary>
    public static class Hooks
    {
        /// <summary>
        /// Injected at the top of PlayerAimAssist.GetRotationDelta. Returning true makes that method
        /// return <paramref name="delta"/> immediately, which lands in PlayerCamera._rot through the
        /// game's own ApplyAimAssist call.
        /// </summary>
        public static bool TryGetAim(PlayerAimAssist self, Vector3 cameraPosition,
                                     Vector3 cameraEuler, out Vector2 delta)
        {
            delta = Vector2.zero;

            try
            {
                var player = Player.LocalPlayer;
                if (player == null || !ReferenceEquals(player.AimAssist, self)) return false;

                Held.Refresh();

                // The trick shot owns the camera while it runs; the aimbot stands down until it ends.
                if (Mlg.TryGetDelta(cameraPosition, cameraEuler, out delta)) return true;
                return Aimbot.TryGetDelta(cameraPosition, cameraEuler, out delta);
            }
            catch (System.Exception e)
            {
                Log.Warn("aim hook failed: " + e.Message);
                delta = Vector2.zero;
                return false;
            }
        }

        /// <summary>
        /// Injected before the return of KillScoreCalculator.GetMultiplier, which is the product of
        /// every bonus a kill earned. Creature.LocalHit feeds it to Server.SetItemMultiplier, so it
        /// scales both the kill score and the item's worth.
        /// </summary>
        public static float ScoreMultiplier(float computed)
        {
            return Cfg.ScoreOverrideEnabled ? Mathf.Max(0.01f, Cfg.ScoreOverride) : computed;
        }

        /// <summary>
        /// Injected over the argument of PlayerCamera.Recoil, which is where a shot kicks the aim.
        /// Zeroing it here covers every weapon at once and leaves the gun's own fire animation alone,
        /// so the weapon still looks alive while the crosshair stays put.
        /// </summary>
        public static Vector2 ScaleRecoil(Vector2 recoil)
        {
            return Cfg.NoRecoil ? Vector2.zero : recoil;
        }

        /// <summary>
        /// Injected over the argument of CasinoManager.ServerRouletteResult. Rewriting the argument
        /// rather than the ball's physics means the wheel still spins and sounds exactly as normal;
        /// only the payout decision changes.
        /// </summary>
        public static BetColor RigRoulette(BetColor rolled)
        {
            try { return Casino.Resolve(rolled); }
            catch (System.Exception e) { Log.Warn("roulette hook failed: " + e.Message); return rolled; }
        }

        /// <summary>
        /// Injected before every return in Player.BlockInputs.
        ///
        /// PlayerHolding routes left click through `if (!_player.BlockInputs)`, so without this every
        /// click on a menu widget would also fire the weapon. Reporting "blocked" while the panel is
        /// open reuses the game's own pause path rather than trying to swallow the input.
        /// </summary>
        public static bool BlockInputs(bool blocked) => blocked || Menu.Open;

        /// <summary>
        /// Injected before every return in Attachments.Damage, taking the computed value off the
        /// stack and handing back the scaled one. That property feeds both the projectile's damage
        /// (captured at spawn in ProjectileManager) and the point-blank raycast path in Weapon.Shoot.
        /// </summary>
        public static int ScaleDamage(int damage)
        {
            if (!Cfg.DamageEnabled) return damage;
            return Mathf.Max(1, Mathf.RoundToInt(damage * Mathf.Max(0.01f, Cfg.DamageMult)));
        }
    }
}
