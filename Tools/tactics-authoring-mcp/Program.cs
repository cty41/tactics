using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

string projectRoot = FindProjectRoot(args.FirstOrDefault());
await new TacticsAuthoringMcpServer(projectRoot, Console.OpenStandardInput(), Console.OpenStandardOutput()).RunAsync();

static string FindProjectRoot(string? supplied)
{
    string current = Path.GetFullPath(supplied ?? Environment.CurrentDirectory);
    while (true)
    {
        if (File.Exists(Path.Combine(current, "godot", "project.godot"))) return current;
        string? parent = Directory.GetParent(current)?.FullName;
        if (parent is null) throw new InvalidOperationException("Canonical project root containing godot/project.godot was not found.");
        current = parent;
    }
}

internal sealed class TacticsAuthoringMcpServer(string projectRoot, Stream input, Stream output)
{
    private static readonly string[] SupportedProtocolVersions = ["2026-07-28", "2025-11-25", "2025-06-18", "2025-03-26"];
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    public async Task RunAsync()
    {
        using var reader = new StreamReader(input, Encoding.UTF8, false, leaveOpen: true);
        using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        while (await reader.ReadLineAsync() is { } line)
        {
            JsonElement request;
            try { request = JsonDocument.Parse(line).RootElement.Clone(); }
            catch (Exception error) { await writer.WriteLineAsync(JsonSerializer.Serialize(Error(null, -32700, error.Message), _json)); continue; }
            bool notification = !request.TryGetProperty("id", out _);
            object? response = await HandleAsync(request);
            if (!notification && response is not null) await writer.WriteLineAsync(JsonSerializer.Serialize(response, _json));
        }
    }

    private async Task<object> HandleAsync(JsonElement request)
    {
        JsonElement? id = request.TryGetProperty("id", out JsonElement value) ? value.Clone() : null;
        try
        {
            string method = request.GetProperty("method").GetString()!;
            if (method == "initialize")
            {
                string requested = request.GetProperty("params").GetProperty("protocolVersion").GetString()!;
                if (!SupportedProtocolVersions.Contains(requested, StringComparer.Ordinal))
                    return Error(id, -32022, $"Unsupported protocol version '{requested}'. Supported: {string.Join(", ", SupportedProtocolVersions)}.");
                return Result(id, new { protocolVersion = requested, capabilities = new { tools = new { listChanged = false } }, serverInfo = new { name = "tactics-authoring", version = "1.0.0" } });
            }
            if (method == "notifications/initialized") return null!;
            if (method == "tools/list") return Result(id, new { tools = ToolSchemas() });
            if (method == "tools/call")
            {
                JsonElement parameters = request.GetProperty("params"); string name = parameters.GetProperty("name").GetString()!;
                JsonElement arguments = parameters.TryGetProperty("arguments", out JsonElement supplied) ? supplied : JsonDocument.Parse("{}").RootElement;
                try
                {
                    string bridgeJson = await CallBridgeAsync(name, arguments);
                    bool failed = JsonDocument.Parse(bridgeJson).RootElement.TryGetProperty("succeeded", out JsonElement succeeded) && !succeeded.GetBoolean();
                    return Result(id, new { content = new[] { new { type = "text", text = bridgeJson } }, isError = failed });
                }
                catch (Exception toolError)
                {
                    return Result(id, new { content = new[] { new { type = "text", text = toolError.Message } }, isError = true });
                }
            }
            if (method == "ping") return Result(id, new { });
            return Error(id, -32601, $"Unknown method '{method}'.");
        }
        catch (Exception error) { return Error(id, -32000, error.Message); }
    }

    private async Task<string> CallBridgeAsync(string tool, JsonElement arguments)
    {
        string sessionDirectory = Path.Combine(projectRoot, "godot", ".godot");
        AuthoringEditorSessionDescriptor descriptor = AuthoringEditorSessionResolver.Resolve(projectRoot, sessionDirectory);
        string token = descriptor.SessionToken;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string requestJson = JsonSerializer.Serialize(new { tool, sessionToken = token, projectRoot, arguments = arguments.Clone() }, _json);
        if (descriptor.Transport == "filesystem-mailbox")
            return await CallFileMailboxAsync(descriptor.EndpointPath, requestJson, timeout.Token);
        using TcpClient? tcp = descriptor.Transport == "loopback-tcp" ? new TcpClient() : null;
        using NamedPipeClientStream? pipe = descriptor.Transport == "loopback-tcp" ? null : new NamedPipeClientStream(".", descriptor.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        Stream stream;
        if (tcp is not null)
        {
            await tcp.ConnectAsync("127.0.0.1", descriptor.Port, timeout.Token);
            stream = tcp.GetStream();
        }
        else
        {
            await pipe!.ConnectAsync(timeout.Token);
            stream = pipe;
        }
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true }; using var reader = new StreamReader(stream, Encoding.UTF8, false, leaveOpen: true);
        await writer.WriteLineAsync(requestJson);
        return await reader.ReadLineAsync(timeout.Token) ?? throw new InvalidOperationException("Editor bridge closed without a response.");
    }

