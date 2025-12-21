#!/bin/bash
set -e

# GitHub Actions Self-Hosted Runner Installation Script
# For PushToTalk automatic deployment

REPO_OWNER="Olbrasoft"
REPO_NAME="PushToTalk"
RUNNER_DIR="$HOME/actions-runner-${REPO_NAME}"
RUNNER_VERSION="2.321.0"

echo "╔══════════════════════════════════════════════════════════════╗"
echo "║       GitHub Actions Self-Hosted Runner Installation         ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo
echo "Repository: ${REPO_OWNER}/${REPO_NAME}"
echo "Runner directory: ${RUNNER_DIR}"
echo

# Step 1: Create runner directory
echo "📁 Creating runner directory..."
mkdir -p "${RUNNER_DIR}"
cd "${RUNNER_DIR}"

# Step 2: Download GitHub Actions runner
echo "📥 Downloading GitHub Actions runner v${RUNNER_VERSION}..."
if [ ! -f "actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz" ]; then
    curl -o "actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz" -L \
        "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz"
    echo "✅ Downloaded runner archive"
else
    echo "ℹ️  Runner archive already exists, skipping download"
fi

# Step 3: Extract runner
echo "📦 Extracting runner..."
if [ ! -f "config.sh" ]; then
    tar xzf "actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz"
    echo "✅ Extracted runner"
else
    echo "ℹ️  Runner already extracted, skipping"
fi

# Step 4: Get registration token from GitHub
echo "🔑 Getting registration token from GitHub..."
REGISTRATION_TOKEN=$(gh api --method POST \
    "repos/${REPO_OWNER}/${REPO_NAME}/actions/runners/registration-token" \
    --jq .token)

if [ -z "$REGISTRATION_TOKEN" ]; then
    echo "❌ Failed to get registration token from GitHub"
    echo "Please make sure you have GitHub CLI installed and authenticated:"
    echo "  gh auth login"
    exit 1
fi
echo "✅ Got registration token"

# Step 5: Configure runner
echo "⚙️  Configuring runner..."
if [ ! -f ".runner" ]; then
    ./config.sh \
        --url "https://github.com/${REPO_OWNER}/${REPO_NAME}" \
        --token "${REGISTRATION_TOKEN}" \
        --name "debian-local-$(hostname)" \
        --labels "self-hosted,Linux,X64,debian" \
        --work "_work" \
        --unattended \
        --replace
    echo "✅ Runner configured"
else
    echo "ℹ️  Runner already configured, skipping"
fi

# Step 6: Install runner as systemd service
echo "🔧 Installing systemd service..."
sudo ./svc.sh install $USER
echo "✅ Service installed"

# Step 7: Start runner service
echo "🚀 Starting runner service..."
sudo ./svc.sh start
echo "✅ Service started"

# Step 8: Check status
echo
echo "📊 Runner status:"
sudo ./svc.sh status || true

echo
echo "╔══════════════════════════════════════════════════════════════╗"
echo "║               ✅ Runner installed successfully!               ║"
echo "╠══════════════════════════════════════════════════════════════╣"
echo "║  Service: actions.runner.${REPO_OWNER}-${REPO_NAME}.*"
echo "║  Directory: ${RUNNER_DIR}"
echo "║"
echo "║  Commands:"
echo "║    Status:  sudo ${RUNNER_DIR}/svc.sh status"
echo "║    Stop:    sudo ${RUNNER_DIR}/svc.sh stop"
echo "║    Start:   sudo ${RUNNER_DIR}/svc.sh start"
echo "║    Restart: sudo ${RUNNER_DIR}/svc.sh stop && sudo ${RUNNER_DIR}/svc.sh start"
echo "║"
echo "║  Logs:"
echo "║    journalctl -u actions.runner.${REPO_OWNER}-${REPO_NAME}.* -f"
echo "╚══════════════════════════════════════════════════════════════╝"
echo
echo "🎉 Deploy workflow will now run automatically on this runner!"
