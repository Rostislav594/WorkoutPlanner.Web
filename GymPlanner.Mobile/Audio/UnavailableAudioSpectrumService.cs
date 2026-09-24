namespace GymPlanner.Mobile.Audio;

public sealed class UnavailableAudioSpectrumService : IAudioSpectrumService
{
    public bool IsAvailable => false;

    public bool IsRunning => false;

    public AudioSpectrumState State => AudioSpectrumState.Unavailable;

    public event Action<AudioSpectrumFrame>? SpectrumUpdated
    {
        add { }
        remove { }
    }

    public event Action<AudioSpectrumState>? StateChanged
    {
        add { }
        remove { }
    }

    public Task<AudioSpectrumState> StartAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AudioSpectrumState.Unavailable);

    public Task StopAsync() => Task.CompletedTask;
}

