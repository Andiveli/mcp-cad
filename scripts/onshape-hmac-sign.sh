#!/usr/bin/env bash
# Sign one Onshape request (for rest.nvim / curl without httpyac JS).
# Usage:
#   source scripts/onshape-keys.env   # or export keys
#   eval "$(scripts/onshape-hmac-sign.sh headers GET /api/v6/documents)"
#   curl -sS -H "Authorization: $AUTH" -H "On-Nonce: $NONCE" -H "On-Date: $DATE" \
#     "https://cad.onshape.com/api/v6/documents"
set -euo pipefail

METHOD="${1:-GET}"
API_PATH="${2:-/api/v6/documents}"
BODY="${3:-}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CFG="${HOME}/.config/mcp-cad/config.json"
if [[ -f "$CFG" ]] && command -v jq >/dev/null; then
	ONSHAPE_ACCESS_KEY="$(jq -r '.providers.onshape.credentials.access_key // empty' "$CFG")"
	ONSHAPE_SECRET_KEY="$(jq -r '.providers.onshape.credentials.secret_key // empty' "$CFG")"
	export ONSHAPE_ACCESS_KEY ONSHAPE_SECRET_KEY
fi
if [[ -z "${ONSHAPE_ACCESS_KEY:-}" || -z "${ONSHAPE_SECRET_KEY:-}" ]]; then
	if [[ -f "${SCRIPT_DIR}/onshape-keys.env" ]]; then
		# shellcheck disable=SC1091
		set -a && source "${SCRIPT_DIR}/onshape-keys.env" && set +a
	fi
fi

: "${ONSHAPE_ACCESS_KEY:?ONSHAPE_ACCESS_KEY missing (config.json or onshape-keys.env)}"
: "${ONSHAPE_SECRET_KEY:?ONSHAPE_SECRET_KEY missing (config.json or onshape-keys.env)}"

NONCE="$(openssl rand -hex 16)"
DATE="$(LC_TIME=C date -u '+%a, %d %b %Y %H:%M:%S GMT')"
CONTENT_TYPE=""
if [[ -n "$BODY" ]]; then
	CONTENT_TYPE="application/json"
fi

CANONICAL="${METHOD}
${API_PATH}
${NONCE}
${DATE}
${CONTENT_TYPE}
${BODY}"

SIG="$(printf '%s' "$CANONICAL" | openssl dgst -sha256 -hmac "$ONSHAPE_SECRET_KEY" -binary | openssl base64 -A)"
AUTH="OnshapeKey ${ONSHAPE_ACCESS_KEY}:${SIG}"

case "${4:-headers}" in
headers)
	printf 'export NONCE=%q\n' "$NONCE"
	printf 'export DATE=%q\n' "$DATE"
	printf 'export AUTH=%q\n' "$AUTH"
	;;
curl)
	echo "curl -sS -w '\\nHTTP %{http_code}\\n' \\"
	echo "  -H 'Authorization: ${AUTH}' \\"
	echo "  -H 'On-Nonce: ${NONCE}' \\"
	echo "  -H 'On-Date: ${DATE}' \\"
	if [[ -n "$BODY" ]]; then
		echo "  -H 'Content-Type: application/json' \\"
		echo "  -d '${BODY}' \\"
	fi
	echo "  'https://cad.onshape.com${API_PATH}'"
	;;
*)
	echo "Usage: onshape-hmac-sign.sh [METHOD] [PATH] [BODY] [headers|curl]" >&2
	exit 1
	;;
esac
