#!/bin/sh
# Install HACS into the running HA container
# Usage: ./scripts/setup-hacs.sh

set -e

CONTAINER="ha-njord-dev"

echo "Installing HACS into $CONTAINER..."
docker exec "$CONTAINER" sh -c '
  apk add --no-cache wget unzip 2>/dev/null || apt-get update && apt-get install -y wget unzip 2>/dev/null
  cd /config
  wget -q -O hacs.zip "https://github.com/hacs/integration/releases/latest/download/hacs.zip"
  mkdir -p custom_components/hacs
  unzip -o hacs.zip -d custom_components/hacs
  rm hacs.zip
  echo "HACS installed. Restart HA to activate."
'

echo ""
echo "Restarting Home Assistant..."
docker restart "$CONTAINER"

echo ""
echo "Done! Wait ~30s then open http://localhost:8123"
echo "  1. Complete HA onboarding"
echo "  2. Go to Settings → Devices & Services → Add Integration → HACS"
echo "  3. njord is already loaded (mounted from local repo)"
echo "  4. Add njord: Settings → Devices & Services → Add Integration → njord Weather"
echo "     Host: njord  Port: 8081"
