using System.Diagnostics;
using System.Text.Json;

internal sealed record AuthoringEditorSessionDescriptor(
    string ProjectRoot,
    string PipeName,
    string Transport,
    int Port,
    string EndpointPath,
    string SessionToken,
    int ProcessId,
    string State,
    string Path);

internal static class AuthoringEditorSessionResolver
{
    public static AuthoringEditorSessionDescriptor Resolve(string projectRoot, string sessionDirectory)
    {
        string canonicalRoot = Canonical(projectRoot);
        var live = new List<AuthoringEditorSessionDescriptor>();
        foreach (string path in Directory.Exists(sessionDirectory)
                     ? Directory.GetFiles(sessionDirectory, "tactics-authoring-session-*.json")
                     : Array.Empty<string>())
        {
            AuthoringEditorSessionDescriptor descriptor = Read(path);
            if (!IsProcessAlive(descriptor.ProcessId)) continue;
            if (!string.Equals(Canonical(descriptor.ProjectRoot), canonicalRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Editor bridge project root differs from the MCP project root: {path}.");
            live.Add(descriptor);
        }

        if (live.Count != 1)
            throw new InvalidOperationException($"Expected exactly one live canonical Editor bridge session, found {live.Count}.");
        AuthoringEditorSessionDescriptor selected = live[0];
        if (!string.Equals(selected.State, "ready", StringComparison.Ordinal))
            throw new InvalidOperationException($"Editor bridge is not ready (state={selected.State}).");
        return selected;
    }

    private static AuthoringEditorSessionDescriptor Read(string path)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            string transport = root.TryGetProperty("transport", out JsonElement transportValue)
                ? transportValue.GetString() ?? "named-pipe"
                : "named-pipe";
            return new AuthoringEditorSessionDescriptor(
                root.GetProperty("projectRoot").GetString() ?? throw new InvalidOperationException("projectRoot is empty"),
                root.TryGetProperty("pipeName", out JsonElement pipeValue) ? pipeValue.GetString() ?? string.Empty : string.Empty,
                transport,
                root.TryGetProperty("port", out JsonElement portValue) ? portValue.GetInt32() : 0,
                root.TryGetProperty("endpointPath", out JsonElement endpointValue) ? endpointValue.GetString() ?? string.Empty : string.Empty,
                root.GetProperty("sessionToken").GetString() ?? throw new InvalidOperationException("sessionToken is empty"),
                root.GetProperty("processId").GetInt32(),
                root.GetProperty("state").GetString() ?? throw new InvalidOperationException("state is empty"),
                path) is { } descriptor &&
                   (descriptor.Transport switch
                   {
                       "loopback-tcp" => descriptor.Port is > 0 and <= 65535,
                       "filesystem-mailbox" => !string.IsNullOrWhiteSpace(descriptor.EndpointPath),
                       _ => !string.IsNullOrWhiteSpace(descriptor.PipeName),
                   })
                    ? descriptor
                    : throw new InvalidOperationException("transport endpoint is invalid");
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidOperationException($"Editor bridge descriptor is invalid: {path}. {error.Message}", error);
        }
    }

    private static string Canonical(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsProcessAlive(int processId)
    {
        try { using Process process = Process.GetProcessById(processId); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }
}
