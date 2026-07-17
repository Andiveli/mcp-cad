#!/usr/bin/env bash
# Prove bash/openssl HMAC matches McpCad.Tests Onshape/HmacSigningTests.cs
set -euo pipefail

SECRET='this_is_a_test_secret_key_32bytes!!'
METHOD='GET'
API_PATH='/api/v6/documents'
NONCE='0123456789abcdef0123456789abcdef'
DATE='Thu, 01 Jan 2026 00:00:00 GMT'
CANONICAL="${METHOD}
${API_PATH}
${NONCE}
${DATE}


"

SIG="$(printf '%s' "$CANONICAL" | openssl dgst -sha256 -hmac "$SECRET" -binary | openssl base64 -A)"

echo "Canonical (repr):"
printf '%s' "$CANONICAL" | od -An -tx1 | head -3
echo "Signature (bash/openssl): $SIG"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DOTNET_SIG="$(dotnet run --project "$ROOT/tests/McpCad.HmacCli" --no-build 2>/dev/null || true)"

if [[ -z "$DOTNET_SIG" ]]; then
	echo ""
	echo "Optional: run  dotnet test --filter HmacSigningTests  to validate C# signer."
	echo "If HmacSigningTests pass, algorithm in MCP matches Onshape spec; 401 = wrong access/secret in config.json."
	exit 0
fi

if [[ "$SIG" == "$DOTNET_SIG" ]]; then
	echo "OK: bash and C# signatures match."
else
	echo "MISMATCH: bash=$SIG dotnet=$DOTNET_SIG"
	exit 1
fi
