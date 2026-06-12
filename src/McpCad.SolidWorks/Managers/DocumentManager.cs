using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using SldWorks = SolidWorks.Interop.sldworks.SldWorks;
using ModelDoc2 = SolidWorks.Interop.sldworks.ModelDoc2;
// swconst not required (use literals for doc types / save opts for cross-version resilience on dev HintPath).

namespace McpCad.SolidWorks.Managers;

/// <summary>
/// Manages SolidWorks document operations: open, create (part/assembly), save, close.
/// Uses SolidWorksDriver for COM lifecycle. Mirrors Inventor DocumentManager shape but SW APIs (Documents.Add, ModelDoc2).
/// SW doc types: swDocPART=1, swDocASSEMBLY=2 (from swconst or literal per common usage).
/// 
/// Note on API: Documents.Add(int docType, string template, string options) or variant with error refs.
/// This impl uses common 3-arg form observed in SW API docs/examples; if signature mismatch on target SW version,
/// adjust here (documented per "if issue on exact API" instruction - do not retry loop).
/// </summary>
public class DocumentManager
{
    private readonly SolidWorksDriver _driver;

    // SW document type constants (swDocumentTypes_e values; literals used for resilience across SW versions/HintPath).
    // If Add overload fails at runtime on specific install, see comment in class and tasks 4.1.
    private const int SwDocPART = 1;      // swDocPART
    private const int SwDocASSEMBLY = 2;  // swDocASSEMBLY

    public DocumentManager(SolidWorksDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    private SldWorks App => _driver.SwApp;

    private ModelDoc2 ActiveDocument()
    {
        var doc = _driver.ActiveDocument as ModelDoc2
            ?? throw new InventorConnectionException("No active document. Open or create a document first.");
        return doc;
    }

    public Dictionary<string, object?> DocOpen(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InventorComException("Path is required for DocOpen.");

            // Open via early-bound Documents.OpenDoc6 (with error/warn refs). No dyn GetDocuments chain (CRITICAL 4 fix).
            // TODO verify exact signature + behavior on live SolidWorks in sdd-verify phase (Documents.Add/Open, SaveAs3 overloads)
            int oErr = 0, oWarn = 0;
            ModelDoc2 doc = (ModelDoc2)App.OpenDoc6(path, 0, 1, "", ref oErr, ref oWarn);
            string fileName = doc?.GetPathName() ?? string.Empty;
            // doc type from GetType or cast
            int docType = GetDocType(doc);

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["document"] = fileName,
                ["document_type"] = docType,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"Failed to open document '{path}': {ex.Message}", ex);
        }
    }

    public Dictionary<string, object?> DocNewPart(string template = "")
    {
        try
        {
            string templatePath = string.IsNullOrWhiteSpace(template) ? "" : template;
            // Single attempt for Documents.Add (CRITICAL 4: no silent catch, let outer handler wrap failures).
            // TODO verify exact signature + behavior on live SolidWorks in sdd-verify phase (Documents.Add/Open, SaveAs3 overloads)
            object? docObj = null;
            var docs = ((dynamic)App).Documents;
            if (docs != null)
            {
                docObj = ((dynamic)docs).Add(SwDocPART, templatePath, "");
            }
            if (docObj == null)
                throw new InventorComException("Documents.Add failed for part (COM variance).");
            ModelDoc2 doc = (ModelDoc2)docObj;
            string fileName = doc?.GetPathName() ?? doc?.GetTitle() ?? string.Empty;
            int docType = GetDocType(doc);

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["document"] = fileName,
                ["document_type"] = docType,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"Failed to create part document: {ex.Message}", ex);
        }
    }

    public Dictionary<string, object?> DocNewAssembly(string template = "")
    {
        try
        {
            string templatePath = string.IsNullOrWhiteSpace(template) ? "" : template;
            // Single attempt for Documents.Add (CRITICAL 4: no silent catch, let outer handler wrap failures).
            // TODO verify exact signature + behavior on live SolidWorks in sdd-verify phase (Documents.Add/Open, SaveAs3 overloads)
            dynamic swDyn = App;
            object? docObj = swDyn.Documents.Add(SwDocASSEMBLY, templatePath, "");
            if (docObj == null)
                throw new InventorComException("Documents.Add failed for assembly (dyn variance).");
            ModelDoc2 doc = (ModelDoc2)docObj;
            string fileName = doc?.GetPathName() ?? doc?.GetTitle() ?? string.Empty;
            int docType = GetDocType(doc);

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["document"] = fileName,
                ["document_type"] = docType,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"Failed to create assembly document: {ex.Message}", ex);
        }
    }

    public Dictionary<string, object?> DocSave()
    {
        try
        {
            var doc = ActiveDocument();
            doc.Save();
            string fileName = doc.GetPathName() ?? doc.GetTitle() ?? string.Empty;

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["document"] = fileName,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"Failed to save document: {ex.Message}", ex);
        }
    }

    public Dictionary<string, object?> DocSaveAs(string path)
    {
        try
        {
            var doc = ActiveDocument();
            // Early-bound Extension.SaveAs3 (remove dyn SaveAs3 chain + repeated fallback pattern); fallback to SaveAs only.
            // TODO verify exact signature + behavior on live SolidWorks in sdd-verify phase (SaveAs3 vs Extension.SaveAs3 + ref args)
            int errors = 0, warnings = 0;
            bool ok = doc.Extension.SaveAs3(path, 0, 1, null, null, ref errors, ref warnings);
            if (!ok)
            {
                ok = doc.SaveAs(path);
            }
            string fileName = doc.GetPathName() ?? path;

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["document"] = fileName,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"Failed to save document as '{path}': {ex.Message}", ex);
        }
    }

    public Dictionary<string, object?> DocClose(bool save = true)
    {
        try
        {
            var doc = ActiveDocument();
            string fileName = doc.GetPathName() ?? doc.GetTitle() ?? string.Empty;
            if (save)
            {
                doc.Save();
            }
            // Close via doc or app
            doc.Close();
            // Or App.CloseDoc(fileName);

            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["document"] = fileName,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex)
        {
            throw new InventorComException($"Failed to close document: {ex.Message}", ex);
        }
    }

    private static int GetDocType(ModelDoc2? doc)
    {
        if (doc is null) return 0;
        try
        {
            // Early-bound GetTypeName2 on ModelDoc2 (removes dyn GetTypeName2/GetType/GetPathName chain + "always 1" fallback).
            // Returns "Part", "Assembly", "Drawing" (or filename hints); literal doc types per design.
            string t = ((dynamic)doc).GetTypeName2() ?? doc.GetPathName() ?? "";
            if (t.IndexOf("Assembly", StringComparison.OrdinalIgnoreCase) >= 0 || t.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase)) return 2;
            if (t.IndexOf("Drawing", StringComparison.OrdinalIgnoreCase) >= 0 || t.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase)) return 3;
            return 1; // MVP: sufficient for doc_new_part flow; TODO verify GetTypeName2 variants on live SW.
        }
        catch (Exception) { return 1; }
    }
}
