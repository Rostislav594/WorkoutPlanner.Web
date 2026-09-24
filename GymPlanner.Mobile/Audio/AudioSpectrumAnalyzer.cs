namespace GymPlanner.Mobile.Audio;

internal sealed class AudioSpectrumAnalyzer
{
    private const int TransformSize = 2048;
    private static readonly (double Start, double End)[] Bands =
    [
        (35, 180),
        (180, 500),
        (500, 2_000),
        (2_000, 8_000)
    ];

    private readonly double[] _real = new double[TransformSize];
    private readonly double[] _imaginary = new double[TransformSize];
    private readonly double[] _peaks = [0.006, 0.004, 0.0025, 0.0015];
    private readonly double[] _smoothed = new double[Bands.Length];
    private double _previousBass;
    private double _beatAverage = 0.004;
    private double _beat;

    public int SampleCount => TransformSize;

    public AudioSpectrumFrame Analyze(short[] samples, int sampleCount, int sampleRate)
    {
        var usableCount = Math.Min(sampleCount, TransformSize);
        var energy = 0d;

        for (var index = 0; index < TransformSize; index++)
        {
            var sample = index < usableCount ? samples[index] / 32768d : 0d;
            var window = 0.5d - 0.5d * Math.Cos(2d * Math.PI * index / (TransformSize - 1));
            _real[index] = sample * window;
            _imaginary[index] = 0d;
            energy += sample * sample;
        }

        Transform(_real, _imaginary);

        var normalizedBands = new double[Bands.Length];
        var rawBands = new double[Bands.Length];
        for (var bandIndex = 0; bandIndex < Bands.Length; bandIndex++)
        {
            rawBands[bandIndex] = BandEnergy(Bands[bandIndex], sampleRate);
            _peaks[bandIndex] = Math.Max(rawBands[bandIndex], _peaks[bandIndex] * 0.992d);

            var normalized = Math.Clamp(rawBands[bandIndex] / Math.Max(_peaks[bandIndex], 0.000001d), 0d, 1d);
            normalized = Math.Pow(normalized, 0.62d);
            var smoothing = normalized > _smoothed[bandIndex] ? 0.82d : 0.28d;
            _smoothed[bandIndex] += (normalized - _smoothed[bandIndex]) * smoothing;
            normalizedBands[bandIndex] = _smoothed[bandIndex];
        }

        var bassRise = Math.Max(0d, rawBands[0] - _previousBass);
        _previousBass = rawBands[0];
        _beatAverage = _beatAverage * 0.9d + bassRise * 0.1d;
        var detectedBeat = bassRise > Math.Max(0.00038d, _beatAverage * 1.75d) && normalizedBands[0] > 0.36d;
        _beat = detectedBeat ? 1d : _beat * 0.55d;

        var level = Math.Clamp(Math.Sqrt(energy / Math.Max(usableCount, 1)) * 5.5d, 0d, 1d);
        return new AudioSpectrumFrame(
            normalizedBands[0],
            normalizedBands[1],
            normalizedBands[2],
            normalizedBands[3],
            _beat,
            level);
    }

    private double BandEnergy((double Start, double End) band, int sampleRate)
    {
        var firstBin = Math.Max(1, (int)Math.Floor(band.Start * TransformSize / sampleRate));
        var lastBin = Math.Min(TransformSize / 2 - 1, (int)Math.Ceiling(band.End * TransformSize / sampleRate));
        var sum = 0d;
        var count = 0;

        for (var bin = firstBin; bin <= lastBin; bin++)
        {
            var magnitudeSquared = _real[bin] * _real[bin] + _imaginary[bin] * _imaginary[bin];
            sum += magnitudeSquared;
            count++;
        }

        return count == 0 ? 0d : Math.Sqrt(sum / count) / TransformSize;
    }

    private static void Transform(double[] real, double[] imaginary)
    {
        var length = real.Length;
        for (int index = 1, reversed = 0; index < length; index++)
        {
            var bit = length >> 1;
            for (; (reversed & bit) != 0; bit >>= 1)
                reversed ^= bit;
            reversed ^= bit;

            if (index >= reversed)
                continue;

            (real[index], real[reversed]) = (real[reversed], real[index]);
            (imaginary[index], imaginary[reversed]) = (imaginary[reversed], imaginary[index]);
        }

        for (var blockSize = 2; blockSize <= length; blockSize <<= 1)
        {
            var angle = -2d * Math.PI / blockSize;
            var phaseStepReal = Math.Cos(angle);
            var phaseStepImaginary = Math.Sin(angle);

            for (var blockStart = 0; blockStart < length; blockStart += blockSize)
            {
                var phaseReal = 1d;
                var phaseImaginary = 0d;
                var halfBlock = blockSize >> 1;

                for (var offset = 0; offset < halfBlock; offset++)
                {
                    var evenIndex = blockStart + offset;
                    var oddIndex = evenIndex + halfBlock;
                    var oddReal = real[oddIndex] * phaseReal - imaginary[oddIndex] * phaseImaginary;
                    var oddImaginary = real[oddIndex] * phaseImaginary + imaginary[oddIndex] * phaseReal;

                    real[oddIndex] = real[evenIndex] - oddReal;
                    imaginary[oddIndex] = imaginary[evenIndex] - oddImaginary;
                    real[evenIndex] += oddReal;
                    imaginary[evenIndex] += oddImaginary;

                    var nextPhaseReal = phaseReal * phaseStepReal - phaseImaginary * phaseStepImaginary;
                    phaseImaginary = phaseReal * phaseStepImaginary + phaseImaginary * phaseStepReal;
                    phaseReal = nextPhaseReal;
                }
            }
        }
    }
}
