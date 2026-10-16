using System.Globalization;

namespace GymPlanner.Mobile.Components.Layout;

/// <summary>
/// Пылинки фона: у каждой своё место, размер, путь, скорость и ритм мерцания,
/// поэтому они плывут вразнобой, а не одним слоем. Набор задаётся один раз
/// с фиксированным зерном — при перерисовке и между запусками он не меняется.
/// </summary>
internal static class AmbientDustField
{
    private const int FarCount = 36;
    private const int NearCount = 18;
    private const int Seed = 20261016;

    public static IReadOnlyList<AmbientDustParticle> Particles { get; } = Create();

    private static IReadOnlyList<AmbientDustParticle> Create()
    {
        var random = new Random(Seed);
        var particles = new List<AmbientDustParticle>(FarCount + NearCount);
        for (var index = 0; index < FarCount; index++)
            particles.Add(CreateParticle(random, near: false));
        for (var index = 0; index < NearCount; index++)
            particles.Add(CreateParticle(random, near: true));
        return particles;
    }

    // Дальние пылинки мельче, тусклее, медленнее и уходят недалеко; ближние
    // крупнее, ярче и проплывают заметнее — так сохраняется глубина.
    private static AmbientDustParticle CreateParticle(Random random, bool near)
    {
        double Between(double from, double to) => from + random.NextDouble() * (to - from);
        double Offset(double reach) => Between(-reach, reach);

        var reach = near ? 56 : 26;
        var warm = random.NextDouble() < .38;
        var drift = near ? Between(14, 26) : Between(24, 46);
        var style =
            $"--x:{F(Between(0, 100))}%;--y:{F(Between(0, 100))}%;" +
            $"--size:{F(near ? Between(3.2, 6) : Between(1.8, 3))}px;" +
            $"--o:{F(near ? Between(.24, .42) : Between(.14, .26))};" +
            $"--dx:{F(Offset(reach))}px;--dy:{F(Offset(reach))}px;" +
            $"--dx2:{F(Offset(reach))}px;--dy2:{F(Offset(reach))}px;" +
            $"--drift:{F(drift)}s;--drift-delay:{F(-Between(0, drift))}s;" +
            $"--twinkle:{F(Between(3.5, 9))}s;--twinkle-delay:{F(-Between(0, 9))}s;";
        var cssClass = "ambient-dust__particle"
            + (near ? " ambient-dust__particle--near" : null)
            + (warm ? " ambient-dust__particle--warm" : null);
        return new AmbientDustParticle(cssClass, style);
    }

    // Значения уходят в CSS, поэтому только с точкой, а не с русской запятой.
    private static string F(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}

internal sealed record AmbientDustParticle(string CssClass, string Style);
