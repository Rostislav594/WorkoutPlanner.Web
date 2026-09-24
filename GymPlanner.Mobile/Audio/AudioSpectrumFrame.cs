namespace GymPlanner.Mobile.Audio;

public readonly record struct AudioSpectrumFrame(
    double Bass,
    double LowMid,
    double Mid,
    double High,
    double Beat,
    double Level);

