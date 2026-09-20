using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace HtfTrainer
{
    [Flags]
    internal enum TargetFilter
    {
        None = 0,
        SeaCreatures = 1 << 0,
        Seagulls = 1 << 1,
        Bosses = 1 << 2,
        Players = 1 << 3,
        All = SeaCreatures | Seagulls | Bosses | Players
    }

    internal enum EspBoxStyle
    {
        /// <summary>Single screen-space rectangle fitted to the hitbox colliders (CS2 style).</summary>
        Box2D,
        /// <summary>Wireframe of each individual hitbox collider, drawn in 3D.</summary>
        Hitbox3D
    }

    internal static class Cfg
    {
        // ---- master ----
        internal static KeyCode MenuKey = KeyCode.F7;

        // ---- aimbot ----
        internal static bool AimbotEnabled = false;
        internal static TargetFilter AimFilter = TargetFilter.All;
        internal static float AimMaxDistance = 120f;
        internal static float AimFovDegrees = 30f;
        /// <summary>0 = no correction, 1 = instant snap. Applied per frame, frame-rate compensated.</summary>
        internal static float AimSmoothing = 0.55f;
        internal static float AimMaxTurnSpeed = 720f;
        internal static bool AimAtHead = true;
        internal static bool AimRequireVisible = true;
        /// <summary>Angle within which the target is considered locked (gates auto-fire).</summary>
        internal static float AimLockAngle = 2.5f;
        internal static bool AimPredict = true;
        /// <summary>Fraction of the target's angular radius that counts as on target.</summary>
        internal static float AimLockTightness = 0.6f;
        /// <summary>Scale the lead down as a target's movement becomes erratic.</summary>
        internal static bool AimDampErratic = true;

        // ---- auto fire ----
        internal static bool AutoFire = false;
        internal static bool AutoFireHoldForPlayers = true;

        // ---- MLG ----
        internal static bool MlgEnabled = false;
        internal static KeyCode MlgKey = KeyCode.V;
        internal static float MlgSpinDuration = 0.38f;
        internal static bool MlgJump = true;
        internal static bool MlgNoScope = true;
        internal static bool MlgPreferAirborne = true;
        internal static float MlgMinDistance = 26f;
        internal static TargetFilter MlgFilter = TargetFilter.SeaCreatures | TargetFilter.Seagulls | TargetFilter.Bosses;

        // ---- weapon ----
        internal static bool UnlimitedAmmo = false;
        internal static bool NoReload = false;
        internal static bool FireRateEnabled = false;
        internal static float FireRateMult = 3f;
        internal static bool NoRecoil = false;
        internal static bool NoSpread = false;
        internal static bool DamageEnabled = false;
        internal static bool PunchDamageEnabled = false;
        internal static float PunchDamageMult = 10f;
        internal static float DamageMult = 5f;

        // ---- score ----
        internal static bool ScoreOverrideEnabled = false;
        internal static float ScoreOverride = 10f;

        // ---- casino (host only) ----
        internal static bool RigRoulette = false;

        // ---- fun ----
        internal static float LaunchForce = 60f;

        // ---- vitals (host only) ----
        internal static bool UnlimitedHealth = false;
        internal static bool UnlimitedFood = false;

        // ---- fishing & economy (host only) ----
        internal static bool InstantBite = false;
        internal static bool MaxWeight = false;
        internal static float WeightMult = 10f;
        internal static bool PerfectCook = false;

        // ---- mobility (client) ----
        internal static bool SpeedEnabled = false;
        internal static float SpeedMult = 1.6f;
        internal static bool JumpEnabled = false;
        internal static float JumpMult = 1.5f;
        internal static bool InfiniteJump = false;

        // ---- drip (host only) ----
        internal static bool DripEnabled = false;
        internal static int DripChance = 50;

        // ---- esp ----
        internal static bool EspEnabled = false;
        internal static TargetFilter EspFilter = TargetFilter.All;
        internal static EspBoxStyle EspStyle = EspBoxStyle.Box2D;
        internal static float EspMaxDistance = 250f;
        internal static bool EspHealthBar = true;
        internal static bool EspName = true;
        internal static bool EspDistance = true;
        internal static bool EspSnapline = false;
        internal static bool EspItems = false;
        internal static bool EspPlayers = false;
        internal static bool EspBossInfo = true;
        internal static bool EspVisibleCheck = false;

        private static string _path;

        private static string Path
        {
            get
            {
                if (_path == null)
                {
                    var dir = System.IO.Path.Combine(Application.persistentDataPath, "HtfTrainer");
                    Directory.CreateDirectory(dir);
                    _path = System.IO.Path.Combine(dir, "config.cfg");
                }
                return _path;
            }
        }

        internal static void Save()
        {
            try
            {
                var lines = new List<string>
                {
                    "# How to Fish trainer settings",
                    K("MenuKey", MenuKey), K("AimbotEnabled", AimbotEnabled), K("AimFilter", AimFilter),
                    K("AimMaxDistance", AimMaxDistance), K("AimFovDegrees", AimFovDegrees),
                    K("AimSmoothing", AimSmoothing), K("AimMaxTurnSpeed", AimMaxTurnSpeed),
                    K("AimAtHead", AimAtHead), K("AimRequireVisible", AimRequireVisible),
                    K("AimLockAngle", AimLockAngle), K("AimPredict", AimPredict),
                    K("AimDampErratic", AimDampErratic), K("RigRoulette", RigRoulette),
                    K("AimLockTightness", AimLockTightness),
                    K("ScoreOverrideEnabled", ScoreOverrideEnabled), K("ScoreOverride", ScoreOverride),
                    K("LaunchForce", LaunchForce),
                    K("AutoFire", AutoFire), K("AutoFireHoldForPlayers", AutoFireHoldForPlayers),
                    K("MlgEnabled", MlgEnabled), K("MlgKey", MlgKey), K("MlgSpinDuration", MlgSpinDuration),
                    K("MlgJump", MlgJump), K("MlgNoScope", MlgNoScope),
                    K("MlgPreferAirborne", MlgPreferAirborne), K("MlgMinDistance", MlgMinDistance),
                    K("MlgFilter", MlgFilter),
                    K("UnlimitedAmmo", UnlimitedAmmo), K("NoReload", NoReload),
                    K("FireRateEnabled", FireRateEnabled), K("FireRateMult", FireRateMult),
                    K("NoRecoil", NoRecoil), K("NoSpread", NoSpread),
                    K("DamageEnabled", DamageEnabled), K("DamageMult", DamageMult),
                    K("PunchDamageEnabled", PunchDamageEnabled), K("PunchDamageMult", PunchDamageMult),
                    K("UnlimitedHealth", UnlimitedHealth), K("UnlimitedFood", UnlimitedFood),
                    K("DripEnabled", DripEnabled), K("DripChance", DripChance),
                    K("InstantBite", InstantBite), K("MaxWeight", MaxWeight),
                    K("WeightMult", WeightMult), K("PerfectCook", PerfectCook),
                    K("SpeedEnabled", SpeedEnabled), K("SpeedMult", SpeedMult),
                    K("JumpEnabled", JumpEnabled), K("JumpMult", JumpMult),
                    K("InfiniteJump", InfiniteJump), K("EspItems", EspItems),
                    K("EspPlayers", EspPlayers), K("EspBossInfo", EspBossInfo),
                    K("EspEnabled", EspEnabled), K("EspFilter", EspFilter), K("EspStyle", EspStyle),
                    K("EspMaxDistance", EspMaxDistance), K("EspHealthBar", EspHealthBar),
                    K("EspName", EspName), K("EspDistance", EspDistance), K("EspSnapline", EspSnapline),
                    K("EspVisibleCheck", EspVisibleCheck)
                };
                File.WriteAllLines(Path, lines);
            }
            catch (Exception e) { Log.Warn("could not save config: " + e.Message); }
        }

        internal static void Load()
        {
            try
            {
                if (!File.Exists(Path)) { Save(); return; }
                foreach (var raw in File.ReadAllLines(Path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    var split = line.IndexOf('=');
                    if (split <= 0) continue;
                    Apply(line.Substring(0, split).Trim(), line.Substring(split + 1).Trim());
                }
            }
            catch (Exception e) { Log.Warn("could not load config: " + e.Message); }
        }

        private static string K(string key, object value) =>
            key + "=" + Convert.ToString(value, CultureInfo.InvariantCulture);

        private static void Apply(string key, string v)
        {
            switch (key)
            {
                case "MenuKey": MenuKey = Enum2(v, MenuKey); break;
                case "AimbotEnabled": AimbotEnabled = Bool(v, AimbotEnabled); break;
                case "AimFilter": AimFilter = Enum2(v, AimFilter); break;
                case "AimMaxDistance": AimMaxDistance = Num(v, AimMaxDistance); break;
                case "AimFovDegrees": AimFovDegrees = Num(v, AimFovDegrees); break;
                case "AimSmoothing": AimSmoothing = Num(v, AimSmoothing); break;
                case "AimMaxTurnSpeed": AimMaxTurnSpeed = Num(v, AimMaxTurnSpeed); break;
                case "AimAtHead": AimAtHead = Bool(v, AimAtHead); break;
                case "AimRequireVisible": AimRequireVisible = Bool(v, AimRequireVisible); break;
                case "AimLockAngle": AimLockAngle = Num(v, AimLockAngle); break;
                case "AimPredict": AimPredict = Bool(v, AimPredict); break;
                case "AimDampErratic": AimDampErratic = Bool(v, AimDampErratic); break;
                case "AimLockTightness": AimLockTightness = Num(v, AimLockTightness); break;
                case "ScoreOverrideEnabled": ScoreOverrideEnabled = Bool(v, ScoreOverrideEnabled); break;
                case "ScoreOverride": ScoreOverride = Num(v, ScoreOverride); break;
                case "RigRoulette": RigRoulette = Bool(v, RigRoulette); break;
                case "LaunchForce": LaunchForce = Num(v, LaunchForce); break;
                case "AutoFire": AutoFire = Bool(v, AutoFire); break;
                case "AutoFireHoldForPlayers": AutoFireHoldForPlayers = Bool(v, AutoFireHoldForPlayers); break;
                case "MlgEnabled": MlgEnabled = Bool(v, MlgEnabled); break;
                case "MlgKey": MlgKey = Enum2(v, MlgKey); break;
                case "MlgSpinDuration": MlgSpinDuration = Num(v, MlgSpinDuration); break;
                case "MlgJump": MlgJump = Bool(v, MlgJump); break;
                case "MlgNoScope": MlgNoScope = Bool(v, MlgNoScope); break;
                case "MlgPreferAirborne": MlgPreferAirborne = Bool(v, MlgPreferAirborne); break;
                case "MlgMinDistance": MlgMinDistance = Num(v, MlgMinDistance); break;
                case "MlgFilter": MlgFilter = Enum2(v, MlgFilter); break;
                case "UnlimitedAmmo": UnlimitedAmmo = Bool(v, UnlimitedAmmo); break;
                case "NoReload": NoReload = Bool(v, NoReload); break;
                case "FireRateEnabled": FireRateEnabled = Bool(v, FireRateEnabled); break;
                case "FireRateMult": FireRateMult = Num(v, FireRateMult); break;
                case "NoRecoil": NoRecoil = Bool(v, NoRecoil); break;
                case "NoSpread": NoSpread = Bool(v, NoSpread); break;
                case "DamageEnabled": DamageEnabled = Bool(v, DamageEnabled); break;
                case "DamageMult": DamageMult = Num(v, DamageMult); break;
                case "PunchDamageEnabled": PunchDamageEnabled = Bool(v, PunchDamageEnabled); break;
                case "PunchDamageMult": PunchDamageMult = Num(v, PunchDamageMult); break;
                case "UnlimitedHealth": UnlimitedHealth = Bool(v, UnlimitedHealth); break;
                case "UnlimitedFood": UnlimitedFood = Bool(v, UnlimitedFood); break;
                case "DripEnabled": DripEnabled = Bool(v, DripEnabled); break;
                case "DripChance": DripChance = (int)Num(v, DripChance); break;
                case "InstantBite": InstantBite = Bool(v, InstantBite); break;
                case "MaxWeight": MaxWeight = Bool(v, MaxWeight); break;
                case "WeightMult": WeightMult = Num(v, WeightMult); break;
                case "PerfectCook": PerfectCook = Bool(v, PerfectCook); break;
                case "SpeedEnabled": SpeedEnabled = Bool(v, SpeedEnabled); break;
                case "SpeedMult": SpeedMult = Num(v, SpeedMult); break;
                case "JumpEnabled": JumpEnabled = Bool(v, JumpEnabled); break;
                case "JumpMult": JumpMult = Num(v, JumpMult); break;
                case "InfiniteJump": InfiniteJump = Bool(v, InfiniteJump); break;
                case "EspItems": EspItems = Bool(v, EspItems); break;
                case "EspPlayers": EspPlayers = Bool(v, EspPlayers); break;
                case "EspBossInfo": EspBossInfo = Bool(v, EspBossInfo); break;
                case "EspEnabled": EspEnabled = Bool(v, EspEnabled); break;
                case "EspFilter": EspFilter = Enum2(v, EspFilter); break;
                case "EspStyle": EspStyle = Enum2(v, EspStyle); break;
                case "EspMaxDistance": EspMaxDistance = Num(v, EspMaxDistance); break;
                case "EspHealthBar": EspHealthBar = Bool(v, EspHealthBar); break;
                case "EspName": EspName = Bool(v, EspName); break;
                case "EspDistance": EspDistance = Bool(v, EspDistance); break;
                case "EspSnapline": EspSnapline = Bool(v, EspSnapline); break;
                case "EspVisibleCheck": EspVisibleCheck = Bool(v, EspVisibleCheck); break;
            }
        }

        private static bool Bool(string v, bool fallback) => bool.TryParse(v, out var r) ? r : fallback;

        private static float Num(string v, float fallback) =>
            float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : fallback;

        private static T Enum2<T>(string v, T fallback) where T : struct =>
            Enum.TryParse<T>(v, true, out var r) ? r : fallback;
    }
}
