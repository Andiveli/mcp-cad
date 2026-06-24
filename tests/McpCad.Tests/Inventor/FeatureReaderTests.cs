using McpCad.Inventor.Helpers;
using System;
using System.Collections.Generic;
using System.Dynamic;

namespace McpCad.Tests.Inventor;

/// <summary>
/// Unit tests for FeatureReader (COM traversal of PartFeature subtypes).
/// Strict TDD: tests written first (RED), then minimal impl to pass (GREEN).
/// Uses ExpandoObject hierarchies to simulate Inventor COM PartFeature objects.
/// </summary>
public class FeatureReaderTests
{
    // ── Helpers to build realistic COM fakes (1-based Item access) ───────────
    // Use Expando + delegate for Item. Dynamic call site on a property holding a delegate
    // will invoke the delegate when written as .Item(i).

    private static dynamic CreateFakeCompDef(params dynamic[] featureList)
    {
        dynamic feats = new ExpandoObject();
        var arr = featureList ?? Array.Empty<dynamic>();
        feats.Count = arr.Length;
        feats.Item = (Func<int, dynamic>)(i =>
        {
            if (i < 1 || i > arr.Length) throw new IndexOutOfRangeException();
            return arr[i - 1];
        });

        dynamic comp = new ExpandoObject();
        comp.Features = feats;
        return comp;
    }

    private static dynamic MakeCollection(int count)
    {
        dynamic col = new ExpandoObject();
        var dummies = new dynamic[count];
        for (int k = 0; k < count; k++) dummies[k] = new ExpandoObject();
        col.Count = count;
        col.Item = (Func<int, dynamic>)(i =>
        {
            if (i < 1 || i > dummies.Length) throw new IndexOutOfRangeException();
            return dummies[i - 1];
        });
        return col;
    }

    private static dynamic MakeProfile(string name)
    {
        dynamic p = new ExpandoObject();
        p.Name = name;
        return p;
    }

    // ── Null / guard tests ───────────────────────────────────────────────────

