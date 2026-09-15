using WorkoutPlanner.Web.Application.Contracts;

namespace WorkoutPlanner.Web.Application.Abstractions;

/// <summary>
/// Черновик свободной тренировки, идущей прямо сейчас.
/// </summary>
/// <remarks>
/// Черновик существует на сервере всё время тренировки, а не только в её конце.
/// Иначе часы не могут показать свободную тренировку: они спрашивают активную
/// тренировку у сервера и о состоянии телефона ничего не знают.
/// </remarks>
public interface IFreeWorkoutDraftService
{
    /// <summary>
    /// Возвращает черновик пользователя, создавая его при необходимости.
    /// </summary>
    Task<FreeWorkoutDraftResult> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает текущий черновик или null, если свободная тренировка не идёт.
    /// </summary>
    Task<FreeWorkoutDraftResult?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет черновик вместе с упражнениями и подходами.
    /// </summary>
    /// <returns>Был ли черновик.</returns>
    Task<bool> DiscardAsync(CancellationToken cancellationToken = default);
}
