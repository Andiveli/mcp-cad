namespace McpCad.Onshape;

internal static class OnshapeApiPaths
{
    public static string Documents() => "/api/v6/documents";

    public static string Document(string documentId) =>
        $"/api/v6/documents/{Uri.EscapeDataString(documentId)}";

    public static string Workspaces(string documentId) =>
        $"/api/v6/documents/d/{Uri.EscapeDataString(documentId)}/workspaces";
}
