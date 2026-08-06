using WorkoutPlanner.Web.Services.Onboarding;

namespace WorkoutPlanner.Web.Components.Onboarding;

public sealed class AppGuideCatalog
{
    public const string FirstStepId = "navigation-welcome";
    public const string PracticeFirstStepId = "practice-ready";

    private readonly CharacterAssetCatalog _characters;
    private readonly AppGuidePracticeService _practice;

    public AppGuideCatalog(
        CharacterAssetCatalog characters,
        AppGuidePracticeService practice)
    {
        _characters = characters;
        _practice = practice;
    }

    public AppGuideScenario CreateScenario()
    {
        var steps = new List<GuideStep>
        {
            Page("navigation-welcome", GuideSection.Navigation, "/",
                "Привет! Я помогу тебе быстро освоиться в GymPlanner.",
                CharacterEmotion.Friendly, CharacterPose.Waving,
                "navigation-home-overview", GuidePlacement.Right),

            Page("navigation-home-overview", GuideSection.Navigation, "/",
                "Это главная страница. Отсюда начинается любой маршрут по приложению.",
                CharacterEmotion.Neutral, CharacterPose.Open,
                "navigation-home-links", GuidePlacement.Right),

            Spot("navigation-home-links", GuideSection.Navigation, "/", "home-navigation",
                "Основные разделы всегда под рукой: сегодня, календарь, тренировки, история и прогресс.",
                CharacterEmotion.Friendly, CharacterPose.PointingRight,
                "navigation-workouts-overview", GuidePlacement.Right),

            Page("navigation-workouts-overview", GuideSection.Navigation, "/workouts",
                "Здесь собраны шаблоны тренировок. Сначала осмотримся, а затем разберём детали.",
                CharacterEmotion.Neutral, CharacterPose.Open,
                "navigation-workouts-details", GuidePlacement.Left),

            Spot("navigation-workouts-details", GuideSection.Navigation, "/workouts", "workout-templates",
                "Шаблон хранит упражнения, количество подходов и отдельный вес для каждого подхода.",
                CharacterEmotion.Neutral, CharacterPose.Explaining,
                "navigation-calendar-overview", GuidePlacement.Below),

            Page("navigation-calendar-overview", GuideSection.Navigation, "/calendar",
                "Это календарь тренировок. Здесь видно расписание на месяц.",
                CharacterEmotion.Neutral, CharacterPose.Open,
                "navigation-calendar-details", GuidePlacement.Right),

            Spot("navigation-calendar-details", GuideSection.Navigation, "/calendar", "calendar-overview",
                "Выбери день, чтобы назначить на него один из подготовленных шаблонов.",
                CharacterEmotion.Friendly, CharacterPose.PointingLeft,
                "navigation-history-overview", GuidePlacement.Below),

            Page("navigation-history-overview", GuideSection.Navigation, "/history",
                "А это история тренировок — здесь сохраняются выполненные занятия и результаты.",
                CharacterEmotion.Neutral, CharacterPose.Open,
                "navigation-history-details", GuidePlacement.Right),

            Spot("navigation-history-details", GuideSection.Navigation, "/history", "workout-journal",
                "Открой прошлую тренировку, чтобы увидеть подходы, веса, повторения и оценить изменение результатов.",
                CharacterEmotion.Supportive, CharacterPose.Explaining,
                "navigation-tour-finish", GuidePlacement.Below),

            Page("navigation-tour-finish", GuideSection.Navigation, "/",
                "Краткая экскурсия закончена. Ты уже знаешь, где находятся основные разделы.",
                CharacterEmotion.Success, CharacterPose.ThumbsUp,
                "navigation-practice-offer", GuidePlacement.Right),

            ChoicePage("navigation-practice-offer", GuideSection.Navigation, "/",
                "Теперь я хотел бы вместе с тобой назначить твою первую тренировку.",
                CharacterEmotion.Friendly, CharacterPose.Inviting, GuidePlacement.Right,
                [
                    new GuideChoice
                    {
                        Id = "guided",
                        Text = "Это будет очень любезно с твоей стороны",
                        NextStepId = PracticeFirstStepId,
                        Outcome = "guided",
                        OnSelectedAsync = token => _practice.EnsureTutorialPlanAsync(token)
                    },
                    new GuideChoice
                    {
                        Id = "independent",
                        Text = "Сам разберусь",
                        NextStepId = "navigation-independent-final",
                        Outcome = "independent"
                    }
                ]),

            Page("navigation-independent-final", GuideSection.Navigation, "/",
                "А я вижу, ты матерый спортсмен! Ну что же, тогда желаю успехов!",
                CharacterEmotion.Playful, CharacterPose.Goodbye,
                null, GuidePlacement.Right, isFinal: true, primaryButtonText: "Готово"),

            Page(PracticeFirstStepId, GuideSection.Practice, "/workouts",
                "Тогда погнали! Я тебе всё расскажу и покажу.",
                CharacterEmotion.Excited, CharacterPose.Inviting,
                "practice-template-card", GuidePlacement.Left,
                allowBack: false, stableResumeStepId: PracticeFirstStepId),

            Spot("practice-template-card", GuideSection.Practice, "/workouts", "tutorial-workout-card",
                "Я подготовил безопасный шаблон «Знакомство с GymPlanner». Открой его — потом его можно изменить или удалить.",
                CharacterEmotion.Friendly, CharacterPose.PointingLeft,
                "practice-workout-overview", GuidePlacement.Left,
                allowInteraction: true, expectedActionId: "tutorial-plan-opened",
                autoAdvance: true, allowBack: false, stableResumeStepId: PracticeFirstStepId),

            Page("practice-workout-overview", GuideSection.Practice, "/",
                "Внутри шаблона находится учебная карточка упражнения. Сейчас настроим её под тебя.",
                CharacterEmotion.Neutral, CharacterPose.Open,
                "practice-exercise-card", GuidePlacement.Left,
                resolveRouteAsync: _practice.GetTutorialRouteAsync,
                allowBack: false, stableResumeStepId: "practice-workout-overview"),

            Spot("practice-exercise-card", GuideSection.Practice, "/", "practice-exercise-card",
                "Карточка показывает упражнение и все его подходы. У каждого подхода свой вес и число повторений.",
                CharacterEmotion.Neutral, CharacterPose.Explaining,
                "practice-exercise-menu", GuidePlacement.Right,
                resolveRouteAsync: _practice.GetTutorialRouteAsync,
                allowBack: false, stableResumeStepId: "practice-workout-overview"),

            Spot("practice-exercise-menu", GuideSection.Practice, "/", "practice-exercise-menu",
                "Открой меню учебного упражнения.",
                CharacterEmotion.Friendly, CharacterPose.PointingLeft,
                "practice-edit-action", GuidePlacement.Left,
                resolveRouteAsync: _practice.GetTutorialRouteAsync,
                allowInteraction: true, expectedActionId: "exercise-menu-opened",
                autoAdvance: true, allowBack: false, stableResumeStepId: "practice-workout-overview"),

            Spot("practice-edit-action", GuideSection.Practice, "/", "practice-edit-action",
                "Выбери редактирование — откроются параметры упражнения.",
                CharacterEmotion.Friendly, CharacterPose.PointingRight,
                "practice-edit-overview", GuidePlacement.Right,
                resolveRouteAsync: _practice.GetTutorialRouteAsync,
                allowInteraction: true, expectedActionId: "exercise-edit-opened",
                autoAdvance: true, allowBack: false, stableResumeStepId: "practice-workout-overview"),

            Page("practice-edit-overview", GuideSection.Practice, "/",
                "Здесь меняются название упражнения, подходы, веса и повторения.",
                CharacterEmotion.Neutral, CharacterPose.Explaining,
                "practice-set-count", GuidePlacement.Left,
                resolveRouteAsync: _practice.GetTutorialRouteAsync,
                allowBack: false, stableResumeStepId: "practice-workout-overview"),

            Spot("practice-set-count", GuideSection.Practice, "/", "practice-set-count",
                "Укажи нужное количество подходов. Поля ниже перестроятся автоматически.",
                CharacterEmotion.Thinking, CharacterPose.PointingRight,
                "practice-set-weight", GuidePlacement.Right,
                resolveRouteAsync: _practice.GetTutorialRouteAsync,
                allowInteraction: true, allowBack: false, stableResumeStepId: "practice-workout-overview"),

            Spot("practice-set-weight", GuideSection.Practice, "/", "practice-set-weight",
                "Вес задаётся отдельно для каждого подхода — именно эти значения используются в истории и прогрессе.",
                CharacterEmotion.Neutral, CharacterPose.PointingLeft,
                "practice-save-exercise", GuidePlacement.Left,
                resolveRouteAsync: _practice.GetTutorialRouteAsync,
                allowInteraction: true, allowBack: false, stableResumeStepId: "practice-workout-overview"),

            Spot("practice-save-exercise", GuideSection.Practice, "/", "practice-save-exercise",
                "Сохрани параметры, когда всё готово.",
                CharacterEmotion.Supportive, CharacterPose.PointingRight,
                "practice-assign", GuidePlacement.Right,
                resolveRouteAsync: _practice.GetTutorialRouteAsync,
                allowInteraction: true, expectedActionId: "exercise-saved",
                autoAdvance: true, allowBack: false, stableResumeStepId: "practice-workout-overview"),

            ChoicePage("practice-assign", GuideSection.Practice, "/calendar",
                "Теперь назначим учебную тренировку на сегодня. Если на сегодня уже есть назначение, оно будет заменено, но история прошлых тренировок сохранится.",
                CharacterEmotion.Friendly, CharacterPose.Inviting, GuidePlacement.Right,
                [
                    new GuideChoice
                    {
                        Id = "assign-today",
                        Text = "Заменить назначение на сегодня и продолжить",
                        NextStepId = "practice-today-overview",
                        OnSelectedAsync = _practice.ScheduleTutorialTodayAsync
                    }
                ],
                allowBack: false, stableResumeStepId: "practice-assign"),

            Page("practice-today-overview", GuideSection.Practice, "/today",
                "Тренировка назначена. На странице «Сегодня» выполняются подходы и завершается занятие.",
                CharacterEmotion.Excited, CharacterPose.Open,
                "practice-set-complete", GuidePlacement.Left,
                allowBack: false, stableResumeStepId: "practice-today-overview"),

            Spot("practice-set-complete", GuideSection.Practice, "/today", "practice-first-set",
                "Отметь первый выполненный подход. Остальные можно выполнить позже.",
                CharacterEmotion.Supportive, CharacterPose.PointingRight,
                "practice-status-button", GuidePlacement.Right,
                allowInteraction: true, expectedActionId: "practice-set-completed",
                autoAdvance: true, allowBack: false, stableResumeStepId: "practice-today-overview"),

            Spot("practice-status-button", GuideSection.Practice, "/today", "practice-status-button",
                "Теперь оцени, насколько тяжело далось упражнение.",
                CharacterEmotion.Thinking, CharacterPose.PointingLeft,
                "practice-status-option", GuidePlacement.Left,
                allowInteraction: true, expectedActionId: "practice-status-opened",
                autoAdvance: true, allowBack: false, stableResumeStepId: "practice-today-overview"),

            Spot("practice-status-option", GuideSection.Practice, "/today", "practice-status-option",
                "Выбери подходящую оценку. Она сохранится вместе с результатом.",
                CharacterEmotion.Friendly, CharacterPose.PointingRight,
                "practice-finish", GuidePlacement.Right,
                allowInteraction: true, expectedActionId: "practice-status-selected",
                autoAdvance: true, allowBack: false, stableResumeStepId: "practice-today-overview"),

            Spot("practice-finish", GuideSection.Practice, "/today", "practice-finish-workout",
                "Заверши тренировку. Результат попадёт в историю и расчёты прогресса.",
                CharacterEmotion.Excited, CharacterPose.PointingLeft,
                "practice-completed", GuidePlacement.Left,
                allowInteraction: true, expectedActionId: "practice-workout-finished",
                autoAdvance: true, allowBack: false, stableResumeStepId: "practice-today-overview"),

            Page("practice-completed", GuideSection.Practice, "/today",
                "Отличная работа! Твоя первая учебная тренировка завершена.",
                CharacterEmotion.Success, CharacterPose.ThumbsUp,
                "practice-history-overview", GuidePlacement.Left,
                allowBack: false, stableResumeStepId: "practice-history-overview"),

            Page("practice-history-overview", GuideSection.Practice, "/history",
                "Завершённая тренировка уже появилась в истории.",
                CharacterEmotion.Supportive, CharacterPose.Open,
                "practice-history-record", GuidePlacement.Right,
                allowBack: false, stableResumeStepId: "practice-history-overview"),

            Spot("practice-history-record", GuideSection.Practice, "/history", "practice-history-card",
                "Открой запись: внутри будут реальные веса каждого подхода, повторения и твоя оценка.",
                CharacterEmotion.Friendly, CharacterPose.PointingRight,
                "practice-success", GuidePlacement.Right,
                allowInteraction: true, expectedActionId: "practice-history-opened",
                autoAdvance: true,
                allowBack: false, stableResumeStepId: "practice-history-overview"),

            Page("practice-success", GuideSection.Practice, "/history",
                "Готово! Теперь ты умеешь настроить, назначить, выполнить тренировку и найти результат в истории.",
                CharacterEmotion.Success, CharacterPose.ThumbsUp,
                null, GuidePlacement.Right, isFinal: true,
                primaryButtonText: "Завершить", allowBack: false,
                stableResumeStepId: "practice-history-overview")
        };

        return new AppGuideScenario(steps);
    }

