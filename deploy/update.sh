#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${IDK_APP_DIR:-/opt/idk-src}"
PUBLISH_DIR="${IDK_PUBLISH_DIR:-/opt/idk}"
SERVICE="${IDK_SYSTEMD_SERVICE:-idk.service}"
BRANCH="${IDK_UPDATE_BRANCH:-master}"

cd "$APP_DIR"

git fetch origin "$BRANCH"
git checkout "$BRANCH"
git reset --hard "origin/$BRANCH"

dotnet restore idk.slnx
dotnet publish src/Idk.Bot/Idk.Bot.csproj -c Release -o "$PUBLISH_DIR" --no-restore

install -m 0755 deploy/update.sh "$PUBLISH_DIR/update.sh.new"
install -m 0755 deploy/restart.sh "$PUBLISH_DIR/restart.sh.new"
mv "$PUBLISH_DIR/update.sh.new" "$PUBLISH_DIR/update.sh"
mv "$PUBLISH_DIR/restart.sh.new" "$PUBLISH_DIR/restart.sh"

sudo systemctl restart "$SERVICE"
