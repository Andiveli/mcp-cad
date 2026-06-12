# Archive Report: installer-gui-wizard

**Archived**: 2026-06-12  
**Branch**: `feature/tui-improvements`  
**Original location**: `openspec/changes/installer-gui-wizard/`  
**Archive location**: `openspec/changes/archive/2026-06-12-installer-gui-wizard/`

## Executive Summary

Added a Windows Forms GUI wizard to `McpCad.Installer` as the **default** experience for download-and-double-click users, while preserving the existing Spectre.Console TUI (`--tui`) and all CLI flags (`--recommended`, `--all`, `--agents`, `--help`). Backend registration, CAD skills deployment, and quirúrgico backups remain centralized in `McpAgent.cs`; the wizard duplicates only the small per-agent execution loop inside `InstallerWizardForm.cs` per locked user decision.

**Verdict**: PASS WITH WARNINGS (see verify-report.md)

## Delivery

| Batch | Scope | Status |
|-------|-------|--------|
| 1 | `net8.0-windows`, P/Invoke, routing skeleton, `.bat` | Complete (`965f286`) |
| 2 | `InstallerWizardForm.cs` (4 pages, Back, reuse) | Complete |
| 3 | Docs, verify, archive | Complete (this archive) |

**Chained PRs**: Applied as sequential commits on `feature/tui-improvements` (TUI commits `aabbd1b`–`49d3f3e`, backups `18fff21`, GUI `965f286`).

## Key Files (production)

| File | Role |
|------|------|
| `src/McpCad.Installer/InstallerWizardForm.cs` | WinForms wizard UI |
| `src/McpCad.Installer/Program.cs` | Mode routing: CLI → GUI default → `--tui` TUI |
| `src/McpCad.Installer/McpCad.Installer.csproj` | `net8.0-windows` + WinForms |
| `McpCad-Install.bat` | Launches exe with `%*` (GUI default) |
| `README.md`, `docs/tools-reference.md` | User-facing GUI-first docs |

## Locked Decisions (honored)

- Do not modify Spectre TUI loop — **honored** (additive routing only)
- Duplicate install loop inside form — **honored**
- Back button — **implemented**
- State path `scripts/tui/state.json` — **unchanged**
- Strict TDD during apply batches — documented in apply-progress

## Follow-ups (out of archive scope)

- Manual GUI E2E on clean Windows VM (verify-report matrix)
- Full portable publish when branch WIP (template/macro tools) no longer breaks Server build
- Merge `feature/tui-improvements` → `main` when ready