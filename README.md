# WinBridge Solution

## Overview
WinBridge is a modular desktop application built on the **Onion Architecture** principle. It provides a robust platform for managing and connecting to various server protocols through a unified interface.

## Architecture
The solution is organized into three distinct layers:

1. **WinBridge.Core** (Inner Circle)
   - Contains domain models, interfaces, and protocol buffers.
   - Has no dependencies on external business logic or UI.

2. **WinBridge.SDK** (Middle Layer)
   - Provides the development kit for third-party modules.
   - Depends on *Core*.

3. **WinBridge.App** (Outer Layer)
   - The main executable (WinUI 3).
   - Composes dependencies and hosts modules.

## 🚀 Getting Started

### Prerequisites
- Windows 10 (1809) or Windows 11
- Visual Studio 2022 (with .NET Desktop Development & WinUI 3 templates)
- .NET 10 SDK (or later)

### Installation
1. **Clone the repository :**
   ```bash
   git clone [https://github.com/remi-deher/WinBridge.git](https://github.com/remi-deher/WinBridge.git)
   cd WinBridge

2. **First-time Setup (Important): Run the setup script to restore dependencies in the correct order :**

```bash
.\setup.ps1
````
*(This prevents NuGet restoration errors on a fresh clone)*

3. **Open WinBridge.slnx in Visual Studio and hit F5 !**

## Build
**To build the entire solution :**

```bash
dotnet build
```

## Structure
- **WinBridge.Core/**: Foundational definitions.
- **WinBridge.SDK/**: Developer toolkit for creating modules.
- **WinBridge.App/**: The end-user desktop application.
