using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// A thing worth shooting: either a creature or another player. Lets the MLG routine treat both
    /// the same way instead of carrying two parallel code paths.
    /// </summary>
    internal readonly struct AimTarget
    {
        private const float PlayerHeadHeight = 1.62f;
        private const float PlayerChestHeight = 1.0f;
        private const float PlayerRadius = 0.4f;

        internal readonly Creature Creature;
        internal readonly Player Player;

        private AimTarget(Creature creature, Player player)
        {
            Creature = creature;
            Player = player;
        }

        internal static AimTarget Of(Creature creature) => new AimTarget(creature, null);
        internal static AimTarget Of(Player player) => new AimTarget(null, player);
        internal static AimTarget None => default;

        internal bool IsPlayer => Player != null;
        internal bool Exists => Creature != null || Player != null;

        internal bool Alive
        {
            get
            {
                if (Player != null)
                {
                    try { return Player.Transform != null && !Player.Dying.IsDead; }
                    catch { return false; }
                }
                return Targets.IsAlive(Creature);
            }
        }

        internal Transform Transform =>
            Player != null ? Player.Transform : Creature != null ? Creature.transform : null;

        internal Vector3 Center =>
            Player != null
                ? Player.Transform.position + Vector3.up * PlayerChestHeight
                : Targets.Center(Creature);

        /// <summary>
        /// Players get no headshot bonus (GetRangedPlayerBonuses does not check for one), so aiming
        /// high is purely about landing the hit rather than scoring.
        /// </summary>
        internal Vector3 AimPointFor(bool preferHead)
        {
            if (Player != null)
                return Player.Transform.position +
                       Vector3.up * (preferHead ? PlayerHeadHeight : PlayerChestHeight);

            return Targets.AimPoint(Creature, preferHead);
        }

        internal Vector3 LeadVelocity
        {
            get
            {
                if (Player == null) return Motion.LeadVelocity(Creature);
                var rig = Player.Rigidbody;
                return Cfg.AimPredict && rig != null ? rig.linearVelocity : Vector3.zero;
            }
        }

        /// <summary>Players walk rather than thrash, so their lead is never damped.</summary>
        internal float Straightness => Player != null ? 1f : Motion.Straightness(Creature);

        internal bool Erratic => Player == null && Motion.IsErratic(Creature);

        internal float AngularRadiusDeg(Vector3 from)
        {
            if (Player == null) return Targets.AngularRadiusDeg(Creature, from);
            var distance = Mathf.Max(0.25f, Vector3.Distance(from, Center));
            return Mathf.Atan2(PlayerRadius, distance) * Mathf.Rad2Deg;
        }

        internal bool Airborne
        {
            get
            {
                if (Player != null)
                {
                    try { return Player.Movement != null && !Player.Movement.Grounded; }
                    catch { return false; }
                }
                return !Physics.Raycast(Creature.transform.position, Vector3.down, 1.5f,
                                        (int)GameInfo.LevelLayer);
            }
        }

        internal bool IsBoss => Creature != null && Creature.BossType != BossType.None;
        internal bool IsEndangered => Creature != null && Creature.IsEndangered;

        internal string Name
        {
            get
            {
                try
                {
                    if (Player != null)
                        return string.IsNullOrEmpty(Player.SteamName) ? "player" : Player.SteamName;
                    return Creature.GetName();
                }
                catch { return "target"; }
            }
        }

        internal TargetFilter Category =>
            Player != null ? TargetFilter.Players : Targets.Classify(Creature);
    }
}
