using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// The settings panel. Drawn from scratch with <see cref="Ui"/> rather than GUI.Window, because
    /// the stock skin is a translucent grey that disappears against the game's water.
    /// </summary>
    internal static class Menu
    {
        private const float TitleH = 40f;
        private const float FooterH = 34f;
        private const float SideW = 146f;
        private const float Pad = 16f;
        private const float RowH = 25f;
        private const float RowGap = 5f;
        private const float SectionH = 22f;

        private static readonly string[] Tabs = { "AIM", "MLG", "WEAPON", "MOVE", "ESP", "WORLD", "CASINO", "FUN" };
        private static readonly string[] FilterNames = { "SEA", "GULLS", "BOSSES", "PLAYERS" };

        internal static bool Open { get; private set; }
        internal static bool IsRebinding => _rebinding != null;

        private static Rect _window = new Rect(60f, 60f, 668f, 600f);
        private static int _tab;
        private static string _rebinding;
        private static string _chatText = "";
        private static string _commandText = "";
        private static bool _dragging;
        private static Vector2 _dragOffset;
        private static float _scroll;
        private static float _contentHeight;

        private static float _y;
        private static float _width;

        internal static void Toggle()
        {
            Open = !Open;
            if (!Open) { Cfg.Save(); _rebinding = null; }
            ApplyCursor();
        }

        internal static void Close()
        {
            if (!Open) return;
            Open = false;
            _rebinding = null;
            Cfg.Save();
            ApplyCursor();
        }

        /// <summary>
        /// Frees the cursor while the panel is up. Player.BlockInputs is hooked separately so clicks
        /// on these widgets do not also fire the weapon.
        /// </summary>
        internal static void ApplyCursor()
        {
            try { PlayerCamera.ToggleMouse(Open); }
            catch { /* main menu, or no local player yet */ }
        }

        internal static void Render()
        {
            if (!Open) return;

            Ui.BeginFrame();
            CaptureRebind();
            ClampToScreen();

            Ui.Fill(_window, Ui.Backdrop);
            Ui.Stroke(_window, Ui.Line);

            DrawTitleBar();
            DrawSidebar();
            DrawContent();
            DrawFooter();

            // Swallow any click that landed on the panel but missed a widget.
            var e = Event.current;
            if (e != null && e.type == EventType.MouseDown && _window.Contains(e.mousePosition)) e.Use();
        }

        private static void ClampToScreen()
        {
            _window.x = Mathf.Clamp(_window.x, -_window.width + 80f, Screen.width - 80f);
            _window.y = Mathf.Clamp(_window.y, 0f, Screen.height - TitleH);
        }

        private static void DrawTitleBar()
        {
            var bar = new Rect(_window.x, _window.y, _window.width, TitleH);
            Ui.Fill(bar, Ui.Surface);
            Ui.Fill(new Rect(bar.x, bar.yMax - 1f, bar.width, 1f), Ui.Line);
            Ui.Fill(new Rect(bar.x, bar.y, 3f, bar.height), Ui.Accent);

            Ui.Text2(new Rect(bar.x + 16f, bar.y, 220f, bar.height), "HOW TO FISH",
                     Ui.Text, Ui.SizeTitle, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Text2(new Rect(bar.x + 136f, bar.y, 120f, bar.height), "TRAINER",
                     Ui.Accent, Ui.SizeTitle, TextAnchor.MiddleLeft, FontStyle.Bold);

            var host = Vitals.IsHost;
            Ui.Chip(new Rect(bar.xMax - 132f, bar.y + 11f, 62f, 18f),
                    host ? "HOST" : "CLIENT", host ? Ui.Good : Ui.Muted);

            var close = new Rect(bar.xMax - 44f, bar.y + 8f, 28f, 24f);
            if (Ui.Button(close, "X")) { Close(); return; }

            HandleDrag(new Rect(bar.x, bar.y, bar.width - 140f, bar.height));
        }

        private static void HandleDrag(Rect grip)
        {
            var e = Event.current;
            if (e == null) return;

            if (e.type == EventType.MouseDown && e.button == 0 && grip.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragOffset = e.mousePosition - new Vector2(_window.x, _window.y);
                e.Use();
            }
            else if (_dragging && e.type == EventType.MouseDrag)
            {
                _window.x = e.mousePosition.x - _dragOffset.x;
                _window.y = e.mousePosition.y - _dragOffset.y;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _dragging = false;
            }
        }

        private static void DrawSidebar()
        {
            var side = new Rect(_window.x, _window.y + TitleH, SideW,
                                _window.height - TitleH - FooterH);
            Ui.Fill(side, Ui.Surface);
            Ui.Fill(new Rect(side.xMax - 1f, side.y, 1f, side.height), Ui.Line);

            for (var i = 0; i < Tabs.Length; i++)
            {
                var row = new Rect(side.x, side.y + 10f + i * 34f, side.width - 1f, 32f);
                var on = i == _tab;

                if (on)
                {
                    Ui.Fill(row, Ui.SurfaceAlt);
                    Ui.Fill(new Rect(row.x, row.y, 3f, row.height), Ui.Accent);
                }

                Ui.Text2(new Rect(row.x + 18f, row.y, row.width - 18f, row.height), Tabs[i],
                         on ? Ui.Text : Ui.Muted, Ui.SizeBody, TextAnchor.MiddleLeft,
                         on ? FontStyle.Bold : FontStyle.Normal);

                var e = Event.current;
                if (e != null && e.type == EventType.MouseDown && e.button == 0 &&
                    row.Contains(e.mousePosition))
                {
                    _tab = i;
                    _scroll = 0f;
                    e.Use();
                }
            }
        }

        private static void DrawContent()
        {
            var area = new Rect(_window.x + SideW, _window.y + TitleH,
                                _window.width - SideW, _window.height - TitleH - FooterH);

            var e = Event.current;
            if (e != null && e.type == EventType.ScrollWheel && area.Contains(e.mousePosition))
            {
                _scroll = Mathf.Clamp(_scroll + e.delta.y * 16f, 0f,
                                      Mathf.Max(0f, _contentHeight - area.height + Pad));
                e.Use();
            }

            GUI.BeginClip(area);
            _width = area.width - Pad * 2f;
            _y = Pad - _scroll;

            switch (_tab)
            {
                case 0: TabAim(); break;
                case 1: TabMlg(); break;
                case 2: TabWeapon(); break;
                case 3: TabMove(); break;
                case 4: TabEsp(); break;
                case 5: TabWorld(); break;
                case 6: TabCasino(); break;
                case 7: TabFun(); break;
            }

            _contentHeight = _y + _scroll;
            GUI.EndClip();
        }

        private static void DrawFooter()
        {
            var bar = new Rect(_window.x, _window.yMax - FooterH, _window.width, FooterH);
            Ui.Fill(bar, Ui.Surface);
            Ui.Fill(new Rect(bar.x, bar.y, bar.width, 1f), Ui.Line);

            Ui.Text2(new Rect(bar.x + 16f, bar.y, bar.width - 200f, bar.height),
                     StatusLine(), Ui.Muted, Ui.SizeSmall);
            Ui.Text2(new Rect(bar.xMax - 190f, bar.y, 174f, bar.height),
                     $"{Cfg.MenuKey} close   ·   {Cfg.MlgKey} MLG",
                     Ui.Faint, Ui.SizeSmall, TextAnchor.MiddleRight);
        }

        private static string StatusLine()
        {
            if (Mlg.Running) return "MLG: " + Mlg.State;
            if (AutoFire.Blocked) return "auto fire held - player in the line of fire";
            if (Aimbot.Target != null)
                return "target: " + SafeName(Aimbot.Target) + (Aimbot.IsLocked ? "  ·  on target" : "  ·  tracking");
            if (Mlg.LastResult.Length > 0) return "last MLG: " + Mlg.LastResult;
            return Held.HasFirearm ? "firearm ready" : "no firearm equipped";
        }

        private static string SafeName(Creature c)
        {
            try { return c.GetName(); } catch { return "creature"; }
        }

        // ---- layout helpers ----

        private static Rect Row(float h = RowH)
        {
            var r = new Rect(Pad, _y, _width, h);
            _y += h + RowGap;
            return r;
        }

        private static void Section(string title)
        {
            _y += 10f;
            Ui.SectionLabel(new Rect(Pad, _y, _width, SectionH), title);
            _y += SectionH + RowGap + 2f;
        }

        private static void Toggle(string label, ref bool value, bool enabled = true) =>
            value = Ui.Toggle(Row(), label, value, enabled);

        private static void Slider(string label, ref float value, float min, float max,
                                   string format = "0.0", bool enabled = true) =>
            value = Ui.Slider(Row(), label, value, min, max, format, enabled);

        private static void Note(string text, Color tint)
        {
            var r = Row(20f);
            Ui.Note(r, text, tint);
        }

        private static bool Action(string label, bool enabled = true)
        {
            var r = Row(28f);
            return Ui.Button(r, label, false, enabled);
        }

        private static bool[] ActionPair(string left, string right, bool enabled = true)
        {
            var r = Row(28f);
            var half = (r.width - 8f) * 0.5f;
            return new[]
            {
                Ui.Button(new Rect(r.x, r.y, half, r.height), left, false, enabled),
                Ui.Button(new Rect(r.x + half + 8f, r.y, half, r.height), right, false, enabled)
            };
        }

        private static void TabCasino()
        {
            var host = Casino.IsHost;

            Section("Roulette" + (host ? "" : "  (host only)"));
            Toggle("Always win", ref Cfg.RigRoulette, host);
            var bet = Casino.PlacedBet;
            Note(bet.HasValue
                    ? $"table is backing {bet.Value}  ·  pot {Casino.TotalWorth}"
                    : "no bet placed",
                 Ui.AccentDim);
            Note("Green pays 35x, red and black pay 2x - bet green with this on.", Ui.LineSoft);

            Section("Slot machine" + (host ? "" : "  (host only)"));
            var slots = ActionPair("RIG LEGENDARY", "RIG RARE", host);
            if (slots[0]) Casino.RigSlots(Rarity.Legendary);
            if (slots[1]) Casino.RigSlots(Rarity.Rare);
            if (Action("CLEAR RIG", host)) Casino.ClearSlotRig();
            Note(Casino.SlotRigStatus, Ui.Accent);

            Section("Money" + (host ? "" : "  (host only)"));
            var money = ActionPair("+ 9,999", "+ 100,000", host);
            if (money[0]) Casino.AddMoney(9999);
            if (money[1]) Casino.AddMoney(100000);
            Note($"balance: {Casino.Money}", Ui.Good);
        }

        private static void TabFun()
        {
            var host = Fun.IsHost;
            var ff = Fun.FriendlyFireOn;

            Section("Players");
            Note(ff ? "friendly fire is ON" : "friendly fire is OFF - player pranks do nothing",
                 ff ? Ui.Good : Ui.Warn);
            if (Action("HEADSHOT EVERYONE", ff)) Fun.HeadshotEveryone(1000);
            if (Action("LAUNCH EVERYONE", ff)) Fun.LaunchEveryone(Cfg.LaunchForce);
            Slider("Launch force", ref Cfg.LaunchForce, 10f, 300f, "0");

            Section("Creatures");
            var creatures = ActionPair("KILL ALL CREATURES", "KILL BOSS");
            if (creatures[0]) Fun.KillAllCreatures();
            if (creatures[1]) Fun.KillBoss();
            Note("Both go through the normal client hit path, so they work without hosting.",
                 Ui.LineSoft);

            Section("Host toggles" + (host ? "" : "  (host only)"));
            var toggles = ActionPair("GOD MODE", "ONE-SHOT", host);
            if (toggles[0]) Fun.ToggleGodMode();
            if (toggles[1]) Fun.ToggleOneShot();
            if (Action("TOGGLE FRIENDLY FIRE", host)) Fun.ToggleFriendlyFire();
            Note($"god mode {(PlayerManager.InGodMode ? "on" : "off")}" +
                 $"  ·  one-shot {(SafeOneShot() ? "on" : "off")}", Ui.AccentDim);

            Section("World" + (host ? "" : "  (host only)"));
            var islands = ActionPair("PREV ISLAND", "NEXT ISLAND", host);
            if (islands[0]) Fun.NextIsland(previous: true);
            if (islands[1]) Fun.NextIsland(previous: false);
            var unlocks = ActionPair("UNLOCK GRILL", "UNLOCK BOAT", host);
            if (unlocks[0]) Fun.UnlockGrill();
            if (unlocks[1]) Fun.UnlockBoat();

            Section("Chat");
            var chatRow = Row(26f);
            _chatText = Ui.TextField(new Rect(chatRow.x, chatRow.y, chatRow.width - 88f, chatRow.height),
                                     _chatText, "htf_chat");
            if (Ui.Button(new Rect(chatRow.xMax - 80f, chatRow.y, 80f, chatRow.height), "SEND"))
            {
                Commands.SendChat(_chatText);
                _chatText = "";
            }
            Note("Goes to everyone in the lobby, shown as you.", Ui.LineSoft);

            Section("Dev console" + (Commands.CanRunCommands ? "" : "  (host only)"));
            var cmdRow = Row(26f);
            _commandText = Ui.TextField(new Rect(cmdRow.x, cmdRow.y, cmdRow.width - 88f, cmdRow.height),
                                        _commandText, "htf_cmd");
            if (Ui.Button(new Rect(cmdRow.xMax - 80f, cmdRow.y, 80f, cmdRow.height), "RUN",
                          false, Commands.CanRunCommands))
                Commands.Run(_commandText);
            Note("The game's own commands: spawn, spawndrip, killboss, allskins,", Ui.LineSoft);
            Note("nextisland, finishgame, unlockachievements, oneshot, godmode…", Ui.LineSoft);
            Note(Commands.Last.Length > 0 ? Commands.Last : "nothing run yet", Ui.Accent);

            Section("Last action");
            Note(Fun.LastAction.Length > 0 ? Fun.LastAction : "nothing yet", Ui.Accent);
        }

        private static bool SafeOneShot()
        {
            try { return ServerSettings.Instance != null && ServerSettings.OneShotEnabled; }
            catch { return false; }
        }

        private static readonly TargetFilter[] FilterBits =
        {
            TargetFilter.SeaCreatures, TargetFilter.Seagulls, TargetFilter.Bosses, TargetFilter.Players
        };

        private static void FilterRow(string label, ref TargetFilter filter, bool enabled = true,
                                      bool includePlayers = false)
        {
            var count = includePlayers ? 4 : 3;
            var r = Row();
            Ui.Text2(new Rect(r.x, r.y, 128f, r.height), label, enabled ? Ui.Text : Ui.Faint);

            var names = new string[count];
            var states = new bool[count];
            for (var i = 0; i < count; i++)
            {
                names[i] = FilterNames[i];
                states[i] = (filter & FilterBits[i]) != 0;
            }

            var seg = new Rect(r.x + 128f, r.y + 2f, r.width - 128f, r.height - 4f);
            var hit = Ui.MultiSegmented(seg, names, states, enabled);
            if (hit < 0) return;

            filter = states[hit] ? filter & ~FilterBits[hit] : filter | FilterBits[hit];
        }

        private static void KeyRow(string label, string slot, KeyCode current)
        {
            var r = Row();
            Ui.Text2(new Rect(r.x, r.y, 128f, r.height), label, Ui.Text);
            var waiting = _rebinding == slot;
            if (Ui.Button(new Rect(r.x + 128f, r.y + 2f, 150f, r.height - 4f),
                          waiting ? "press a key…" : current.ToString(), waiting))
                _rebinding = waiting ? null : slot;
        }

        // ---- tabs ----

        private static void TabAim()
        {
            Section("Targeting");
            Toggle("Aimbot enabled", ref Cfg.AimbotEnabled);
            FilterRow("Targets", ref Cfg.AimFilter);
            Slider("Field of view", ref Cfg.AimFovDegrees, 1f, 180f, "0");
            Slider("Max distance", ref Cfg.AimMaxDistance, 10f, 400f, "0");
            Slider("Max lock angle", ref Cfg.AimLockAngle, 0.2f, 15f, "0.0");
            Slider("Lock tightness", ref Cfg.AimLockTightness, 0.15f, 1.5f, "0.00");
            Note("Lock scales with how big the target looks, capped by the max above.", Ui.LineSoft);

            Section("Tracking");
            Slider("Smoothing", ref Cfg.AimSmoothing, 0.02f, 1f, "0.00");
            Slider("Max turn speed", ref Cfg.AimMaxTurnSpeed, 60f, 1440f, "0");
            Toggle("Aim at head", ref Cfg.AimAtHead);
            Toggle("Lead moving targets", ref Cfg.AimPredict);
            Toggle("Damp lead on erratic targets", ref Cfg.AimDampErratic);
            Toggle("Require line of sight", ref Cfg.AimRequireVisible);
            if (Aimbot.Target != null)
            {
                var straight = Motion.Straightness(Aimbot.Target);
                Note($"target motion: {straight * 100f:0}% steady" +
                     (straight < Motion.ErraticBelow ? "  - lead damped" : ""),
                     straight < Motion.ErraticBelow ? Ui.Warn : Ui.Good);
            }

            Section("Ballistics");
            if (Held.HasFirearm)
            {
                Note(Held.IsHitScan
                        ? "hitscan weapon - no lead or drop needed"
                        : $"muzzle {Held.ProjSpeed:0} u/s  ·  gravity {Held.Gravity:0.#}" +
                          (Aimbot.TravelTime > 0f ? $"  ·  flight {Aimbot.TravelTime:0.00}s" : ""),
                     Ui.AccentDim);
                Note($"barrel offset {Held.BarrelOffsetDegrees:0.00}deg" +
                     (Held.UsesCameraLine ? " (scoped: firing down the camera line)" : " (corrected)") +
                     (Aimbot.Target != null ? $"  ·  lock {Aimbot.LockTolerance:0.00}deg" : ""),
                     Ui.Good);
            }
            else Note("no firearm equipped", Ui.LineSoft);

            Section("Auto fire");
            Toggle("Auto fire", ref Cfg.AutoFire);
            Toggle("Hold when a player is in the way", ref Cfg.AutoFireHoldForPlayers);
            Note("Active only with a firearm equipped and aiming down sights.", Ui.AccentDim);
        }

        private static void TabMlg()
        {
            Section("Trick shot");
            Toggle("MLG enabled", ref Cfg.MlgEnabled);
            KeyRow("Trigger key", "mlg", Cfg.MlgKey);
            FilterRow("Targets", ref Cfg.MlgFilter, true, includePlayers: true);
            if ((Cfg.MlgFilter & TargetFilter.Players) != 0)
                Note(Fun.FriendlyFireOn
                        ? "players: friendly fire is on, shots will damage them"
                        : "players: friendly fire is OFF, the shot lands but deals no damage",
                     Fun.FriendlyFireOn ? Ui.Good : Ui.Warn);

            Section("Routine");
            Toggle("Jump first", ref Cfg.MlgJump);
            Toggle("Force no-scope", ref Cfg.MlgNoScope);
            Toggle("Prefer airborne targets", ref Cfg.MlgPreferAirborne);
            Slider("Spin duration", ref Cfg.MlgSpinDuration, 0.15f, 1f, "0.00");
            Slider("Min distance", ref Cfg.MlgMinDistance, 5f, 120f, "0");

            Section("Score multiplier");
            Toggle("Override kill multiplier", ref Cfg.ScoreOverrideEnabled);
            Slider("Multiplier", ref Cfg.ScoreOverride, 1f, 100f, "0.0x", Cfg.ScoreOverrideEnabled);
            Note("Replaces the product of all earned bonuses, so it also scales the item's worth.",
                 Ui.LineSoft);

            Section("Last run");
            Note(Mlg.LastResult.Length > 0 ? Mlg.LastResult : "nothing yet", Ui.Accent);
            Note("Bonuses are scored on impact, so the shot leaves as soon as the spin ends.", Ui.LineSoft);
        }

        private static void TabWeapon()
        {
            Section("Ammunition");
            Toggle("Unlimited ammo", ref Cfg.UnlimitedAmmo);
            Toggle("No reload", ref Cfg.NoReload);
            if (Cfg.UnlimitedAmmo)
                Note("Unlimited ammo blocks the Last Bullet bonus (it needs an empty magazine).", Ui.Warn);

            Section("Handling");
            Toggle("No recoil", ref Cfg.NoRecoil);
            Toggle("No spread", ref Cfg.NoSpread);
            Note("Recoil is zeroed at the camera, so the gun still animates normally.", Ui.LineSoft);

            Section("Output");
            Toggle("Fire rate override", ref Cfg.FireRateEnabled);
            Slider("Fire rate", ref Cfg.FireRateMult, 1f, 20f, "0.0x", Cfg.FireRateEnabled);
            Toggle("Damage override", ref Cfg.DamageEnabled);
            Slider("Damage", ref Cfg.DamageMult, 0.1f, 50f, "0.0x", Cfg.DamageEnabled);

            Section("Fists");
            Toggle("Punch damage override", ref Cfg.PunchDamageEnabled);
            Slider("Punch damage", ref Cfg.PunchDamageMult, 0.1f, 100f, "0.0x", Cfg.PunchDamageEnabled);
            Note(Hands.StockDamage > 0
                    ? $"stock {Hands.StockDamage}  ->  now {Hands.CurrentDamage}"
                    : "punch with bare hands once to read the stock value",
                 Ui.AccentDim);

            Section("Current weapon");
            Note(Held.HasFirearm
                    ? $"ammo {Held.Ammo}/{Held.AmmoPerMag}   ·   " +
                      (Held.IsHitScan ? "hitscan" : $"projectile {Held.ProjSpeed:0} u/s, gravity {Held.Gravity:0.#}")
                    : "no firearm equipped",
                 Ui.AccentDim);
        }

        private static void TabMove()
        {
            Section("Speed");
            Toggle("Speed multiplier", ref Cfg.SpeedEnabled);
            Slider("Speed", ref Cfg.SpeedMult, 0.5f, 6f, "0.0x", Cfg.SpeedEnabled);

            Section("Jump");
            Toggle("Jump height multiplier", ref Cfg.JumpEnabled);
            Slider("Jump", ref Cfg.JumpMult, 0.5f, 5f, "0.0x", Cfg.JumpEnabled);
            Toggle("Infinite jump (space in mid-air)", ref Cfg.InfiniteJump);

            Section("Notes");
            Note("Client-side: works whether or not you host.", Ui.Good);
            Note("This game has no fall damage, so nothing to disable there.", Ui.LineSoft);
        }

        private static void TabEsp()
        {
            Section("Visibility");
            Toggle("ESP enabled", ref Cfg.EspEnabled);
            FilterRow("Show", ref Cfg.EspFilter);
            Slider("Max distance", ref Cfg.EspMaxDistance, 20f, 600f, "0");
            Toggle("Visible targets only", ref Cfg.EspVisibleCheck);

            Section("Style");
            var r = Row();
            Ui.Text2(new Rect(r.x, r.y, 128f, r.height), "Box", Ui.Text);
            var style = Ui.Segmented(new Rect(r.x + 128f, r.y + 2f, r.width - 128f, r.height - 4f),
                                     new[] { "2D BOX", "HITBOXES" }, (int)Cfg.EspStyle);
            Cfg.EspStyle = (EspBoxStyle)style;

            Toggle("Health bar", ref Cfg.EspHealthBar);
            Toggle("Name", ref Cfg.EspName);
            Toggle("Distance", ref Cfg.EspDistance);
            Toggle("Snapline", ref Cfg.EspSnapline);

            Section("Also show");
            Toggle("Loose items and loot", ref Cfg.EspItems);
            Toggle("Players", ref Cfg.EspPlayers);
            Toggle("Boss HP and timer", ref Cfg.EspBossInfo);

            Section("Legend");
            Note("gold = drip  ·  red = boss  ·  cyan = bird  ·  green = target  ·  orange = player", Ui.LineSoft);
        }

        private static void TabWorld()
        {
            var host = Vitals.IsHost;

            Section("Player");
            Toggle("Unlimited health", ref Cfg.UnlimitedHealth, host);
            Toggle("Unlimited food", ref Cfg.UnlimitedFood, host);

            Section("Catching" + (host ? "" : "  (host only)"));
            Toggle("Instant bite", ref Cfg.InstantBite, host);
            Toggle("Drip chance override", ref Cfg.DripEnabled, Drip.IsHost);
            var chance = Ui.Slider(Row(), "Drip chance", Cfg.DripChance, 0f, 100f, "0", Drip.IsHost);
            Cfg.DripChance = Mathf.RoundToInt(chance);

            Section("Value" + (host ? "" : "  (host only)"));
            Toggle("Max fish weight", ref Cfg.MaxWeight, host);
            Slider("Weight", ref Cfg.WeightMult, 1f, 50f, "0.0x", host && Cfg.MaxWeight);
            Toggle("Perfect cook everything", ref Cfg.PerfectCook, host);
            Note($"worth = weight x cookness curve; best cookness is {Fishing.BestCookness:0.00}",
                 Ui.LineSoft);
            if (Action("UNLOCK ALL SKINS", host)) Fun.Report(Fishing.UnlockAllSkins());

            Section("Status");
            if (host)
                Note($"hosting - drip is {Drip.CurrentChance}% on every catch", Ui.Good);
            else
                Note("These are server-side: health, food and drip only apply when you host the lobby.",
                     Ui.Warn);

            Section("Settings");
            KeyRow("Menu key", "menu", Cfg.MenuKey);
            if (Ui.Button(new Rect(Pad, _y, 150f, 26f), "SAVE SETTINGS", true)) Cfg.Save();
            _y += 26f + RowGap;
        }

        private static void CaptureRebind()
        {
            if (_rebinding == null) return;
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;

            if (e.keyCode != KeyCode.Escape)
            {
                if (_rebinding == "mlg") Cfg.MlgKey = e.keyCode;
                else if (_rebinding == "menu") Cfg.MenuKey = e.keyCode;
            }

            _rebinding = null;
            Cfg.Save();
            e.Use();
        }
    }
}
