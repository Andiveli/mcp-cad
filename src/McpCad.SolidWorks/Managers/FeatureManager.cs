using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using McpCad.SolidWorks.Helpers;
using ModelDoc2 = SolidWorks.Interop.sldworks.ModelDoc2;
using IFeatureManager = SolidWorks.Interop.sldworks.IFeatureManager;
using IFeature = SolidWorks.Interop.sldworks.IFeature;

namespace McpCad.SolidWorks.Managers;

/// <summary>
/// Feature manager for SolidWorks basic loop (Phase 4.3 per tasks).
/// Implements minimal Extrude(profile, distance, ...) using profile resolve (index priority "1" + @tag via SwTagStore) + SelectByID2 + mark + InsertExtrude.
/// 
/// SW APIs (exact per design/tasks 4.3):
/// - doc.Extension.SelectByID2(..., mark) to select profile (mark != 0 for extrude profiles)
/// - doc.FeatureManager.InsertExtrude( or InsertExtrude2 / CreateExtrudeDefinition + Add ) 
///   Common: IFeature feat = featMgr.InsertExtrude( endCond, rev, dist, ... ) or variant with taper, dir, op.
///   Map direction/operation to swEndConditions_e (1=Blind etc), swDirection (1=positive), swFeatureOperation (0=base etc).
///   Literals used for resilience (swconst may bind differently); documented.
/// 
/// Other features (Revolve, Fillet etc) return clear ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.") per narrow scope.
/// Tagging/selection: uses SelectionHelper + SwTagStore (store real ref from Create* + resolve to selectable profile via mark for extrude in basic loop).
/// If exact InsertExtrude overload/signature variance on target SW: literal calls here + "TODO verify on live SW in verify phase" comment.
/// Profile resolution lives in Helpers (per design).
/// Mirrors Inventor FeatureManager shape (resolve + map + call) but SW COM.
/// </summary>
public class FeatureManager
{
    private readonly SolidWorksDriver _driver;
    private readonly SwTagStore _tagStore;
    private readonly SelectionHelper _selectionHelper;

    // SW feature constants (literals from swconst.swEndConditions_e etc for cross-version; TODO verify on live in verify phase)
    private const int SwEndCondBlind = 1;      // swEndCondBlind
    private const int SwDirectionPositive = 1; // positive dir
    private const int SwOpJoin = 0;            // base/join approx (common for new body on first extrude)

    public FeatureManager(SolidWorksDriver driver, SwTagStore? tagStore = null, SelectionHelper? selHelper = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _tagStore = tagStore ?? new SwTagStore();
        _selectionHelper = selHelper ?? new SelectionHelper();
    }

    private ModelDoc2 ActiveDocument()
    {
        var doc = _driver.ActiveDocument as ModelDoc2
            ?? throw new CadConnectionException("No active document. Open or create a document first.");
        return doc;
    }

    private IFeatureManager FeatureMgr()
    {
        var doc = ActiveDocument();
        return (IFeatureManager)doc.FeatureManager;
    }

