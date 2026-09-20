using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Solves where to point so a travelling projectile meets a moving target.
    ///
    /// The game integrates every projectile once per FixedUpdate, advancing position *before*
    /// applying gravity (see ProjectileManager.UpdateProjectilePos):
    ///     position += velocity * dt;
    ///     velocity += Vector3.down * gravity * dt;
    /// Unrolling n steps with t = n*dt gives a vertical drop of
    ///     drop(t) = 0.5 * gravity * (t^2 - t*dt)
    /// which is the standard 0.5*g*t^2 minus a half-step correction. Matching that exactly is what
    /// keeps long shots landing instead of falling slightly short.
    /// </summary>
    internal static class Ballistics
    {
        private const int MaxIterations = 10;
        private const float ConvergenceEpsilon = 0.0002f;
        private const float MaxTravelTime = 6f;

        internal static float Drop(float gravity, float t, float dt)
        {
            if (gravity <= 0f) return 0f;
            return 0.5f * gravity * (t * t - t * dt);
        }

        /// <summary>
        /// Finds the world point to aim at so the projectile intercepts the target.
        /// Returns false when the target simply cannot be reached (too fast, too far, diverging).
        /// </summary>
        internal static bool Solve(Vector3 origin, Vector3 targetPos, Vector3 targetVel,
                                   float speed, float gravity, float dt,
                                   out Vector3 aimPoint, out float travelTime)
        {
            aimPoint = targetPos;
            travelTime = 0f;

            // Hitscan weapons resolve instantly: no lead, no drop.
            if (speed <= 0.01f) return true;

            var t = Vector3.Distance(origin, targetPos) / speed;

            for (var i = 0; i < MaxIterations; i++)
            {
                var predicted = targetPos + targetVel * t;
                var candidate = predicted + Vector3.up * Drop(gravity, t, dt);
                var next = Vector3.Distance(origin, candidate) / speed;

                if (float.IsNaN(next) || float.IsInfinity(next) || next > MaxTravelTime)
                {
                    aimPoint = targetPos;
                    travelTime = 0f;
                    return false;
                }

                aimPoint = candidate;
                var converged = Mathf.Abs(next - t) < ConvergenceEpsilon;
                t = next;
                if (converged) break;
            }

            travelTime = t;
            return true;
        }

        /// <summary>Euler angles that look from <paramref name="from"/> toward <paramref name="to"/>.</summary>
        internal static Vector3 LookEuler(Vector3 from, Vector3 to)
        {
            var dir = to - from;
            return dir.sqrMagnitude < 1e-6f
                ? Vector3.zero
                : Quaternion.LookRotation(dir, Vector3.up).eulerAngles;
        }

        /// <summary>
        /// Pitch/yaw correction to apply to the camera euler so it points at <paramref name="target"/>.
        /// Matches the layout the game's own aim assist feeds back into PlayerCamera._rot:
        /// x = pitch delta, y = yaw delta, both in degrees.
        /// </summary>
        internal static Vector2 DeltaTo(Vector3 camPos, Vector3 camEuler, Vector3 target)
        {
            var wanted = LookEuler(camPos, target);
            return new Vector2(Mathf.DeltaAngle(camEuler.x, wanted.x),
                               Mathf.DeltaAngle(camEuler.y, wanted.y));
        }
    }
}
