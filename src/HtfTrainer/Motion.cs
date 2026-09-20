using System.Collections.Generic;
using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Short motion history per creature, used to lead shots sensibly.
    ///
    /// Reading Rigidbody.linearVelocity straight off a fish is a poor predictor: they flop and dart,
    /// so a single instant can point anywhere and the solver then leads the shot into empty water.
    /// This keeps a ~0.3s window of positions and derives two things from it:
    ///
    ///   velocity     — averaged over the window, which filters the per-frame jitter
    ///   straightness — |average velocity| / average speed, in 0..1
    ///
    /// Straightness is the useful part. A fish swimming in a line scores near 1; one thrashing in
    /// place scores near 0, because the displacements cancel out while the distance travelled does
    /// not. Scaling the lead by it collapses the prediction toward the target's current position
    /// exactly when its motion is unpredictable — which is the correct estimate for random movement.
    /// </summary>
    internal static class Motion
    {
        private const int Samples = 8;
        private const float Interval = 0.04f;
        private const float PruneInterval = 10f;
        /// <summary>Below this, a target is treated as thrashing rather than travelling.</summary>
        internal const float ErraticBelow = 0.45f;

        private sealed class Track
        {
            internal readonly Vector3[] Pos = new Vector3[Samples];
            internal readonly float[] At = new float[Samples];
            internal int Count;
            internal int Head;
            internal float NextSample;
            internal Creature Owner;
        }

        private static readonly Dictionary<EntityId, Track> Tracks = new Dictionary<EntityId, Track>();
        private static readonly List<EntityId> Stale = new List<EntityId>();
        private static float _nextPrune;

        internal static void SampleAll()
        {
            var now = Time.time;

            foreach (var creature in Targets.Alive())
            {
                var id = creature.GetEntityId();
                if (!Tracks.TryGetValue(id, out var track))
                {
                    track = new Track { Owner = creature };
                    Tracks[id] = track;
                }

                if (now < track.NextSample) continue;
                track.NextSample = now + Interval;

                track.Pos[track.Head] = Targets.Center(creature);
                track.At[track.Head] = now;
                track.Head = (track.Head + 1) % Samples;
                if (track.Count < Samples) track.Count++;
            }

            if (now < _nextPrune) return;
            _nextPrune = now + PruneInterval;

            Stale.Clear();
            foreach (var pair in Tracks)
                if (pair.Value.Owner == null) Stale.Add(pair.Key);
            foreach (var key in Stale) Tracks.Remove(key);
            Stale.Clear();
        }

        /// <summary>Ordered oldest to newest. Returns false when there is not enough history yet.</summary>
        private static bool Window(Creature c, out Vector3 oldest, out Vector3 newest,
                                   out float span, out float pathLength)
        {
            oldest = newest = Vector3.zero;
            span = pathLength = 0f;

            if (c == null || !Tracks.TryGetValue(c.GetEntityId(), out var track) || track.Count < 3)
                return false;

            var first = (track.Head - track.Count + Samples) % Samples;
            var last = (track.Head - 1 + Samples) % Samples;

            oldest = track.Pos[first];
            newest = track.Pos[last];
            span = track.At[last] - track.At[first];
            if (span <= 0.001f) return false;

            for (var i = 1; i < track.Count; i++)
            {
                var a = (first + i - 1) % Samples;
                var b = (first + i) % Samples;
                pathLength += Vector3.Distance(track.Pos[a], track.Pos[b]);
            }

            return true;
        }

        /// <summary>Window-averaged velocity, falling back to the rigidbody before history exists.</summary>
        internal static Vector3 Velocity(Creature c)
        {
            if (!Window(c, out var oldest, out var newest, out var span, out _))
            {
                var rig = c != null ? c.Rig : null;
                return rig != null ? rig.linearVelocity : Vector3.zero;
            }
            return (newest - oldest) / span;
        }

        /// <summary>0 = thrashing in place, 1 = travelling in a straight line.</summary>
        internal static float Straightness(Creature c)
        {
            if (!Window(c, out var oldest, out var newest, out var span, out var pathLength))
                return 1f; // no evidence of erratic movement yet, so do not damp the lead

            if (pathLength <= 0.0001f) return 1f; // stationary: leading by zero either way
            return Mathf.Clamp01(Vector3.Distance(oldest, newest) / pathLength);
        }

        internal static bool IsErratic(Creature c) => Straightness(c) < ErraticBelow;

        /// <summary>
        /// Velocity to actually lead with: full for a target holding a course, damped toward zero as
        /// its movement becomes unpredictable.
        /// </summary>
        internal static Vector3 LeadVelocity(Creature c)
        {
            if (!Cfg.AimPredict) return Vector3.zero;
            if (!Cfg.AimDampErratic) return Velocity(c);
            return Velocity(c) * Straightness(c);
        }

        internal static void Clear() => Tracks.Clear();
    }
}
