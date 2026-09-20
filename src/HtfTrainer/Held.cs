using System.Reflection;
using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Everything the modules need to know about the firearm currently in the local player's hands.
    /// Re-resolved whenever the weapon instance changes, so swapping guns picks up the new stats.
    /// </summary>
    internal static class Held
    {
        private static FieldInfo _fProjSpeed, _fWeaponInfo, _fTimeBetweenShots, _fAmmo;
        private static FieldInfo _fIsReloading, _fQueueReload, _fHasCoolDown, _fReloadRefilled, _fHoldingAds;
        private static FieldInfo _fAimPercent;
        private static MethodInfo _mShoot;
        private static bool _resolved;

        internal static Weapon Weapon { get; private set; }

        /// <summary>Muzzle velocity in units/second. 0 for hitscan weapons.</summary>
        internal static float ProjSpeed { get; private set; }
        internal static float Gravity { get; private set; }
        internal static bool IsHitScan { get; private set; }
        internal static float BaseTimeBetweenShots { get; private set; }

        private static Weapon _lastWeapon;

        internal static bool HasFirearm => Weapon != null;
        internal static bool IsAiming => Weapon != null && Weapon.IsAds;

        internal static Transform FirePoint
        {
            get
            {
                var a = Weapon != null ? Weapon.Attachments : null;
                return a != null ? a.FirePoint : null;
            }
        }

        private static Transform CamTransform
        {
            get
            {
                var p = Player.LocalPlayer;
                return p != null && p.Camera != null ? p.Camera.CamTransform : null;
            }
        }

        /// <summary>
        /// True when Weapon.Shoot fires straight down the camera line instead of the barrel, which it
        /// does for scoped weapons once aim is past 90%.
        /// </summary>
        internal static bool UsesCameraLine
        {
            get
            {
                if (Weapon == null) return false;
                var a = Weapon.Attachments;
                if (a == null || !a.UseSniperUi) return false;
                return Refl.Get(_fAimPercent, Weapon, 0f) > 0.9f;
            }
        }

        internal static Vector3 MuzzlePosition
        {
            get
            {
                if (UsesCameraLine)
                {
                    var cam = CamTransform;
                    if (cam != null) return cam.position;
                }

                var fp = FirePoint;
                if (fp != null) return fp.position;
                var p = Player.LocalPlayer;
                return p != null && p.CamObject != null ? p.CamObject.position : Vector3.zero;
            }
        }

        /// <summary>
        /// Converts a desired *bullet* direction into the camera direction that produces it.
        ///
        /// This matters more than it sounds. Weapon.Shoot launches along _attachments.FirePoint.forward
        /// - the barrel - while the aimbot can only steer the camera. The barrel carries ADS offset,
        /// sway, bob and the recoil rig, so it sits at a small angle to the camera, and that angle
        /// becomes a miss of distance x tan(angle): harmless at 10m, over a metre at 100m. Undoing the
        /// offset here is what makes long shots land.
        /// </summary>
        internal static Vector3 CameraDirFor(Vector3 wantedBulletDir)
        {
            if (UsesCameraLine) return wantedBulletDir;

            var cam = CamTransform;
            var fp = FirePoint;
            if (cam == null || fp == null) return wantedBulletDir;

            var camToBarrel = Quaternion.FromToRotation(cam.forward, fp.forward);
            return Quaternion.Inverse(camToBarrel) * wantedBulletDir;
        }

        /// <summary>Angle between where the camera points and where the round will actually go.</summary>
        internal static float BarrelOffsetDegrees
        {
            get
            {
                if (UsesCameraLine) return 0f;
                var cam = CamTransform;
                var fp = FirePoint;
                if (cam == null || fp == null) return 0f;
                return Vector3.Angle(cam.forward, fp.forward);
            }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            var t = typeof(Weapon);
            _fProjSpeed = Refl.Field(t, "_projSpeed");
            _fWeaponInfo = Refl.Field(t, "_weaponInfo");
            _fTimeBetweenShots = Refl.Field(t, "_timeBetweenShots");
            _fAmmo = Refl.Field(t, "<Ammo>k__BackingField");
            _fIsReloading = Refl.Field(t, "_isReloading");
            _fQueueReload = Refl.Field(t, "_queueReload");
            _fHasCoolDown = Refl.Field(t, "_hasCoolDown");
            _fReloadRefilled = Refl.Field(t, "_reloadAmmoRefilled");
            _fHoldingAds = Refl.Field(t, "_holdingAdsInput");
            _fAimPercent = Refl.Field(t, "_aimPercent");
            _mShoot = Refl.Method(t, "Shoot");
        }

        /// <summary>Call once per frame before any module reads the weapon state.</summary>
        internal static void Refresh()
        {
            Resolve();

            var player = Player.LocalPlayer;
            var item = player != null && player.Holding != null ? player.Holding.HeldItem : null;
            Weapon = item != null ? item.Weapon : null;

            if (Weapon == null) { _lastWeapon = null; return; }
            if (ReferenceEquals(Weapon, _lastWeapon)) return;

            _lastWeapon = Weapon;
            ProjSpeed = Refl.Get(_fProjSpeed, Weapon, 0f);
            BaseTimeBetweenShots = Refl.Get(_fTimeBetweenShots, Weapon, 0.1f);

            var info = Refl.Get<WeaponInfo>(_fWeaponInfo, Weapon);
            Gravity = info != null ? info.ProjectileGravity : 0f;

            IsHitScan = false;
            if (info != null && ProjectileManager.Instance != null)
            {
                var type = ProjectileManager.Instance.GetType(info.ProjectileType);
                if (type != null) IsHitScan = type.IsHitScan;
            }

            // A hitscan round arrives instantly; treat it as infinite speed so the solver skips lead.
            if (IsHitScan) ProjSpeed = 0f;
        }

        internal static int Ammo => Weapon != null ? Weapon.Ammo : 0;

        internal static int AmmoPerMag
        {
            get
            {
                var a = Weapon != null ? Weapon.Attachments : null;
                return a != null ? a.AmmoPerMag : 0;
            }
        }

        internal static void SetAmmo(int value) => Refl.Set(_fAmmo, Weapon, value);
        internal static void SetTimeBetweenShots(float value) => Refl.Set(_fTimeBetweenShots, Weapon, value);

        internal static bool IsReloading => Refl.Get(_fIsReloading, Weapon, false);
        internal static bool ReloadQueued => Refl.Get(_fQueueReload, Weapon, false);

        internal static void CancelReload()
        {
            Refl.Set(_fIsReloading, Weapon, false);
            Refl.Set(_fQueueReload, Weapon, false);
            Refl.Set(_fReloadRefilled, Weapon, true);
        }

        internal static void ClearCooldown() => Refl.Set(_fHasCoolDown, Weapon, false);

        /// <summary>Drops aim-down-sights so PlayerSkills can register a no-scope.</summary>
        internal static void ReleaseAds()
        {
            if (Weapon == null || !Weapon.IsAds) return;
            Refl.Set(_fHoldingAds, Weapon, false);
            PlayerSkills.OnADSChange(to: false);
        }

        /// <summary>Fires one round through the game's own path, keeping every cooldown/ammo rule intact.</summary>
        internal static void Fire()
        {
            if (Weapon == null || _mShoot == null) return;
            _mShoot.Invoke(Weapon, null);
        }
    }
}
