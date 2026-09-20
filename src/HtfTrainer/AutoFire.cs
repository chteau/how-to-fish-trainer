using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Pulls the trigger once the aimbot reports a lock, and holds fire whenever another player is
    /// standing anywhere along the shot. The check walks the actual projectile arc rather than a
    /// straight line, and advances both the bullet and the other players over the flight time, so a
    /// teammate who is about to walk into a long shot still blocks it.
    /// </summary>
    internal static class AutoFire
    {
        private const float SafetyRadius = 0.75f;
        private const int PathSamples = 12;
        private const float PlayerFeetOffset = 0.2f;
        private const float PlayerHeadOffset = 1.8f;
        /// <summary>Distance sampled ahead for hitscan shots, which have no flight time to walk.</summary>
        private const float HitscanProbeRange = 400f;

        internal static bool Blocked { get; private set; }

        internal static void Tick()
        {
            Blocked = false;

            if (!Cfg.AutoFire || !Aimbot.Active || !Aimbot.IsLocked) return;
            if (Aimbot.Target == null || Held.Weapon == null) return;
            if (Held.Ammo <= 0 || Held.IsReloading) return;

            var muzzle = Held.MuzzlePosition;

            if (Cfg.AutoFireHoldForPlayers && PlayerInLineOfFire(muzzle, Aimbot.SolvedPoint, Aimbot.TravelTime))
            {
                Blocked = true;
                return;
            }

            Held.Fire();
        }

        private static bool PlayerInLineOfFire(Vector3 muzzle, Vector3 solved, float travelTime)
        {
            var others = PlayerManager.AlivePlayers;
            if (others == null || others.Count == 0) return false;

            var local = Player.LocalPlayer;
            var dir = solved - muzzle;
            if (dir.sqrMagnitude < 1e-6f) return false;
            dir.Normalize();

            var speed = Held.ProjSpeed;
            var hitscan = speed <= 0.01f || travelTime <= 0.001f;
            var dt = Time.fixedDeltaTime;

            foreach (var other in others)
            {
                if (other == null || ReferenceEquals(other, local)) continue;
                var tf = other.Transform;
                if (tf == null) continue;

                var rb = other.Rigidbody;
                var vel = rb != null ? rb.linearVelocity : Vector3.zero;

                for (var i = 0; i <= PathSamples; i++)
                {
                    var f = (float)i / PathSamples;

                    Vector3 bullet;
                    float t;
                    if (hitscan)
                    {
                        // No flight time to walk: sample straight out to the impact point.
                        t = 0f;
                        var reach = Mathf.Min(HitscanProbeRange, Vector3.Distance(muzzle, solved));
                        bullet = muzzle + dir * (reach * f);
                    }
                    else
                    {
                        t = travelTime * f;
                        bullet = muzzle + dir * (speed * t) - Vector3.up * Ballistics.Drop(Held.Gravity, t, dt);
                    }

                    var basePos = tf.position + vel * t;
                    var feet = basePos + Vector3.up * PlayerFeetOffset;
                    var head = basePos + Vector3.up * PlayerHeadOffset;

                    if (PointToSegment(bullet, feet, head) <= SafetyRadius) return true;
                }
            }

            return false;
        }

        private static float PointToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-6f) return Vector3.Distance(point, a);
            var t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lenSq);
            return Vector3.Distance(point, a + ab * t);
        }
    }
}
