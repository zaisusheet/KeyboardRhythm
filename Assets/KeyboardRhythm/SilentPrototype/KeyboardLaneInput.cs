using UnityEngine.InputSystem;

namespace KeyboardRhythm.SilentPrototype
{
    // Same provisional symbol-key mapping as the confirmed input diagnostic.
    public static class KeyboardLaneInput
    {
        public static readonly Key[] Keys = {
            Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P,
            Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L, Key.Semicolon,
            Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M, Key.Comma, Key.Period, Key.Slash,
            Key.Space
        };
        public static readonly string[] Labels = {
            "Q / A / Z", "W / S / X", "E / D / C", "R / F / V", "T / G / B",
            "Y / H / N", "U / J / M", "I / K / ,", "O / L / .", "P / + / ?"
        };

        public static void Read(Keyboard keyboard, bool[] newlyPressed, bool[] heldLanes, bool[] heldKeys = null)
        {
            for (int i = 0; i < heldLanes.Length; i++) heldLanes[i] = false;
            for (int i = 0; i < Keys.Length; i++)
            {
                newlyPressed[i] = keyboard != null && keyboard[Keys[i]].wasPressedThisFrame;
                if (heldKeys != null) heldKeys[i] = keyboard != null && keyboard[Keys[i]].isPressed;
                if (keyboard != null && keyboard[Keys[i]].isPressed)
                    heldLanes[i == 30 ? 10 : i % 10] = true;
            }
        }
        public static int LaneForKeyIndex(int index) { return index == 30 ? 0 : index % 10 + 1; }
    }
}
