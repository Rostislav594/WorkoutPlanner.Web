namespace GymPlanner.Mobile.Audio;

public interface IAudioSpectrumService
{
    bool IsAvailable { get; }

    bool IsRunning { get; }

    AudioSpectrumState State { get; }

    event Action<AudioSpectrumFrame>? SpectrumUpdated;

    event Action<AudioSpectrumState>? StateChanged;

    Task<AudioSpectrumState> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}

