#!/usr/bin/env bash
# End-to-end MCP stdio: health + ws_list against live Onshape (requires ~/.config/mcp-cad/config.json).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SERVER="${ROOT}/dist/mcp-cad/McpCad.Server"
CFG="${HOME}/.config/mcp-cad/config.json"
IDS="${ROOT}/scripts/onshape-doc-ids.json"

if [[ ! -x "$SERVER" ]]; then
	echo "Build first: dotnet publish src/McpCad.Server/McpCad.Server.csproj -c Release -o dist/mcp-cad"
	exit 1
fi

DID="$(jq -r .documentId "$IDS")"

send() {
	local payload="$1"
	printf '%s' "$payload" | "$SERVER" 2>/dev/null
}

echo "=== MCP onshape_health ==="
HEALTH_JSON=$(send '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"onshape_health","arguments":{}}}' | tail -1)
echo "$HEALTH_JSON" | jq -r '.result.content[0].text // .error // .' 2>/dev/null || echo "$HEALTH_JSON"

echo ""
echo "=== MCP onshape_ws_list (document_id=$DID) ==="
WS_JSON=$(send "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"onshape_ws_list\",\"arguments\":{\"document_id\":\"$DID\"}}}" | tail -1)
TEXT=$(echo "$WS_JSON" | jq -r '.result.content[0].text // empty' 2>/dev/null)
if [[ -z "$TEXT" ]]; then
	echo "$WS_JSON" | jq . 2>/dev/null || echo "$WS_JSON"
	exit 1
fi

echo "$TEXT" | jq . 2>/dev/null || echo "$TEXT"
if echo "$TEXT" | jq -e '.success == true' >/dev/null 2>&1; then
	echo ""
	echo "OK: MCP Onshape ws_list succeeded."
else
	echo ""
	echo "FAIL: check credentials and /mcp reconnect mcp-cad in Pi."
	exit 1
fi
