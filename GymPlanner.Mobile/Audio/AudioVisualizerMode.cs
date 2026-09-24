namespace GymPlanner.Mobile.Audio;

/// <summary>What the header equalizer shows while it is on.</summary>
public enum AudioVisualizerMode
{
    /// <summary>Sound around the phone, heard through the microphone.</summary>
    Ambient,

    /// <summary>A generated rhythm with no audio, for decoration only.</summary>
    Decorative
}

/// <summary>The last mode the user chose, reused when the speaker is tapped.</summary>
public static class AudioVisualizerModePreference
{
    private const string Key = "AudioVisualizerMode";

    public static AudioVisualizerMode Load() =>
        Enum.TryParse(Preferences.Default.Get(Key, nameof(AudioVisualizerMode.Decorative)), out AudioVisualizerMode mode)
            ? mode
            : AudioVisualizerMode.Decorative;

    public static void Save(AudioVisualizerMode mode) =>
        Preferences.Default.Set(Key, mode.ToString());
}
