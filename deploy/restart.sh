#!/usr/bin/env bash
set -euo pipefail

SERVICE="${IDK_SYSTEMD_SERVICE:-idk.service}"

sudo systemctl restart "$SERVICE"
