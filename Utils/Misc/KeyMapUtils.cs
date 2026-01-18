using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Rage;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.Misc{
    public static class KeyMapUtils{
        private const int KeyEventfScancode = 0x0008;
        private const int KeyEventfKeyup    = 0x0002;

        private static readonly int CurrentProcessId = Process.GetCurrentProcess().Id;

        [DllImport("user32.dll")] private static extern void keybd_event(byte   bVk,   byte bScan, uint dwFlags, int dwExtraInfo); // keystroke
        [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint uCode, uint uMapType);                             // translate vk to scan code

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();                                         // active window handle
        [DllImport("user32.dll")] private static extern uint   GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId); // get pid from window

        // check game window focused
        private static bool IsGameWindowFocused()
        {
            var foregroundWindow = GetForegroundWindow(); // handle
            if (foregroundWindow == IntPtr.Zero) return false;

            GetWindowThreadProcessId(foregroundWindow, out var foregroundProcessId); // check foxused window is of foreground
            return foregroundProcessId == CurrentProcessId;
        }

        /// <summary>
        ///     Keypress taking in Keys w/ 50ms b/t down and up
        /// </summary>
        private static void Press(Keys key)
        {
            GameFiber.StartNew(delegate
            {
                try
                {
                    if (Game.IsPaused)
                    {
                        Logger.LogInfo($"KeyMapUtils: Key press for {key} ignored. Game is paused.");
                        return;
                    }

                    if (!IsGameWindowFocused())
                    {
                        Logger.LogInfo($"KeyMapUtils: Key press for {key} ignored. Game window is not focused.");
                        return;
                    }

                    var scanCode = MapVirtualKey((uint)key, 0);
                    if (scanCode != 0)
                    {
                        keybd_event(0, (byte)scanCode, KeyEventfScancode, 0);                  // run keydown for mapped
                        GameFiber.Sleep(50);                                                   // or else wont register
                        keybd_event(0, (byte)scanCode, KeyEventfScancode | KeyEventfKeyup, 0); // run keyup for mapped
                    }
                    else
                    {
                        Game.LogTrivial($"KeyMapUtils Error: Could not map scan code for key {key}");
                    }
                }
                catch (Exception ex)
                {
                    Game.LogTrivial($"KeyMapUtils Error: Failed to execute key {key}. Details: {ex.Message}");
                }
            });
        }

        /// <summary>
        ///     Keypress for string key name (for when parsing from msg)
        /// </summary>
        public static void Press(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName))
            {
                Logger.LogError("KeyMapUtils: Received empty or null key request.");
                return;
            }

            try
            {
                if (Enum.TryParse(keyName, true, out Keys result))
                    Press(result);
                else
                    Logger.LogError($"KeyMapUtils: Failed to parse '{keyName}' into valid Forms.Keys enum.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"KeyMapUtils: Exception occurred while parsing key '{keyName}'. Error: {ex.Message}");
            }
        }
    }
}