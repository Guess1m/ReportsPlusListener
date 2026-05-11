# ReportsPlus Listener

**Game Bridge for ReportsPlus External MDT, Created by Guess1m**

![License](https://img.shields.io/github/license/Guess1m/ReportsPlusListener)
![C#](https://img.shields.io/badge/C%23-Language-blue)
![RPH](https://img.shields.io/badge/RAGE_Plugin_Hook-Framework-red)
![Status](https://img.shields.io/badge/Status-Active%20Development-green)

This repository contains the source code for the **Listener Plugin** (C#) of ReportsPlus.

This plugin runs inside **Grand Theft Auto V** via the **RAGE Plugin Hook**. Its primary function is to act as a bridge between the game engine and the **[ReportsPlus Web Application](https://github.com/Guess1m/ReportsPlusWebApplication)**. It listens for game events, gathers data, sends it to the external MDT, and executes commands received from the web interface.

## 🔗 Official Links

- **LCPDFR Hub:** [View Project](https://www.lcpdfr.com/downloads/gta5mods/scripts/46968-reportsplus-external-mdt-new-custom-reports/)
- **Web App Repository:** [Java Backend Source](https://github.com/Guess1m/ReportsPlusWebApplication)
- **Support/Community:** [Join our Discord](https://discord.gg/tjsBtSKZNF)

## ⚠️ Rewrite Disclaimer

**This project is currently in active development (Alpha).**
It is not a finished product. You **will** encounter bugs and incomplete features. This is the client-side component of a very large project I work on in my free time.

- **Feedback:** Please report issues or suggestions to help improve the data synchronization or game integration.
- **Bug Reporting:** Please report genuine errors (crashes, connection failures). Avoid reporting issues caused by intentional stress-testing.

## ✨ Key Capabilities

- **Data Extraction:** Scrapes detailed information about Peds and Vehicles (license status, flags, passengers, inventory) directly from the game memory.
- **Real-Time Synchronization:** Sends player status, location, and game state changes to the Web Application instantly.
- **Integration Hooks:**
  - **Policing Redefined/CDF:** Pulls definitions and data via the Common Data Framework API.
  - **Callout Interface:** Detects active callouts and pushes details (location, code, description) to the MDT.
  - **StopThePed (STP):** Listens for search results and ID checks to populate the MDT automatically.
- **Remote Execution:** Receives commands from the Web Application to trigger in-game actions (e.g., requesting backup, modifying vehicle blips, or triggering plugin functions).
- **Resilient Networking:** Features auto-reconnection logic to maintain communication with the local Java server even if the game pauses or lags.

## ‼️ Basic Usage

### Relationship to Web App

**This plugin cannot function alone.** It requires the **[ReportsPlus Web Application](https://github.com/Guess1m/ReportsPlusWebApplication)** (Spring Backend) to be running to interface with the Application.

### Configuration

The plugin includes an `.ini` file (usually generated on first run or included in the release) where you can configure:

- **Connection Settings:** IP Address and Port of the Java application (Default: `127.0.0.1:6969`). If you want to run the Application from another machine rather than the one hosting GTA, you can input the address to that machine.
- **Keybinds:** Toggles for overlay features or reconnection actions.

## 🛠️ Tech Stack

- **Language:** C#
- **Framework:** RAGE Plugin Hook (RPH) / LSPDFR API
- **Dependencies:**
  - **Newtonsoft.Json:** For serializing game objects to JSON for the web app.
  - **System.Net.Http:** For handling API requests to the local server.
  - **LSPDFR SDK:** For interfacing with police functions.

## 🚀 Getting Started

### Prerequisites

- **Grand Theft Auto V** (Legitimate copy).
- **RAGE Plugin Hook** (Latest version).
- **LSPDFR** (Latest version).
- **.NET Framework 4.8** (Standard for RPH plugins).

### Installation (For Users)

For the compiled binaries, please do not download from GitHub source unless you are a developer. Use the official release package which bundles both the Java App and this Plugin together.
👉 **[Download on LCPDFR.com](https://www.lcpdfr.com/downloads/gta5mods/scripts/46968-reportsplus-external-mdt-new-custom-reports/)**

### Development Setup

If you wish to contribute to the game-logic side:

1. **Clone the repository**
   ```bash
   git clone [https://github.com/Guess1m/ReportsPlusListener.git](https://github.com/Guess1m/ReportsPlusListener.git)
   ```
2. **Open in Visual Studio**
   Open the `.sln` file. You will likely need to fix references to RAGE Plugin Hook and LSPDFR files (`RagePluginHookSDK.dll`, `LSPD First Response.dll`) as these are not included in the repo. Point them to your local DLL copies.
3. **Build**
   Build the solution in **Release** mode.
4. **Deploy**
   Copy the resulting `ReportsPlus.dll` and `.pdb` to `Grand Theft Auto V/Plugins/LSPDFR/`.

## 🤝 Support

The best place to go for support is the [ReportsPlus Discord](https://discord.gg/tjsBtSKZNF).

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1. Fork the repository.
2. Create a feature branch (`git checkout -b feature/NewIntegration`).
3. Commit your changes.
4. Push to the branch.
5. Open a Pull Request.

## 📄 License

This project is licensed under the **Eclipse Public License 2.0 (EPL-2.0)** - see the [LICENSE](LICENSE) file for details.

---

**Disclaimer:** This tool is a fan-made modification and is not affiliated with Rockstar Games or the LSPDFR development team.
