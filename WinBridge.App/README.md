# WinBridge Desktop App

## Description

**WinBridge.App** is the reference implementation of the WinBridge host. It acts as the main dashboard, handling module orchestration, data persistence, and user interaction.

## Technology Stack

* **Framework**: .NET 10
* **UI System**: WinUI 3 (Windows App SDK)
* **Architecture**: MVVM (Model-View-ViewModel)
* **Data**: SQLite for local persistence

This project depends on **WinBridge.SDK** to consume the same interfaces exposed to external modules, ensuring dogfooding of the API.

