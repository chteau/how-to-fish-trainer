using System.Collections.Generic;
using UnityEngine;

namespace HtfTrainer
{
    internal enum MlgState { Idle, Prep, Spin, Snap, Cooldown }

    /// <summary>
    /// Orchestrates a scored trick shot out of the bonuses the game already awards in
    /// KillScoreCalculator.
    ///
    /// The subtlety worth knowing: those bonuses are evaluated when the projectile *lands*, not when
    /// it is fired (Creature.LocalHit and PlayerVitals.LocalHit both call the calculator on impact).
    /// So the 360 has to still be inside its one-second window and the player still off the ground at
    /// impact, which is why the jump happens first, the spin is short, and the shot leaves as soon as
    /// the spin completes.
    ///
    /// Targets may be creatures or players. Projectiles damage players through the same path
    /// (ProjectileManager.Hit resolves the body part and calls PlayerVitals.LocalHit with
    /// rangedHit: true), so player trick shots score real bonuses - though the player bonus list has
    /// no headshot entry, and friendly fire must be on for the damage to land at all.
    /// </summary>
    internal static class Mlg
    {
        /// <summary>Comfortably past the game's 320 degree threshold without overshooting the window.</summary>
        private const float SpinDegrees = 340f;
        private const float NoScopeSettle = 0.28f; // PlayerSkills needs 0.25s since ADS stopped
        private const float SnapTimeout = 0.7f;
        private const float CooldownTime = 0.35f;
        private const float Timeout = 3f;

        internal static MlgState State { get; private set; } = MlgState.Idle;
        internal static AimTarget Target { get; private set; } = AimTarget.None;
        internal static string LastResult { get; private set; } = "";

        private static float _triggeredAt;
        private static float _stateEnteredAt;
        private static float _adsReleasedAt;
        private static float _spinAccumulated;
        private static float _spinTotal;
        private static Vector3 _solved;

        internal static bool Running => State != MlgState.Idle && State != MlgState.Cooldown;

        internal static void Trigger()
        {
            if (!Cfg.MlgEnabled || Running) return;
            if (!Held.HasFirearm) { LastResult = "no firearm equipped"; return; }
            if (Held.Ammo <= 0) { LastResult = "no ammo"; return; }

            var player = Player.LocalPlayer;
            if (player == null || player.CamObject == null) return;

            Target = PickTarget(player.CamObject.position);
            if (!Target.Exists) { LastResult = "no eligible target"; return; }

            if (Target.IsPlayer && !Fun.FriendlyFireOn)
                LastResult = "note: friendly fire is off, the hit will not damage them";

            Aimbot.Suspended = true;
            _triggeredAt = Time.time;
            Held.ReleaseAds();
            _adsReleasedAt = Time.time;
            Enter(MlgState.Prep);
        }

        /// <summary>
        /// Safety net: the routine is driven from the aim hook, which stops being called if the player
        /// dies or stows the weapon mid-spin. Without this the aimbot would stay suspended.
        /// </summary>
        internal static void Watchdog()
        {
            if (!Running) return;
            if (Time.time - _triggeredAt > Timeout) { Abort("timed out"); return; }
            if (Player.LocalPlayer == null || !Held.HasFirearm) Abort("weapon lost");
        }

        internal static void Abort(string why)
        {
            if (State == MlgState.Idle) return;
            Target = AimTarget.None;
            Aimbot.Suspended = false;
            LastResult = why;
            Enter(MlgState.Cooldown);
        }

        private static void Enter(MlgState state)
        {
            State = state;
            _stateEnteredAt = Time.time;
        }

        private static float Elapsed => Time.time - _stateEnteredAt;

        /// <summary>Per-frame driver. Returns this frame's camera correction while the routine owns the aim.</summary>
        internal static bool TryGetDelta(Vector3 camPos, Vector3 camEuler, out Vector2 delta)
        {
            delta = Vector2.zero;

            switch (State)
            {
                case MlgState.Idle:
                    return false;

                case MlgState.Cooldown:
                    if (Elapsed >= CooldownTime) State = MlgState.Idle;
                    return false;

                case MlgState.Prep:
                {
                    if (!Target.Alive) { Abort("target lost"); return false; }

                    var movement = Player.LocalPlayer != null ? Player.LocalPlayer.Movement : null;
                    if (Cfg.MlgJump && movement != null && movement.Grounded) movement.Jump();

                    if (!Resolve()) { Abort("no firing solution"); return false; }

                    // End the spin already facing the target so the snap is a single frame.
                    var yawError = ErrorTo(camPos, camEuler).y;
                    _spinTotal = SpinDegrees + Mathf.Repeat(yawError, 360f);
                    _spinAccumulated = 0f;
                    Enter(MlgState.Spin);
                    return false;
                }

                case MlgState.Spin:
                {
                    if (!Target.Alive) { Abort("target lost"); return false; }

                    var duration = Mathf.Max(0.12f, Cfg.MlgSpinDuration);
                    var step = _spinTotal * (Time.deltaTime / duration);
                    var remaining = _spinTotal - _spinAccumulated;
                    if (step > remaining) step = remaining;
                    _spinAccumulated += step;

                    // Bring the pitch in over the back half of the spin so only yaw is left to settle.
                    var pitchStep = 0f;
                    if (_spinAccumulated > _spinTotal * 0.5f && Resolve())
                        pitchStep = ErrorTo(camPos, camEuler).x *
                                    Mathf.Clamp01(Time.deltaTime / duration * 2.5f);

                    delta = new Vector2(pitchStep, step);

                    if (_spinAccumulated >= _spinTotal - 0.01f) Enter(MlgState.Snap);
                    return true;
                }

                case MlgState.Snap:
                {
                    if (!Target.Alive) { Abort("target lost"); return false; }
                    if (Elapsed > SnapTimeout) { Abort("snap timed out"); return false; }
                    if (!Resolve()) { Abort("no firing solution"); return false; }

                    var error = ErrorTo(camPos, camEuler);
                    delta = error; // instant snap, the trick shot is already committed

                    var tolerance = Aimbot.LockAngleFromRadius(Target.AngularRadiusDeg(camPos));
                    var onTarget = error.magnitude <= tolerance;
                    var noScopeReady = !Cfg.MlgNoScope || Time.time - _adsReleasedAt >= NoScopeSettle;

                    if (onTarget && noScopeReady)
                    {
                        var preview = PreviewBonuses();
                        Held.Fire();
                        LastResult = preview;
                        Target = AimTarget.None;
                        Aimbot.Suspended = false;
                        Enter(MlgState.Cooldown);
                    }
                    return true;
                }
            }

            return false;
        }

