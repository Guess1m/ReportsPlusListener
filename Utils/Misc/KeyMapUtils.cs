using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Rage;

namespace ReportsPlus.Utils{
    public static class KeyMapUtils{
        private const int KeyEventfScancode = 0x0008;
        private const int KeyEventfKeyup    = 0x0002;

        // P/Invoke Definitions
        [DllImport("user32.dll")] private static extern void keybd_event(byte        bVk,   byte bScan, uint dwFlags, int dwExtraInfo);
        [DllImport("user32.dll")] private static extern uint MapVirtualKeyPress(uint uCode, uint uMapType);

        /// <summary>
        ///     Simulates key press (wait 50ms between down and up) in a separate GameFiber.
        ///     Will not block the calling code.
        /// </summary>
        /// <param name="key">The key to simulate.</param>
        public static void Press(Keys key)
        {
            GameFiber.StartNew(delegate
            {
                try
                {
                    // MapVirtualKey(uCode, 0) maps a virtual-key code to a scan code.
                    var scanCode = MapVirtualKeyPress((uint)key, 0);
                    if (scanCode != 0)
                    {
                        // Press
                        keybd_event(0, (byte)scanCode, KeyEventfScancode, 0);
                        GameFiber.Sleep(50);
                        // Release
                        keybd_event(0, (byte)scanCode, KeyEventfScancode | KeyEventfKeyup, 0);
                    }
                    else
                    {
                        Game.LogTrivial($"KeyController Error: Could not map scan code for key {key}");
                    }
                }
                catch (Exception ex)
                {
                    Game.LogTrivial($"KeyController Error: Failed to execute key {key}. Details: {ex.Message}");
                }
            });
        }
    }
}