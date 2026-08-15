using System.Text.Json;
using Godot;
using GodotFileAccess = Godot.FileAccess;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record GodotAudioSettingsV1(float Master, float Music, float Sfx, float Ui, bool MasterMuted)
{
    public const string Format = "tactics-audio-settings-v1";
    public static GodotAudioSettingsV1 Default { get; } = new(1f, 0.8f, 1f, 1f, false);
}

public sealed class GodotAudioSettingsStore
{
    private const string Path = "user://audio-settings-v1.json";

    public GodotAudioSettingsV1 Load()
    {
        if (!GodotFileAccess.FileExists(Path)) return GodotAudioSettingsV1.Default;
        using GodotFileAccess file = GodotFileAccess.Open(Path, GodotFileAccess.ModeFlags.Read);
        var document = JsonSerializer.Deserialize<AudioSettingsDocument>(file.GetAsText());
        return document is { Format: GodotAudioSettingsV1.Format }
            ? Validate(document.Settings)
            : throw new InvalidOperationException("Audio settings format is unsupported.");
    }

    public void Save(GodotAudioSettingsV1 settings)
    {
        settings = Validate(settings);
        string temporary = Path + ".tmp";
        string backup = Path + ".bak";
        using (GodotFileAccess file = GodotFileAccess.Open(temporary, GodotFileAccess.ModeFlags.Write))
        {
            file.StoreString(JsonSerializer.Serialize(new AudioSettingsDocument(GodotAudioSettingsV1.Format, settings)));
            file.Flush();
        }
        string absolutePath = ProjectSettings.GlobalizePath(Path);
        string absoluteTemporary = ProjectSettings.GlobalizePath(temporary);
        string absoluteBackup = ProjectSettings.GlobalizePath(backup);
        if (GodotFileAccess.FileExists(backup)) DirAccess.RemoveAbsolute(absoluteBackup);
        bool hadCurrent = GodotFileAccess.FileExists(Path);
        if (hadCurrent && DirAccess.RenameAbsolute(absolutePath, absoluteBackup) != Error.Ok)
            throw new IOException("Audio settings backup promotion failed.");
        Error error = DirAccess.RenameAbsolute(absoluteTemporary, absolutePath);
        if (error == Error.Ok) return;
        if (hadCurrent) DirAccess.RenameAbsolute(absoluteBackup, absolutePath);
        throw new IOException($"Audio settings promotion failed: {error}.");
    }

    public static GodotAudioSettingsV1 Validate(GodotAudioSettingsV1 settings)
    {
        static float Volume(float value) => float.IsFinite(value) && value is >= 0f and <= 1f
            ? value
            : throw new InvalidOperationException("Audio volume must be within 0..1.");
        return settings with { Master = Volume(settings.Master), Music = Volume(settings.Music), Sfx = Volume(settings.Sfx), Ui = Volume(settings.Ui) };
    }

    private sealed record AudioSettingsDocument(string Format, GodotAudioSettingsV1 Settings);
}
