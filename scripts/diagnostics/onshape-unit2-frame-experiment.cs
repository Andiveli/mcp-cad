// Offline-first bounded Onshape frame diagnostic. Live execution requires --live <evidence-dir> or --resume-isolated <new-evidence-dir>.
// Resume uses only the receipt-pinned isolated document; it never accepts target identifiers.
// Live evidence is an immutable, flushed snapshot journal: <run-id>.<sequence>.json.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using McpCad.Onshape.Builders;

static class Program
{
    const string DefaultBase = "https://cad.onshape.com";
    const double Depth = .004, Tolerance = 1e-7;
    const int BodyLimit = 512 * 1024;
    const string ResumeDocument = "a2089a4bc04d53fc6874d6b7", ResumeWorkspace = "3800a6dfd27df2f682c627a5", ResumeElement = "92e39c519027e4c612f4dbb3";
    static readonly (string Name, P[] Points)[] Shapes =
    [
        ("Top", [new(3.1, 1.7), new(7.9, 2.4), new(4.2, 6.8)]),
        ("Front", [new(11.3, 3.9), new(16.9, 4.8), new(12.6, 9.1)]),
        ("Right", [new(20.1, 6.3), new(25.8, 7.9), new(21.7, 13.2)]),
    ];
    static readonly Dictionary<string, string> BuilderDatumIds = new(StringComparer.Ordinal)
    { ["Top"] = "JDC", ["Front"] = "JCC", ["Right"] = "JEC" };

    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args is ["--self-test"]) return SelfTest();
        if (args.Length != 2 || args[0] is not ("--live" or "--resume-isolated")) return Fail("usage");
        using var cancelled = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancelled.Cancel(); };
        try { await Live(args[1], args[0] == "--resume-isolated", cancelled.Token); return 0; }
        catch (OperationCanceledException) { return Fail("cancelled"); }
        catch (HttpFailure x) { return Fail($"http stage={x.Stage} status={x.Status}"); }
        catch (DiagnosticFailure x) { return Fail($"inconclusive stage={x.Stage} type={x.Type}"); }
        catch (Exception) { return Fail("inconclusive stage=internal type=unexpected"); }
    }

    // Offline self-test creates isolated journal fixtures under the approved private /tmp artifact root.
    static int SelfTest()
    {
        try
        {
            var defaults = JsonDocument.Parse("""{"defaultFeatures":[{"name":"Top","featureId":"top-feature"},{"name":"Front","featureId":"front-feature"},{"name":"Right","featureId":"right-feature"}]}""");
            Check(DefaultDatums(defaults.RootElement)["Top"] == "top-feature", "defaultFeatures");
            ExpectFailure(() => DefaultDatums(JsonDocument.Parse("""{"features":[]}""").RootElement));
            Check(PreScript().Contains("p.origin / meter", StringComparison.Ordinal), "origin3d");
            var current = """{"btType":"BTFSValueArray-1499","value":[{"btType":"BTFSValueNumber-772","value":1.25},{"btType":"BTFSValueString-1422","value":"Top"}]}""";
            var legacy = """{"btType":"com.belmonttech.serialize.fsvalue.BTFSValueArray","value":[{"btType":"com.belmonttech.serialize.fsvalue.BTFSValueNumber","value":2},{"btType":"com.belmonttech.serialize.fsvalue.BTFSValueString","value":"Front"}]}""";
            Check(Fs.Value(JsonDocument.Parse(current).RootElement) is List<object?> c && (double)c[0]! == 1.25 && (string)c[1]! == "Top", "current");
            Check(Fs.Value(JsonDocument.Parse(legacy).RootElement) is List<object?> l && (double)l[0]! == 2 && (string)l[1]! == "Front", "legacy");
            var scalar = """{"btType":"BTFeatureScriptEvalResponse-1859","microversionSkew":false,"notices":[],"result":{"btType":"com.belmonttech.serialize.fsvalue.BTFSValueArray","typeTag":"ARRAY","value":[{"btType":"com.belmonttech.serialize.fsvalue.BTFSValueNumber","typeTag":"NUMBER","value":0},{"btType":"com.belmonttech.serialize.fsvalue.BTFSValueNumber","typeTag":"NUMBER","value":1},{"btType":"com.belmonttech.serialize.fsvalue.BTFSValueString","typeTag":"STRING","value":"probe"}]}}""";
            Check(Fs.Response(JsonDocument.Parse(scalar)) is List<object?> s && (double)s[0]! == 0 && (double)s[1]! == 1 && (string)s[2]! == "probe", "scalar_response");
            ExpectFailure(() => Fs.Value(JsonDocument.Parse("""{"btType":"com.belmonttech.serialize.fsvalue.BTFSValueNumber-772","value":1}""").RootElement));
            ExpectFailure(() => Fs.Value(JsonDocument.Parse("""{"btType":"Unknown","value":1}""").RootElement));
            ExpectFailure(() => Fs.Value(JsonDocument.Parse("""{"btType":"com.belmonttech.serialize.fsvalue.BTFSValueNumber","value":"2"}""").RootElement));
            ExpectFailure(() => Fs.Value(JsonDocument.Parse("""{"btType":"com.belmonttech.serialize.fsvalue.BTFSValueNumber","value":true}""").RootElement));
            var f = new Frame("Top", "JDC", new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), []);
            Check(Same(f, f).Ok, "same");
            Check(!Same(f, f with { X = new(0, 1, 0), Y = new(-1, 0, 0) }).Ok, "rotated");
            Check(!Same(f, f with { Y = new(0, -1, 0) }).Ok, "reflected");
            Check(!Same(f, f with { X = new(2, 0, 0) }).Ok, "invalidbasis");
            ExpectFailure(() => Frames(new List<object?> { new List<object?> { "Top", "JDC", new List<object?> { 0d, 0d }, new List<object?> { 1d, 0d, 0d }, new List<object?> { 0d, 1d, 0d }, new List<object?> { 0d, 0d, 1d } } }, false));
            Check(Shapes.All(s => Prism(f, f with { Vertices = ExpectedPrism(f, s.Points) }, s.Points)), "prism");
            Check(!Prism(f, f with { Vertices = ExpectedPrism(f, Shapes[0].Points).Select((v, i) => i == 5 ? v with { Z = Depth * 2 } : v).ToList() }, Shapes[0].Points), "excessdepth");
            Check(!Prism(f, f with { Vertices = ExpectedPrism(f, Shapes[0].Points).Skip(1).Append(new(99, 0, 0)).ToList() }, Shapes[0].Points), "missingbase");
            Check(!Prism(f, f with { Vertices = ExpectedPrism(f, Shapes[0].Points).Take(5).Append(ExpectedPrism(f, Shapes[0].Points)[0]).ToList() }, Shapes[0].Points), "duplicate");
            TransportTests();
            EvidenceRegressionTests();
            using var sketch = OnshapeSketchBuilder.BuildClosedProfile(Shapes[0].Points.Select(p => new SketchPoint(p.X, p.Y)).ToArray(), "Top");
            using var extrude = OnshapeFeatureTemplates.ExtrudeFromSketch("self-test", Depth, "positive", "new");
            Check(sketch.RootElement.TryGetProperty("feature", out _) && extrude.RootElement.TryGetProperty("feature", out _), "builders");
            Console.WriteLine("self-test: parser, frames, prism, transport, builders, and filesystem journal passed; no credentials or live transport invoked");
            return 0;
        }
        catch (Exception) { return Fail("self-test failed"); }
    }

    static async Task Live(string evidenceDir, bool resume, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(evidenceDir) || !Directory.Exists(evidenceDir)) throw new DiagnosticFailure("arguments", "evidence_dir");
        Evidence state;
        try { state = new Evidence(evidenceDir); } catch (IOException) { throw new DiagnosticFailure("evidence", "existing_or_unwritable_run"); }
        using (state)
        try
        {
            var baseUri = BaseUri();
            var credentials = Credentials(); // Explicit live-only credential boundary.
            using var transport = new Transport(baseUri, credentials.Access, credentials.Secret);
            state.WriteLimit = resume ? 6 : 8; state.Set("started", new { baseUrl = baseUri.ToString(), resume });
            if (resume)
            {
                state.Doc = ResumeDocument; state.Workspace = ResumeWorkspace; state.Element = ResumeElement;
                state.Set("resumed_pinned_document_partstudio", new { source = "prior_confirmed_receipt" });
            }
            else
            {
                using var doc = await Write(transport, state, "create_document", "/api/v6/documents", new { name = $"onshape-unit2-frame-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}" }, cancellationToken);
                state.Doc = Required(doc.RootElement, "id"); state.Workspace = Required(doc.RootElement.GetProperty("defaultWorkspace"), "id"); state.Confirmed("document");
                using var part = await Write(transport, state, "create_partstudio", $"/api/v6/partstudios/d/{Esc(state.Doc)}/w/{Esc(state.Workspace)}", new { name = "Unit 2 frame diagnostic" }, cancellationToken);
                state.Element = Required(part.RootElement, "id"); state.Confirmed("partstudio");
            }
            var featurePath = FeaturePath(state); state.Endpoints.Add(EndpointShape(featurePath));
            using var features = await transport.Get(featurePath, "get_features", cancellationToken);
            if (resume && (!features.RootElement.TryGetProperty("features", out var fs) || fs.ValueKind != JsonValueKind.Array || fs.GetArrayLength() != 0)) throw new DiagnosticFailure("get_features", "resume_features_not_empty_or_missing");
            state.LibraryVersion = features.RootElement.GetProperty("libraryVersion").GetInt32(); state.DefaultFeatureIds = DefaultDatums(features.RootElement); state.Confirmed("default_features");
            using var preflight = await Eval(transport, state, PreScript(), "preflight_eval", cancellationToken);
            var before = Frames(Fs.Response(preflight), false); state.Before = before; state.Set("preflight_frames_observed", null); RequireFrames(before, BuilderDatumIds); state.Confirmed("preflight_frames");
            foreach (var shape in Shapes)
            {
                using var sketch = OnshapeSketchBuilder.BuildClosedProfile(shape.Points.Select(p => new SketchPoint(p.X, p.Y)).ToArray(), shape.Name);
                using var created = await Write(transport, state, $"create_sketch_{shape.Name}", featurePath, sketch.RootElement, cancellationToken);
                state.Sketches[shape.Name] = FeatureId(created.RootElement); state.Confirmed($"sketch_{shape.Name}");
                using var extrusion = OnshapeFeatureTemplates.ExtrudeFromSketch(state.Sketches[shape.Name], Depth, "positive", "new");
                using var extruded = await Write(transport, state, $"create_extrude_{shape.Name}", featurePath, extrusion.RootElement, cancellationToken);
                state.Extrudes[shape.Name] = FeatureId(extruded.RootElement); state.Confirmed($"extrude_{shape.Name}");
            }
            using var postflight = await Eval(transport, state, PostScript(state), "post_geometry_eval", cancellationToken);
            var after = Frames(Fs.Response(postflight), true); RequireFrames(after, state.Sketches); state.After = after;
            var comparisons = Shapes.Select(s => new { datum = s.Name, frame = Same(before[s.Name], after[s.Name]), prism = Prism(before[s.Name], after[s.Name], s.Points) }).ToArray();
            state.Comparisons = comparisons;
            if (!comparisons.All(x => x.frame.Ok && x.prism)) { Finish(state, "inconclusive_evidence", new { reason = "frame_or_prism_mismatch" }); throw new DiagnosticFailure("evidence", "inconclusive_evidence"); }
            Finish(state, "complete", null);
        }
        catch (Exception x)
        {
            if (!state.Terminal) state.Set(string.IsNullOrEmpty(state.PendingWrite) ? "stopped" : "stopped_unknown_mutation_after_dispatch", new { error = ErrorType(x), pendingWrite = string.IsNullOrEmpty(state.PendingWrite) ? null : state.PendingWrite });
            throw;
        }
    }

    static async Task<JsonDocument> Write(Transport t, Evidence s, string stage, string path, object body, CancellationToken ct)
    {
        if (++s.Writes > s.WriteLimit) throw new DiagnosticFailure(stage, "write_limit");
        s.Endpoints.Add(EndpointShape(path)); s.PendingWrite = stage;
        s.Set("dispatching_" + stage, new { endpoint = EndpointShape(path), mutationUncertain = true });
        return await t.Send(HttpMethod.Post, path, body, stage, ct);
    }
    static async Task<JsonDocument> Eval(Transport t, Evidence s, string script, string stage, CancellationToken ct)
    {
        if (++s.Evals > 2) throw new DiagnosticFailure(stage, "eval_limit");
        var path = $"/api/v17/partstudios/d/{Esc(s.Doc)}/w/{Esc(s.Workspace)}/e/{Esc(s.Element)}/featurescript?rollbackBarIndex=-1";
        s.Endpoints.Add(EndpointShape(path));
        return await t.Send(HttpMethod.Post, path, new { btType = "BTFeatureScriptEvalCall-2377", script, libraryVersion = s.LibraryVersion }, stage, ct);
    }

    static Uri BaseUri()
    {
        var text = Environment.GetEnvironmentVariable("ONSHAPE_BASE_URL") ?? DefaultBase;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "cad.onshape.com" || uri.Port != 443 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.UserInfo.Length != 0) throw new DiagnosticFailure("base", "unrecognized_base");
        return uri;
    }
    static (string Access, string Secret) Credentials()
    {
        var a = Environment.GetEnvironmentVariable("ONSHAPE_ACCESS_KEY"); var s = Environment.GetEnvironmentVariable("ONSHAPE_SECRET_KEY");
        if (!string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(s)) return (a, s);
        if (!string.IsNullOrWhiteSpace(a) || !string.IsNullOrWhiteSpace(s)) throw new DiagnosticFailure("credentials", "partial_environment");
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "mcp-cad", "config.json");
        try { using var cfg = JsonDocument.Parse(File.ReadAllText(path)); var c = cfg.RootElement.GetProperty("providers").GetProperty("onshape").GetProperty("credentials"); return (Required(c, "access_key"), Required(c, "secret_key")); }
        catch (DiagnosticFailure) { throw; } catch (Exception) { throw new DiagnosticFailure("credentials", "unavailable"); }
    }

    static Dictionary<string, string> DefaultDatums(JsonElement root)
    {
        var answer = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("defaultFeatures", out var fs) || fs.ValueKind != JsonValueKind.Array) throw new DiagnosticFailure("get_features", "missing_defaultFeatures");
        foreach (var f in fs.EnumerateArray())
        {
            if (!f.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String) continue;
            var name = n.GetString();
            if (name is not ("Top" or "Front" or "Right")) continue;
            if (!answer.TryAdd(name, FeatureId(f))) throw new DiagnosticFailure("get_features", "ambiguous_defaultFeatures");
        }
        if (answer.Count != 3) throw new DiagnosticFailure("get_features", "ambiguous_defaultFeatures");
        return answer;
    }
    static string FeatureId(JsonElement root)
    {
        if (root.TryGetProperty("featureState", out var state) && state.TryGetProperty("featureStatus", out var status) && status.ValueKind == JsonValueKind.String && status.GetString() is "ERROR" or "UNKNOWN") throw new DiagnosticFailure("feature_response", "feature_error");
        return root.TryGetProperty("featureId", out _) ? Required(root, "featureId") : root.TryGetProperty("feature", out var f) ? Required(f, "featureId") : throw new DiagnosticFailure("feature_response", "missing_feature_id");
    }
    static string Required(JsonElement o, string name) => o.TryGetProperty(name, out var x) && x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()) ? x.GetString()! : throw new DiagnosticFailure("response", "missing_id");
    static string FeaturePath(Evidence s) => $"/api/v6/partstudios/d/{Esc(s.Doc)}/w/{Esc(s.Workspace)}/e/{Esc(s.Element)}/features";
    static string Esc(string id) => Uri.EscapeDataString(id);
    static string EndpointShape(string path) => path.Contains("featurescript", StringComparison.Ordinal) ? "/api/v17/partstudios/d/{document}/w/{workspace}/e/{element}/featurescript?rollbackBarIndex=-1" : path == "/api/v6/documents" ? path : path.EndsWith("/features", StringComparison.Ordinal) ? "/api/v6/partstudios/d/{document}/w/{workspace}/e/{element}/features" : "/api/v6/partstudios/d/{document}/w/{workspace}";

    static string PreScript() => """
function(context is Context, queries) {
    return mapArray(["Top", "Front", "Right"], function(name) {
        var faces = evaluateQuery(context, qCreatedBy(makeId(name), EntityType.FACE));
        if (size(faces) != 1) throw regenError("datum");
        var p = alignCanonically(context, evPlane(context, {"face" : faces[0]}));
        return [name, transientQueriesToStrings(faces)[0], p.origin / meter, p.x, cross(p.normal, p.x), p.normal];
    });
}
""";
    static string PostScript(Evidence s) => "function(context is Context, queries) { return [" + string.Join(",", Shapes.Select(x => PostOne(x.Name, SafeId(s.Sketches[x.Name]), SafeId(s.Extrudes[x.Name])))) + "]; }";
    static string PostOne(string name, string sketch, string extrude) =>
        "function() { var p = evOwnerSketchPlane(context, {\"entity\" : qCreatedBy(makeId(\"" + sketch + "\"), EntityType.EDGE)}); var bodies = evaluateQuery(context, qCreatedBy(makeId(\"" + extrude + "\"), EntityType.BODY)); if (size(bodies) != 1) throw regenError(\"body\"); var v = evaluateQuery(context, qOwnedByBody(bodies[0], EntityType.VERTEX)); if (size(v) != 6) throw regenError(\"vertices\"); return [\"" + name + "\", \"" + sketch + "\", p.origin / meter, p.x, cross(p.normal,p.x), p.normal, mapArray(v, function(vertex) { return evVertexPoint(context, {\"vertex\" : vertex}) / meter; })]; }()";
    static string SafeId(string id) => id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') ? id : throw new DiagnosticFailure("response", "unsafe_returned_id");

    static Dictionary<string, Frame> Frames(object value, bool geometry)
    {
        if (value is not List<object?> rows) throw new DiagnosticFailure("eval", "result_shape");
        var map = new Dictionary<string, Frame>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row is not List<object?> r || r.Count != (geometry ? 7 : 6) || r[0] is not string name || r[1] is not string id) throw new DiagnosticFailure("eval", "result_row");
            if (!map.TryAdd(name, new Frame(name, id, Vector(r[2]), Vector(r[3]), Vector(r[4]), Vector(r[5]), geometry ? Vectors(r[6]) : []))) throw new DiagnosticFailure("eval", "duplicate_frame");
        }
        return map;
    }
    static void RequireFrames(Dictionary<string, Frame> frames, IReadOnlyDictionary<string, string> ids)
    {
        if (frames.Count != 3 || Shapes.Any(x => !frames.TryGetValue(x.Name, out var f) || !ids.TryGetValue(x.Name, out var id) || f.Id != id || !Valid(f))) throw new DiagnosticFailure("eval", "ambiguous_or_invalid_frame");
    }
    static V Vector(object? value) { var n = Numbers(value); return n.Count == 3 ? new(n[0], n[1], n[2]) : throw new DiagnosticFailure("eval", "vector"); }
    static List<V> Vectors(object? value) => value is List<object?> xs ? xs.Select(Vector).ToList() : throw new DiagnosticFailure("eval", "vertices");
    static List<double> Numbers(object? value) { if (value is not List<object?> xs || xs.Any(x => x is not double d || !double.IsFinite(d))) throw new DiagnosticFailure("eval", "number"); return xs.Cast<double>().ToList(); }
    static bool Valid(Frame f) => Finite(f.O) && Finite(f.X) && Finite(f.Y) && Finite(f.N) && Length(f.X).Near(1) && Length(f.Y).Near(1) && Length(f.N).Near(1) && Math.Abs(Dot(f.X, f.Y)) <= Tolerance && Math.Abs(Dot(f.X, f.N)) <= Tolerance && Math.Abs(Dot(f.Y, f.N)) <= Tolerance && Max(f.O, new(0, 0, 0)) <= Tolerance && Max(Cross(f.X, f.Y), f.N) <= Tolerance;
    static Comparison Same(Frame a, Frame b) => new(Valid(a) && Valid(b), Max(a.O, b.O), Max(a.X, b.X), Max(a.Y, b.Y), Max(a.N, b.N));
    static bool Prism(Frame before, Frame after, P[] points)
    {
        if (!Valid(before) || !Valid(after) || after.Vertices.Count != 6 || after.Vertices.Any(v => !Finite(v))) return false;
        var expected = ExpectedPrism(before, points); var used = new bool[after.Vertices.Count];
        foreach (var want in expected) { var match = Enumerable.Range(0, after.Vertices.Count).FirstOrDefault(i => !used[i] && Max(want, after.Vertices[i]) <= Tolerance); if (used[match] || Max(want, after.Vertices[match]) > Tolerance) return false; used[match] = true; }
        return true;
    }
    static List<V> ExpectedPrism(Frame frame, P[] points) => points.Select(p => Add(frame.O, Add(Scale(frame.X, p.X * .01), Scale(frame.Y, p.Y * .01)))).Concat(points.Select(p => Add(Add(frame.O, Add(Scale(frame.X, p.X * .01), Scale(frame.Y, p.Y * .01))), Scale(frame.N, Depth)))).ToList();
    static V Add(V a, V b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z); static V Scale(V v, double n) => new(v.X * n, v.Y * n, v.Z * n); static V Cross(V a, V b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X); static double Dot(V a, V b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z; static double Length(V v) => Math.Sqrt(Dot(v, v)); static bool Finite(V v) => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z); static double Max(V a, V b) => Math.Max(Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)), Math.Abs(a.Z - b.Z)); static bool Near(this double value, double expected) => Math.Abs(value - expected) <= Tolerance;

    static void EvidenceRegressionTests()
    {
        var dir = Path.Combine("/tmp/onshape-frame-harness.198E8jhA", "evidence-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        using var journal = new Evidence(dir);
        ExpectIOException(() => { using var overlapping = new Evidence(dir); });
        Check(!Directory.EnumerateFiles(dir, "*.json").Any(), "overlap_no_snapshot");
        journal.Doc = "confirmed-document"; journal.Workspace = "confirmed-workspace"; journal.Element = "confirmed-element";
            journal.Sketches["Top"] = "confirmed-sketch"; journal.Extrudes["Top"] = "confirmed-extrude"; journal.Confirmed("document");
        journal.PendingWrite = "create_partstudio"; journal.Set("dispatching_create_partstudio", null);
        var prior = Evidence.Recover(dir, journal.RunId);
        Check(prior.DocumentId == "confirmed-document" && prior.PendingWrite == "create_partstudio" && prior.Snapshot is JsonElement full && full.GetProperty("workspaceId").GetString() == "confirmed-workspace" && full.GetProperty("elementId").GetString() == "confirmed-element" && full.GetProperty("sketchFeatureIds").GetProperty("Top").GetString() == "confirmed-sketch" && full.GetProperty("extrudeFeatureIds").GetProperty("Top").GetString() == "confirmed-extrude", "prior_snapshot");
        File.WriteAllText(Path.Combine(dir, $"onshape-unit2-frame-evidence.{journal.RunId}.999999.json"), "{\"snapshotState\":\"complete\",\"sequence\":999}");
        Check(Evidence.Recover(dir, journal.RunId).Sequence == prior.Sequence, "valid_incomplete_tail_ignored");
        journal.FailNextSnapshotForTests = true; ExpectIOException(() => journal.Set("injected_failure", null));
        var recovered = Evidence.Recover(dir, journal.RunId);
        Check(recovered.DocumentId == "confirmed-document" && recovered.PendingWrite == "create_partstudio" && recovered.Sequence == prior.Sequence, "partial_tail_ignored");
        journal.FailNextSnapshotForTests = true; ExpectIOException(() => Finish(journal, "complete", null));
        Check(!journal.Terminal, "terminal_after_flush");
    }
    static void Finish(Evidence state, string stage, object? extra) { state.Set(stage, extra); state.Terminal = true; }
    static void ExpectIOException(Action action) { try { action(); } catch (IOException) { return; } throw new InvalidOperationException("expected_io_failure"); }

    static void TransportTests()
    {
        foreach (var code in new[] { HttpStatusCode.TooManyRequests, HttpStatusCode.Found }) { var h = new ReplyHandler(() => new HttpResponseMessage(code)); using var t = new Transport(h); ExpectAsync<HttpFailure>(() => t.Get("/x", "test", CancellationToken.None)); Check(h.Calls == 1, "single_send"); }
        using (var t = new Transport(new ReplyHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{") }))) ExpectAsync<DiagnosticFailure>(() => t.Get("/x", "test", CancellationToken.None));
        using (var t = new Transport(new ReplyHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[BodyLimit + 1]) }))) ExpectAsync<DiagnosticFailure>(() => t.Get("/x", "test", CancellationToken.None));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); using var pending = new Transport(new ReplyHandler(() => new HttpResponseMessage(HttpStatusCode.OK))); ExpectAsync<OperationCanceledException>(() => pending.Get("/x", "test", cancelled.Token));
    }
    static void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); }
    static void ExpectFailure(Action action) { try { action(); } catch (DiagnosticFailure) { return; } throw new InvalidOperationException("expected_failure"); }
    static void ExpectAsync<T>(Func<Task> action) where T : Exception { try { action().GetAwaiter().GetResult(); } catch (T) { return; } throw new InvalidOperationException("expected_async_failure"); }
    static int Fail(string message) { Console.Error.WriteLine(message); return 1; }
    static string ErrorType(Exception x) => x switch { HttpFailure h => $"http_{h.Status}", OperationCanceledException => "cancelled", DiagnosticFailure d => d.Type, _ => "unexpected" };

    sealed class Transport : IDisposable
    {
        readonly HttpClient client; readonly AuthenticationHeaderValue auth;
        public Transport(Uri baseUri, string access, string secret) : this(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }, baseUri, access, secret) { }
        internal Transport(HttpMessageHandler handler) : this(handler, new Uri(DefaultBase), "test", "test") { }
        Transport(HttpMessageHandler handler, Uri baseUri, string access, string secret) { client = new HttpClient(handler) { BaseAddress = baseUri, Timeout = Timeout.InfiniteTimeSpan }; auth = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(access + ":" + secret))); }
        public Task<JsonDocument> Get(string path, string stage, CancellationToken ct) => Send(HttpMethod.Get, path, null, stage, ct);
        public async Task<JsonDocument> Send(HttpMethod method, string path, object? body, string stage, CancellationToken ct)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct); linked.CancelAfter(TimeSpan.FromSeconds(25));
            using var request = new HttpRequestMessage(method, path) { Headers = { Authorization = auth } };
            if (body is not null) request.Content = new StringContent(body is JsonElement e ? e.GetRawText() : JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            if (!response.IsSuccessStatusCode) throw new HttpFailure(stage, (int)response.StatusCode);
            await using var stream = await response.Content.ReadAsStreamAsync(linked.Token); using var bytes = new MemoryStream(); var buffer = new byte[8192];
            while (true) { var read = await stream.ReadAsync(buffer.AsMemory(), linked.Token); if (read == 0) break; if (bytes.Length + read > BodyLimit) throw new DiagnosticFailure(stage, "body_limit"); bytes.Write(buffer, 0, read); }
            try { return JsonDocument.Parse(bytes.ToArray()); } catch (JsonException) { throw new DiagnosticFailure(stage, "malformed_json"); }
        }
        public void Dispose() => client.Dispose();
    }
    sealed class ReplyHandler(Func<HttpResponseMessage> reply) : HttpMessageHandler
    { public int Calls { get; private set; } protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage _, CancellationToken ct) { Calls++; ct.ThrowIfCancellationRequested(); return Task.FromResult(reply()); } }
    sealed class Evidence : IDisposable
    {
        const string Prefix = "onshape-unit2-frame-evidence";
        readonly string dir; readonly FileStream owner;
        public string RunId { get; } = Guid.NewGuid().ToString("N");
        public string Doc = "", Workspace = "", Element = "", PendingWrite = "";
        public int LibraryVersion, Writes, Evals, Sequence, WriteLimit = 8;
        public bool Terminal, FailNextSnapshotForTests;
        public List<string> Endpoints = [];
        public Dictionary<string, string> DefaultFeatureIds = [], Sketches = [], Extrudes = [];
        public Dictionary<string, Frame>? Before, After;
        public object? Comparisons;
        public Evidence(string evidenceDir)
        {
            dir = evidenceDir;
            owner = new FileStream(Path.Combine(dir, Prefix + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        public void Confirmed(string stage)
        {
            var pending = PendingWrite;
            PendingWrite = "";
            try { Set(stage + "_confirmed", null); }
            catch { PendingWrite = pending; throw; }
        }
        public void Set(string stage, object? extra)
        {
            var next = ++Sequence;
            var snapshot = new { snapshotState = "complete", sequence = next, schema = "onshape-unit2-frame/v1", timestamp = DateTimeOffset.UtcNow, stage, writes = Writes, readonlyEvals = Evals, pendingWrite = string.IsNullOrEmpty(PendingWrite) ? null : PendingWrite, requestEndpointShapes = Endpoints, documentId = Doc, workspaceId = Workspace, elementId = Element, libraryVersion = LibraryVersion, defaultFeatureIds = DefaultFeatureIds, expectedBuilderDatumFaceIds = BuilderDatumIds, sketchFeatureIds = Sketches, extrudeFeatureIds = Extrudes, sentVerticesCm = Shapes.Select(s => new { datum = s.Name, verticesCm = s.Points }), preflightFrames = Before, ownerFrames = After, comparisons = Comparisons, extra };
            var path = Path.Combine(dir, $"{Prefix}.{RunId}.{next:D6}.json");
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            if (FailNextSnapshotForTests)
            {
                FailNextSnapshotForTests = false;
                var partial = Encoding.UTF8.GetBytes("{\"snapshotState\":\"incomplete\"");
                file.Write(partial, 0, partial.Length); file.Flush(true);
                throw new IOException("injected_snapshot_failure");
            }
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            file.Write(bytes, 0, bytes.Length); file.Flush(true);
        }
        public static Recovery Recover(string evidenceDir, string runId)
        {
            Recovery latest = new(0, null);
            foreach (var path in Directory.EnumerateFiles(evidenceDir, $"{Prefix}.{runId}.*.json").OrderBy(path => path, StringComparer.Ordinal))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    var root = doc.RootElement;
                    if (!ValidSnapshot(root, runId, path, out var sequence) || sequence <= latest.Sequence) continue;
                    latest = new Recovery(sequence, root.Clone());
                }
                catch (Exception) { /* Ignore malformed, incomplete, or schema-invalid immutable tail snapshots. */ }
            }
            return latest;
        }
        static bool ValidSnapshot(JsonElement root, string runId, string path, out int sequence)
        {
            sequence = 0;
            if (root.ValueKind != JsonValueKind.Object || Text(root, "snapshotState") != "complete" || Text(root, "schema") != "onshape-unit2-frame/v1" || !root.TryGetProperty("sequence", out var value) || !value.TryGetInt32(out sequence) || sequence < 1 || Path.GetFileName(path) != $"{Prefix}.{runId}.{sequence:D6}.json") return false;
            var required = new[] { "timestamp", "stage", "writes", "readonlyEvals", "pendingWrite", "requestEndpointShapes", "documentId", "workspaceId", "elementId", "libraryVersion", "defaultFeatureIds", "expectedBuilderDatumFaceIds", "sketchFeatureIds", "extrudeFeatureIds", "sentVerticesCm", "preflightFrames", "ownerFrames", "comparisons", "extra" };
            if (required.Any(name => !root.TryGetProperty(name, out _)) || Text(root, "timestamp") is null || Text(root, "stage") is null || !root.GetProperty("writes").TryGetInt32(out _) || !root.GetProperty("readonlyEvals").TryGetInt32(out _) || !root.GetProperty("libraryVersion").TryGetInt32(out _)) return false;
            return String(root, "documentId") && String(root, "workspaceId") && String(root, "elementId") && NullOrString(root, "pendingWrite") && Kind(root, "requestEndpointShapes", JsonValueKind.Array) && Kind(root, "defaultFeatureIds", JsonValueKind.Object) && Kind(root, "expectedBuilderDatumFaceIds", JsonValueKind.Object) && Kind(root, "sketchFeatureIds", JsonValueKind.Object) && Kind(root, "extrudeFeatureIds", JsonValueKind.Object) && Kind(root, "sentVerticesCm", JsonValueKind.Array);
        }
        static string? Text(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        static bool String(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String;
        static bool NullOrString(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.Null or JsonValueKind.String;
        static bool Kind(JsonElement root, string name, JsonValueKind kind) => root.TryGetProperty(name, out var value) && value.ValueKind == kind;
        public void Dispose() => owner.Dispose();
    }
    sealed record Recovery(int Sequence, JsonElement? Snapshot)
    {
        public string? DocumentId => Value("documentId");
        public string? PendingWrite => Value("pendingWrite");
        string? Value(string name) => Snapshot is JsonElement root && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
    static class Fs
    {
        public static object Value(JsonElement e)
        {
            var type = Required(e, "btType"); var v = e.GetProperty("value");
            if (type is "BTFSValueArray-1499" or "com.belmonttech.serialize.fsvalue.BTFSValueArray") return v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(Value).Cast<object?>().ToList() : throw new DiagnosticFailure("eval", "array");
            if (type is "BTFSValueNumber-772" or "com.belmonttech.serialize.fsvalue.BTFSValueNumber") return v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) && double.IsFinite(n) ? n : throw new DiagnosticFailure("eval", "number");
            if (type is "BTFSValueString-1422" or "com.belmonttech.serialize.fsvalue.BTFSValueString") return v.ValueKind == JsonValueKind.String ? v.GetString() ?? throw new DiagnosticFailure("eval", "string") : throw new DiagnosticFailure("eval", "string");
            throw new DiagnosticFailure("eval", UnsupportedType(type));
        }
        static string UnsupportedType(string type)
        {
            return type.Length <= 120 && type.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_') && (type.StartsWith("BTFSValue", StringComparison.Ordinal) || type.StartsWith("com.belmonttech.serialize.fsvalue.BTFSValue", StringComparison.Ordinal)) ? "unsupported_btType_" + type : "unsupported_btType";
        }
        public static object Response(JsonDocument doc)
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("microversionSkew", out var skew) && skew.ValueKind == JsonValueKind.True) throw new DiagnosticFailure("eval", "microversion_skew");
            if (root.TryGetProperty("notices", out var notices) && notices.ValueKind == JsonValueKind.Array && notices.GetArrayLength() > 0) throw new DiagnosticFailure("eval", "notices");
            return root.TryGetProperty("result", out var result) ? Value(result) : throw new DiagnosticFailure("eval", "missing_result");
        }
    }
    sealed class DiagnosticFailure : Exception { public string Stage { get; } public string Type { get; } public DiagnosticFailure(string stage, string type) { Stage = stage; Type = type; } }
    sealed class HttpFailure : Exception { public string Stage { get; } public int Status { get; } public HttpFailure(string stage, int status) { Stage = stage; Status = status; } }
    sealed record P(double X, double Y); sealed record V(double X, double Y, double Z); sealed record Frame(string Datum, string Id, V O, V X, V Y, V N, List<V> Vertices);
    sealed record Comparison(bool Valid, double Origin, double X, double Y, double Normal) { public bool Ok => Valid && Origin <= Tolerance && X <= Tolerance && Y <= Tolerance && Normal <= Tolerance; }
}
