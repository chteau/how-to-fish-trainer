using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Per-frame driver. The aim correction itself is applied from the PlayerAimAssist hook so it
    /// lands inside the camera's own update; everything else runs from here.
    /// </summary>
    internal sealed class TrainerBehaviour : MonoBehaviour
    {
        private void Update()
        {
            if (!Menu.IsRebinding && Input.GetKeyDown(Cfg.MenuKey)) Menu.Toggle();
            if (Menu.Open) Menu.ApplyCursor();

            if (Player.LocalPlayer == null)
            {
                Aimbot.Reset();
                Mlg.Abort("no local player");
                return;
            }

            Held.Refresh();
            Mlg.Watchdog();
            Targets.Prune();
            Motion.SampleAll();

            if (!Menu.Open && Cfg.MlgEnabled && Input.GetKeyDown(Cfg.MlgKey)) Mlg.Trigger();

            Weapons.Tick();
            Mobility.Tick();
            Hands.Tick();
            Fishing.Tick();
            Vitals.Tick();
            Drip.Tick();
            AutoFire.Tick();
        }

        private void OnGUI()
        {
            Esp.Render();
            Menu.Render();
        }

        private void OnDestroy()
        {
            Weapons.RestoreAll();
            Mobility.Restore();
            Hands.Restore();
            Drip.Restore();
            Targets.Clear();
        }
    }
}
