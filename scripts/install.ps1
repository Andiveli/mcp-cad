<#
.SYNOPSIS
    Install mcp-cad MCP server for Autodesk Inventor.

.DESCRIPTION
    Creates a Python virtual environment, installs dependencies (pywin32, mcp),
    and generates MCP configuration for OpenCode, Claude Desktop, and Pi.

.PARAMETER InstallDir
    Directory where mcp-cad should be installed (default: current directory).

.PARAMETER RegisterIn
    Where to register the MCP server: OpenCode, Claude, Pi, All (default: All).

.PARAMETER MCPName
    Name for the MCP server entry (default: "mcp-cad").

.EXAMPLE
    .\install.ps1
    Installs in current directory, registers in all supported tools.

.EXAMPLE
    .\install.ps1 -InstallDir "C:\Tools\mcp-cad" -RegisterIn OpenCode
    Installs to C:\Tools\mcp-cad, only registers in OpenCode.
#>

param(
    [string]$InstallDir = (Split-Path -Parent $PSScriptRoot),
    [ValidateSet("OpenCode", "Claude", "Pi", "All")]
    [string]$RegisterIn = "All",
    [string]$MCPName = "mcp-cad"
)

$ErrorActionPreference = "Stop"

# ------------------------------------------------------------------
# 1. Prerequisites check
# ------------------------------------------------------------------

Write-Host "=== mcp-cad Installer ===" -ForegroundColor Cyan
Write-Host ""

# Check Python
$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    Write-Error "Python 3.10+ is required but not found in PATH."
    exit 1
}
$pyVersion = & python -c "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')"
Write-Host "[OK] Python $pyVersion" -ForegroundColor Green

# Check Inventor (optional -- warn if missing)
$inventorProgId = Get-ItemProperty "HKLM:\SOFTWARE\Classes\Inventor.Application\CLSID" -ErrorAction SilentlyContinue
if (-not $inventorProgId) {
    Write-Warning "Autodesk Inventor COM registration not found."
    Write-Warning "mcp-cad will install, but tools will not work until Inventor is installed."
    Write-Host ""
}

# ------------------------------------------------------------------
# 2. Virtual environment
# ------------------------------------------------------------------

$venvDir = Join-Path $InstallDir ".venv"
Write-Host "Creating virtual environment at $venvDir ..."
if (Test-Path $venvDir) {
    Write-Host "  (removing existing .venv)"
    Remove-Item -Recurse -Force $venvDir
}
& python -m venv $venvDir
Write-Host "[OK] Virtual environment created" -ForegroundColor Green

# Activate
$activateScript = Join-Path $venvDir "Scripts\Activate.ps1"
. $activateScript

# ------------------------------------------------------------------
# 3. Install dependencies
# ------------------------------------------------------------------

Write-Host "Installing mcp-cad dependencies ..."
& pip install --upgrade pip -q
& pip install -e "$InstallDir" -q
& pip install pytest -q

Write-Host "[OK] Dependencies installed:" -ForegroundColor Green
& pip list | Select-String -Pattern "mcp-cad|pywin32|mcp"

# ------------------------------------------------------------------
# 4. Generate MCP configuration
# ------------------------------------------------------------------

$pythonExe = Join-Path $venvDir "Scripts\python.exe"

Write-Host ""
Write-Host "=== MCP Configuration ===" -ForegroundColor Cyan
Write-Host ""

# Helper to write config snippets
function Write-ConfigBlock($title, $json) {
    Write-Host "--- $title ---" -ForegroundColor Yellow
    Write-Host $json
    Write-Host ""
}

# OpenCode format (project-level opencode.json)
$openCodeConfig = @"
{
  "`$schema": "https://opencode.ai/config.json",
  "mcp": {
    "$MCPName": {
      "type": "local",
      "command": ["$($pythonExe.Replace('\', '\\'))", "-m", "mcp_cad"]
    }
  }
}
"@

# Claude Desktop format (mcpServers in claude_desktop_config.json)
$claudeConfig = @"
{
  "mcpServers": {
    "$MCPName": {
      "command": "$($pythonExe.Replace('\', '\\'))",
      "args": ["-m", "mcp_cad"]
    }
  }
}
"@

# Pi format (mcpServers in pi config)
$piConfig = @"
{
  "mcpServers": {
    "$MCPName": {
      "command": "$($pythonExe.Replace('\', '\\'))",
      "args": ["-m", "mcp_cad"],
      "directTools": true,
      "lifecycle": "lazy"
    }
  }
}
"@

switch ($RegisterIn) {
    "OpenCode" { Write-ConfigBlock "OpenCode (opencode.json)" $openCodeConfig }
    "Claude"   { Write-ConfigBlock "Claude Desktop" $claudeConfig }
    "Pi"       { Write-ConfigBlock "Pi" $piConfig }
    "All" {
        Write-ConfigBlock "OpenCode (opencode.json)" $openCodeConfig
        Write-ConfigBlock "Claude Desktop" $claudeConfig
        Write-ConfigBlock "Pi" $piConfig
    }
}

# ------------------------------------------------------------------
# 5. Auto-register in project opencode.json
# ------------------------------------------------------------------

Write-Host "=== Auto-registration ===" -ForegroundColor Cyan
Write-Host ""

