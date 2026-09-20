using System.Collections.Generic;
using UnityEngine;

namespace HtfTrainer
{
    /// <summary>A live creature plus the cached hitbox data the aimbot and ESP both need.</summary>
    internal sealed class TargetInfo
    {
        internal Item Owner;
        internal Collider[] Hitboxes;
        internal float NextColliderRefresh;
    }

    internal static class Targets
    {
        private static readonly Dictionary<EntityId, TargetInfo> Cache = new Dictionary<EntityId, TargetInfo>();
        private static readonly List<Creature> Scratch = new List<Creature>(128);
        private const float ColliderRefreshInterval = 2f;

        internal static TargetFilter Classify(Creature c)
        {
            if (c == null) return TargetFilter.None;
            if (c.BossType != BossType.None) return TargetFilter.Bosses;
            return c is Bird ? TargetFilter.Seagulls : TargetFilter.SeaCreatures;
        }

        internal static bool Passes(Creature c, TargetFilter filter) => (Classify(c) & filter) != 0;

        internal static bool IsAlive(Creature c) =>
            c != null && c.isActiveAndEnabled && !c.IsDeinitializing && !c.IsDead;

        /// <summary>Alive, unheld creatures currently tracked by the game's item registry.</summary>
        internal static List<Creature> Alive()
        {
            Scratch.Clear();
            foreach (var item in ItemManager.Items.Values)
            {
                if (item == null) continue;
                var c = item.Creature;
                if (!IsAlive(c)) continue;
                if (item.Holder != null) continue; // being carried, not a target
                Scratch.Add(c);
            }
            return Scratch;
        }

        /// <summary>Raw instantaneous velocity. Prefer Motion.LeadVelocity for aiming.</summary>
        internal static Vector3 Velocity(Creature c)
        {
            var rig = c != null ? c.Rig : null;
            return rig != null ? rig.linearVelocity : Vector3.zero;
        }

        internal static Vector3 Center(Item c)
        {
            if (c == null) return Vector3.zero;
            var rig = c.Rig;
            return rig != null ? rig.worldCenterOfMass : c.transform.position;
        }

        internal static Collider[] Hitboxes(Item c)
        {
            if (c == null) return null;
            var id = c.GetEntityId();
            if (!Cache.TryGetValue(id, out var info))
            {
                info = new TargetInfo { Owner = c };
                Cache[id] = info;
            }

            if (info.Hitboxes == null || Time.time >= info.NextColliderRefresh)
            {
                info.Hitboxes = c.GetComponentsInChildren<Collider>(false);
                info.NextColliderRefresh = Time.time + ColliderRefreshInterval;
            }
            return info.Hitboxes;
        }

        /// <summary>World-space AABB covering every enabled hitbox, falling back to the transform.</summary>
        internal static bool WorldBounds(Item c, out Bounds bounds)
        {
            bounds = default;
            var cols = Hitboxes(c);
            var started = false;

            if (cols != null)
            {
                foreach (var col in cols)
                {
                    if (col == null || !col.enabled || col.isTrigger) continue;
                    if (!started) { bounds = col.bounds; started = true; }
                    else bounds.Encapsulate(col.bounds);
                }
            }

            if (!started && c != null)
            {
                bounds = new Bounds(c.transform.position, Vector3.one * 0.5f);
                started = true;
            }
            return started;
        }

        /// <summary>
        /// Point to shoot for a headshot bonus. The game credits a headshot when the hit, expressed in
        /// the creature's local space, has z greater than Creature.HeadPos — so aim between that
        /// threshold and the front-most extent of the hitboxes rather than at the threshold itself.
        /// </summary>
        internal static Vector3 AimPoint(Creature c, bool preferHead)
        {
            var center = Center(c);
            if (!preferHead || c == null) return center;

            var headPos = c.HeadPos;
            if (headPos <= 0f) return center;

            var cols = Hitboxes(c);
            if (cols == null || cols.Length == 0) return center;

            var tf = c.transform;
            var maxZ = float.NegativeInfinity;

            foreach (var col in cols)
            {
                if (col == null || !col.enabled || col.isTrigger) continue;
                var b = col.bounds;
                var min = b.min;
                var max = b.max;
                for (var i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? min.x : max.x,
                        (i & 2) == 0 ? min.y : max.y,
                        (i & 4) == 0 ? min.z : max.z);
                    var z = tf.InverseTransformPoint(corner).z;
                    if (z > maxZ) maxZ = z;
                }
            }

            if (float.IsNegativeInfinity(maxZ) || maxZ <= headPos) return center;

            // Sit midway between the headshot threshold and the nose so small tracking errors still land.
            var aimZ = Mathf.Lerp(headPos, maxZ, 0.5f);
            var local = tf.InverseTransformPoint(center);
            return tf.TransformPoint(new Vector3(local.x, local.y, aimZ));
        }

        /// <summary>
        /// Half-angle the target subtends from a viewpoint, in degrees. A fixed angular tolerance is
        /// the wrong unit for "on target": 2.5 degrees is a comfortable hit at 10m and a 4m miss at
        /// 100m. Gating on the target's own angular size makes the tolerance scale correctly.
        /// </summary>
        internal static float AngularRadiusDeg(Item c, Vector3 from)
        {
            if (!WorldBounds(c, out var bounds)) return 1f;

            var e = bounds.extents;
            var radius = Mathf.Max(0.06f, Mathf.Min(e.x, Mathf.Min(e.y, e.z)));
            var distance = Mathf.Max(0.25f, Vector3.Distance(from, bounds.center));
            return Mathf.Atan2(radius, distance) * Mathf.Rad2Deg;
        }

        internal static bool Visible(Vector3 from, Vector3 to) =>
            !Physics.Linecast(from, to, (int)GameInfo.LevelLayer | (int)GameInfo.BoatLayer,
                              QueryTriggerInteraction.Ignore);

        private static float _nextPrune;
        private static readonly List<EntityId> Stale = new List<EntityId>();

        /// <summary>
        /// Drops hitbox entries for creatures that have been destroyed. Without this the cache grows
        /// for every fish caught over a long session.
        /// </summary>
        internal static void Prune()
        {
            if (Time.time < _nextPrune) return;
            _nextPrune = Time.time + 10f;

            Stale.Clear();
            foreach (var pair in Cache)
                if (pair.Value.Owner == null) Stale.Add(pair.Key);

            foreach (var key in Stale) Cache.Remove(key);
            Stale.Clear();
        }

        internal static void Forget(Creature c)
        {
            if (c != null) Cache.Remove(c.GetEntityId());
        }

        internal static void Clear() => Cache.Clear();
    }
}
