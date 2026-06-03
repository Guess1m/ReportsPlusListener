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
        private readonly UIMenuCheckboxItem _autoConnectItem;
        private readonly UIMenuNumericScrollerItem<int> _intervalItem;
        private readonly UIMenuNumericScrollerItem<int> _autoConnectIntervalItem;
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
            var keybindsBtn = new UIMenuItem("~b~Keybindings", "Customize plugin shortcuts.");

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

            _saveSettingsItem = new UIMenuItem("~g~Save Configuration", "Save current settings to INI file. ~r~Requires Reconnect for Server IPv4/Port Changes.");
            _saveSettingsItem.Activated += (sender, item) =>
            {
                ConfigLoader.SaveSettings(Main.Settings, "plugins/LSPDFR/ReportsPlus.ini");
                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~ReportsPlus", "~g~Configuration Saved", "Settings have been written to disk.");
            };

            Main.Pool.Add(keybindsMenu);
            settingsMenu.BindMenuToItem(keybindsMenu, keybindsBtn);

            _autoConnectItem = new UIMenuCheckboxItem(
                "Auto-Connect",
                Main.Settings.AutoConnectEnabled,
                "Automatically reconnect to the server when the connection is lost.");
            _autoConnectItem.CheckboxEvent += (sender, isChecked) =>
            {
                Main.Settings.AutoConnectEnabled = isChecked;
                Game.DisplayNotification(isChecked
                    ? "Auto-Connect ~g~ENABLED~s~. Reconnects will happen automatically."
                    : "Auto-Connect ~r~DISABLED~s~. Use Force Reconnect to connect manually.");
            };

            var safeAutoInterval = MathHelper.Clamp(Main.Settings.AutoConnectInterval, 5000, 300000);
            _autoConnectIntervalItem = new UIMenuNumericScrollerItem<int>(
                "Reconnect Interval (ms)",
                "How long to wait between auto-reconnect attempts.",
                5000, 300000, 1000)
            {
                Value = safeAutoInterval
            };
            _autoConnectIntervalItem.WithTextEditing();
            _autoConnectIntervalItem.IndexChanged += (sender, oldIndex, newIndex) =>
            {
                Main.Settings.AutoConnectInterval = _autoConnectIntervalItem.Value;
            };

            var statusDisplayMenu = new UIMenu("REPORTS PLUS", "STATUS DISPLAY");
            var statusDisplayBtn = new UIMenuItem("~b~Status Display", "Configure the on-screen MDT status overlay.");

            var statusEnabledItem = new UIMenuCheckboxItem(
                "Show Status Overlay",
                Main.Settings.StatusOverlayEnabled,
                "Display the MDT connection status on screen at all times.");
            statusEnabledItem.CheckboxEvent += (sender, isChecked) =>
            {
                Main.Settings.StatusOverlayEnabled = isChecked;
            };

            var statusLabelItem = new UIMenuItem("Status Label", "The text shown before the connection status (e.g. 'MDT Status:').");
            statusLabelItem.WithTextEditing(() => Main.Settings.StatusOverlayLabel, newVal => Main.Settings.StatusOverlayLabel = newVal);

            var safeStatusX = MathHelper.Clamp(Main.Settings.StatusOverlayX, 0, 100);
            var statusXItem = new UIMenuNumericScrollerItem<int>("Position X", "Horizontal screen position (0 = left, 100 = right).", 0, 100, 1)
            {
                Value = safeStatusX
            };
            statusXItem.WithTextEditing();
            statusXItem.IndexChanged += (sender, oldIndex, newIndex) => { Main.Settings.StatusOverlayX = statusXItem.Value; };

            var safeStatusY = MathHelper.Clamp(Main.Settings.StatusOverlayY, 0, 100);
            var statusYItem = new UIMenuNumericScrollerItem<int>("Position Y", "Vertical screen position (0 = top, 100 = bottom).", 0, 100, 1)
            {
                Value = safeStatusY
            };
            statusYItem.WithTextEditing();
            statusYItem.IndexChanged += (sender, oldIndex, newIndex) => { Main.Settings.StatusOverlayY = statusYItem.Value; };

            var safeStatusSize = MathHelper.Clamp(Main.Settings.StatusOverlaySize, 10, 100);
            var statusSizeItem = new UIMenuNumericScrollerItem<int>("Text Size", "Size of the overlay text (10 = smallest, 100 = largest).", 10, 100, 5)
            {
                Value = safeStatusSize
            };
            statusSizeItem.WithTextEditing();
            statusSizeItem.IndexChanged += (sender, oldIndex, newIndex) => { Main.Settings.StatusOverlaySize = statusSizeItem.Value; };

            statusDisplayMenu.AddItems(statusEnabledItem, statusLabelItem, statusXItem, statusYItem, statusSizeItem);
            Main.Pool.Add(statusDisplayMenu);
            settingsMenu.BindMenuToItem(statusDisplayMenu, statusDisplayBtn);
            statusDisplayMenu.RemoveBanner();

            settingsMenu.AddItems(addressItem, portScroller, _intervalItem, _autoConnectItem, _autoConnectIntervalItem, keybindsBtn, statusDisplayBtn, _saveSettingsItem);

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

            if (_autoConnectItem.Checked != Main.Settings?.AutoConnectEnabled)
                _autoConnectItem.Checked = Main.Settings?.AutoConnectEnabled ?? false;
        }

        // update connection status
        private void UpdateConnectionStatus()
        {
            string label;
            string desc;

            if (Main.IsConnected)
            {
                label = "~g~Connected";
                desc = $"Connected to {Main.Settings?.ClientAddress}";
            }
            else if (Main.IsConnecting)
            {
                label = "~y~Connecting...";
                desc = $"Connecting to {Main.Settings?.ClientAddress}...";
            }
            else
            {
                label = "~r~Disconnected";
                desc = Main.Settings?.AutoConnectEnabled == true
                    ? $"Disconnected — auto-reconnect is active (every {Main.Settings.AutoConnectInterval / 1000}s)."
                    : "Disconnected — auto-connect is off. Use Force Reconnect.";
            }

            if (_statusItem.RightLabel == label) return;
            _statusItem.RightLabel = label;
            _statusItem.Description = desc;
        }

        // update input lock state
        private void UpdateInputLockState()
        {
            if (_inputLockItem.Checked != Main.IsInputDisabled) _inputLockItem.Checked = Main.IsInputDisabled;
        }
    }
}