#!/usr/bin/env bash
# Same HMAC as McpCad.Onshape — uses ~/.config/mcp-cad/config.json first (same as MCP server).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CFG="${HOME}/.config/mcp-cad/config.json"
IDS="${SCRIPT_DIR}/onshape-doc-ids.json"

if [[ -f "$CFG" ]]; then
	export ONSHAPE_ACCESS_KEY
	export ONSHAPE_SECRET_KEY
	ONSHAPE_ACCESS_KEY="$(jq -r '.providers.onshape.credentials.access_key // empty' "$CFG")"
	ONSHAPE_SECRET_KEY="$(jq -r '.providers.onshape.credentials.secret_key // empty' "$CFG")"
fi

if [[ -z "${ONSHAPE_ACCESS_KEY:-}" || -z "${ONSHAPE_SECRET_KEY:-}" ]]; then
	if [[ -f "${SCRIPT_DIR}/onshape-keys.env" ]]; then
		set -a && source "${SCRIPT_DIR}/onshape-keys.env" && set +a
	fi
fi

: "${ONSHAPE_ACCESS_KEY:?No access_key in $CFG or onshape-keys.env}"
: "${ONSHAPE_SECRET_KEY:?No secret_key in $CFG or onshape-keys.env}"

call() {
	local label="$1" method="$2" api_path="$3" body="${4:-}"
	echo "==> $label"
	local code
	local curl_args=(-sS -o /tmp/onshape-smoke.json -w '%{http_code}' -u "${ONSHAPE_ACCESS_KEY}:${ONSHAPE_SECRET_KEY}")
	if [[ -n "$body" ]]; then
		curl_args+=(-H "Content-Type: application/json" -d "$body")
	fi
	if [[ "$method" == GET ]]; then
		code=$(curl "${curl_args[@]}" "https://cad.onshape.com${api_path}")
	else
		code=$(curl -X "$method" "${curl_args[@]}" "https://cad.onshape.com${api_path}")
	fi
	echo "HTTP $code"
	head -c 400 /tmp/onshape-smoke.json
	echo
	echo
}

call "1) List documents" GET "/api/v6/documents"

DID="$(jq -r .documentId "$IDS")"
WID="$(jq -r .workspaceId "$IDS")"
WVM="$(jq -r .wvm "$IDS")"
EID="$(jq -r .elementId "$IDS")"

call "2) Workspaces" GET "/api/v6/documents/d/${DID}/workspaces"
call "3) Elements" GET "/api/v6/documents/d/${DID}/${WVM}/${WID}/elements"
call "4) Part Studio features" GET "/api/v6/partstudios/d/${DID}/${WVM}/${WID}/e/${EID}/features"
