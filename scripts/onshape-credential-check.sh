#!/usr/bin/env bash
# Safe diagnostics — never prints secrets.
set -euo pipefail

CFG="${HOME}/.config/mcp-cad/config.json"
ENV_FILE="$(dirname "$0")/onshape-keys.env"

echo "=== mcp-cad Onshape credential check ==="

if [[ ! -f "$CFG" ]]; then
	echo "MISSING: $CFG"
	exit 1
fi

jq -e '.providers.onshape.credentials' "$CFG" >/dev/null

AK="$(jq -r '.providers.onshape.credentials.access_key // ""' "$CFG")"
SK="$(jq -r '.providers.onshape.credentials.secret_key // ""' "$CFG")"

echo "config.json access_key: len=${#AK} prefix=${AK:0:4}…"
echo "config.json secret_key: len=${#SK} (hidden)"

if [[ -z "$AK" || -z "$SK" ]]; then
	echo "FAIL: access_key or secret_key empty in config.json"
	exit 1
fi

if [[ "$AK" != on_* ]]; then
	echo "WARN: access_key should start with on_ (Onshape API key)"
fi

if [[ "$SK" == *$'\n'* || "$SK" == *$'\r'* ]]; then
	echo "FAIL: secret_key contains newline — re-paste without line breaks"
	exit 1
fi

if [[ -f "$ENV_FILE" ]]; then
	EAK="$(grep -E '^ONSHAPE_ACCESS_KEY=' "$ENV_FILE" | cut -d= -f2- | tr -d '\r' || true)"
	ESK="$(grep -E '^ONSHAPE_SECRET_KEY=' "$ENV_FILE" | cut -d= -f2- | tr -d '\r' || true)"
	if [[ -n "$EAK" && "$EAK" != "$AK" ]]; then
		echo "WARN: onshape-keys.env access_key DIFFERS from config.json (Kulala/smoke used config first now)"
	fi
	if [[ -n "$ESK" && "$ESK" != "$SK" ]]; then
		echo "WARN: onshape-keys.env secret_key DIFFERS from config.json"
	fi
fi

echo ""
echo "If run-onshape-smoke.sh step 1 is HTTP 401:"
echo "  1) Onshape → Account → API keys → revoke old key, create NEW pair"
echo "  2) Paste Clave de acceso + Clave secreta into config.json only"
echo "  3) /mcp reconnect mcp-cad"
echo "  4) Re-run ./scripts/run-onshape-smoke.sh — step 1 must be HTTP 200"
echo ""
echo "403 on elements WITH step 1 still 401 usually means bad keys; ignore 403 until step 1 is 200."
