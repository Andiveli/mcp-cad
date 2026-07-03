namespace McpCad.Onshape;

internal static class OnshapeApiPaths
{
    private static string Seg(string value) => Uri.EscapeDataString(value);

    public static string Document(string documentId) => $"/api/v6/documents/{Seg(documentId)}";

    public static string DocumentCopy(string documentId) => $"/api/v6/documents/{Seg(documentId)}/copy";

    public static string Documents() => "/api/v6/documents";

    public static string Workspaces(string documentId) => $"/api/v6/documents/d/{Seg(documentId)}/workspaces";

    public static string Elements(string documentId, string wvm, string wvmId) =>
        $"/api/v6/documents/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/elements";

    public static string Element(string documentId, string wvm, string wvmId, string elementId) =>
        $"/api/v6/elements/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/e/{Seg(elementId)}";

    public static string PartStudioFeatures(string documentId, string wvm, string wvmId, string elementId) =>
        $"/api/v6/partstudios/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/e/{Seg(elementId)}/features";

    public static string AssemblyInstances(string documentId, string wvm, string wvmId, string elementId) =>
        $"/api/v6/assemblies/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/e/{Seg(elementId)}/instances";

    public static string PartStudioExportStep(string documentId, string wvm, string wvmId, string elementId) =>
        $"/api/v6/partstudios/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/e/{Seg(elementId)}/export/step";

    public static string PartStudioExportStl(string documentId, string wvm, string wvmId, string elementId) =>
        $"/api/v6/partstudios/d/{Seg(documentId)}/{Seg(wvm)}/{Seg(wvmId)}/e/{Seg(elementId)}/export/stl";

    public static string Version(string documentId) => $"/api/v6/documents/d/{Seg(documentId)}/versions";

    public static string Microversion(string documentId, string wvm, string wvmId, string elementId) =>
        Element(documentId, wvm, wvmId, elementId);

    public static string Translation(string translationId) => $"/api/v6/translations/{Seg(translationId)}";
}