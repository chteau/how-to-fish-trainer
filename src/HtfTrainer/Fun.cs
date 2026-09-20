using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// One-shot actions built on the game's own server commands and its client-authoritative damage
    /// path. Anything that goes through Item.LocalHit or PlayerVitals.LocalHit works as a plain
    /// client, because the shooter's machine reports the hit; the rest needs the host.
    /// </summary>
    internal static class Fun
    {
        internal static string LastAction { get; private set; } = "";

        /// <summary>Lets neighbouring modules surface a result line in the UI.</summary>
        internal static void Report(string message) => LastAction = message;

        private const float PlayerHeadHeight = 1.62f;

        private static bool ServerReady =>
            Server.Instance != null && Server.Instance.IsServerInitialized;

        internal static bool IsHost => ServerReady;

        internal static bool FriendlyFireOn
        {
            get
            {
                try { return ServerSettings.Instance != null && ServerSettings.UseFriendlyFire; }
                catch { return false; }
            }
        }

        // ---- players ----

        /// <summary>
        /// Lands a simultaneous headshot on everyone else. Damage is reported by our client the same
        /// way a real shot is, so this works without hosting — but PlayerVitals.LocalHit drops the
        /// damage entirely when friendly fire is off, which only the host can change.
        /// </summary>
        internal static void HeadshotEveryone(int damage)
        {
            var me = Player.LocalPlayer;
            if (me == null) { LastAction = "no local player"; return; }

            if (!FriendlyFireOn)
            {
                LastAction = "friendly fire is off — no damage will apply";
                return;
            }

            var hit = 0;
            foreach (var other in PlayerManager.AlivePlayers)
            {
                if (other == null || ReferenceEquals(other, me) || other.Transform == null) continue;

                var head = other.Transform.position + Vector3.up * PlayerHeadHeight;
                var dir = (head - me.CamObject.position).normalized;
                other.Vitals.LocalHit(head, dir, me, damage, rangedHit: true, dir * GameInfo.PlayerKillForce);
                hit++;
            }

            LastAction = hit == 0 ? "nobody else in the lobby" : $"headshot {hit} player(s)";
        }

        /// <summary>Minimal damage, maximal launch: sends everyone skyward without killing them.</summary>
        internal static void LaunchEveryone(float force)
        {
            var me = Player.LocalPlayer;
            if (me == null) { LastAction = "no local player"; return; }

            if (!FriendlyFireOn)
            {
                LastAction = "friendly fire is off — launch needs it on";
                return;
            }

            var hit = 0;
            foreach (var other in PlayerManager.AlivePlayers)
            {
                if (other == null || ReferenceEquals(other, me) || other.Transform == null) continue;

                var point = other.Transform.position + Vector3.up;
                other.Vitals.LocalHit(point, Vector3.up, me, 1, rangedHit: false, Vector3.up * force);
                hit++;
            }

            LastAction = hit == 0 ? "nobody else in the lobby" : $"launched {hit} player(s)";
        }

        // ---- creatures ----

        internal static void KillAllCreatures()
        {
            var me = Player.LocalPlayer;
            if (me == null) return;

            var killed = 0;
            foreach (var creature in Targets.Alive().ToArray())
            {
                if (creature == null) continue;
                creature.LocalHit(creature.transform, creature.transform.position, Vector3.up,
                                  me, 999999, rangedHit: false, Vector3.zero);
                killed++;
            }

            LastAction = $"killed {killed} creature(s)";
        }

        internal static void KillBoss()
        {
            var boss = BossManager.Boss;
            var me = Player.LocalPlayer;
            if (boss == null || me == null) { LastAction = "no boss active"; return; }

            boss.LocalHit(boss.transform, boss.transform.position, Vector3.up,
                          me, 999999, rangedHit: false, Vector3.zero);
            LastAction = "boss killed";
        }

        // ---- host toggles ----

        internal static void ToggleGodMode()
        {
            if (!ServerReady) { LastAction = "host only"; return; }
            PlayerManager.ToggleGodMode();
            LastAction = "god mode " + (PlayerManager.InGodMode ? "on (everyone)" : "off");
        }

        internal static void ToggleOneShot()
        {
            if (!ServerReady || ServerSettings.Instance == null) { LastAction = "host only"; return; }
            ServerSettings.Instance.ToggleOneShot();
            LastAction = "one-shot " + (ServerSettings.OneShotEnabled ? "on" : "off");
        }

        internal static void ToggleFriendlyFire()
        {
            if (!ServerReady || ServerSettings.Instance == null) { LastAction = "host only"; return; }
            ServerSettings.Instance.ToggleFriendlyFire(!ServerSettings.UseFriendlyFire);
            LastAction = "friendly fire " + (ServerSettings.UseFriendlyFire ? "on" : "off");
        }

        internal static void NextIsland(bool previous)
        {
            if (!ServerReady) { LastAction = "host only"; return; }
            OnlineIslandManager.TpToNextIsland(previous);
            LastAction = previous ? "warped to previous island" : "warped to next island";
        }

        internal static void UnlockGrill()
        {
            if (!ServerReady) { LastAction = "host only"; return; }
            NPCManager.UnlockGrill();
            LastAction = "grill unlocked";
        }

        internal static void UnlockBoat()
        {
            if (!ServerReady || BoatManager.Boat == null) { LastAction = "host only"; return; }
            BoatManager.Boat.UnlockBoat();
            LastAction = "boat unlocked";
        }
    }
}
