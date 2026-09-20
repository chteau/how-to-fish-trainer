using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Creature ESP drawn from the real hitbox colliders. Box2D fits one CS2-style rectangle to the
    /// union of the hitboxes; Hitbox3D wireframes each collider individually (oriented for box
    /// colliders, axis-aligned otherwise).
    /// </summary>
    internal static class Esp
    {
        private static readonly Vector3[] Corners = new Vector3[8];
        private static readonly Vector2[] Projected = new Vector2[8];

        private static readonly Color DripColor = new Color(1f, 0.82f, 0.15f);
        private static readonly Color BossColor = new Color(1f, 0.35f, 0.25f);
        private static readonly Color BirdColor = new Color(0.45f, 0.85f, 1f);
        private static readonly Color FishColor = new Color(0.92f, 0.92f, 0.92f);
        private static readonly Color TargetColor = new Color(0.4f, 1f, 0.45f);
        private static readonly Color ItemColor = new Color(0.72f, 0.78f, 0.9f);
        private static readonly Color PlayerColor = new Color(1f, 0.72f, 0.28f);

        internal static void Render()
        {
            if (!Cfg.EspEnabled) return;

            var player = Player.LocalPlayer;
            if (player == null) return;

            var cam = player.CurCam;
            if (cam == null) return;

            var camPos = cam.transform.position;
            var screenH = Screen.height;
            var bottomCenter = new Vector2(Screen.width * 0.5f, screenH);

            if (Cfg.EspPlayers) DrawPlayers(cam, camPos, screenH);
            if (Cfg.EspItems) DrawItems(cam, camPos, screenH);
            if (Cfg.EspBossInfo) DrawBossInfo();

            foreach (var creature in Targets.Alive())
            {
                if (!Targets.Passes(creature, Cfg.EspFilter)) continue;

                var center = Targets.Center(creature);
                var distance = Vector3.Distance(camPos, center);
                if (distance > Cfg.EspMaxDistance) continue;
                if (Cfg.EspVisibleCheck && !Targets.Visible(camPos, center)) continue;
                if (!Targets.WorldBounds(creature, out var bounds)) continue;
                if (!ProjectBounds(cam, bounds, screenH, out var rect)) continue;

                var color = ColorFor(creature);

                if (Cfg.EspStyle == EspBoxStyle.Hitbox3D) DrawHitboxes(cam, creature, screenH, color);
                else Draw.OutlinedBox(rect, color);

                if (Cfg.EspSnapline)
                    Draw.Line(bottomCenter, new Vector2(rect.center.x, rect.yMax), color, 1f);

                if (Cfg.EspHealthBar) DrawHealthBar(rect, creature);

                var labelY = rect.yMin - 17f;
                if (Cfg.EspName)
                {
                    var name = SafeName(creature);
                    if (creature.IsDrip) name = "DRIP " + name;
                    Draw.TextCentered(rect.center.x, labelY, name, color, 12);
                    labelY -= 15f;
                }

                if (Cfg.EspDistance)
                    Draw.TextCentered(rect.center.x, rect.yMax + 3f, Mathf.RoundToInt(distance) + "m",
                                      new Color(0.93f, 0.95f, 0.98f), 12);
            }
        }

        /// <summary>Loose items: dropped catches, treasure, bait boxes. Worth is shown where known.</summary>
        private static void DrawItems(Camera cam, Vector3 camPos, float screenH)
        {
            foreach (var item in ItemManager.Items.Values)
            {
                if (item == null || item.Creature != null) continue;   // creatures drawn separately
                if (item.Holder != null) continue;                     // carried, not loose

                var center = Targets.Center(item);
                var distance = Vector3.Distance(camPos, center);
                if (distance > Cfg.EspMaxDistance) continue;
                if (!Targets.WorldBounds(item, out var bounds)) continue;
                if (!ProjectBounds(cam, bounds, screenH, out var rect)) continue;

                Draw.OutlinedBox(rect, ItemColor);

                var label = SafeItemName(item);
                var worth = SafeWorth(item);
                if (worth > 0) label += "  " + worth;
                Draw.TextCentered(rect.center.x, rect.yMin - 16f, label, ItemColor, 11);
            }
        }

        private static void DrawPlayers(Camera cam, Vector3 camPos, float screenH)
        {
            var me = Player.LocalPlayer;

            foreach (var player in PlayerManager.Players)
            {
                if (player == null || ReferenceEquals(player, me)) continue;
                var tf = player.Transform;
                if (tf == null) continue;

                var feet = tf.position;
                var distance = Vector3.Distance(camPos, feet + Vector3.up);
                if (distance > Cfg.EspMaxDistance) continue;

                // Players have no single tidy collider to fit, so use a capsule-sized box.
                var bounds = new Bounds(feet + Vector3.up * 0.95f, new Vector3(0.8f, 1.9f, 0.8f));
                if (!ProjectBounds(cam, bounds, screenH, out var rect)) continue;

                Draw.OutlinedBox(rect, PlayerColor);

                if (Cfg.EspHealthBar) DrawPlayerHealth(rect, player);
                if (Cfg.EspName)
                    Draw.TextCentered(rect.center.x, rect.yMin - 17f, SafePlayerName(player), PlayerColor, 12);
                if (Cfg.EspDistance)
                    Draw.TextCentered(rect.center.x, rect.yMax + 3f, Mathf.RoundToInt(distance) + "m",
                                      new Color(0.93f, 0.95f, 0.98f), 12);
            }
        }

        private static void DrawPlayerHealth(Rect box, Player player)
        {
            float pct;
            try { pct = Mathf.Clamp01(player.Vitals.Health / 100f); }
            catch { return; }

            var bar = new Rect(box.xMin - 6f, box.yMin, 3f, box.height);
            Draw.Rect(new Rect(bar.xMin - 1f, bar.yMin - 1f, bar.width + 2f, bar.height + 2f),
                      new Color(0f, 0f, 0f, 0.85f));
            var filled = bar.height * pct;
            Draw.Rect(new Rect(bar.xMin, bar.yMax - filled, bar.width, filled),
                      Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.95f, 0.35f), pct));
        }

        /// <summary>Boss HP and the time left before it leaves, read from BossManager's tick fields.</summary>
        private static void DrawBossInfo()
        {
            var boss = BossManager.Boss;
            if (boss == null) return;

            string line;
            try
            {
                var maxHp = Mathf.Max(1, BossManager.BossMaxHp);
                var pct = Mathf.Clamp01((float)boss.Hp / maxHp);
                line = $"{SafeName(boss)}   {boss.Hp}/{maxHp}   ({pct * 100f:0}%)";

                var now = FishNet.InstanceFinder.TimeManager.Tick;
                var leaves = BossManager.BossLeavesTick;
                var rate = Mathf.Max(1, (int)FishNet.InstanceFinder.TimeManager.TickRate);
                if (leaves > now) line += $"   ·   leaves in {(leaves - now) / rate}s";
            }
            catch { return; }

            var w = 420f;
            var box = new Rect((Screen.width - w) * 0.5f, 14f, w, 24f);
            Draw.Rect(box, new Color(0f, 0f, 0f, 0.55f));
            Draw.TextCentered(box.center.x, box.y + 4f, line, new Color(1f, 0.45f, 0.35f), 13);
        }

        private static string SafeItemName(Item item)
        {
            try { return item.GetName(); } catch { return "item"; }
        }

        private static int SafeWorth(Item item)
        {
            try { return item.TotalWorth; } catch { return 0; }
        }

        private static string SafePlayerName(Player player)
        {
            try { return string.IsNullOrEmpty(player.SteamName) ? "player" : player.SteamName; }
            catch { return "player"; }
        }

        private static Color ColorFor(Creature c)
        {
            if (ReferenceEquals(c, Aimbot.Target) || ReferenceEquals(c, Mlg.Target.Creature)) return TargetColor;
            if (c.IsDrip) return DripColor;
            if (c.BossType != BossType.None) return BossColor;
            return c is Bird ? BirdColor : FishColor;
        }

        private static string SafeName(Creature c)
        {
            try { return c.GetName(); }
            catch { return "Creature"; }
        }

        private static void DrawHealthBar(Rect box, Creature c)
        {
            var maxHp = Mathf.Max(1, c.MaxHp);
            var pct = Mathf.Clamp01((float)c.Hp / maxHp);

            var bar = new Rect(box.xMin - 6f, box.yMin, 3f, box.height);
            Draw.Rect(new Rect(bar.xMin - 1f, bar.yMin - 1f, bar.width + 2f, bar.height + 2f),
                      new Color(0f, 0f, 0f, 0.85f));

            var filled = bar.height * pct;
            Draw.Rect(new Rect(bar.xMin, bar.yMax - filled, bar.width, filled),
                      Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.95f, 0.35f), pct));
        }

        private static bool ProjectBounds(Camera cam, Bounds b, float screenH, out Rect rect)
        {
            rect = default;
            var min = b.min;
            var max = b.max;

            float xMin = float.MaxValue, yMin = float.MaxValue;
            float xMax = float.MinValue, yMax = float.MinValue;

            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);

                var sp = cam.WorldToScreenPoint(corner);
                if (sp.z <= 0.05f) return false; // any corner behind the lens: skip rather than invert

                var gx = sp.x;
                var gy = screenH - sp.y;
                if (gx < xMin) xMin = gx;
                if (gx > xMax) xMax = gx;
                if (gy < yMin) yMin = gy;
                if (gy > yMax) yMax = gy;
            }

            if (xMax <= xMin || yMax <= yMin) return false;
            rect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            return true;
        }

        private static void DrawHitboxes(Camera cam, Creature creature, float screenH, Color color)
        {
            var colliders = Targets.Hitboxes(creature);
            if (colliders == null) return;

            foreach (var col in colliders)
            {
                if (col == null || !col.enabled || col.isTrigger) continue;
                FillCorners(col);

                var visible = true;
                for (var i = 0; i < 8 && visible; i++)
                {
                    var sp = cam.WorldToScreenPoint(Corners[i]);
                    if (sp.z <= 0.05f) visible = false;
                    else Projected[i] = new Vector2(sp.x, screenH - sp.y);
                }
                if (!visible) continue;

                // Connect every pair of corners differing in exactly one axis bit: the 12 box edges.
                for (var a = 0; a < 8; a++)
                    for (var bit = 1; bit <= 4; bit <<= 1)
                    {
                        var b = a | bit;
                        if (b != a && b > a) Draw.Line(Projected[a], Projected[b], color, 1f);
                    }
            }
        }

        private static void FillCorners(Collider col)
        {
            if (col is BoxCollider box)
            {
                var c = box.center;
                var e = box.size * 0.5f;
                var tf = box.transform;
                for (var i = 0; i < 8; i++)
                {
                    var local = new Vector3(
                        c.x + ((i & 1) == 0 ? -e.x : e.x),
                        c.y + ((i & 2) == 0 ? -e.y : e.y),
                        c.z + ((i & 4) == 0 ? -e.z : e.z));
                    Corners[i] = tf.TransformPoint(local);
                }
                return;
            }

            var b = col.bounds;
            var min = b.min;
            var max = b.max;
            for (var i = 0; i < 8; i++)
                Corners[i] = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
        }
    }
}
