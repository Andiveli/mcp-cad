using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Inventor;

class Program
{
    static Inventor.Application _invApp;
    static dynamic _compDef;

    [STAThread]
    static void Main()
    {
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        string line;
        while ((line = Console.ReadLine()) != null)
        {
            string response = HandleCommand(line.Trim());
            Console.WriteLine(response);
            Console.Out.Flush();
        }
    }

    static string HandleCommand(string json)
    {
        try
        {
            SimpleJson cmd = SimpleJson.Parse(json);
            if (cmd == null) return Error("Invalid JSON");
            string action = GetString(cmd, "action");
            if (action == "connect") return Connect();
            if (action == "inspect") return InspectSketch(cmd);
            if (action == "revolve") return Revolve(cmd);
            return Error("Unknown action: " + action);
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    static string Connect()
    {
        try
        {
            _invApp = (Inventor.Application)Marshal.GetActiveObject("Inventor.Application");
            Document doc = _invApp.ActiveDocument;
            if (doc is PartDocument)
                _compDef = ((PartDocument)doc).ComponentDefinition;
            else if (doc is AssemblyDocument)
                _compDef = ((AssemblyDocument)doc).ComponentDefinition;
            var data = new Dictionary<string, object>();
            data["connected"] = true;
            data["version"] = _invApp.SoftwareVersion.DisplayName;
            return Ok(data);
        }
        catch (Exception ex) { return Error("Connect failed: " + ex.Message); }
    }

    static string Revolve(SimpleJson cmd)
    {
        if (_invApp == null) return Error("Not connected");
        int sketchIdx = GetInt(cmd, "sketch");
        int axisIdx = GetInt(cmd, "axis");
        string opStr = GetString(cmd, "operation", "join");
        double angle = double.Parse(GetString(cmd, "angle", "360"));

        PartFeatureOperationEnum op;
        if (opStr == "join") op = PartFeatureOperationEnum.kJoinOperation;
        else if (opStr == "cut") op = PartFeatureOperationEnum.kCutOperation;
        else if (opStr == "intersect") op = PartFeatureOperationEnum.kIntersectOperation;
        else return Error("Invalid operation: " + opStr);

        PlanarSketch sketch = GetSketch(sketchIdx);
        if (sketch == null) return Error("Sketch " + sketchIdx + " not found");

        try
        {
            Profiles profiles = sketch.Profiles;
            // Try to get existing profile first (Inventor auto-computes)
            Profile profile = null;
            if (profiles.Count > 0)
                profile = profiles[1];

            if (profile == null)
            {
                try { profiles.AddForSolid(); profile = profiles[1]; }
                catch { }
            }
            if (profile == null)
            {
                try { profiles.AddForSurface(); profile = profiles[1]; }
                catch { }
            }
            if (profile == null)
                return Error("No closed profile in sketch " + sketchIdx
                    + " (count=" + profiles.Count + ")");
            SketchLine axisLine = sketch.SketchLines[axisIdx];

            RevolveFeatures rf = _compDef.Features.RevolveFeatures;
            if (angle >= 359.99)
                rf.AddFull(profile, axisLine, op);
            else
                rf.AddByAngle(profile, axisLine, angle * Math.PI / 180.0,
                    PartFeatureExtentDirectionEnum.kPositiveExtentDirection, op);

            var data = new Dictionary<string, object>();
            data["success"] = true;
            data["feature_type"] = "revolve";
            data["angle"] = angle;
            data["operation"] = opStr;
            return Ok(data);
        }
        catch (Exception ex)
        {
            return Error("Revolve failed: " + ex.Message);
        }
    }

    static string InspectSketch(SimpleJson cmd)
    {
        if (_invApp == null) return Error("Not connected");
        int sketchIdx = GetInt(cmd, "sketch");
        PlanarSketch sketch = GetSketch(sketchIdx);
        if (sketch == null) return Error("Sketch " + sketchIdx + " not found");

        try
        {
            var entities = new List<object>();
            dynamic ses = sketch.SketchEntities;
            int count = ses.Count;

            for (int i = 1; i <= count; i++)
            {
                dynamic ent = ses[i];
                string entType = "SketchEntity";
                if (ent is SketchLine) entType = "SketchLine";
                else if (ent is SketchCircle) entType = "SketchCircle";
                else if (ent is SketchArc) entType = "SketchArc";
                else if (ent is SketchPoint) entType = "SketchPoint";

                string tag = null;
                try { tag = (string)ent.AttributeSets["mcp_cad_tags"]["tag"].Value; }
                catch { }

                var info = new Dictionary<string, object>();
                info["index"] = i;
                info["type"] = entType;
                try
                {
                    if (entType == "SketchLine") {
                        info["start"] = new double[] { (double)ent.StartSketchPoint.Geometry.X, (double)ent.StartSketchPoint.Geometry.Y };
                        info["end"] = new double[] { (double)ent.EndSketchPoint.Geometry.X, (double)ent.EndSketchPoint.Geometry.Y };
                    } else if (entType == "SketchCircle") {
                        info["center"] = new double[] { (double)ent.CenterSketchPoint.Geometry.X, (double)ent.CenterSketchPoint.Geometry.Y };
                        info["radius"] = (double)ent.Geometry.Radius;
                    } else if (entType == "SketchPoint") {
                        info["x"] = (double)ent.Geometry.X;
                        info["y"] = (double)ent.Geometry.Y;
                    }
                }
                catch { }

                if (tag != null) info["tag"] = tag;
                entities.Add(info);
            }

            var data = new Dictionary<string, object>();
            data["sketch"] = sketchIdx;
            data["entity_count"] = count;
            data["entities"] = entities;
            return Ok(data);
        }
        catch (Exception ex) { return Error("Inspect failed: " + ex.Message); }
    }

    static PlanarSketch GetSketch(int index)
    {
        if (_invApp == null || _compDef == null) return null;
        try { return (PlanarSketch)_compDef.Sketches[index]; }
        catch { return null; }
    }

    static int GetInt(SimpleJson cmd, string key) { return int.Parse(cmd.GetValue(key)); }
    static string GetString(SimpleJson cmd, string key, string def) { string v = cmd.GetValue(key); return v ?? def; }
    static string GetString(SimpleJson cmd, string key) { return GetString(cmd, key, ""); }
    static string Ok(Dictionary<string, object> data) { return "{\"ok\":true,\"data\":" + SimpleJson.Stringify(data) + "}"; }
    static string Error(string msg) { return "{\"ok\":false,\"error\":\"" + msg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}"; }
}

class SimpleJson
{
    Dictionary<string, string> _data = new Dictionary<string, string>();
    public string GetValue(string key) { string v; _data.TryGetValue(key, out v); return v; }
    public static SimpleJson Parse(string json) { SimpleJson r = new SimpleJson(); int i = 0; SkipWS(json, ref i); if (i >= json.Length || json[i] != '{') return null; i++; while (i < json.Length) { SkipWS(json, ref i); if (json[i] == '}') { i++; break; } if (json[i] == ',') { i++; continue; } string k = ParseString(json, ref i); SkipWS(json, ref i); if (json[i] != ':') return null; i++; SkipWS(json, ref i); string v; if (json[i] == '"') v = ParseString(json, ref i); else if (json[i] == '-' || char.IsDigit(json[i])) v = ParseNumber(json, ref i); else v = ParseRaw(json, ref i); r._data[k] = v; } return r; }
    static string ParseString(string s, ref int i) { i++; int st = i; while (i < s.Length && s[i] != '"') { if (s[i] == '\\') i++; i++; } string val = s.Substring(st, i - st); if (i < s.Length) i++; return val.Replace("\\\"", "\"").Replace("\\\\", "\\"); }
    static string ParseNumber(string s, ref int i) { int st = i; while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '.')) i++; return s.Substring(st, i - st); }
    static string ParseRaw(string s, ref int i) { int st = i; while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']') i++; return s.Substring(st, i - st).Trim(); }
    static void SkipWS(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
    public static string Stringify(object obj) { if (obj == null) return "null"; string s = obj as string; if (s != null) return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; if (obj is int) return obj.ToString(); if (obj is double) return ((double)obj).ToString(System.Globalization.CultureInfo.InvariantCulture); if (obj is bool) return ((bool)obj) ? "true" : "false"; double[] arr = obj as double[]; if (arr != null) { var p = new List<string>(); foreach (var x in arr) p.Add(Stringify(x)); return "[" + string.Join(",", p) + "]"; } System.Collections.IList list = obj as System.Collections.IList; if (list != null) { var p = new List<string>(); foreach (var x in list) p.Add(Stringify(x)); return "[" + string.Join(",", p) + "]"; } Dictionary<string, object> dict = obj as Dictionary<string, object>; if (dict != null) { var p = new List<string>(); foreach (var kv in dict) p.Add("\"" + kv.Key + "\":" + Stringify(kv.Value)); return "{" + string.Join(",", p) + "}"; } return "\"" + obj.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }
}
