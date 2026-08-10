namespace GymPlanner.Mobile.Onboarding;

public static class MobileOnboardingCatalog
{
    public static IReadOnlyList<MobileOnboardingStep> Steps { get; } =
    [
        new(
            "navigation-welcome",
            "Добро пожаловать",
            "GymPlanner хранит планы и историю на защищённом сервере, а мобильное приложение помогает тренироваться с телефона.",
            "/"),
        new(
            "navigation-home-overview",
            "Тренировка на сегодня",
            "На главной странице находятся назначенная тренировка, ручные значения каждого подхода и безопасное завершение с сохранением истории.",
            "/"),
        new(
            "navigation-workouts-overview",
            "Планы и упражнения",
            "В разделе планов можно создавать упражнения и отдельно задавать вес и повторения для каждого подхода. Автоматическая прогрессия не применяется.",
            "/workouts"),
        new(
            "navigation-calendar-overview",
            "Расписание",
            "Календарь назначает один из ваших планов на выбранную дату и показывает завершённые тренировки.",
            "/calendar"),
        new(
            "navigation-history-overview",
            "История",
            "Журнал сохраняет неизменяемый снимок тренировки: фактические веса, повторения, отметки подходов и оценку упражнений.",
            "/history"),
        new(
            "navigation-history-details",
            "Прогресс",
            "Графики строятся сервером по фактическим значениям завершённых тренировок. Мобильный клиент только отображает рассчитанные точки.",
            "/progress"),
        new(
            "navigation-independent-final",
            "Всё готово",
            "Теперь можно назначить план в календаре и провести первую тренировку. Обучение всегда можно запустить заново из профиля.",
            null)
    ];
}

public sealed record MobileOnboardingStep(
    string Id,
    string Title,
    string Message,
    string? Route);
