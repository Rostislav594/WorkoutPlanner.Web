namespace WorkoutPlanner.UI.Components;

/// <summary>
/// Каталог контекстной помощи.
/// </summary>
/// <remarks>
/// Хранит КЛЮЧИ ресурсов, а не готовый текст: каталог статический и строится
/// один раз при загрузке типа, а язык пользователь переключает в любой момент.
/// Переводит ключи тот компонент, который их показывает
/// (<c>HelpDialog</c>, <c>HelpButton</c>, <c>WelcomeGuide</c>).
/// </remarks>
public static class HelpContentCatalog
{
    public static IReadOnlyList<WelcomeGuideSlide> WelcomeSlides { get; } =
    [
        new(
            "Welcome_Slide1_Title",
            "Welcome_Slide1_Message",
            WelcomeGuideVisual.Orientation),
        new(
            "Welcome_Slide2_Title",
            "Welcome_Slide2_Message",
            WelcomeGuideVisual.ContextualHelp),
        new(
            "Welcome_Slide3_Title",
            "Welcome_Slide3_Message",
            WelcomeGuideVisual.Support),
        new(
            "Welcome_Slide4_Title",
            "Welcome_Slide4_Message",
            WelcomeGuideVisual.Finish)
    ];

    public static IReadOnlyList<WelcomeGuideSlide> WelcomeSlidesWeb { get; } = WelcomeSlides;

    // Только для веба: в мобильном приложении «?» на «Сегодня» запускает
    // интерактивную демонстрацию вместо текстового руководства.
    public static HelpTopic TodayWeb { get; } = new(
        "Help_Today_Title",
        "Help_TodayWeb_Intro",
        [
            new("Help_TodayWeb_S1_Title", "Help_TodayWeb_S1_Text", ["Help_TodayWeb_S1_Step1", "Help_TodayWeb_S1_Step2", "Help_TodayWeb_S1_Step3"]),
            new("Help_TodayWeb_S2_Title", "Help_TodayWeb_S2_Text", ["Help_TodayWeb_S2_Step1", "Help_TodayWeb_S2_Step2", "Help_TodayWeb_S2_Step3"])
        ]);

    /// <summary>Список шаблонов. Шаги совпадают на мобильном и в вебе.</summary>
    public static HelpTopic Workouts { get; } = new(
        "Help_Workouts_Title",
        "Help_Workouts_Intro",
        [
            new("Help_Workouts_S1_Title", "Help_Workouts_S1_Text", ["Help_Workouts_S1_Step1", "Help_Workouts_S1_Step2", "Help_Workouts_S1_Step3"]),
            new("Help_Workouts_S2_Title", "Help_Workouts_S2_Text", ["Help_Workouts_S2_Step1", "Help_Workouts_S2_Step2", "Help_Workouts_S2_Step3"]),
            new("Help_Workouts_S3_Title", "Help_Workouts_S3_Text", ["Help_Workouts_S3_Step1", "Help_Workouts_S3_Step2", "Help_Workouts_S3_Step3"]),
            new("Help_Workouts_S4_Title", "Help_Workouts_S4_Text", ["Help_Workouts_S4_Step1", "Help_Workouts_S4_Step2", "Help_Workouts_S4_Step3"])
        ]);

    // В вебе карточка упражнения открывает меню, а не реагирует на удержание,
    // диалог добавления другой, а суперсетов на этой странице нет.
    public static HelpTopic WorkoutDetailsWeb { get; } = new(
        "Help_WorkoutDetails_Title",
        "Help_WorkoutDetails_Intro",
        [
            new("Help_WorkoutDetails_S1_Title", "Help_WorkoutDetailsWeb_S1_Text", ["Help_WorkoutDetailsWeb_S1_Step1", "Help_WorkoutDetailsWeb_S1_Step2", "Help_WorkoutDetailsWeb_S1_Step3", "Help_WorkoutDetailsWeb_S1_Step4"]),
            new("Help_WorkoutDetails_S2_Title", "Help_WorkoutDetailsWeb_S2_Text", ["Help_WorkoutDetailsWeb_S2_Step1", "Help_WorkoutDetailsWeb_S2_Step2"]),
            new("Help_WorkoutDetails_S4_Title", "Help_WorkoutDetailsWeb_S3_Text", ["Help_WorkoutDetailsWeb_S3_Step1", "Help_WorkoutDetailsWeb_S3_Step2", "Help_WorkoutDetailsWeb_S3_Step3"])
        ]);

    // В вебе назначение идёт в два шага через отдельную кнопку в окне дня,
    // а переноса на другую дату нет.
    public static HelpTopic CalendarWeb { get; } = new(
        "Help_Calendar_Title",
        "Help_CalendarWeb_Intro",
        [
            new("Help_Calendar_S1_Title", "Help_CalendarWeb_S1_Text", ["Help_CalendarWeb_S1_Step1", "Help_CalendarWeb_S1_Step2", "Help_CalendarWeb_S1_Step3", "Help_CalendarWeb_S1_Step4"]),
            new("Help_CalendarWeb_S2_Title", "Help_CalendarWeb_S2_Text", ["Help_CalendarWeb_S2_Step1", "Help_CalendarWeb_S2_Step2"]),
            new("Help_CalendarWeb_S3_Title", "Help_CalendarWeb_S3_Text", ["Help_CalendarWeb_S3_Step1", "Help_CalendarWeb_S3_Step2"])
        ]);

