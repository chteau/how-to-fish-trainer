using System;
using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Entry point. The patcher injects a call to <see cref="Init"/> at the top of GameInfo.Awake,
    /// which runs once when the game's core singleton comes up.
    /// </summary>
    public static class Loader
    {
        private static bool _initialized;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                Cfg.Load();

                var host = new GameObject("HtfTrainer");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.HideAndDontSave;
                host.AddComponent<TrainerBehaviour>();

                Log.Info($"loaded — press {Cfg.MenuKey} for the menu, {Cfg.MlgKey} for MLG");
            }
            catch (Exception e)
            {
                Log.Error("initialisation failed: " + e);
            }
        }
    }
}
