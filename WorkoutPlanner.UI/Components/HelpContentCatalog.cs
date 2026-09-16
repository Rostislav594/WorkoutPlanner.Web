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

    public static HelpTopic Today { get; } = new(
        "Help_Today_Title",
        "Help_Today_Intro",
        [
            new("Help_Today_S1_Title", "Help_Today_S1_Text", ["Help_Today_S1_Step1", "Help_Today_S1_Step2", "Help_Today_S1_Step3"]),
            new("Help_Today_S2_Title", "Help_Today_S2_Text", ["Help_Today_S2_Step1", "Help_Today_S2_Step2", "Help_Today_S2_Step3"]),
            new("Help_Today_S3_Title", "Help_Today_S3_Text", ["Help_Today_S3_Step1", "Help_Today_S3_Step2", "Help_Today_S3_Step3", "Help_Today_S3_Step4"]),
            new("Help_Today_S4_Title", "Help_Today_S4_Text", ["Help_Today_S4_Step1", "Help_Today_S4_Step2", "Help_Today_S4_Step3"]),
            new("Help_Today_S5_Title", "Help_Today_S5_Text", ["Help_Today_S5_Step1", "Help_Today_S5_Step2"]),
            new("Help_Today_S6_Title", "Help_Today_S6_Text", ["Help_Today_S6_Step1", "Help_Today_S6_Step2"]),
            new("Help_Today_S7_Title", "Help_Today_S7_Text", ["Help_Today_S7_Step1", "Help_Today_S7_Step2", "Help_Today_S7_Step3"])
        ]);

    // У веба свои заголовки секций: свободных тренировок и таймеров отдыха там нет,
    // поэтому мобильные секции сюда не подходят.
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

    /// <summary>
    /// Страница шаблона. Раньше обе страницы раздела делили одну тему «Workouts»,
    /// из-за чего на странице упражнений открывалась помощь про список шаблонов.
    /// </summary>
    public static HelpTopic WorkoutDetails { get; } = new(
        "Help_WorkoutDetails_Title",
        "Help_WorkoutDetails_Intro",
        [
            new("Help_WorkoutDetails_S1_Title", "Help_WorkoutDetails_S1_Text", ["Help_WorkoutDetails_S1_Step1", "Help_WorkoutDetails_S1_Step2", "Help_WorkoutDetails_S1_Step3", "Help_WorkoutDetails_S1_Step4"]),
            new("Help_WorkoutDetails_S2_Title", "Help_WorkoutDetails_S2_Text", ["Help_WorkoutDetails_S2_Step1", "Help_WorkoutDetails_S2_Step2"]),
            new("Help_WorkoutDetails_S3_Title", "Help_WorkoutDetails_S3_Text", ["Help_WorkoutDetails_S3_Step1", "Help_WorkoutDetails_S3_Step2"]),
            new("Help_WorkoutDetails_S4_Title", "Help_WorkoutDetails_S4_Text", ["Help_WorkoutDetails_S4_Step1", "Help_WorkoutDetails_S4_Step2"]),
            new("Help_WorkoutDetails_S5_Title", "Help_WorkoutDetails_S5_Text", ["Help_WorkoutDetails_S5_Step1", "Help_WorkoutDetails_S5_Step2", "Help_WorkoutDetails_S5_Step3"])
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

    public static HelpTopic Calendar { get; } = new(
        "Help_Calendar_Title",
        "Help_Calendar_Intro",
        [
            new("Help_Calendar_S1_Title", "Help_Calendar_S1_Text", ["Help_Calendar_S1_Step1", "Help_Calendar_S1_Step2", "Help_Calendar_S1_Step3", "Help_Calendar_S1_Step4"]),
            new("Help_Calendar_S2_Title", "Help_Calendar_S2_Text", ["Help_Calendar_S2_Step1", "Help_Calendar_S2_Step2", "Help_Calendar_S2_Step3"]),
            new("Help_Calendar_S3_Title", "Help_Calendar_S3_Text", ["Help_Calendar_S3_Step1", "Help_Calendar_S3_Step2"]),
            new("Help_Calendar_S4_Title", "Help_Calendar_S4_Text", ["Help_Calendar_S4_Step1", "Help_Calendar_S4_Step2"])
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

    /// <summary>История. Шаги совпадают на мобильном и в вебе.</summary>
    public static HelpTopic History { get; } = new(
        "Help_History_Title",
        "Help_History_Intro",
        [
            new("Help_History_S1_Title", "Help_History_S1_Text", ["Help_History_S1_Step1", "Help_History_S1_Step2", "Help_History_S1_Step3"]),
            new("Help_History_S2_Title", "Help_History_S2_Text", ["Help_History_S2_Step1", "Help_History_S2_Step2"]),
            new("Help_History_S3_Title", "Help_History_S3_Text", ["Help_History_S3_Step1", "Help_History_S3_Step2"])
        ]);

    /// <summary>Обзор раздела прогресса: только выбор между двумя подразделами.</summary>
    public static HelpTopic Progress { get; } = new(
        "Help_Progress_Title",
        "Help_Progress_Intro",
        [
            new("Help_Progress_S1_Title", "Help_Progress_S1_Text", ["Help_Progress_S1_Step1", "Help_Progress_S1_Step2"]),
            new("Help_Progress_S2_Title", "Help_Progress_S2_Text", ["Help_Progress_S2_Step1", "Help_Progress_S2_Step2"])
        ]);

    // Иллюстрация-пример графика переехала сюда с обзорной страницы: она
    // поясняет именно чтение графика, а на обзоре пояснять было нечего.
    public static HelpTopic ProgressExercises { get; } = new(
        "Help_ProgressExercises_Title",
        "Help_ProgressExercises_Intro",
        [
            new("Help_ProgressExercises_S1_Title", "Help_ProgressExercises_S1_Text", ["Help_ProgressExercises_S1_Step1", "Help_ProgressExercises_S1_Step2", "Help_ProgressExercises_S1_Step3"]),
            new("Help_ProgressExercises_S2_Title", "Help_ProgressExercises_S2_Text", ["Help_ProgressExercises_S2_Step1", "Help_ProgressExercises_S2_Step2"], Illustration: HelpIllustration.ProgressExercise),
            new("Help_ProgressExercises_S3_Title", "Help_ProgressExercises_S3_Text", ["Help_ProgressExercises_S3_Step1", "Help_ProgressExercises_S3_Step2"])
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

    /// <summary>Прогресс тренировок. Шаги совпадают на мобильном и в вебе.</summary>
    public static HelpTopic ProgressWorkouts { get; } = new(
        "Help_ProgressWorkouts_Title",
        "Help_ProgressWorkouts_Intro",
        [
            new("Help_ProgressWorkouts_S1_Title", "Help_ProgressWorkouts_S1_Text", ["Help_ProgressWorkouts_S1_Step1", "Help_ProgressWorkouts_S1_Step2"]),
            new("Help_ProgressWorkouts_S2_Title", "Help_ProgressWorkouts_S2_Text", ["Help_ProgressWorkouts_S2_Step1", "Help_ProgressWorkouts_S2_Step2"], Illustration: HelpIllustration.ProgressWorkout),
            new("Help_ProgressWorkouts_S3_Title", "Help_ProgressWorkouts_S3_Text", ["Help_ProgressWorkouts_S3_Step1", "Help_ProgressWorkouts_S3_Step2", "Help_ProgressWorkouts_S3_Step3"])
        ]);

    public static HelpTopic Profile { get; } = new(
        "Help_Profile_Title",
        "Help_Profile_Intro",
        [
            new("Help_Profile_S1_Title", "Help_Profile_S1_Text", ["Help_Profile_S1_Step1", "Help_Profile_S1_Step2", "Help_Profile_S1_Step3"]),
            new("Help_Profile_S2_Title", "Help_Profile_S2_Text", ["Help_Profile_S2_Step1", "Help_Profile_S2_Step2", "Help_Profile_S2_Step3"]),
            new("Help_Profile_S3_Title", "Help_Profile_S3_Text", ["Help_Profile_S3_Step1", "Help_Profile_S3_Step2"]),
            new("Help_Profile_S4_Title", "Help_Profile_S4_Text", ["Help_Profile_S4_Step1", "Help_Profile_S4_Step2", "Help_Profile_S4_Step3", "Help_Profile_S4_Step4"])
        ]);

    // В вебе профиль — одна страница блоками, без списка разделов и без
    // таймеров отдыха и языка, которые есть только в мобильном приложении.
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