    // В вебе нет архива: вся история лежит одним списком.
    public static HelpTopic HistoryWeb { get; } = new(
        "Help_History_Title",
        "Help_History_Intro",
        [
            new("Help_History_S1_Title", "Help_History_S1_Text", ["Help_History_S1_Step1", "Help_History_S1_Step2", "Help_History_S1_Step3"]),
            new("Help_History_S2_Title", "Help_History_S2_Text", ["Help_History_S2_Step1", "Help_History_S2_Step2"]),
            new("Help_History_S3_Title", "Help_History_S3_Text", ["Help_History_S3_Step1", "Help_History_S3_Step2"])
        ]);

    // В вебе плитки без сводок, поэтому шагов про них нет.
    public static HelpTopic ProgressWeb { get; } = new(
        "Help_Progress_Title",
        "Help_Progress_Intro",
        [
            new("Help_Progress_S1_Title", "Help_Progress_S1_Text", ["Help_Progress_S1_Step1", "Help_Progress_S1_Step2"]),
            new("Help_Progress_S2_Title", "Help_Progress_S2_Text", ["Help_Progress_S2_Step1", "Help_Progress_S2_Step2"])
        ]);

    // В вебе упражнения сгруппированы по тренировкам, поэтому путь на шаг длиннее,
    // а автоповорота экрана нет.
    public static HelpTopic ProgressExercisesWeb { get; } = new(
        "Help_ProgressExercises_Title",
        "Help_ProgressExercises_Intro",
        [
            new("Help_ProgressExercises_S1_Title", "Help_ProgressExercisesWeb_S1_Text", ["Help_ProgressExercisesWeb_S1_Step1", "Help_ProgressExercisesWeb_S1_Step2", "Help_ProgressExercisesWeb_S1_Step3"]),
            new("Help_ProgressExercises_S2_Title", "Help_ProgressExercises_S2_Text", ["Help_ProgressExercises_S2_Step1", "Help_ProgressExercises_S2_Step2"], Illustration: HelpIllustration.ProgressExercise),
            new("Help_ProgressExercises_S3_Title", "Help_ProgressExercises_S3_Text", ["Help_ProgressExercisesWeb_S3_Step1", "Help_ProgressExercisesWeb_S3_Step2"])
        ]);

    // В вебе точка графика не открывает запись истории.
    public static HelpTopic ProgressWorkoutsWeb { get; } = new(
        "Help_ProgressWorkouts_Title",
        "Help_ProgressWorkouts_Intro",
        [
            new("Help_ProgressWorkouts_S1_Title", "Help_ProgressWorkouts_S1_Text", ["Help_ProgressWorkouts_S1_Step1", "Help_ProgressWorkouts_S1_Step2"]),
            new("Help_ProgressWorkouts_S2_Title", "Help_ProgressWorkouts_S2_Text", ["Help_ProgressWorkouts_S2_Step1", "Help_ProgressWorkouts_S2_Step2"], Illustration: HelpIllustration.ProgressWorkout),
            new("Help_ProgressWorkouts_S3_Title", "Help_ProgressWorkouts_S3_Text", ["Help_ProgressWorkouts_S3_Step1", "Help_ProgressWorkouts_S3_Step2", "Help_ProgressWorkouts_S3_Step3"])
        ]);

    // Подсказка профиля осталась только в вебе, где профиль — одна страница
    // блоками. В мобильном приложении кнопку помощи на профиле убрали.
    public static HelpTopic ProfileWeb { get; } = new(
        "Help_Profile_Title",
        "Help_ProfileWeb_Intro",
        [
            new("Help_Profile_S1_Title", "Help_ProfileWeb_S1_Text", ["Help_ProfileWeb_S1_Step1", "Help_ProfileWeb_S1_Step2"]),
            new("Help_ProfileWeb_S2_Title", "Help_ProfileWeb_S2_Text", ["Help_ProfileWeb_S2_Step1", "Help_ProfileWeb_S2_Step2", "Help_ProfileWeb_S2_Step3"]),
            new("Help_ProfileWeb_S3_Title", "Help_ProfileWeb_S3_Text", ["Help_ProfileWeb_S3_Step1", "Help_ProfileWeb_S3_Step2"])
        ]);
}

public sealed record WelcomeGuideSlide(string TitleKey, string MessageKey, WelcomeGuideVisual Visual);

public enum WelcomeGuideVisual
{
    Orientation,
    ContextualHelp,
    Support,
    Finish
}

public sealed record HelpTopic(string TitleKey, string IntroductionKey, IReadOnlyList<HelpSection> Sections);

public sealed record HelpSection(
    string TitleKey,
    string TextKey,
    IReadOnlyList<string>? StepKeys = null,
    string? ImageSource = null,
    string? ImageAltKey = null,
    HelpIllustration Illustration = HelpIllustration.None);

public enum HelpIllustration
{
    None,
    ProgressExercise,
    ProgressWorkout
}
