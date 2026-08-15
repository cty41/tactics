using Godot;

namespace Tactics.Godot.Adapter.Runtime;

public partial class GodotAudioRuntime : Node
{
    private static readonly string[] BusNames = ["Music", "SFX", "UI"];
    private readonly Dictionary<string, List<AudioStreamPlayer>> _active = new(StringComparer.Ordinal);
    private GodotAudioSettingsV1 _settings = GodotAudioSettingsV1.Default;
    private IReadOnlyDictionary<string, AudioCueDefinitionResource> _eventCues = new Dictionary<string, AudioCueDefinitionResource>();

    public override void _Ready()
    {
        EnsureBuses();
        ApplySettings(new GodotAudioSettingsStore().Load());
    }

    public void ApplySettings(GodotAudioSettingsV1 settings)
    {
        _settings = GodotAudioSettingsStore.Validate(settings);
        SetBus("Master", settings.Master, settings.MasterMuted);
        SetBus("Music", settings.Music, false);
        SetBus("SFX", settings.Sfx, false);
        SetBus("UI", settings.Ui, false);
    }

    public bool Play(AudioCueDefinitionResource cue, ulong deterministicVariant = 0)
    {
        cue.Validate();
        if (cue.Variants.Length == 0) return false;
        Prune(cue.CueId);
        List<AudioStreamPlayer> players = _active.GetValueOrDefault(cue.CueId) ?? [];
        if (players.Count >= cue.MaxConcurrent) return false;
        var player = new AudioStreamPlayer
        {
            Stream = cue.Variants[(int)(deterministicVariant % (ulong)cue.Variants.Length)],
            Bus = BusNames[(int)cue.Bus],
            VolumeDb = cue.VolumeDb
        };
        player.Finished += () => { players.Remove(player); player.QueueFree(); };
        players.Add(player);
        _active[cue.CueId] = players;
        AddChild(player);
        player.Play();
        return true;
    }

    public void Configure(AudioCueCatalogResource catalog) => _eventCues = catalog.Compile();

    public bool PlayCommittedEvent(string eventId, ulong deterministicVariant = 0) =>
        _eventCues.TryGetValue(eventId, out AudioCueDefinitionResource? cue) && Play(cue, deterministicVariant);

    public void StopAll()
    {
        foreach (AudioStreamPlayer player in _active.Values.SelectMany(value => value).ToArray())
            if (GodotObject.IsInstanceValid(player)) player.QueueFree();
        _active.Clear();
    }

    public override void _ExitTree() => StopAll();

    private static void EnsureBuses()
    {
        foreach (string name in BusNames)
            if (AudioServer.GetBusIndex(name) < 0)
            {
                AudioServer.AddBus();
                AudioServer.SetBusName(AudioServer.BusCount - 1, name);
            }
    }

    private static void SetBus(string name, float linear, bool muted)
    {
        int index = AudioServer.GetBusIndex(name);
        if (index < 0) return;
        AudioServer.SetBusVolumeDb(index, linear <= 0f ? -80f : Mathf.LinearToDb(linear));
        AudioServer.SetBusMute(index, muted);
    }

    private void Prune(string cueId)
    {
        if (!_active.TryGetValue(cueId, out List<AudioStreamPlayer>? players)) return;
        players.RemoveAll(value => !GodotObject.IsInstanceValid(value) || !value.Playing);
    }
}
