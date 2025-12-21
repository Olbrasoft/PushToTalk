# GitHub Actions Self-Hosted Runner

Scripts for installing and managing GitHub Actions self-hosted runner for automatic PushToTalk deployment.

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated:
  ```bash
  gh auth login
  ```

## Installation

Run the installation script:

```bash
./scripts/install-runner.sh
```

This will:
1. Download GitHub Actions runner
2. Configure runner for Olbrasoft/PushToTalk repository
3. Install runner as systemd service
4. Start the service

## Verification

Check runner status:

```bash
# Using svc.sh
sudo ~/actions-runner-PushToTalk/svc.sh status

# Using systemctl
systemctl --user status actions.runner.Olbrasoft-PushToTalk.*
```

View runner logs:

```bash
journalctl -u actions.runner.Olbrasoft-PushToTalk.* -f
```

Check runner on GitHub:
- Go to: https://github.com/Olbrasoft/PushToTalk/settings/actions/runners
- You should see your runner listed as "Online"

## How It Works

### Automatic Deployment Flow

1. **Developer pushes to main branch**
   ```bash
   git push origin main
   ```

2. **Build workflow runs** (ubuntu-latest)
   - Calculates version: `1.0.${{ github.run_number }}`
   - Restores dependencies
   - Builds with auto-calculated version
   - Runs tests

3. **Deploy workflow triggers** (self-hosted runner)
   - Waits for build success
   - Pulls latest code
   - Runs `deploy/deploy.sh /opt/olbrasoft/push-to-talk --no-version-bump`
   - Application deployed and restarted

### Version Management

Versions are automatically incremented:
- Format: `1.0.X` where X = GitHub run number
- Example: Push #123 → version `1.0.123`
- No manual version bumps needed

## Management Commands

### Start/Stop/Restart

```bash
# Stop runner
sudo ~/actions-runner-PushToTalk/svc.sh stop

# Start runner
sudo ~/actions-runner-PushToTalk/svc.sh start

# Restart runner
sudo ~/actions-runner-PushToTalk/svc.sh stop
sudo ~/actions-runner-PushToTalk/svc.sh start
```

### View Logs

```bash
# Follow logs
journalctl -u actions.runner.Olbrasoft-PushToTalk.* -f

# Last 100 lines
journalctl -u actions.runner.Olbrasoft-PushToTalk.* -n 100
```

## Uninstallation

Remove runner completely:

```bash
./scripts/uninstall-runner.sh
```

This will:
1. Stop the service
2. Uninstall systemd service
3. Remove runner from GitHub
4. Delete runner directory

## Troubleshooting

### Runner offline

1. Check service status:
   ```bash
   sudo ~/actions-runner-PushToTalk/svc.sh status
   ```

2. Check logs:
   ```bash
   journalctl -u actions.runner.Olbrasoft-PushToTalk.* -n 50
   ```

3. Restart service:
   ```bash
   sudo ~/actions-runner-PushToTalk/svc.sh stop
   sudo ~/actions-runner-PushToTalk/svc.sh start
   ```

### Deploy workflow not running

1. Check if runner is online:
   - https://github.com/Olbrasoft/PushToTalk/settings/actions/runners

2. Check build workflow succeeded:
   - https://github.com/Olbrasoft/PushToTalk/actions

3. Check deploy workflow logs:
   - https://github.com/Olbrasoft/PushToTalk/actions/workflows/deploy.yml

### Permission issues

Make sure runner user has sudo access for deploy.sh:

```bash
# Edit sudoers (replace <username> with your username)
sudo visudo

# Add line:
<username> ALL=(ALL) NOPASSWD: /home/jirka/Olbrasoft/PushToTalk/deploy/deploy.sh
```

## Security

- Runner runs as your user account
- Uses GitHub CLI authentication
- Service runs with systemd
- Deploy script has sudo access (configured via sudoers)

## Files

- `install-runner.sh` - Installation script
- `uninstall-runner.sh` - Uninstallation script
- `README.md` - This file

## Runner Location

- Directory: `~/actions-runner-PushToTalk/`
- Service: `actions.runner.Olbrasoft-PushToTalk.*`
- Logs: `journalctl -u actions.runner.Olbrasoft-PushToTalk.*`