$projectConfig = Join-Path $InstallDir "opencode.json"

if (Test-Path $projectConfig) {
    Write-Host "Updating project opencode.json with venv Python path ..."

    # Read existing config
    $cfg = $null
    try {
        $raw = Get-Content -Raw $projectConfig
        if ($raw.Trim()) {
            $cfg = $raw | ConvertFrom-Json
        }
    } catch {
        Write-Host "  (existing config unreadable -- replacing)"
    }

    # Build the mcp-cad server entry
    $serverEntry = [PSCustomObject]@{
        type    = "local"
        command = @($pythonExe, "-m", "mcp_cad")
    }

    if (-not $cfg) {
        # Fresh config
        $cfg = [PSCustomObject]@{
            '$schema' = "https://opencode.ai/config.json"
            mcp       = [PSCustomObject]@{}
        }
    }

    # Ensure mcp section exists
    if (-not (Get-Member -InputObject $cfg -Name "mcp" -MemberType Properties)) {
        $cfg | Add-Member -MemberType NoteProperty -Name "mcp" -Value ([PSCustomObject]@{})
    }

    # Add/update mcp-cad
    $mcpServers = $cfg.mcp
    if (Get-Member -InputObject $mcpServers -Name $MCPName -MemberType Properties) {
        $mcpServers.$MCPName = $serverEntry
    } else {
        $mcpServers | Add-Member -MemberType NoteProperty -Name $MCPName -Value $serverEntry
    }

    # Write back
    $cfg | ConvertTo-Json -Depth 5 | Set-Content -Path $projectConfig
    Write-Host "[OK] opencode.json updated -> command: $pythonExe -m mcp_cad" -ForegroundColor Green

    # Global registration offer
    $globalConfigDir = "$env:USERPROFILE\.config\opencode"
    $globalConfigFile = Join-Path $globalConfigDir "opencode.json"

    Write-Host ""
    Write-Host "This config is project-level (only active when you open this folder)."
    Write-Host "To make mcp-cad available globally in ALL OpenCode workspaces,"
    Write-Host "add this to: $globalConfigFile"
    Write-Host ""

    $response = Read-Host "Register globally? (y/N)"
    if ($response -eq 'y' -or $response -eq 'Y') {
        if (-not (Test-Path $globalConfigDir)) {
            New-Item -ItemType Directory -Force -Path $globalConfigDir | Out-Null
        }

        $gCfg = $null
        if (Test-Path $globalConfigFile) {
            try {
                $gRaw = Get-Content -Raw $globalConfigFile
                if ($gRaw.Trim()) {
                    $gCfg = $gRaw | ConvertFrom-Json
                }
            } catch {}
        }
        if (-not $gCfg) {
            $gCfg = [PSCustomObject]@{}
        }
        if (-not (Get-Member -InputObject $gCfg -Name "mcp" -MemberType Properties)) {
            $gCfg | Add-Member -MemberType NoteProperty -Name "mcp" -Value ([PSCustomObject]@{})
        }
        $gMcp = $gCfg.mcp
        if (Get-Member -InputObject $gMcp -Name $MCPName -MemberType Properties) {
            $gMcp.$MCPName = $serverEntry
        } else {
            $gMcp | Add-Member -MemberType NoteProperty -Name $MCPName -Value $serverEntry
        }
        $gCfg | ConvertTo-Json -Depth 5 | Set-Content $globalConfigFile
        Write-Host "[OK] Global registration added to $globalConfigFile" -ForegroundColor Green
    }
} else {
    Write-Host "[WARN] No opencode.json found in project root." -ForegroundColor Yellow
    Write-Host "       The MCP server will not auto-detect in OpenCode."
}

Write-Host ""

# ------------------------------------------------------------------
# 6. Verify installation
# ------------------------------------------------------------------

Write-Host "=== Verification ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Running smoke test ..."

$testResult = & $pythonExe -c "from mcp_cad.inventor.client import RealInventorDriver; print('mcp_cad imports OK')" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] mcp_cad module imports successfully" -ForegroundColor Green
} else {
    Write-Host "[WARN] Import test: $testResult" -ForegroundColor Yellow
    Write-Host "       This is expected if pywin32 COM modules are not available."
    Write-Host "       The server will work once Inventor is accessible."
}

# Test suite
Write-Host "Running test suite ..."
& $pythonExe -m pytest "$InstallDir\tests" -q 2>&1 | Tee-Object -Variable testOutput
if ($LASTEXITCODE -eq 0) {
    $testCount = ($testOutput | Select-String "(\d+) passed" | ForEach-Object { $_.Matches.Groups[1].Value })
    Write-Host "[OK] $testCount tests passed" -ForegroundColor Green
} else {
    Write-Warning "Some tests failed -- this may be expected on non-Inventor machines."
}

Write-Host ""
Write-Host "=== Installation complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Ensure Autodesk Inventor is installed on this machine"
Write-Host "  2. Copy the MCP config block above into your tool's config file"
Write-Host "  3. Restart OpenCode / Claude Desktop / Pi"
Write-Host "  4. Try: 'inventor_connect' in your AI assistant"
Write-Host ""
Write-Host "To start manually: python -m mcp_cad" -ForegroundColor Gray
