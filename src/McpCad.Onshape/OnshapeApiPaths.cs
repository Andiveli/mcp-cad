namespace McpCad.Onshape;

internal static class OnshapeApiPaths
{
    private static string Seg(string value) => Uri.EscapeDataString(value);

    public static string Document(string documentId) => $"/api/v6/documents/{Seg(documentId)}";

    public static string DocumentWorkspaceCopy(string documentId, string workspaceId) =>
        $"/api/v6/documents/{Seg(documentId)}/workspaces/{Seg(workspaceId)}/copy";

    public static string Documents() => "/api/v6/documents";

    public static string DocumentCreate() => Documents();

    public static string Workspaces(string documentId) => $"/api/v6/documents/d/{Seg(documentId)}/workspaces";

    public static string Elements(string documentId, string wvm, string wvmId) =>
        $"/api/v6/documents/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/elements";

    public static string PartStudioCreate(string documentId, string workspaceId) =>
        $"/api/v6/partstudios/d/{Seg(documentId)}/w/{Seg(workspaceId)}";

    public static string Element(string documentId, string wvm, string wvmId, string elementId) =>
        $"/api/v6/elements/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/e/{Seg(elementId)}";

    /// <summary>
    /// Part Studio features endpoint. GET lists features; POST adds or replaces sketch/feature payloads.
    /// </summary>
    public static string PartStudioFeatures(string documentId, string wvm, string wvmId, string elementId) =>
        $"/api/v6/partstudios/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/e/{Seg(elementId)}/features";

    /// <summary>Alias for sketch replace POST (same path as <see cref="PartStudioFeatures"/>).</summary>
    public static string SketchFeatures(string documentId, string wvm, string wvmId, string elementId) =>
        PartStudioFeatures(documentId, wvm, wvmId, elementId);

    public static string Assembly(string documentId, string wvm, string wvmId, string elementId) =>
        $"/api/v6/assemblies/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/e/{Seg(elementId)}";

    public static string AssemblyInstances(string documentId, string workspaceId, string elementId) =>
        $"/api/v6/assemblies/d/{Seg(documentId)}/w/{Seg(workspaceId)}/e/{Seg(elementId)}/instances";

    public static string AssemblyInstance(string documentId, string workspaceId, string elementId, string nodeId) =>
        $"/api/v6/assemblies/d/{Seg(documentId)}/w/{Seg(workspaceId)}/e/{Seg(elementId)}/instance/nodeid/{Seg(nodeId)}";

    public static string PartStudioBodyDetails(string documentId, string wvm, string wvmId, string elementId) =>
        $"/api/v6/partstudios/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/e/{Seg(elementId)}/bodydetails";

    public static string PartStudioExportStep(string documentId, string wvm, string wvmId, string elementId) =>
        $"/api/v6/partstudios/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/e/{Seg(elementId)}/export/step";

    public static string PartStudioExportStl(string documentId, string wvm, string wvmId, string elementId) =>
        $"/api/v6/partstudios/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/e/{Seg(elementId)}/stl";

    public static string Version(string documentId) => $"/api/v6/documents/d/{Seg(documentId)}/versions";

    public static string Microversion(string documentId, string wvm, string wvmId, string elementId) =>
        Element(documentId, wvm, wvmId, elementId);

    public static string Translation(string translationId) => $"/api/v6/translations/{Seg(translationId)}";
}
