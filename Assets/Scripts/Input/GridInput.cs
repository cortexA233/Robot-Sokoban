using Sokoban.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sokoban
{
    public sealed class GridInput
    {
        private static readonly Key[] Keys = { Key.UpArrow, Key.RightArrow, Key.DownArrow, Key.LeftArrow, Key.W, Key.D, Key.S, Key.A };
        private Key? held;
        private Direction lockedDirection;
        private Direction? pending;
        private float repeatAfter;
        private bool suppressUntilRelease;

        public void Clear()
        { held = null; pending = null; suppressUntilRelease = true; }
        public Direction? Poll(bool busy, Direction cameraForward)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return null;
            if (suppressUntilRelease)
            {
                bool any = false;
                foreach (var key in Keys) any |= keyboard[key].isPressed;
                if (any) return null;
                suppressUntilRelease = false;
            }
            if (held.HasValue && !keyboard[held.Value].isPressed) held = null;
            for (int i = 0; i < Keys.Length; i++)
                if (keyboard[Keys[i]].wasPressedThisFrame)
                {
                    held = Keys[i]; lockedDirection = (Direction)((i % 4 + (i >= 4 ? (int)cameraForward : 0)) % 4);
                    pending = lockedDirection; repeatAfter = Time.unscaledTime + .25f;
                }
            if (busy) return null;
            if (pending.HasValue) { var direction = pending; pending = null; return direction; }
            if (held.HasValue && Time.unscaledTime >= repeatAfter) { repeatAfter = Time.unscaledTime + .12f; return lockedDirection; }
            return null;
        }
    }
}
