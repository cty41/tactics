#if TOOLS
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

public interface IAuthoringResourceHandler
{
    string ResourceTypeId { get; }
    AuthoringDocumentKind Kind { get; }
    bool CanHandle(Resource resource);
    IAuthoringDocument Read(Resource resource);
    string Serialize(IAuthoringDocument document);
    IAuthoringDocument Deserialize(string snapshot);
    IReadOnlyList<AuthoringDiagnostic> Validate(IAuthoringDocument document);
    Resource Stage(Resource source, IAuthoringDocument document);
    AuthoringPreviewEvidence Preview(IAuthoringDocument document, int seed);
    void Write(Resource resource, IAuthoringDocument document);
}

public sealed class AuthoringResourceHandlerRegistry
{
    private readonly IReadOnlyDictionary<string, IAuthoringResourceHandler> _handlers;

    public AuthoringResourceHandlerRegistry(IEnumerable<IAuthoringResourceHandler> handlers)
    {
        IAuthoringResourceHandler[] values = handlers.ToArray();
        if (values.GroupBy(value => value.ResourceTypeId, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new ArgumentException("Authoring resource handler IDs must be unique.", nameof(handlers));
        _handlers = values.ToDictionary(value => value.ResourceTypeId, StringComparer.Ordinal);
    }

    public IAuthoringResourceHandler Get(string resourceTypeId) =>
        _handlers.TryGetValue(Normalize(resourceTypeId), out IAuthoringResourceHandler? value)
            ? value
            : throw new InvalidOperationException($"Unsupported authoring kind '{resourceTypeId}'.");

    public bool TryGet(string resourceTypeId, out IAuthoringResourceHandler? handler) => _handlers.TryGetValue(Normalize(resourceTypeId), out handler);

    public static string Normalize(string resourceTypeId) => resourceTypeId.Equals("map", StringComparison.OrdinalIgnoreCase) ? "run-map" : resourceTypeId;

    public static AuthoringResourceHandlerRegistry CreateDefault() => new(
    [
        Handler<PureRunMapResource, MapAuthoringDocument>("run-map", AuthoringDocumentKind.Map,
            MapAuthoringEditorService.Read, value => MapAuthoringJson.Serialize(value), MapAuthoringJson.Deserialize, MapAuthoringEditorService.Write),
        Handler<PureRunLayerFourResource, EventAuthoringDocument>("event", AuthoringDocumentKind.Event,
            EventTreasureAuthoringEditorService.Read, EventAuthoringJson.SerializePayload, EventAuthoringJson.Deserialize, EventTreasureAuthoringEditorService.Write),
        Handler<PureRunTreasureResource, TreasureAuthoringDocument>("treasure", AuthoringDocumentKind.Treasure,
            EventTreasureAuthoringEditorService.Read, TreasureAuthoringJson.Serialize, TreasureAuthoringJson.Deserialize, EventTreasureAuthoringEditorService.Write),
        Handler<EncounterDefinitionResource, EncounterAuthoringDocument>("encounter", AuthoringDocumentKind.Encounter,
            EncounterAuthoringEditorService.Read, EncounterAuthoringJson.Serialize, EncounterAuthoringJson.Deserialize, EncounterAuthoringEditorService.Write),
        Handler<BattleLayoutResource, BattleLayoutAuthoringDocument>("battle-layout", AuthoringDocumentKind.BattleLayout,
            EncounterAuthoringEditorService.Read, BattleLayoutAuthoringJson.Serialize, BattleLayoutAuthoringJson.Deserialize, EncounterAuthoringEditorService.Write),
        Handler<AiDefinitionResource, AiAuthoringDocument>("ai", AuthoringDocumentKind.Ai,
            AiAuthoringEditorService.Read, AiAuthoringJson.Serialize, AiAuthoringJson.Deserialize, AiAuthoringEditorService.Write),
        Handler<SkillDefinitionResource, SkillAuthoringDocument>("skill", AuthoringDocumentKind.Skill,
            SkillAuthoringEditorService.Read, SkillAuthoringJson.Serialize, SkillAuthoringJson.Deserialize, SkillAuthoringEditorService.Write),
        new PresentationProfileHandler()
    ]);

    private static IAuthoringResourceHandler Handler<TResource, TDocument>(string id, AuthoringDocumentKind kind,
        Func<TResource, TDocument> read, Func<TDocument, string> serialize, Func<string, TDocument> deserialize,
        Action<TResource, TDocument> write) where TResource : Resource where TDocument : class, IAuthoringDocument =>
        new DelegateHandler<TResource, TDocument>(id, kind, read, serialize, deserialize, write);

    private sealed class DelegateHandler<TResource, TDocument>(string resourceTypeId, AuthoringDocumentKind kind,
        Func<TResource, TDocument> read, Func<TDocument, string> serialize, Func<string, TDocument> deserialize,
        Action<TResource, TDocument> write) : IAuthoringResourceHandler
        where TResource : Resource where TDocument : class, IAuthoringDocument
    {
        public string ResourceTypeId => resourceTypeId;
        public AuthoringDocumentKind Kind => kind;
        public bool CanHandle(Resource resource) => resource is TResource;
        public IAuthoringDocument Read(Resource resource) => read((TResource)resource);
        public string Serialize(IAuthoringDocument document) => serialize((TDocument)document);
        public IAuthoringDocument Deserialize(string snapshot) => deserialize(snapshot);
        public IReadOnlyList<AuthoringDiagnostic> Validate(IAuthoringDocument document)
        {
            if (document is MapAuthoringDocument map) return MapAuthoringValidator.Validate(map);
            _ = Preview(document, 0);
            if (document is AiAuthoringDocument ai && ai.Nodes.Any(value => value.Kind == AiAuthoringNodeKind.Rule))
                return [new AuthoringDiagnostic("ai.rule_runtime_ignored", AuthoringDiagnosticSeverity.Warning,
                    "Rule node fields round-trip but AiDecisionService does not consume them. Intent priority and Score weight remain effective runtime parameters.")];
            return Array.Empty<AuthoringDiagnostic>();
        }
        public Resource Stage(Resource source, IAuthoringDocument document)
        {
            Resource staged = (Resource)source.Duplicate(true);
            Write(staged, document);
            return staged;
        }
        public AuthoringPreviewEvidence Preview(IAuthoringDocument document, int seed) => AuthoringPreviewCompiler.Compile(document, seed);
        public void Write(Resource resource, IAuthoringDocument document) => write((TResource)resource, (TDocument)document);
    }

    private sealed class PresentationProfileHandler : IAuthoringResourceHandler
    {
        public string ResourceTypeId => "presentation";
        public AuthoringDocumentKind Kind => AuthoringDocumentKind.Presentation;
        public bool CanHandle(Resource resource) => resource is SkillPresentationResource or StatusPresentationResource or StandardUnitPresentationResource;
        public IAuthoringDocument Read(Resource resource)
        {
            if (!CanHandle(resource)) throw new InvalidOperationException("Poison Spear Graph is Workbench-only and is not a generic Presentation Profile.");
            return PresentationProfileAuthoringEditorService.Read(resource);
        }
        public string Serialize(IAuthoringDocument document) => PresentationProfileAuthoringJson.Serialize((PresentationProfileAuthoringDocument)document);
        public IAuthoringDocument Deserialize(string snapshot) => PresentationProfileAuthoringJson.Deserialize(snapshot);
        public IReadOnlyList<AuthoringDiagnostic> Validate(IAuthoringDocument document)
        {
            _ = Preview(document, 0);
            return Array.Empty<AuthoringDiagnostic>();
        }
        public Resource Stage(Resource source, IAuthoringDocument document)
        {
            Resource staged = (Resource)source.Duplicate(true);
            Write(staged, document);
            return staged;
        }
        public AuthoringPreviewEvidence Preview(IAuthoringDocument document, int seed) => AuthoringPreviewCompiler.Compile(document, seed);
        public void Write(Resource resource, IAuthoringDocument document) => PresentationProfileAuthoringEditorService.Write(resource, (PresentationProfileAuthoringDocument)document);
    }
}
#endif
