/**
 * Credentials + HTTP Basic for scripts/onshape-api.http (httpyac / kulala).
 * Same as McpCad.Onshape and Onshape OpenAPI BasicAuth.
 */
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

function loadCredentials(variables) {
	let accessKey;
	let secretKey;
	const dir = scriptsDir();
	const home = process.env.HOME || process.env.USERPROFILE || "";
	const configPath = path.join(home, ".config", "mcp-cad", "config.json");

	// 1) Same source as MCP server (DPR)
	if (fs.existsSync(configPath)) {
		try {
			const cfg = JSON.parse(fs.readFileSync(configPath, "utf8"));
			const creds = cfg?.providers?.onshape?.credentials || {};
			accessKey = creds.access_key;
			secretKey = creds.secret_key;
		} catch {
			/* ignore */
		}
	}

	// 2) scripts/onshape-keys.env
	if (!accessKey || !secretKey) {
		const envFile = path.join(dir, "onshape-keys.env");
		if (fs.existsSync(envFile)) {
			const text = fs.readFileSync(envFile, "utf8");
			for (const line of text.split("\n")) {
				const m = line.match(/^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$/);
				if (!m) continue;
				const key = m[1];
				const val = m[2].replace(/^['"]|['"]$/g, "").trim();
				if (key === "ONSHAPE_ACCESS_KEY" && val) accessKey = val;
				if (key === "ONSHAPE_SECRET_KEY" && val) secretKey = val;
			}
		}
	}

	// 3) httpyac @dotenv / shell
	accessKey =
		accessKey ||
		(variables && variables.get("ONSHAPE_ACCESS_KEY")) ||
		process.env.ONSHAPE_ACCESS_KEY;
	secretKey =
		secretKey ||
		(variables && variables.get("ONSHAPE_SECRET_KEY")) ||
		process.env.ONSHAPE_SECRET_KEY;

	if (!accessKey || !secretKey) {
		throw new Error(
			"Missing Onshape keys. Create scripts/onshape-keys.env (see onshape-keys.env.example) " +
				"or set providers.onshape.credentials in ~/.config/mcp-cad/config.json",
		);
	}

	return { accessKey, secretKey };
}

function applyAuth(request, method, apiPath, body = "", vars = null) {
	const scope = vars || (typeof variables !== "undefined" ? variables : null);
	const { accessKey, secretKey } = loadCredentials(scope);
	const basic = Buffer.from(`${accessKey}:${secretKey}`, "utf8").toString(
		"base64",
	);
	request.headers["Authorization"] = `Basic ${basic}`;
	if (body) request.body = body;
}

function scriptsDir() {
	if (typeof __dirname !== "undefined" && __dirname.includes("mcp-cad")) {
		return __dirname;
	}
	return (
		process.env.MCP_CAD_SCRIPTS ||
		"/home/samael/Escritorio/Programacion/work/fisica/mcp-cad/scripts"
	);
}

function loadDocIds() {
	const p = path.join(scriptsDir(), "onshape-doc-ids.json");
	if (!fs.existsSync(p)) {
		throw new Error(`Missing ${p} (document/workspace/element ids)`);
	}
	try {
		return JSON.parse(fs.readFileSync(p, "utf8"));
	} catch (e) {
		throw new Error(`Invalid onshape-doc-ids.json: ${e.message}`);
	}
}

function requireAuthModule() {
	const modPath = path.join(scriptsDir(), "onshape-httpyac-auth.js");
	return require(modPath);
}

module.exports = {
	loadCredentials,
	applyAuth,
	scriptsDir,
	loadDocIds,
	requireAuthModule,
};
