# SolidWorks Provider (McpCad.SolidWorks) - Skeleton Notes

**Status**: Driver + provider + managers + SwTagStore/SelectionHelper (structural + Round 2 surgical fixes for the confirmed issues: tagging/selection MVP, COM, contract, build cond, claims toned, indent/docs, lifetime, inspection). Basic 10-step loop shapes viable via index "1" + @tag (MVP skeleton after fixes); full runtime requires live SW + "Cad:Provider=SolidWorks" config. See apply-progress + tasks.md + solidworks-basic-loop spec. TODO verify on live.

**Interop (dev machine only)**:
- HintPath in .csproj: C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll (and swconst).
- Private=false (per design).
- Not redistributed in portable/installer this increment (follow-up).

**Known challenges (from engram #272)**:
- sw-01: COM activation/lifetime, GetActiveObject quirks, add-in interference.
- sw-02: attach-to-running preferred vs CreateObject/launch.
- sw-03: version detection (RevisionNumber).
- Selection/tagging: differs from Inventor (no AttributeSets; uses SelectionManager + SelectByID2(mark) + GetPersistReference3 / PID). MVP uses indices from profiles() + in-mem stub for @tag. Full SwTagStore/SelectionHelper in Phase 5+.
- InsertSketch / Documents.Add / SaveAs3 signatures can vary slightly by SW version/release; literals + fallbacks used. See manager code comments.

**Scope**: Document + basic Sketch + Feature (extrude min) + Inspection + helpers (MVP). All other IMechanicalCadProvider surface returns clear ErrorResult "Not yet implemented for SolidWorks provider. See roadmap." (or delegates to stub mgrs). Full loop runtime on live SW.

**Live SolidWorks**: Restricted to explicit sdd-verify phase (Strict TDD). Unit tests use real driver (safe error paths when not running) + shapes.

**Build note**: SW interop required on build/dev for full types (HintPath). Added Compile Remove + conditional refs in csprojs/tests/server for skeleton build on non-SW/CI machines (per design/tasks). Runtime + full loop still requires live SW + Cad:Provider=SolidWorks. TODO verify.

**Next**: Further per tasks (advanced tagging, remaining, verify on live). Current: structural + fixes for Round 2 confirmed.

See: design.md, tasks.md, solidworks-basic-loop/spec.md, driver/provider/manager source comments.

(Added for 3.7 project-local doc.)