    private GuideStep Page(
        string id, GuideSection section, string route, string message,
        CharacterEmotion emotion, CharacterPose pose, string? nextStepId,
        GuidePlacement placement, bool isFinal = false,
        string primaryButtonText = "Далее", bool allowBack = true,
        string? stableResumeStepId = null,
        Func<CancellationToken, Task<string>>? resolveRouteAsync = null)
    {
        return new GuideStep
        {
            Id = id, Section = section, Route = route,
            ResolveRouteAsync = resolveRouteAsync,
            DisplayMode = GuideDisplayMode.PageOverview,
            Message = message, Emotion = emotion, Pose = pose,
            CharacterImage = _characters.Get(pose), Placement = placement,
            NextStepId = nextStepId, IsFinal = isFinal,
            PrimaryButtonText = primaryButtonText, AllowBack = allowBack,
            StableResumeStepId = stableResumeStepId
        };
    }

    private GuideStep ChoicePage(
        string id, GuideSection section, string route, string message,
        CharacterEmotion emotion, CharacterPose pose, GuidePlacement placement,
        IReadOnlyList<GuideChoice> choices, bool allowBack = true,
        string? stableResumeStepId = null)
    {
        var step = Page(id, section, route, message, emotion, pose, null,
            placement, allowBack: allowBack, stableResumeStepId: stableResumeStepId);
        return new GuideStep
        {
            Id = step.Id, Section = step.Section, Route = step.Route,
            DisplayMode = step.DisplayMode, Message = step.Message,
            Emotion = step.Emotion, Pose = step.Pose,
            CharacterImage = step.CharacterImage, Placement = step.Placement,
            Choices = choices, AllowBack = step.AllowBack,
            StableResumeStepId = step.StableResumeStepId
        };
    }

    private GuideStep Spot(
        string id, GuideSection section, string route, string target, string message,
        CharacterEmotion emotion, CharacterPose pose, string nextStepId,
        GuidePlacement placement,
        Func<CancellationToken, Task<string>>? resolveRouteAsync = null,
        bool allowInteraction = false, string? expectedActionId = null,
        bool autoAdvance = false, bool allowBack = true,
        string? stableResumeStepId = null)
    {
        return new GuideStep
        {
            Id = id, Section = section, Route = route,
            ResolveRouteAsync = resolveRouteAsync,
            Target = target, DisplayMode = GuideDisplayMode.Spotlight,
            Message = message, Emotion = emotion, Pose = pose,
            CharacterImage = _characters.Get(pose), Placement = placement,
            NextStepId = nextStepId, AllowTargetInteraction = allowInteraction,
            ExpectedActionId = expectedActionId,
            AutoAdvanceOnAction = autoAdvance,
            AllowBack = allowBack, StableResumeStepId = stableResumeStepId
        };
    }
}