    [Fact]
    public void ReadFeatures_NullCompDef_ReturnsEmptyFeaturesAndWarning()
    {
        var readResult = FeatureReader.ReadFeatures(null!);
        var features = readResult.Item1;
        var warnings = readResult.Item2;

        Assert.NotNull(features);
        Assert.Empty(features);
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.Contains("No component definition", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReadFeatures_EmptyFeaturesCollection_EmitsEmptyWarning()
    {
        // Empty Features collection → reader should still produce a warning (per current impl)
        dynamic compDef = new ExpandoObject();
        dynamic emptyFeats = new ExpandoObject();
        emptyFeats.Count = 0;
        emptyFeats.Item = (Func<int, dynamic>)(i => { throw new IndexOutOfRangeException(); });
        compDef.Features = emptyFeats;

        var readResult = FeatureReader.ReadFeatures(compDef);
        var features = readResult.Item1;
        var warnings = readResult.Item2;

        Assert.NotNull(features);
        Assert.Empty(features);
        Assert.NotEmpty(warnings);
        // Current impl emits a generic "no supported features" warning when nothing is extracted
        bool hasEmptyWarning = false;
        foreach (var w in warnings)
        {
            if (w.Contains("no supported features", StringComparison.OrdinalIgnoreCase) ||
                w.Contains("empty", StringComparison.OrdinalIgnoreCase))
            {
                hasEmptyWarning = true; break;
            }
        }
        Assert.True(hasEmptyWarning);
    }

    // ── Creational order (core contract) ─────────────────────────────────────

    [Fact]
    public void ReadFeatures_PreservesCreationalOrder()
    {
        // GIVEN features created in order: Extrude, then Fillet, then Hole
        // Build directly (no Action<dynamic> lambda — avoids CS1977)
        dynamic extrude = new ExpandoObject();
        extrude.Name = "Extrude1";
        extrude.SubType = "ExtrudeFeature";
        extrude.Profile = MakeProfile("1");
        dynamic ext1 = new ExpandoObject(); ext1.Distance = 10.0; extrude.Extent = ext1;
        extrude.Direction = "Positive";

        dynamic fillet = new ExpandoObject();
        fillet.Name = "Fillet1";
        fillet.SubType = "FilletFeature";
        fillet.Edges = MakeCollection(1);
        fillet.Radius = 0.5;

        dynamic hole = new ExpandoObject();
        hole.Name = "Hole1";
        hole.SubType = "HoleFeature";
        dynamic pt = new ExpandoObject(); pt.X = 0; pt.Y = 0; hole.PlacementPoint = pt;
        hole.Diameter = 0.5; hole.Depth = 1.0;

        dynamic comp = CreateFakeCompDef(extrude, fillet, hole);

        var readResult = FeatureReader.ReadFeatures(comp);
        var features = readResult.Item1;

        Assert.Equal(3, features.Count);
        Assert.Equal("extrude", features[0]["feature_type"]);
        Assert.Equal("fillet", features[1]["feature_type"]);
        Assert.Equal("hole", features[2]["feature_type"]);
        Assert.Equal("Extrude1", features[0]["name"]);
        Assert.Equal("Fillet1", features[1]["name"]);
        Assert.Equal("Hole1", features[2]["name"]);
    }

    // ── Typed descriptor shapes — REAL assertions on keys + values ───────────

    [Fact]
    public void ReadFeatures_ExtrudeYieldsProfileDistanceDirectionOperationTaper()
    {
        dynamic prof = MakeProfile("Sketch1");
        dynamic extent = new ExpandoObject(); extent.Distance = 5.0;
        dynamic extrude = new ExpandoObject();
        extrude.Name = "Extrude1";
        extrude.SubType = "ExtrudeFeature";
        extrude.Profile = prof;
        extrude.Extent = extent;
        extrude.Direction = "Positive";
        extrude.Operation = "NewBody";
        extrude.Taper = 0.0;

        dynamic comp = CreateFakeCompDef(extrude);

        var readResult = FeatureReader.ReadFeatures(comp);
        var features = readResult.Item1;
        var warnings = readResult.Item2;

        if (features.Count == 0)
        {
            Assert.True(false, "No features extracted. Warnings: " + string.Join(" | ", warnings));
        }
        Assert.Single(features);
        bool hasUnsupportedWarning = false;
        foreach (var w in warnings) { if (w.Contains("Unsupported", StringComparison.OrdinalIgnoreCase)) { hasUnsupportedWarning = true; break; } }
        Assert.False(hasUnsupportedWarning);
        var d = features[0];
        Assert.Equal("extrude", d["feature_type"]);
        Assert.Equal("Extrude1", d["name"]);
        Assert.Equal("Sketch1", d["profile"]);
        Assert.Equal(5.0, d["distance"]);
        Assert.Equal("positive", d["direction"]);
        Assert.Equal("new_body", d["operation"]);
        Assert.Equal(0.0, d["taper"]);
    }

    [Fact]
    public void ReadFeatures_FilletYieldsEdgesRadiusMode()
    {
        dynamic edges = MakeCollection(2);
        dynamic fillet = new ExpandoObject();
        fillet.Name = "Fillet1";
        fillet.SubType = "FilletFeature";
        fillet.Edges = edges;
        fillet.Radius = 0.75;

        dynamic comp = CreateFakeCompDef(fillet);

        var readResult = FeatureReader.ReadFeatures(comp);
        var features = readResult.Item1;

        Assert.Single(features);
        var d = features[0];
        Assert.Equal("fillet", d["feature_type"]);
        Assert.Equal("1,2", d["edges"]); // ResolveEdgeList returns indices
        Assert.Equal(0.75, d["radius"]);
        Assert.Equal("constant", d["mode"]);
    }

    [Fact]
    public void ReadFeatures_HoleYieldsPositionDiameterDepthType()
    {
        dynamic pt = new ExpandoObject(); pt.X = 3.0; pt.Y = -1.5;
        dynamic hole = new ExpandoObject();
        hole.Name = "Hole1";
        hole.SubType = "HoleFeature";
        hole.PlacementPoint = pt;
        hole.Diameter = 0.8;
        hole.Depth = 2.5;

        dynamic comp = CreateFakeCompDef(hole);

        var readResult = FeatureReader.ReadFeatures(comp);
        var features = readResult.Item1;

        Assert.Single(features);
        var d = features[0];
        Assert.Equal("hole", d["feature_type"]);
        Assert.Equal(3.0, d["x"]);
        Assert.Equal(-1.5, d["y"]);
        Assert.Equal(0.8, d["diameter"]);
        Assert.Equal(2.5, d["depth"]);
        Assert.Equal("drilled", d["type"]);
    }

    [Fact]
    public void ReadFeatures_CircularPatternYieldsProfileAxisCountAngle_NoParentInV1()
    {
        // v1 limitation: parent_feature_index removed per design review (tracked limitation)
        dynamic prof = MakeProfile("1");
        dynamic pattern = new ExpandoObject();
        pattern.Name = "CircularPattern1";
        pattern.SubType = "CircularPatternFeature";
        pattern.Profiles = prof;  // code looks for Profiles or ParentFeature
        pattern.Axis = "Y";
        pattern.Count = 6;
        pattern.Angle = 360.0;
        // intentionally no ParentFeature set for v1

        dynamic comp = CreateFakeCompDef(pattern);

        var readResult = FeatureReader.ReadFeatures(comp);
        var features = readResult.Item1;

        Assert.Single(features);
        var d = features[0];
        Assert.Equal("circular_pattern", d["feature_type"]);
        Assert.Equal("1", d["profile"]);
        Assert.Equal("Y", d["axis"]);
        Assert.Equal(6, d["count"]);
        Assert.Equal(360.0, d["angle"]);
        Assert.False(d.ContainsKey("parent_feature_index"), "parent_feature_index removed for v1");
    }

    [Fact]
    public void ReadFeatures_UnsupportedTypeEmitsWarningAndContinues()
    {
        dynamic unknown = new ExpandoObject();
        unknown.Name = "iFeature1";
        unknown.SubType = "iFeature";
        // no special props

        dynamic comp = CreateFakeCompDef(unknown);

        var readResult = FeatureReader.ReadFeatures(comp);
        var features = readResult.Item1;
        var warnings = readResult.Item2;

        Assert.Single(features); // still emits a descriptor
        var d = features[0];
        Assert.Equal("unsupported", d["feature_type"]);
        // MapFeatureType lowercases then we store original_type from subType as-is in the unsupported path
        Assert.Equal("ifeature", d["original_type"]);
        Assert.NotEmpty(warnings);
        bool hasUnsupportedMsg = false;
        bool hasName = false;
        foreach (var w in warnings) { if (w.Contains("Unsupported or unrecognized feature type", StringComparison.OrdinalIgnoreCase)) hasUnsupportedMsg = true; if (w.Contains("iFeature1")) hasName = true; }
        Assert.True(hasUnsupportedMsg);
        Assert.True(hasName);
    }

    [Fact]
    public void ReadFeatures_RevolveYieldsProfileAxisAngleOperation()
    {
        dynamic prof = MakeProfile("1");
        dynamic revolve = new ExpandoObject();
        revolve.Name = "Revolve1";
        revolve.SubType = "RevolveFeature";
        revolve.Profile = prof;

        // Axis must be an object with .Name for ResolveAxis (it reads axis.Name)
        dynamic axisObj = new ExpandoObject();
        axisObj.Name = "X";
        revolve.Axis = axisObj;

        revolve.Angle = 180.0;
        revolve.Operation = "Join";

        dynamic comp = CreateFakeCompDef(revolve);

        var readResult = FeatureReader.ReadFeatures(comp);
        var features = readResult.Item1;

        Assert.Single(features);
        var d = features[0];
        Assert.Equal("revolve", d["feature_type"]);
        Assert.Equal("1", d["profile"]);
        Assert.Equal("X", d["axis"]);
        Assert.Equal(180.0, d["angle"]);
        Assert.Equal("join", d["operation"]);
    }

    [Fact]
    public void ReadFeatures_ChamferYieldsEdgesDistanceMode()
    {
        dynamic edges = MakeCollection(1);
        dynamic chamfer = new ExpandoObject();
        chamfer.Name = "Chamfer1";
        chamfer.SubType = "ChamferFeature";
        chamfer.Edges = edges;
        chamfer.Distance = 0.3;

        dynamic comp = CreateFakeCompDef(chamfer);

        var readResult = FeatureReader.ReadFeatures(comp);
        var features = readResult.Item1;

        Assert.Single(features);
        var d = features[0];
        Assert.Equal("chamfer", d["feature_type"]);
        Assert.Equal("1", d["edges"]);
        Assert.Equal(0.3, d["distance"]);
        Assert.Equal("equal_distance", d["mode"]);
    }
}
