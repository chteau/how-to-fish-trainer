using System.Reflection;
using UnityEngine;

namespace HtfTrainer
{
    /// <summary>
    /// Movement tuning. The local player's rigidbody is owner-simulated, so these are client-side and
    /// work whether or not you host.
    ///
    /// There is no fall damage anywhere in this game, so there is nothing to disable for that.
    /// </summary>
    internal static class Mobility
    {
        private static FieldInfo _walk, _sprint, _jump;
        private static float _stockWalk, _stockSprint, _stockJump;
        private static bool _captured;
        private static bool _resolved;

        private static PlayerMovement Local
        {
            get
            {
                var p = Player.LocalPlayer;
                return p != null ? p.Movement : null;
            }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            var t = typeof(PlayerMovement);
            _walk = Refl.Field(t, "_walkSpeed");
            _sprint = Refl.Field(t, "_sprintSpeed");
            _jump = Refl.Field(t, "_jumpForce");
        }

        internal static void Tick()
        {
            var movement = Local;
            if (movement == null) { _captured = false; return; }

            Resolve();

            if (!_captured)
            {
                _stockWalk = Refl.Get(_walk, movement, 5f);
                _stockSprint = Refl.Get(_sprint, movement, 8f);
                _stockJump = Refl.Get(_jump, movement, 5f);
                _captured = true;
            }

            var speed = Cfg.SpeedEnabled ? Mathf.Max(0.1f, Cfg.SpeedMult) : 1f;
            Refl.Set(_walk, movement, _stockWalk * speed);
            Refl.Set(_sprint, movement, _stockSprint * speed);

            var jump = Cfg.JumpEnabled ? Mathf.Max(0.1f, Cfg.JumpMult) : 1f;
            Refl.Set(_jump, movement, _stockJump * jump);

            if (!Cfg.InfiniteJump || Menu.Open) return;

            var player = Player.LocalPlayer;
            if (player == null || player.BlockInputs) return;
            if (!Input.GetKeyDown(KeyCode.Space) || movement.Grounded) return;

            // The game's own jump input refuses mid-air, but Jump() itself is happy to be called.
            movement.Jump();
        }

        internal static void Restore()
        {
            var movement = Local;
            if (movement == null || !_captured) return;
            Refl.Set(_walk, movement, _stockWalk);
            Refl.Set(_sprint, movement, _stockSprint);
            Refl.Set(_jump, movement, _stockJump);
        }
    }
}
