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
            var analyzer = new AudioSpectrumAnalyzer(SampleRate);
            var bufferSize = Math.Max(minimumBufferSize, analyzer.WindowSize * sizeof(short));
            _audioRecord = new AudioRecord(
                SelectAudioSource(),
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
        var samples = new short[analyzer.HopSize];
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

                SpectrumUpdated?.Invoke(analyzer.Analyze(samples, read));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            // StopAsync stops the recorder after cancelling, so a failed read then is expected.
            if (cancellationToken.IsCancellationRequested)
                return;

            System.Diagnostics.Debug.WriteLine($"Audio spectrum capture failed: {exception}");
            // StopAsync holds the state lock while awaiting this task, so the
            // recorder must be released from outside the capture loop.
            _ = Task.Run(() => ReleaseFailedCaptureAsync(recorder), CancellationToken.None);
        }

        await Task.CompletedTask;
    }

    private async Task ReleaseFailedCaptureAsync(AudioRecord recorder)
    {
        await _stateLock.WaitAsync();
        try
        {
            // A concurrent Stop or restart has already released this recorder.
            if (!ReferenceEquals(_audioRecord, recorder))
                return;

            try
            {
                recorder.Stop();
            }
            catch (InvalidOperationException)
            {
                // Recorder may already be stopped after the read failure.
            }

            if (_captureTask is not null)
                await _captureTask;

            await ReleaseRecorderAsync();
            SetState(AudioSpectrumState.Error);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Audio spectrum cleanup failed: {exception}");
            SetState(AudioSpectrumState.Error);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    // AudioSource.Mic runs through the device's automatic gain control, which
    // raises the gain in a quiet room until background noise fills the display.
    // Unprocessed is the raw signal; VoiceRecognition is the documented fallback
    // with gain control and noise suppression off by default.
    private static AudioSource SelectAudioSource()
    {
        var audioManager = Android.App.Application.Context.GetSystemService(
            Android.Content.Context.AudioService) as AudioManager;
        var supportsUnprocessed = string.Equals(
            audioManager?.GetProperty(AudioManager.PropertySupportAudioSourceUnprocessed),
            "true",
            StringComparison.OrdinalIgnoreCase);

        return supportsUnprocessed ? AudioSource.Unprocessed : AudioSource.VoiceRecognition;
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
