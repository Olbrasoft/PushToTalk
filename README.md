# PushToTalk

Push-to-Talk functionality for Linux voice assistant. Monitors mouse buttons and triggers speech-to-text recording.

## Getting Started

### Prerequisites

- .NET 10 SDK
- Linux (tested on Debian/Ubuntu)
- libevdev (for mouse button monitoring)

### Installation

```bash
git clone https://github.com/Olbrasoft/PushToTalk.git
cd PushToTalk
dotnet build
```

### Running Tests

```bash
dotnet test
```

## Project Structure

```
PushToTalk/
├── src/
│   └── PushToTalk.Core/       # Core logic, mouse monitoring
├── tests/
│   └── PushToTalk.Core.Tests/ # Unit tests
├── .github/workflows/         # CI/CD
└── PushToTalk.sln
```

## Architecture

- **Strategy Pattern** for different mouse button monitoring implementations
- **SOLID principles** throughout

## License

MIT License - see [LICENSE](LICENSE) file.
