using McpCad.Core.Exceptions;
using McpCad.Core.Models;
using InvApp = Inventor.Application;

namespace McpCad.Inventor.Managers;

/// <summary>
/// Manages Inventor export operations: STEP, STL, PDF, DXF.
/// Uses TranslatorAddIn via early-bound COM for reliable SaveCopyAs.
/// </summary>
public class ExportManager(InventorDriver driver)
{
    // STEP translator AddIn GUID
    private const string StepAddInGuid = "{90AF7F40-0C01-11D5-8E83-0010B541CD80}";

    // Drawing document type constant (for PDF SaveAs detection)
    // Matches DocumentManager.DrawingDocumentType
    private const int DrawingDocumentType = 12293; // kDrawingDocumentObject

    private InvApp App => driver.InventorApp;

    private dynamic ActiveDocument()
    {
        var doc = driver.ActiveDocument
            ?? throw new InventorComException("No active document. Open or create a document first.");
        return doc;
    }

    /// <summary>
    /// Look up a TranslatorAddIn by its class ID string (GUID).
    /// </summary>
    private dynamic GetTranslatorByGuid(string addInGuid)
    {
        try
        {
            dynamic addIns = App.ApplicationAddIns;
            int count = addIns.Count;

            for (int i = 1; i <= count; i++)
            {
                dynamic addIn = addIns.Item(i);
                string classId = addIn.ClassIdString ?? string.Empty;
                if (classId.Equals(addInGuid, StringComparison.OrdinalIgnoreCase))
                    return addIn;
            }

            throw new InventorComException($"Translator AddIn with GUID '{addInGuid}' not found.");
        }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to find translator AddIn by GUID: {ex.Message}", ex); }
    }

    /// <summary>
    /// Look up a TranslatorAddIn by display name.
    /// Tries FileManager.GetTranslatorByName first, falls back to iterating ApplicationAddIns.
    /// </summary>
    private dynamic GetTranslatorByName(string name)
    {
        // Try the FileManager route first (more reliable)
        try
        {
            dynamic fm = App.FileManager;
            dynamic translator = fm.GetTranslatorByName(name);
            if (translator is not null)
                return translator;
        }
        catch
        {
            // GetTranslatorByName not available on older versions — fall through
        }

        // Fallback: iterate ApplicationAddIns looking for a matching name
        try
        {
            dynamic addIns = App.ApplicationAddIns;
            int count = addIns.Count;

            for (int i = 1; i <= count; i++)
            {
                dynamic addIn = addIns.Item(i);
                string displayName = addIn.DisplayName ?? string.Empty;
                if (displayName.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return addIn;
            }
        }
        catch (Exception ex) { throw new InventorComException($"Failed to find translator by name '{name}': {ex.Message}", ex); }

        throw new InventorComException($"Translator AddIn for '{name}' not found.");
    }

    /// <summary>
    /// Execute an export via TranslatorAddIn.SaveCopyAs.
    /// </summary>
    private Dictionary<string, object?> RunExport(dynamic doc, string path, dynamic translator)
    {
        try
        {
            translator.SaveCopyAs(doc, null, path);
            return new Dictionary<string, object?>
            {
                ["success"] = true,
                ["path"] = path,
            };
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Export failed for '{path}': {ex.Message}", ex); }
    }

    /// <summary>
    /// Export the active document to STEP format (.stp/.step).
    /// </summary>
    public Dictionary<string, object?> ExportStep(string path, Dictionary<string, object?>? opts = null)
    {
        try
        {
            var doc = ActiveDocument();
            dynamic translator = GetTranslatorByGuid(StepAddInGuid);
            return RunExport(doc, path, translator);
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to export STEP: {ex.Message}", ex); }
    }

    /// <summary>
    /// Export the active document to STL format (.stl).
    /// </summary>
    public Dictionary<string, object?> ExportStl(string path, Dictionary<string, object?>? opts = null)
    {
        try
        {
            var doc = ActiveDocument();
            dynamic translator = GetTranslatorByName("STL");
            return RunExport(doc, path, translator);
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to export STL: {ex.Message}", ex); }
    }

    /// <summary>
    /// Export the active document to PDF format.
    /// For drawing documents, uses SaveAs; otherwise uses TranslatorAddIn.
    /// </summary>
    public Dictionary<string, object?> ExportPdf(string path, Dictionary<string, object?>? opts = null)
    {
        try
        {
            var doc = ActiveDocument();

            // Drawing documents can use SaveAs directly
            try
            {
                int docType = doc.DocumentType;
                if (docType == DrawingDocumentType)
                {
                    doc.SaveAs(path, true);
                    return new Dictionary<string, object?>
                    {
                        ["success"] = true,
                        ["path"] = path,
                    };
                }
            }
            catch { /* fall through to translator approach */ }

            dynamic translator = GetTranslatorByName("PDF");
            return RunExport(doc, path, translator);
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to export PDF: {ex.Message}", ex); }
    }

    /// <summary>
    /// Export the active document's sketch or flat pattern to DXF format.
    /// </summary>
    public Dictionary<string, object?> ExportDxf(string path, Dictionary<string, object?>? opts = null)
    {
        try
        {
            var doc = ActiveDocument();
            dynamic translator = GetTranslatorByName("DXF");
            return RunExport(doc, path, translator);
        }
        catch (InventorConnectionException) { throw; }
        catch (InventorComException) { throw; }
        catch (Exception ex) { throw new InventorComException($"Failed to export DXF: {ex.Message}", ex); }
    }
}