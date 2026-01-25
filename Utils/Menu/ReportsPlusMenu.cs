using System;
using System.Windows.Forms;
using INIUtility;
using Rage;
using RAGENativeUI;
using RAGENativeUI.Elements;

namespace ReportsPlus.Utils.Menu
{
    public sealed class ReportsPlusMenu : UIMenu
    {
        private readonly UIMenuItem _discardCitationItem;
        private readonly UIMenuItem _giveCitationItem;

        // items
        private readonly UIMenuCheckboxItem _inputLockItem;
        private readonly UIMenuNumericScrollerItem<int> _intervalItem;
        private readonly UIMenuItem _reconnectItem;
        private readonly UIMenuItem _saveSettingsItem;
        private readonly UIMenuItem _statusItem;

        private uint _lastUpdateTick;

        public ReportsPlusMenu() : base("REPORTS PLUS", "MAIN MENU")
        {
            _statusItem = new UIMenuItem("Connection Status", "Current status of the CAD WebSocket connection.")
            {
                Enabled = false,
                RightLabel = "Initializing..."
            };

            _reconnectItem = new UIMenuItem("Force Reconnect", "Attempt to reconnect to the server using current settings.");
            _reconnectItem.Activated += (sender, item) => { Main.AttemptConnection(); };

            _giveCitationItem = new UIMenuItem("~g~Give Citation", "Issue the pending citation to the target.")
            {
                Enabled = false
            };
            _giveCitationItem.Activated += (sender, item) =>
            {
                if (!Main.IsCitationPending) return;
                Main.TriggerGiveCitation = true;
            };

            _discardCitationItem = new UIMenuItem("~r~Discard Citation", "Discard the pending citation request.")
            {
                Enabled = false
            };
            _discardCitationItem.Activated += (sender, item) =>
            {
                if (!Main.IsCitationPending) return;
                Main.TriggerDiscardCitation = true;
                Main.Pool.CloseAllMenus();
            };

            _inputLockItem = new UIMenuCheckboxItem("Disable Game Input", Main.IsInputDisabled, "Prevents game inputs while using the CAD.");
            _inputLockItem.CheckboxEvent += (sender, isChecked) =>
            {
                Main.IsInputDisabled = isChecked;
                Game.DisplayNotification(Main.IsInputDisabled ? "All input DISABLED via Menu." : "All input ENABLED via Menu.");
            };

            var settingsMenu = new UIMenu("REPORTS PLUS", "SETTINGS");
            var settingsBtn = new UIMenuItem("Configuration", "Modify server address, port, and keybindings.");

            var addressItem = new UIMenuItem("Server Address", "The IP or Hostname of the WebSocket server.");
            addressItem.WithTextEditing(() => Main.Settings.ClientAddress, newVal => Main.Settings.ClientAddress = newVal);

            var safePort = MathHelper.Clamp(Main.Settings.ClientPort, 1, 65535);
            var portScroller = new UIMenuNumericScrollerItem<int>("Server Port", "The port of the WebSocket server.", 1, 65535, 1)
            {
                Value = safePort
            };
            portScroller.WithTextEditing();
            portScroller.IndexChanged += (sender, oldIndex, newIndex) => { Main.Settings.ClientPort = portScroller.Value; };

            var safeInterval = MathHelper.Clamp(Main.Settings.ContinuousUpdateInterval, 1000, 60000);
            _intervalItem = new UIMenuNumericScrollerItem<int>("Update Interval (ms)", "Time between continuous updates.", 1000, 60000, 500)
            {
                Value = safeInterval
            };
            _intervalItem.IndexChanged += (sender, oldIndex, newIndex) => { Main.Settings.ContinuousUpdateInterval = _intervalItem.Value; };

            var keybindsMenu = new UIMenu("REPORTS PLUS", "KEYBINDINGS");
            var keybindsBtn = new UIMenuItem("Keybindings", "Customize plugin shortcuts.");

            AddKeybindSetting(keybindsMenu, "Menu Toggle", "Key to open/close this menu.",
                () => Main.Settings.MenuKey,
                v => Main.Settings.MenuKey = v);

            AddKeybindSetting(keybindsMenu, "Input Lock Toggle", "Key to toggle game input.",
                () => Main.Settings.InputLockKey,
                v => Main.Settings.InputLockKey = v);

            AddKeybindSetting(keybindsMenu, "Give Citation", "Key to issue a parking citation.",
                () => Main.Settings.GiveCitationKey,
                v => Main.Settings.GiveCitationKey = v);

            AddKeybindSetting(keybindsMenu, "Discard Citation", "Key to discard a parking citation.",
                () => Main.Settings.DiscardCitationKey,
                v => Main.Settings.DiscardCitationKey = v);

            _saveSettingsItem = new UIMenuItem("Save Configuration", "Save current settings to INI file. ~r~Requires Reconnect.");
            _saveSettingsItem.Activated += (sender, item) =>
            {
                ConfigLoader.SaveSettings(Main.Settings, "plugins/LSPDFR/ReportsPlus.ini");
                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Configuration Saved", "Settings have been written to disk.");
            };

            Main.Pool.Add(keybindsMenu);
            settingsMenu.BindMenuToItem(keybindsMenu, keybindsBtn);

            settingsMenu.AddItems(addressItem, portScroller, _intervalItem, keybindsBtn, _saveSettingsItem);

            Main.Pool.Add(this);
            Main.Pool.Add(settingsMenu);
            BindMenuToItem(settingsMenu, settingsBtn);

            AddItems(_statusItem, _reconnectItem, _inputLockItem, settingsBtn, _giveCitationItem, _discardCitationItem);

            RemoveBanner();
            settingsMenu.RemoveBanner();
            keybindsMenu.RemoveBanner();
        }

