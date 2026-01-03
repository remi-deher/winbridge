# WinBridge.Core

## Description
**WinBridge.Core** is the foundational library of the solution. It contains all shared logic that must be strictly decoupled from specific implementations.

## Contents
- **Models**: Shared data structures (e.g., Server, CredentialMetadata).
- **Constants**: Global application constants.
- **Protobufs**: ridge.proto definitions for gRPC communication.

> **Note:** This package has zero dependencies on business logic or UI frameworks. It serves as the common language between the App and the SDK.
