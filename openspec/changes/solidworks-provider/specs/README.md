# solidworks-provider Specs

This directory contains the detailed SDD specification artifacts (hybrid engram + openspec) for the SolidWorks Provider / Provider-Agnostic Generalization change.

## Structure
- `generalization/spec.md` — Public surface rename (cad_* primary), deprecated inventor_* aliases, docs/skills updates, TDD requirements.
- `pluggable-server/spec.md` — Config-driven selection ("Cad:Provider"), DI wiring, legacy Inventor:AutoConnect support, pluggability tests.
- `solidworks-basic-loop/spec.md` — McpCad.SolidWorks project, driver/provider/managers skeleton (scoped to minimal viable loop per proposal + engram #272), tagging neutrality, error contracts, provider-agnostic contract tests, risks.

All specs enforce Strict TDD (tests before / alongside changes), base directly on the locked proposal, and include GIVEN/WHEN/THEN acceptance scenarios plus edge/error cases.

## Persistence
Primary: engram topic_key `sdd/solidworks-provider/spec`
Reviewable: these openspec files (following patterns from cad-provider-protocol, weld-feature, etc.).

Next phase typically: sdd-design.