    private static async Task<string> CallFileMailboxAsync(string directory, string requestJson, CancellationToken cancellation)
    {
        string requestId = Guid.NewGuid().ToString("N");
        string temporary = Path.Combine(directory, $"{requestId}.request.tmp");
        string request = Path.Combine(directory, $"{requestId}.request.json");
        string response = Path.Combine(directory, $"{requestId}.response.json");
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(temporary, requestJson, cancellation);
            File.Move(temporary, request);
            while (!File.Exists(response)) await Task.Delay(25, cancellation);
            return await File.ReadAllTextAsync(response, cancellation);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            if (File.Exists(request)) File.Delete(request);
            if (File.Exists(response)) File.Delete(response);
        }
    }

    private static object[] ToolSchemas() =>
    [
        Tool("tactics_authoring_list", "List canonical authoring documents in the unique Editor session.", new { type = "object", properties = new { kind = new { type = "string" } } }),
        Tool("tactics_authoring_get", "Get a normalized authoring snapshot and revision.", Required("kind", "contentId", new { kind = StringProperty(), contentId = StringProperty() })),
        Tool("tactics_authoring_validate", "Validate a stored or supplied authoring snapshot without saving.", Required("kind", "contentId", new { kind = StringProperty(), contentId = StringProperty(), snapshot = StringProperty(), expectedRevision = StringProperty() })),
        Tool("tactics_authoring_apply", "Atomically apply snapshots and Create/Duplicate/Delete lifecycle operations with revision and reference fencing.", ApplySchema()),
        Tool("tactics_authoring_preview", "Run an approved draft preview in the canonical Editor; Skill accepts a typed BattleTransition context.", PreviewSchema()),
        Tool("tactics_authoring_reference_audit", "Audit forward and reverse Catalog references.", new { type = "object", properties = new { contentId = StringProperty() } })
    ];
    private static object Tool(string name, string description, object schema) => new { name, description, inputSchema = schema };
    private static object StringProperty() => new { type = "string" };
    private static object ApplySchema()
    {
        object change = new
        {
            type = "object",
            properties = new { kind = StringProperty(), contentId = StringProperty(), expectedRevision = StringProperty(), snapshot = StringProperty() },
            required = new[] { "kind", "contentId", "expectedRevision", "snapshot" },
            additionalProperties = false
        };
        object lifecycle = new
        {
            type = "object",
            properties = new
            {
                operation = new { type = "string", @enum = new[] { "create", "duplicate", "delete" } },
                contentId = StringProperty(),
                sourceContentId = StringProperty(),
                resourceType = StringProperty(),
                path = StringProperty(),
                expectedReferenceRevision = StringProperty()
            },
            required = new[] { "operation", "contentId" },
            additionalProperties = false
        };
        return new
        {
            type = "object",
            properties = new
            {
                kind = StringProperty(), contentId = StringProperty(), expectedRevision = StringProperty(), snapshot = StringProperty(),
                changes = new { type = "array", minItems = 1, items = change },
                lifecycle = new { type = "array", minItems = 1, items = lifecycle }
            },
            anyOf = new object[]
            {
                new { required = new[] { "kind", "contentId", "expectedRevision", "snapshot" } },
                new { required = new[] { "changes" } },
                new { required = new[] { "lifecycle" } }
            },
            additionalProperties = false
        };
    }
    private static object PreviewSchema() => new
    {
        type = "object",
        properties = new
        {
            kind = StringProperty(), contentId = StringProperty(), snapshot = StringProperty(), seed = new { type = "integer" },
            context = new
            {
                type = "object",
                properties = new
                {
                    encounterContentId = StringProperty(), casterUnitInstanceId = StringProperty(),
                    casterUnitContentId = StringProperty(), targetUnitInstanceId = StringProperty(),
                    targetX = new { type = "integer", minimum = 0, maximum = 9 },
                    targetY = new { type = "integer", minimum = 0, maximum = 9 },
                    seed = new { type = "integer", minimum = 0 }
                },
                required = new[] { "encounterContentId", "casterUnitInstanceId", "targetX", "targetY" },
                additionalProperties = false
            }
        },
        required = new[] { "kind", "contentId" },
        additionalProperties = false
    };
    private static object Required(string first, string second, object properties) => new { type = "object", properties, required = new[] { first, second } };
    private static object Required(string first, string second, string third, string fourth, object properties) => new { type = "object", properties, required = new[] { first, second, third, fourth } };
    private static object Result(JsonElement? id, object value) => new { jsonrpc = "2.0", id, result = value };
    private static object Error(JsonElement? id, int code, string message) => new { jsonrpc = "2.0", id, error = new { code, message } };
}