    /// <summary>
    /// Minimal extrude for basic loop viability.
    /// Resolve profile (index "1" priority per MVP, or @tag), select with mark, call InsertExtrude variant.
    /// </summary>
    public Dictionary<string, object?> Extrude(
        string profile, double distance,
        string direction = "positive",
        double taper = 0.0,
        string operation = "new_body")
    {
        try
        {
            var doc = ActiveDocument();
            var featMgr = FeatureMgr();

            // Sketch key for tag resolve (MVP uses active; resolved inside SelectionHelper + SwTagStore for @tag/PID)
            string sketchKey = "active";

            // Resolve + select profile with mark (mark=1 for primary profile); tag/index now produce usable via SwTagStore/SelectionHelper
            _selectionHelper.ClearSelection(doc);
            bool profileSelected = _selectionHelper.SelectProfileByIndexOrTag(doc, profile, sketchKey, _tagStore, mark: 1);
            if (!profileSelected)
            {
                // Fallback per common SW: try select without name for active profile context
                profileSelected = doc.Extension.SelectByID2("", "SKETCHSEGMENT", 0, 0, 0, false, 1, null, 0);
            }
            if (!profileSelected)
            {
                throw new CadComException($"Profile selection failed for '{profile}'. Ensure sketch_profiles() returned usable index or tag set on create (ref captured from Create* lines/circles). TODO verify SelectByID2 behavior on live SW in verify phase.");
            }

            // Map params (use literals; full swconst.sw* later)
            int endCond = SwEndCondBlind;
            int dir = (direction?.ToLowerInvariant() == "negative") ? -SwDirectionPositive : SwDirectionPositive;
            int op = SwOpJoin; // new_body / join approx for first feature

            // Early-bound IFeatureManager.InsertExtrude2 (common stable); removed dyn Create*Definition / Insert* chains + guessed overloads (repeated fragile pattern).
            // TODO verify exact signature + behavior on live SolidWorks in sdd-verify phase (InsertExtrude2 param count/order for taper/op/dir)
            object? newFeat = null;
            try
            {
                newFeat = featMgr.InsertExtrude2(endCond, false, distance, dir, 0, op, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            }
            catch
            {
                try { newFeat = featMgr.InsertExtrude(endCond, false, distance, dir); } catch { }
            }

            if (newFeat == null)
            {
                throw new CadComException("InsertExtrude returned no feature (selection or API variance). TODO verify exact InsertExtrude/SelectByID2 + mark on live SW in verify phase.");
            }

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["feature_type"] = "extrude",
                ["distance"] = distance,
                ["direction"] = direction,
                ["taper"] = taper != 0.0 ? taper : null,
                ["operation"] = operation,
                ["feature_name"] = (newFeat as IFeature)?.Name ?? "Extrude1",
                ["note"] = "Minimal extrude via SelectByID2(mark) + InsertExtrude. Full profile resolve + taper/op in follow-up."
            };
        }
        catch (CadConnectionException) { throw; }
        catch (CadComException) { throw; }
        catch (Exception ex)
        {
            throw new CadComException($"Failed to extrude: {ex.Message}. TODO verify on live SW in verify phase.", ex);
        }
    }

    // ── All other features: clear not-impl per tasks 4.3 / narrow basic loop scope ─────────────

    public Dictionary<string, object?> Revolve(string profile, string axis, double angle = 360.0, string direction = "positive", string operation = "join")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Sweep(string profile, string path, string sweepType = "path", string operation = "new_body", double taper = 0, string pathSketch = "", string profileSketch = "")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Fillet(string edges, double radius, string mode = "constant")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Chamfer(string edges, double distance, string mode = "equal_distance")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> CircularPattern(string profile, string axis, int count, double angle = 360.0, bool fitWithinAngle = true, bool naturalDirection = true)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> MirrorFeature(string profile, string mirrorPlane)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> RectangularPattern(string profile, string xAxis, int xCount, double xSpacing, string yAxis = "", int yCount = 1, double ySpacing = 0.0)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Loft(string profiles, string operation = "new_body")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Coil(string profile, string axis, double pitch, double revolutions, string operation = "new_body")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Rib(string profile, double thickness, string direction = "normal", string operation = "new_body")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Emboss(string profile, double depth, string type = "emboss_from_face")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Derive(string sourcePath)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Hole(double x, double y, double diameter, double depth, string type = "drilled", string operation = "join", string direction = "positive")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Thread(string face, string specification, string direction = "right")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> InspectEdges()
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Shell(string faces, double thickness, string direction = "inside", string operation = "new_body")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Draft(string faces, double angle, string mode = "fixed_edge", string pullDirection = "z", string fixedEntity = "")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Split(string splitTool, string removeSide = "positive", string targetBody = "")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Combine(string baseBody, string toolBodies, string operation = "join", bool keepToolBodies = false)
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");

    public Dictionary<string, object?> Thicken(string faces, double thickness, string direction = "positive", string operation = "new_body")
        => ErrorResult.Create("Not yet implemented for SolidWorks provider. See roadmap.");
}
