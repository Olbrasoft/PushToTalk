# PushToTalk - Claude Code Guide

Linux push-to-talk voice dictation application using Whisper AI.

## Build & Deploy

```bash
# Build
cd ~/Olbrasoft/PushToTalk && dotnet build

# Test (MUST pass before deployment)
dotnet test

# Deploy to production
sudo ./deploy/deploy.sh /opt/olbrasoft/push-to-talk
```

**Production path:** `/opt/olbrasoft/push-to-talk` (ONLY deployment target - no dev environment)

## Architecture

- **PushToTalk.Core** - Interfaces, domain logic, WhisperModelLocator
- **PushToTalk.Linux** - Linux-specific mouse monitoring (libevdev)
- **PushToTalk.App** - Desktop application (GTK, system tray)
- **PushToTalk.Service** - Background service (future)

## Dependencies

| Dependency | Location | Purpose |
|------------|----------|---------|
| Whisper models | `~/.local/share/whisper-models/` | AI speech-to-text (FHS-compliant) |
| libevdev | System package | Mouse button monitoring |

**Required models:**
- `ggml-large-v3-turbo.bin` (1.6 GB) - **Default** - Fast, good quality
- `ggml-large-v3.bin` (2.9 GB) - Highest quality
- `ggml-medium.bin` (1.5 GB) - Medium quality
- `ggml-tiny.bin` (75 MB) - Fast testing

## Configuration

**appsettings.json:**
```json
{
  "Dictation": {
    "GgmlModelPath": "ggml-large-v3-turbo.bin",  // Just filename, WhisperModelLocator finds it
    "WhisperLanguage": "cs",
    "TriggerKey": "CapsLock",
    "CancelKey": "Escape"
  }
}
```

WhisperModelLocator searches in:
1. `~/.local/share/whisper-models/` (preferred)
2. `/usr/share/whisper-models/` (system-wide)
3. `~/apps/asr-models/` (legacy fallback)

## Development Standards

- **.NET 10** (`net10.0`)
- **xUnit + Moq** for testing
- **Strategy Pattern** for mouse monitoring
- **SOLID principles** throughout
- **Production-only deployment** (no separate dev environment)

## Deployment Structure

```
/opt/olbrasoft/push-to-talk/
├── app/                          # Binaries (AppContext.BaseDirectory)
│   ├── push-to-talk
│   ├── appsettings.json
│   └── ...
├── certs/                        # SSL certificates (optional)
│   └── 192.168.0.182+3.p12
```

Desktop entry: `~/.local/share/applications/io.olbrasoft.PushToTalk.desktop`
Icons: `~/.local/share/icons/hicolor/*/apps/io.olbrasoft.PushToTalk.*`

## Running

**From GNOME launcher:**
Press Super key → search "Push To Talk"

**From command line:**
```bash
/opt/olbrasoft/push-to-talk/app/push-to-talk
```

## Troubleshooting

**Model not found:**
```bash
# Check if models exist
ls -lh ~/.local/share/whisper-models/

# If missing, copy from legacy location
cp ~/apps/asr-models/*.bin ~/.local/share/whisper-models/
```

**Application not found in launcher:**
```bash
# Check desktop entry
cat ~/.local/share/applications/io.olbrasoft.PushToTalk.desktop

# Update icon cache
gtk-update-icon-cache -f -t ~/.local/share/icons/hicolor
```
