namespace GymPlanner.Mobile.Audio;

/// <summary>
/// Band levels from low to high frequency, each normalized to 0..1 on a shared
/// decibel scale.
/// </summary>
public sealed record AudioSpectrumFrame(float[] Bands);