        /// <summary>
        ///     Adds a keybind configuration row to the menu, including a main key selector and a checkbox for the Control modifier.
        /// </summary>
        /// <param name="menu">The menu to which the items will be added.</param>
        /// <param name="name">The display name of the keybind.</param>
        /// <param name="description">The description of the keybind action.</param>
        /// <param name="getter">Function to retrieve the current KeyBinding.</param>
        /// <param name="setter">Action to set the updated KeyBinding.</param>
        private void AddKeybindSetting(UIMenu menu, string name, string description, Func<KeyBinding> getter, Action<KeyBinding> setter)
        {
            if (menu == null || getter == null || setter == null) return;

            var currentBinding = getter();

            var keyItem = new UIMenuItem(name, description);
            keyItem.WithKeyEditing(
                () => getter().Key,
                newKey => setter(new KeyBinding(newKey, getter().Modifier))
            );

            var modCheckbox = new UIMenuCheckboxItem(
                $"Use 'Ctrl' with {name}",
                currentBinding.Modifier == Keys.Control,
                $"If checked, you must hold Control + {getter().Key} to trigger this action."
            );

            modCheckbox.CheckboxEvent += (sender, isChecked) =>
            {
                var currentKey = getter().Key;
                setter(new KeyBinding(currentKey, isChecked ? Keys.Control : Keys.None));
            };

            menu.AddItem(keyItem);
            menu.AddItem(modCheckbox);
        }
        // update connection status and input lock state every second
        public override void ProcessControl()
        {
            base.ProcessControl();

            // Update Citation Menu Items - only assign property if the value has changed
            var isPending = Main.IsCitationPending;
            if (_giveCitationItem.Enabled != isPending)
            {
                _giveCitationItem.Enabled = isPending;
                _giveCitationItem.RightLabel = isPending ? "" : "No Pending Request";
            }

            if (_discardCitationItem.Enabled != isPending)
            {
                _discardCitationItem.Enabled = isPending;
                _discardCitationItem.RightLabel = isPending ? "" : "No Pending Request";
            }

            if (Game.GameTime - _lastUpdateTick < 1000) return;
            _lastUpdateTick = Game.GameTime;

            UpdateConnectionStatus();
            UpdateInputLockState();
        }

        // update connection status
        private void UpdateConnectionStatus()
        {
            var isConnected = Main.IsConnected;
            if (isConnected)
            {
                if (_statusItem.RightLabel == "~g~Connected") return;
                _statusItem.RightLabel = "~g~Connected";
                _statusItem.Description = "Successfully connected to " + Main.Settings.ClientAddress;
            }
            else
            {
                if (_statusItem.RightLabel == "~r~Disconnected") return;
                _statusItem.RightLabel = "~r~Disconnected";
                _statusItem.Description = "Not connected. Check settings and try reconnecting.";
            }
        }

        // update input lock state
        private void UpdateInputLockState()
        {
            if (_inputLockItem.Checked != Main.IsInputDisabled) _inputLockItem.Checked = Main.IsInputDisabled;
        }
    }
}