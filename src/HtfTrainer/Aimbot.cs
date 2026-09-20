using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Picks a creature, solves the interception point for the held weapon's ballistics and feeds a
    /// rotation delta back through the game's own aim-assist channel (PlayerAimAssist.GetRotationDelta),
    /// so the correction rides the normal camera pipeline instead of fighting it.
    /// </summary>
    internal static class Aimbot
    {
        private const float ScanInterval = 0.08f;
        /// <summary>How far off-target before a locked creature is dropped, relative to the acquire FOV.</summary>
        private const float BreakAngleFactor = 1.8f;

        internal static Creature Target { get; private set; }
        internal static bool IsLocked { get; private set; }
        internal static Vector3 SolvedPoint { get; private set; }
        internal static float TravelTime { get; private set; }
        /// <summary>Angular tolerance currently required to count as on target, in degrees.</summary>
        internal static float LockTolerance { get; private set; }
        internal static bool Suspended { get; set; }

        private static float _nextScan;

        internal static void Reset()
        {
            Target = null;
            IsLocked = false;
            TravelTime = 0f;
        }

        internal static bool Active =>
            Cfg.AimbotEnabled && !Suspended && Held.HasFirearm && Held.IsAiming;

        /// <summary>Computes this frame's aim correction. Returns false when the aimbot should stay silent.</summary>
        internal static bool TryGetDelta(Vector3 camPos, Vector3 camEuler, out Vector2 delta)
        {
            delta = Vector2.zero;
            IsLocked = false;

            if (!Active) { Target = null; return false; }

            var forward = Quaternion.Euler(camEuler) * Vector3.forward;

            if (!StillValid(Target, camPos, forward)) Target = null;

            if (Target == null && Time.time >= _nextScan)
            {
                Target = FindBest(camPos, forward);
                _nextScan = Time.time + ScanInterval;
            }

            if (Target == null) return false;

            var aimPoint = Targets.AimPoint(Target, Cfg.AimAtHead);
            var muzzle = Held.MuzzlePosition;
            var solved = aimPoint;
            var tof = 0f;

            if (Cfg.AimPredict)
            {
                if (!Ballistics.Solve(muzzle, aimPoint, Motion.LeadVelocity(Target),
                                      Held.ProjSpeed, Held.Gravity, Time.fixedDeltaTime,
                                      out solved, out tof))
                {
                    // Unreachable with this weapon — drop it and look for something else.
                    Target = null;
                    return false;
                }
            }

            SolvedPoint = solved;
            TravelTime = tof;

            // Steer the camera so the BARREL ends up pointing at the solution, not the camera itself.
            var camTarget = CameraTargetFor(camPos, muzzle, solved);
            var raw = Ballistics.DeltaTo(camPos, camEuler, camTarget);

            LockTolerance = LockAngleFor(Target, camPos);
            IsLocked = raw.magnitude <= LockTolerance;

            var dt = Time.deltaTime;
            // Frame-rate independent easing: the configured value is the fraction closed at 60 fps.
            var ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(Cfg.AimSmoothing), dt * 60f);
            delta = Vector2.ClampMagnitude(raw * ease, Cfg.AimMaxTurnSpeed * dt);
            return true;
        }

        /// <summary>
        /// Re-expresses a desired bullet impact point as the point the camera must look at, undoing
        /// the barrel's angular offset from the camera.
        /// </summary>
        internal static Vector3 CameraTargetFor(Vector3 camPos, Vector3 muzzle, Vector3 solved)
        {
            var wanted = solved - muzzle;
            if (wanted.sqrMagnitude < 1e-6f) return solved;

            var camDir = Held.CameraDirFor(wanted.normalized);
            return camPos + camDir * Vector3.Distance(camPos, solved);
        }

        /// <summary>
        /// Tolerance scaled to how large the target actually looks, capped by the configured maximum.
        /// A flat angle fires wildly off target at range; this does not.
        /// </summary>
        internal static float LockAngleFor(Creature target, Vector3 from) =>
            LockAngleFromRadius(Targets.AngularRadiusDeg(target, from));

        internal static float LockAngleFromRadius(float angularRadiusDeg)
        {
            var angular = angularRadiusDeg * Mathf.Max(0.05f, Cfg.AimLockTightness);
            return Mathf.Clamp(angular, 0.03f, Cfg.AimLockAngle);
        }

        private static bool StillValid(Creature c, Vector3 camPos, Vector3 forward)
        {
            if (!Targets.IsAlive(c)) return false;
            if (!Targets.Passes(c, Cfg.AimFilter)) return false;

            var point = Targets.Center(c);
            var to = point - camPos;
            var dist = to.magnitude;
            if (dist < 0.01f || dist > Cfg.AimMaxDistance) return false;

            var angle = Vector3.Angle(forward, to);
            if (angle > Cfg.AimFovDegrees * BreakAngleFactor) return false;

            return !Cfg.AimRequireVisible || Targets.Visible(camPos, point);
        }

        private static Creature FindBest(Vector3 camPos, Vector3 forward)
        {
            Creature best = null;
            var bestAngle = float.MaxValue;

            foreach (var c in Targets.Alive())
            {
                if (!Targets.Passes(c, Cfg.AimFilter)) continue;

                var point = Targets.Center(c);
                var to = point - camPos;
                var dist = to.magnitude;
                if (dist < 0.01f || dist > Cfg.AimMaxDistance) continue;

                var angle = Vector3.Angle(forward, to);
                if (angle > Cfg.AimFovDegrees) continue;
                if (angle >= bestAngle) continue;
                if (Cfg.AimRequireVisible && !Targets.Visible(camPos, point)) continue;

                bestAngle = angle;
                best = c;
            }

            return best;
        }
    }
}
