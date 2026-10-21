namespace GymPlanner.Mobile.Orientation;

public interface IScreenOrientationService
{
    /// <summary>Горизонтально — только для окна графика прогресса.</summary>
    void LockLandscape();

    /// <summary>Обычное положение приложения: всегда вертикально, без поворота.</summary>
    void LockPortrait();
}
