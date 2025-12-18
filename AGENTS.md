# AGENTS.md

Instructions for AI agents working with this repository.

## Project Overview

Push-to-Talk functionality for Linux voice assistant. Monitors mouse buttons (side buttons, middle click) and triggers speech-to-text recording via HTTP API calls.

## Build Commands

```bash
dotnet build
dotnet test
dotnet publish -c Release -o ./publish
```

## Code Style

- Follow Microsoft C# naming conventions
- Use xUnit + Moq for testing
- Target .NET 10
- Namespace prefix: `Olbrasoft.PushToTalk`

## Important Paths

- Source: `src/`
- Tests: `tests/`
- Solution: `PushToTalk.sln`

## Architecture

- **Strategy Pattern** for mouse button monitoring (libevdev, udev, etc.)
- **SOLID principles** - especially Single Responsibility and Dependency Inversion
- Each source project has its own test project

## Testing Requirements

- Test naming: `[Method]_[Scenario]_[Expected]`
- Example: `Monitor_ButtonPressed_TriggersCallback`
- Framework: xUnit + Moq

## Secrets

Never commit secrets. Use:
- `dotnet user-secrets` for local development
- GitHub Secrets for CI/CD

## Related Projects

- VirtualAssistant (`~/Olbrasoft/VirtualAssistant/`) - Main voice assistant
- edge-tts-server - Text-to-speech service
