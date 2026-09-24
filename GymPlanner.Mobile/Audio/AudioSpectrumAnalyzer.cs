namespace GymPlanner.Mobile.Audio;

/// <summary>
/// Real-time analyzer: log-spaced bands of roughly half an octave, all
/// measured on one decibel scale so a sound only raises the bands it occupies.
/// </summary>
internal sealed class AudioSpectrumAnalyzer
{
    public const int BandCount = 20;
    private const int TransformSize = 4096;
    private const int Hop = 1024;
    private const double LowestFrequency = 45;
    private const double HighestFrequency = 16_000;

    // Decibels shown between the baseline and the top of the display.
    private const double DisplayRange = 30;

    // The top of the scale follows the loudest band, but never drops below this
    // level, so quiet-room microphone noise stays under the baseline.
    private const double MinimumReference = -46;
    private const double ReferenceFallPerHop = 0.2;

    private readonly double[] _history = new double[TransformSize];
    private readonly double[] _window = new double[TransformSize];
    private readonly double[] _real = new double[TransformSize];
    private readonly double[] _imaginary = new double[TransformSize];
    private readonly (int FirstBin, int LastBin)[] _bands;
    private readonly double _powerScale;
    private double _reference = MinimumReference;

    public AudioSpectrumAnalyzer(int sampleRate)
    {
        var windowSum = 0d;
        for (var index = 0; index < TransformSize; index++)
        {
            _window[index] = 0.5d - 0.5d * Math.Cos(2d * Math.PI * index / (TransformSize - 1));
            windowSum += _window[index];
        }

        // A full-scale sine then reads close to 0 dBFS.
        _powerScale = 4d / (windowSum * windowSum);
        _bands = CreateBands(sampleRate);
    }

    public int HopSize => Hop;

    public int WindowSize => TransformSize;

    public AudioSpectrumFrame Analyze(short[] samples, int sampleCount)
    {
        var count = Math.Min(sampleCount, TransformSize);
        Array.Copy(_history, count, _history, 0, TransformSize - count);
        for (var index = 0; index < count; index++)
            _history[TransformSize - count + index] = samples[index] / 32768d;

        for (var index = 0; index < TransformSize; index++)
        {
            _real[index] = _history[index] * _window[index];
            _imaginary[index] = 0d;
        }

        Transform(_real, _imaginary);

        var decibels = new double[BandCount];
        var loudest = double.NegativeInfinity;
        for (var bandIndex = 0; bandIndex < BandCount; bandIndex++)
        {
            var (firstBin, lastBin) = _bands[bandIndex];
            var power = 0d;
            for (var bin = firstBin; bin <= lastBin; bin++)
                power += _real[bin] * _real[bin] + _imaginary[bin] * _imaginary[bin];

            decibels[bandIndex] = 10d * Math.Log10(power * _powerScale + 1e-12);
            loudest = Math.Max(loudest, decibels[bandIndex]);
        }

        // One shared reference keeps the relative height of the bands intact.
        _reference = loudest > _reference
            ? loudest
            : Math.Max(MinimumReference, _reference - ReferenceFallPerHop);

        var floor = _reference - DisplayRange;
        var bands = new float[BandCount];
        for (var bandIndex = 0; bandIndex < BandCount; bandIndex++)
            bands[bandIndex] = (float)Math.Clamp((decibels[bandIndex] - floor) / DisplayRange, 0d, 1d);

        return new AudioSpectrumFrame(bands);
    }

    private static (int FirstBin, int LastBin)[] CreateBands(int sampleRate)
    {
        var binWidth = (double)sampleRate / TransformSize;
        var lastUsableBin = TransformSize / 2 - 1;
        var ratio = Math.Pow(HighestFrequency / LowestFrequency, 1d / BandCount);
        var bands = new (int, int)[BandCount];

        for (var bandIndex = 0; bandIndex < BandCount; bandIndex++)
        {
            var start = LowestFrequency * Math.Pow(ratio, bandIndex);
            var end = start * ratio;
            var firstBin = Math.Clamp((int)Math.Ceiling(start / binWidth), 1, lastUsableBin);
            var lastBin = Math.Clamp((int)Math.Floor(end / binWidth), firstBin, lastUsableBin);
            bands[bandIndex] = (firstBin, lastBin);
        }

        return bands;
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