        /// <summary>Camera correction needed so the barrel lines up with the firing solution.</summary>
        private static Vector2 ErrorTo(Vector3 camPos, Vector3 camEuler)
        {
            var camTarget = Aimbot.CameraTargetFor(camPos, Held.MuzzlePosition, _solved);
            return Ballistics.DeltaTo(camPos, camEuler, camTarget);
        }

        private static bool Resolve()
        {
            if (!Target.Exists) return false;

            // A thrashing fish is a poor headshot candidate: the head is a small volume and it moves
            // faster than the body centre. Give up the 1.25x and take the kill.
            var head = Cfg.AimAtHead && !Target.Erratic;
            var aimPoint = Target.AimPointFor(head);

            if (!Cfg.AimPredict)
            {
                _solved = aimPoint;
                return true;
            }

            return Ballistics.Solve(Held.MuzzlePosition, aimPoint, Target.LeadVelocity,
                                    Held.ProjSpeed, Held.Gravity, Time.fixedDeltaTime,
                                    out _solved, out _);
        }

        /// <summary>
        /// Scores candidates by what they would be worth: airborne targets pick up Dogfight/Fly
        /// Fishing, anything past the longshot threshold is worth swinging for, and a target holding a
        /// course is far likelier to still be there when the round arrives.
        /// </summary>
        private static AimTarget PickTarget(Vector3 camPos)
        {
            var best = AimTarget.None;
            var bestScore = float.MinValue;

            void Consider(AimTarget candidate)
            {
                if (!candidate.Alive) return;
                if ((candidate.Category & Cfg.MlgFilter) == 0) return;

                var point = candidate.Center;
                var distance = Vector3.Distance(camPos, point);
                if (distance > Cfg.AimMaxDistance) return;
                if (!Targets.Visible(camPos, point)) return;

                var score = 0f;
                if (distance >= Cfg.MlgMinDistance) score += 40f;
                else score -= (Cfg.MlgMinDistance - distance) * 2f;

                if (Cfg.MlgPreferAirborne && candidate.Airborne) score += 35f;
                if (candidate.IsBoss) score += 10f;
                score += candidate.Straightness * 45f;
                score -= distance * 0.2f;

                if (score <= bestScore) return;
                bestScore = score;
                best = candidate;
            }

            if ((Cfg.MlgFilter & (TargetFilter.SeaCreatures | TargetFilter.Seagulls | TargetFilter.Bosses)) != 0)
                foreach (var creature in Targets.Alive()) Consider(AimTarget.Of(creature));

            if ((Cfg.MlgFilter & TargetFilter.Players) != 0)
            {
                var me = Player.LocalPlayer;
                foreach (var other in PlayerManager.AlivePlayers)
                {
                    if (other == null || ReferenceEquals(other, me)) continue;
                    Consider(AimTarget.Of(other));
                }
            }

            return best;
        }

        /// <summary>Best-effort list of the bonuses this shot is set up to earn, for the readout.</summary>
        private static string PreviewBonuses()
        {
            var list = new List<string>();
            var player = Player.LocalPlayer;

            if (PlayerSkills.RecentlyDid360) list.Add("360");
            if (PlayerSkills.NoScope) list.Add("No-scope");
            else if (PlayerSkills.QuickScope) list.Add("Quickscope");

            var airborne = player != null && player.Movement != null && !player.Movement.Grounded;
            var targetAir = Target.Airborne;
            if (airborne && targetAir) list.Add("Dogfight");
            else if (airborne) list.Add("Aerial");
            else if (targetAir) list.Add("Fly fishing");

            if (player != null && Target.Exists && Target.Transform != null &&
                Vector3.Distance(Target.Transform.position, player.CamObject.position) >= PlayerSkills.LongshotDistance)
                list.Add("Longshot");

            // Players earn no headshot or endangered bonus - GetRangedPlayerBonuses has neither.
            if (!Target.IsPlayer)
            {
                if (Cfg.AimAtHead && !Target.Erratic) list.Add("Headshot");
                if (Target.IsEndangered) list.Add("Endangered");
            }

            if (Held.Ammo == 1) list.Add("Last bullet");
            if (PlayerSkills.Multikill > 0) list.Add("Multikill");
            if (list.Count >= 5) list.Add("Impressive");

            var name = Target.Name;
            return (list.Count == 0 ? "fired" : string.Join(" + ", list.ToArray())) + "  ->  " + name;
        }
    }
}
