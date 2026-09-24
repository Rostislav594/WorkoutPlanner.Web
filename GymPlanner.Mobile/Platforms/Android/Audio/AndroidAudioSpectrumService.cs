using Android.Media;
using AndroidAudioEncoding = Android.Media.Encoding;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;

namespace GymPlanner.Mobile.Audio;

public sealed class AndroidAudioSpectrumService : IAudioSpectrumService
{
    private const int SampleRate = 44_100;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private CancellationTokenSource? _captureCancellation;
    private Task? _captureTask;
    private AudioRecord? _audioRecord;
    private AudioSpectrumState _state = AudioSpectrumState.Inactive;

    public bool IsAvailable => true;

    public bool IsRunning => _state == AudioSpectrumState.Active;

    public AudioSpectrumState State => _state;

    public event Action<AudioSpectrumFrame>? SpectrumUpdated;

    public event Action<AudioSpectrumState>? StateChanged;

    public async Task<AudioSpectrumState> StartAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (_state == AudioSpectrumState.Active)
                return _state;

            SetState(AudioSpectrumState.Starting);
            var permission = await MauiPermissions.RequestAsync<MauiPermissions.Microphone>();
            if (permission != PermissionStatus.Granted)
            {
                SetState(AudioSpectrumState.PermissionDenied);
                return _state;
            }

            var minimumBufferSize = AudioRecord.GetMinBufferSize(
                SampleRate,
                ChannelIn.Mono,
                AndroidAudioEncoding.Pcm16bit);
            if (minimumBufferSize <= 0)
            {
                SetState(AudioSpectrumState.Unavailable);
                return _state;
            }

            await ReleaseRecorderAsync();
            var analyzer = new AudioSpectrumAnalyzer();
            var bufferSize = Math.Max(minimumBufferSize, analyzer.SampleCount * sizeof(short) * 2);
            _audioRecord = new AudioRecord(
                AudioSource.Mic,
                SampleRate,
                ChannelIn.Mono,
                AndroidAudioEncoding.Pcm16bit,
                bufferSize);

            if (_audioRecord.State != Android.Media.State.Initialized)
            {
                _audioRecord.Release();
                _audioRecord.Dispose();
                _audioRecord = null;
                SetState(AudioSpectrumState.Unavailable);
                return _state;
            }

            _captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _audioRecord.StartRecording();
            SetState(AudioSpectrumState.Active);
            _captureTask = Task.Run(
                () => CaptureAsync(_audioRecord, analyzer, _captureCancellation.Token),
                CancellationToken.None);
            return _state;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ReleaseRecorderAsync();
            SetState(AudioSpectrumState.Inactive);
            throw;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Audio spectrum startup failed: {exception}");
            await ReleaseRecorderAsync();
            SetState(AudioSpectrumState.Error);
            return _state;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_captureCancellation is null && _audioRecord is null)
            {
                if (_state != AudioSpectrumState.PermissionDenied)
                    SetState(AudioSpectrumState.Inactive);
                return;
            }

            _captureCancellation?.Cancel();
            try
            {
                _audioRecord?.Stop();
            }
            catch (InvalidOperationException)
            {
                // Recorder may already have stopped after an input-route change.
            }

            if (_captureTask is not null)
            {
                try
                {
                    await _captureTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            await ReleaseRecorderAsync();
            SetState(AudioSpectrumState.Inactive);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task CaptureAsync(
        AudioRecord recorder,
        AudioSpectrumAnalyzer analyzer,
        CancellationToken cancellationToken)
    {
        var samples = new short[analyzer.SampleCount];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = recorder.Read(samples, 0, samples.Length);
                if (read <= 0)
                {
                    if (read < 0)
                        throw new InvalidOperationException($"AudioRecord stopped with status {read}.");
                    continue;
                }

                SpectrumUpdated?.Invoke(analyzer.Analyze(samples, read, SampleRate));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Audio spectrum capture failed: {exception}");
            SetState(AudioSpectrumState.Error);
        }

        await Task.CompletedTask;
    }

    private Task ReleaseRecorderAsync()
    {
        _captureCancellation?.Dispose();
        _captureCancellation = null;
        _captureTask = null;

        if (_audioRecord is not null)
        {
            _audioRecord.Release();
            _audioRecord.Dispose();
            _audioRecord = null;
        }

        return Task.CompletedTask;
    }

    private void SetState(AudioSpectrumState state)
    {
        if (_state == state)
            return;

        _state = state;
        StateChanged?.Invoke(state);
    }
}
