#!/bin/bash
set -e

# GitHub Actions Self-Hosted Runner Uninstallation Script
# For PushToTalk

REPO_NAME="PushToTalk"
RUNNER_DIR="$HOME/actions-runner-${REPO_NAME}"

echo "╔══════════════════════════════════════════════════════════════╗"
echo "║     GitHub Actions Self-Hosted Runner Uninstallation         ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo
echo "Runner directory: ${RUNNER_DIR}"
echo

if [ ! -d "${RUNNER_DIR}" ]; then
    echo "❌ Runner directory not found: ${RUNNER_DIR}"
    exit 1
fi

cd "${RUNNER_DIR}"

# Step 1: Stop service
echo "🛑 Stopping runner service..."
if [ -f "svc.sh" ]; then
    sudo ./svc.sh stop || true
    echo "✅ Service stopped"
fi

# Step 2: Uninstall service
echo "🗑️  Uninstalling service..."
if [ -f "svc.sh" ]; then
    sudo ./svc.sh uninstall || true
    echo "✅ Service uninstalled"
fi

# Step 3: Remove runner from GitHub
echo "🔓 Removing runner from GitHub..."
if [ -f "config.sh" ]; then
    # Get removal token
    REMOVAL_TOKEN=$(gh api --method POST \
        "repos/Olbrasoft/${REPO_NAME}/actions/runners/remove-token" \
        --jq .token)

    if [ -n "$REMOVAL_TOKEN" ]; then
        ./config.sh remove --token "${REMOVAL_TOKEN}"
        echo "✅ Runner removed from GitHub"
    else
        echo "⚠️  Could not get removal token, skipping GitHub cleanup"
    fi
fi

# Step 4: Remove directory
echo "🗑️  Removing runner directory..."
cd "$HOME"
rm -rf "${RUNNER_DIR}"
echo "✅ Directory removed"

echo
echo "╔══════════════════════════════════════════════════════════════╗"
echo "║            ✅ Runner uninstalled successfully!                ║"
echo "╚══════════════════════════════════════════════════════════════╝"
