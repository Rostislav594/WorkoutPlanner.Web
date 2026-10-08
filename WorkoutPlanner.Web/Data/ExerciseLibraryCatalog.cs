using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Data;

/// <summary>
/// Встроенная библиотека упражнений: мышцы, упражнения, переводы и коэффициенты нагрузки.
/// </summary>
/// <remarks>
/// <para>
/// Ключ записи — русское название: по нему <see cref="ExerciseLibrarySeeder"/>
/// находит уже существующие строки базы. Переименовать упражнение здесь значит
/// добавить в базу новое, поэтому русские названия не меняются.
/// </para>
/// <para>
/// Коэффициент упражнения показывает, насколько оно нагружает основную мышцу
/// (база ~1,85–2,0, изоляция ~1,6–1,75, предплечья ~1,4–1,5). Коэффициент
/// вторичной мышцы — её доля от этой нагрузки. Первые 75 упражнений перенесены
/// из первой партии библиотеки без изменений: на них уже посчитан прогресс.
/// </para>
/// </remarks>
public static class ExerciseLibraryCatalog
{
    public sealed record MuscleEntry(
        string Name,
        string NameUk,
        string NameEn,
        MuscleBodyPart BodyPart);

    public sealed record SecondaryMuscleEntry(string Muscle, double Coefficient);

    public sealed record ExerciseEntry(
        string Name,
        string NameUk,
        string NameEn,
        string PrimaryMuscle,
        double Coefficient,
        IReadOnlyList<SecondaryMuscleEntry> SecondaryMuscles);

    private const string Chest = "Грудные мышцы";
    private const string Lats = "Широчайшие мышцы спины";
    private const string Traps = "Трапециевидные мышцы";
    private const string Rhomboids = "Ромбовидные мышцы";
    private const string Erectors = "Разгибатели спины";
    private const string FrontDelts = "Передняя дельтовидная";
    private const string SideDelts = "Средняя дельтовидная";
    private const string RearDelts = "Задняя дельтовидная";
    private const string Biceps = "Бицепс";
    private const string Triceps = "Трицепс";
    private const string Forearms = "Предплечья";
    private const string Abs = "Прямая мышца живота";
    private const string Obliques = "Косые мышцы живота";
    private const string HipFlexors = "Сгибатели бедра";
    private const string Quads = "Квадрицепс";
    private const string Hamstrings = "Бицепс бедра";
    private const string Glutes = "Ягодичные мышцы";
    private const string Calves = "Икроножные мышцы";
    private const string Adductors = "Приводящие мышцы бедра";

    public static IReadOnlyList<MuscleEntry> Muscles { get; } =
    [
        new(Chest, "Грудні м'язи", "Chest", MuscleBodyPart.Chest),
        new(Lats, "Найширші м'язи спини", "Lats", MuscleBodyPart.Back),
        new(Traps, "Трапецієподібні м'язи", "Traps", MuscleBodyPart.Back),
        new(Rhomboids, "Ромбоподібні м'язи", "Rhomboids", MuscleBodyPart.Back),
        new(Erectors, "Розгиначі спини", "Spinal erectors", MuscleBodyPart.Back),
        new(FrontDelts, "Передня дельта", "Front delts", MuscleBodyPart.Shoulders),
        new(SideDelts, "Середня дельта", "Side delts", MuscleBodyPart.Shoulders),
        new(RearDelts, "Задня дельта", "Rear delts", MuscleBodyPart.Shoulders),
        new(Biceps, "Біцепс", "Biceps", MuscleBodyPart.Arms),
        new(Triceps, "Трицепс", "Triceps", MuscleBodyPart.Arms),
        new(Forearms, "Передпліччя", "Forearms", MuscleBodyPart.Arms),
        new(Abs, "Прямий м'яз живота", "Abs", MuscleBodyPart.Core),
        new(Obliques, "Косі м'язи живота", "Obliques", MuscleBodyPart.Core),
        new(HipFlexors, "Згиначі стегна", "Hip flexors", MuscleBodyPart.Legs),
        new(Quads, "Квадрицепс", "Quads", MuscleBodyPart.Legs),
        new(Hamstrings, "Біцепс стегна", "Hamstrings", MuscleBodyPart.Legs),
        new(Glutes, "Сідничні м'язи", "Glutes", MuscleBodyPart.Glutes),
        new(Calves, "Литкові м'язи", "Calves", MuscleBodyPart.Legs),
        new(Adductors, "Привідні м'язи стегна", "Adductors", MuscleBodyPart.Legs)
    ];

    public static IReadOnlyList<ExerciseEntry> Exercises { get; } =
    [
        // Первая партия — порядок и значения как в базе.
        // Грудь
        E("Жим лёжа", "Жим лежачи", "Barbell bench press", Chest, 1.92, (FrontDelts, .35), (Triceps, .45)),
        E("Жим гантелей лёжа", "Жим гантелей лежачи", "Dumbbell bench press", Chest, 1.90, (FrontDelts, .35), (Triceps, .45)),
        E("Жим в тренажёре", "Жим у тренажері", "Machine chest press", Chest, 1.80, (FrontDelts, .30), (Triceps, .40)),
        E("Жим в Смите", "Жим у Сміті", "Smith machine bench press", Chest, 1.76, (FrontDelts, .30), (Triceps, .40)),
        E("Разведения гантелей лёжа", "Розведення гантелей лежачи", "Dumbbell fly", Chest, 1.63, (FrontDelts, .15)),
        E("Сведения рук в кроссовере", "Зведення рук у кросовері", "Cable crossover", Chest, 1.69, (FrontDelts, .15)),
        E("Пек-дек", "Пек-дек", "Pec deck", Chest, 1.70, (FrontDelts, .15)),
        E("Отжимания от пола", "Віджимання від підлоги", "Push-up", Chest, 1.73, (FrontDelts, .30), (Triceps, .40)),
        E("Отжимания на брусьях (грудь)", "Віджимання на брусах (груди)", "Chest dip", Chest, 1.88, (FrontDelts, .25), (Triceps, .50)),
        E("Жим гантелей на наклонной скамье", "Жим гантелей на похилій лаві", "Incline dumbbell press", Chest, 1.91, (FrontDelts, .45), (Triceps, .40)),
        E("Жим штанги на наклонной скамье", "Жим штанги на похилій лаві", "Incline barbell bench press", Chest, 1.90, (FrontDelts, .45), (Triceps, .40)),
        E("Жим в Hammer", "Жим у Hammer", "Hammer Strength chest press", Chest, 1.84, (FrontDelts, .30), (Triceps, .40)),
        E("Сведения рук в тренажёре", "Зведення рук у тренажері", "Machine chest fly", Chest, 1.69, (FrontDelts, .15)),

        // Широчайшие
        E("Подтягивания", "Підтягування", "Pull-up", Lats, 1.95, (Traps, .15), (RearDelts, .20), (Biceps, .50)),
        E("Подтягивания обратным хватом", "Підтягування зворотним хватом", "Chin-up", Lats, 1.93, (Traps, .10), (RearDelts, .15), (Biceps, .65)),
        E("Тяга верхнего блока", "Тяга верхнього блока", "Lat pulldown", Lats, 1.82, (Traps, .15), (RearDelts, .20), (Biceps, .45)),
        E("Тяга верхнего блока обратным хватом", "Тяга верхнього блока зворотним хватом", "Reverse-grip lat pulldown", Lats, 1.81, (Traps, .10), (RearDelts, .15), (Biceps, .60)),
        E("Тяга горизонтального блока", "Тяга горизонтального блока", "Seated cable row", Lats, 1.84, (Traps, .25), (RearDelts, .30), (Biceps, .40)),
        E("Тяга Т-грифа", "Тяга Т-грифа", "T-bar row", Lats, 1.90, (Traps, .30), (RearDelts, .30), (Biceps, .35)),
        E("Тяга штанги в наклоне", "Тяга штанги в нахилі", "Barbell bent-over row", Lats, 1.94, (Traps, .30), (Erectors, .25), (RearDelts, .30), (Biceps, .35)),
        E("Тяга гантели одной рукой", "Тяга гантелі однією рукою", "One-arm dumbbell row", Lats, 1.88, (Traps, .25), (RearDelts, .25), (Biceps, .35)),
        E("Тяга в Hammer", "Тяга в Hammer", "Hammer Strength row", Lats, 1.84, (Traps, .25), (RearDelts, .25), (Biceps, .35)),
        E("Пуловер в кроссовере", "Пуловер у кросовері", "Cable straight-arm pulldown", Lats, 1.67, (Triceps, .10)),

        // Плечи
        E("Жим штанги стоя", "Жим штанги стоячи", "Overhead barbell press", FrontDelts, 1.91, (Traps, .15), (SideDelts, .35), (Triceps, .45)),
        E("Жим гантелей сидя", "Жим гантелей сидячи", "Seated dumbbell shoulder press", FrontDelts, 1.89, (Traps, .15), (SideDelts, .35), (Triceps, .40)),
        E("Махи гантелями в стороны", "Махи гантелями в сторони", "Dumbbell lateral raise", SideDelts, 1.70, (Traps, .20)),
        E("Махи в кроссовере", "Махи в кросовері", "Cable lateral raise", SideDelts, 1.75, (Traps, .20)),
        E("Разведения в тренажёре", "Розведення в тренажері", "Machine rear delt fly", RearDelts, 1.69, (Traps, .20), (Rhomboids, .15)),
        E("Обратный пек-дек", "Зворотний пек-дек", "Reverse pec deck", RearDelts, 1.74),

        // Бицепс
        E("Подъём штанги на бицепс стоя", "Підйом штанги на біцепс стоячи", "Barbell curl", Biceps, 1.86, (Forearms, .30)),
        E("Подъём EZ-штанги на бицепс", "Підйом EZ-штанги на біцепс", "EZ-bar curl", Biceps, 1.88, (Forearms, .25)),
        E("Подъём гантелей на бицепс", "Підйом гантелей на біцепс", "Dumbbell curl", Biceps, 1.84, (Forearms, .30)),
        E("Молотки", "Молотки", "Hammer curl", Biceps, 1.81, (Forearms, .50)),
        E("Сгибание рук на скамье Скотта", "Згинання рук на лаві Скотта", "Preacher curl", Biceps, 1.87, (Forearms, .20)),
        E("Сгибание рук в кроссовере", "Згинання рук у кросовері", "Cable curl", Biceps, 1.85, (Forearms, .20)),

        // Трицепс
        E("Французский жим лёжа", "Французький жим лежачи", "Lying triceps extension", Triceps, 1.86, (FrontDelts, .10)),
        E("Французский жим сидя", "Французький жим сидячи", "Seated overhead triceps extension", Triceps, 1.84, (FrontDelts, .10)),
        E("Разгибание рук на верхнем блоке", "Розгинання рук на верхньому блоці", "Triceps pushdown", Triceps, 1.81, (FrontDelts, .10)),
        E("Разгибание руки с гантелью из-за головы", "Розгинання руки з гантеллю з-за голови", "One-arm overhead dumbbell extension", Triceps, 1.83, (FrontDelts, .10)),
        E("Отжимания узким хватом", "Віджимання вузьким хватом", "Close-grip push-up", Triceps, 1.89, (Chest, .35), (FrontDelts, .25)),
        E("Разгибание рук в кроссовере обратным хватом", "Розгинання рук у кросовері зворотним хватом", "Reverse-grip triceps pushdown", Triceps, 1.79),

        // Предплечья
        E("Сгибание кистей со штангой", "Згинання кистей зі штангою", "Barbell wrist curl", Forearms, 1.48),
        E("Разгибание кистей со штангой", "Розгинання кистей зі штангою", "Barbell reverse wrist curl", Forearms, 1.42),
        E("Сгибание кистей с гантелями", "Згинання кистей з гантелями", "Dumbbell wrist curl", Forearms, 1.46),
        E("Разгибание кистей с гантелями", "Розгинання кистей з гантелями", "Dumbbell reverse wrist curl", Forearms, 1.41),
        E("Сгибание кистей в кроссовере", "Згинання кистей у кросовері", "Cable wrist curl", Forearms, 1.44),

        // Пресс
        E("Скручивания", "Скручування", "Crunch", Abs, 1.66),
        E("Подъём ног в висе", "Підйом ніг у висі", "Hanging leg raise", Abs, 1.86, (HipFlexors, .40)),
        E("Подъём коленей в упоре", "Підйом колін в упорі", "Captain's chair knee raise", Abs, 1.80),
        E("Скручивания в тренажёре", "Скручування в тренажері", "Machine crunch", Abs, 1.70),
        E("Планка", "Планка", "Plank", Abs, 1.58),
        E("Боковые скручивания", "Бічні скручування", "Oblique crunch", Obliques, 1.64),

        // Ноги
        E("Приседания со штангой", "Присідання зі штангою", "Barbell back squat", Quads, 2.00, (Erectors, .20), (Hamstrings, .30), (Glutes, .45)),
        E("Фронтальные приседания", "Фронтальні присідання", "Front squat", Quads, 1.97, (Erectors, .25), (Glutes, .25)),
        E("Жим ногами", "Жим ногами", "Leg press", Quads, 1.90, (Hamstrings, .20), (Glutes, .30)),
        E("Болгарские выпады", "Болгарські випади", "Bulgarian split squat", Quads, 1.95, (Hamstrings, .30), (Glutes, .45)),
        E("Выпады со штангой", "Випади зі штангою", "Barbell lunge", Quads, 1.93, (Hamstrings, .30), (Glutes, .45)),
        E("Выпады с гантелями", "Випади з гантелями", "Dumbbell lunge", Quads, 1.90),
        E("Разгибание ног в тренажёре", "Розгинання ніг у тренажері", "Leg extension", Quads, 1.73),
        E("Румынская тяга", "Румунська тяга", "Romanian deadlift", Hamstrings, 1.96, (Erectors, .30), (Glutes, .45)),
        E("Становая тяга на прямых ногах", "Станова тяга на прямих ногах", "Stiff-leg deadlift", Hamstrings, 1.95, (Erectors, .35), (Glutes, .40)),
        E("Сгибание ног лёжа", "Згинання ніг лежачи", "Lying leg curl", Hamstrings, 1.74),
        E("Сгибание ног сидя", "Згинання ніг сидячи", "Seated leg curl", Hamstrings, 1.73),
        E("Ягодичный мост", "Сідничний міст", "Glute bridge", Glutes, 1.87, (Hamstrings, .35)),
        E("Хип Траст", "Хіп-траст", "Barbell hip thrust", Glutes, 1.92, (Hamstrings, .35)),
        E("Отведение ноги в кроссовере", "Відведення ноги в кросовері", "Cable hip abduction", Glutes, 1.65),
        E("Отведение ноги в тренажёре", "Відведення ноги в тренажері", "Machine hip abduction", Glutes, 1.63),
        E("Подъём на носки стоя", "Підйом на носки стоячи", "Standing calf raise", Calves, 1.76),
        E("Подъём на носки сидя", "Підйом на носки сидячи", "Seated calf raise", Calves, 1.73),
        E("Подъём на носки в тренажёре", "Підйом на носки в тренажері", "Machine calf raise", Calves, 1.75),

        // Разгибатели и трапеции
        E("Гиперэкстензия", "Гіперекстензія", "Back extension", Erectors, 1.79, (Hamstrings, .30), (Glutes, .35)),
        E("Обратная гиперэкстензия", "Зворотна гіперекстензія", "Reverse hyperextension", Erectors, 1.77, (Hamstrings, .35), (Glutes, .50)),
        E("Шраги со штангой", "Шраги зі штангою", "Barbell shrug", Traps, 1.83),
        E("Шраги с гантелями", "Шраги з гантелями", "Dumbbell shrug", Traps, 1.81),
        E("Шраги в тренажёре", "Шраги в тренажері", "Machine shrug", Traps, 1.79),

        // Расширение библиотеки, 2026-10. Только упражнения для зала.
        // Грудь
        E("Жим штанги на скамье с наклоном вниз", "Жим штанги на лаві з нахилом вниз", "Decline barbell bench press", Chest, 1.88, (FrontDelts, .25), (Triceps, .45)),
        E("Жим гантелей на скамье с наклоном вниз", "Жим гантелей на лаві з нахилом вниз", "Decline dumbbell press", Chest, 1.86, (FrontDelts, .25), (Triceps, .40)),
        E("Жим гантелей лёжа нейтральным хватом", "Жим гантелей лежачи нейтральним хватом", "Neutral-grip dumbbell press", Chest, 1.88, (FrontDelts, .35), (Triceps, .50)),
        E("Жим в Смите на наклонной скамье", "Жим у Сміті на похилій лаві", "Incline Smith machine press", Chest, 1.78, (FrontDelts, .40), (Triceps, .35)),
        E("Наклонный жим в тренажёре", "Похилий жим у тренажері", "Incline machine chest press", Chest, 1.79, (FrontDelts, .40), (Triceps, .35)),
        E("Наклонный жим в Hammer", "Похилий жим у Hammer", "Hammer Strength incline press", Chest, 1.83, (FrontDelts, .40), (Triceps, .35)),
        E("Разведения гантелей на наклонной скамье", "Розведення гантелей на похилій лаві", "Incline dumbbell fly", Chest, 1.64, (FrontDelts, .20)),
        E("Разведения в кроссовере лёжа на скамье", "Розведення в кросовері лежачи на лаві", "Lying cable fly", Chest, 1.66, (FrontDelts, .15)),
        E("Сведения рук в кроссовере снизу вверх", "Зведення рук у кросовері знизу вгору", "Low-to-high cable fly", Chest, 1.66, (FrontDelts, .20)),
        E("Сведения рук в кроссовере сверху вниз", "Зведення рук у кросовері згори вниз", "High-to-low cable fly", Chest, 1.67, (FrontDelts, .10)),
        E("Пуловер с гантелью", "Пуловер з гантеллю", "Dumbbell pullover", Chest, 1.68, (Lats, .40), (Triceps, .15)),
        E("Отжимания на брусьях в гравитроне", "Віджимання на брусах у гравітроні", "Assisted dip", Chest, 1.80, (FrontDelts, .25), (Triceps, .50)),

        // Широчайшие
        E("Подтягивания широким хватом", "Підтягування широким хватом", "Wide-grip pull-up", Lats, 1.95, (Traps, .15), (RearDelts, .20), (Biceps, .40)),
        E("Подтягивания нейтральным хватом", "Підтягування нейтральним хватом", "Neutral-grip pull-up", Lats, 1.94, (Traps, .10), (RearDelts, .15), (Biceps, .55)),
        E("Подтягивания в гравитроне", "Підтягування в гравітроні", "Assisted pull-up", Lats, 1.84, (Traps, .15), (RearDelts, .20), (Biceps, .45)),
        E("Тяга верхнего блока узким хватом", "Тяга верхнього блока вузьким хватом", "Close-grip lat pulldown", Lats, 1.82, (Traps, .10), (RearDelts, .15), (Biceps, .50)),
        E("Тяга верхнего блока за голову", "Тяга верхнього блока за голову", "Behind-the-neck lat pulldown", Lats, 1.80, (Traps, .15), (RearDelts, .25), (Biceps, .40)),
        E("Тяга верхнего блока одной рукой", "Тяга верхнього блока однією рукою", "Single-arm lat pulldown", Lats, 1.80, (Traps, .10), (RearDelts, .15), (Biceps, .45)),
        E("Тяга горизонтального блока широким хватом", "Тяга горизонтального блока широким хватом", "Wide-grip seated cable row", Lats, 1.82, (Traps, .30), (Rhomboids, .30), (RearDelts, .40), (Biceps, .30)),
        E("Тяга горизонтального блока одной рукой", "Тяга горизонтального блока однією рукою", "Single-arm seated cable row", Lats, 1.83, (Traps, .20), (RearDelts, .25), (Biceps, .35)),
        E("Тяга в тренажёре сидя", "Тяга в тренажері сидячи", "Seated machine row", Lats, 1.82, (Traps, .25), (Rhomboids, .25), (RearDelts, .30), (Biceps, .35)),
        E("Тяга в Hammer сверху", "Тяга в Hammer згори", "Hammer Strength pulldown", Lats, 1.82, (Traps, .10), (RearDelts, .15), (Biceps, .45)),
        E("Тяга гантелей в наклоне", "Тяга гантелей у нахилі", "Dumbbell bent-over row", Lats, 1.88, (Traps, .30), (Erectors, .15), (RearDelts, .30), (Biceps, .35)),
        E("Тяга гантелей лёжа на наклонной скамье", "Тяга гантелей лежачи на похилій лаві", "Chest-supported dumbbell row", Lats, 1.86, (Traps, .30), (Rhomboids, .30), (RearDelts, .30), (Biceps, .30)),
        E("Тяга штанги лёжа на скамье", "Тяга штанги лежачи на лаві", "Seal row", Lats, 1.88, (Traps, .30), (Rhomboids, .30), (RearDelts, .30), (Biceps, .30)),
        E("Тяга штанги в наклоне обратным хватом", "Тяга штанги в нахилі зворотним хватом", "Reverse-grip barbell row", Lats, 1.93, (Traps, .25), (Erectors, .25), (RearDelts, .25), (Biceps, .45)),
        E("Тяга Пендли", "Тяга Пендлі", "Pendlay row", Lats, 1.93, (Traps, .30), (Erectors, .25), (RearDelts, .30), (Biceps, .30)),
        E("Тяга в наклоне в Смите", "Тяга в нахилі в Сміті", "Smith machine bent-over row", Lats, 1.88, (Traps, .30), (Erectors, .15), (RearDelts, .30), (Biceps, .35)),
        E("Тяга Т-грифа с упором в грудь", "Тяга Т-грифа з упором у груди", "Chest-supported T-bar row", Lats, 1.88, (Traps, .30), (Rhomboids, .25), (RearDelts, .30), (Biceps, .35)),
        E("Тяга Мидоуса", "Тяга Мідоуза", "Meadows row", Lats, 1.86, (Traps, .25), (RearDelts, .30), (Biceps, .35)),
        E("Пуловер со штангой", "Пуловер зі штангою", "Barbell pullover", Lats, 1.68, (Chest, .35), (Triceps, .15)),
        E("Пуловер в тренажёре", "Пуловер у тренажері", "Machine pullover", Lats, 1.70, (Triceps, .10)),

        // Трапеции и разгибатели спины
        E("Шраги в Смите", "Шраги в Сміті", "Smith machine shrug", Traps, 1.80),
        E("Шраги на нижнем блоке", "Шраги на нижньому блоці", "Cable shrug", Traps, 1.78),
        E("Становая тяга", "Станова тяга", "Deadlift", Erectors, 2.00, (Traps, .30), (Forearms, .20), (Quads, .30), (Hamstrings, .45), (Glutes, .50)),
        E("Тяга с плинтов", "Тяга з плінтів", "Rack pull", Erectors, 1.90, (Lats, .20), (Traps, .40), (Hamstrings, .25), (Glutes, .35)),
        E("Разгибание спины в тренажёре", "Розгинання спини в тренажері", "Machine back extension", Erectors, 1.74, (Glutes, .20)),

        // Плечи
        E("Жим штанги сидя", "Жим штанги сидячи", "Seated barbell shoulder press", FrontDelts, 1.89, (Traps, .15), (SideDelts, .35), (Triceps, .40)),
        E("Жим гантелей стоя", "Жим гантелей стоячи", "Standing dumbbell shoulder press", FrontDelts, 1.89, (Traps, .15), (SideDelts, .35), (Triceps, .40)),
        E("Жим Арнольда", "Жим Арнольда", "Arnold press", FrontDelts, 1.88, (SideDelts, .40), (Triceps, .35)),
        E("Жим на плечи в тренажёре", "Жим на плечі в тренажері", "Machine shoulder press", FrontDelts, 1.82, (SideDelts, .30), (Triceps, .40)),
        E("Жим в Смите сидя", "Жим у Сміті сидячи", "Seated Smith machine shoulder press", FrontDelts, 1.84, (SideDelts, .30), (Triceps, .40)),
        E("Жим штанги одной рукой (лэндмайн)", "Жим штанги однією рукою (лендмайн)", "Single-arm landmine press", FrontDelts, 1.82, (Chest, .25), (Triceps, .30)),
        E("Подъём гантелей перед собой", "Підйом гантелей перед собою", "Dumbbell front raise", FrontDelts, 1.66, (Chest, .15), (SideDelts, .20)),
        E("Подъём штанги перед собой", "Підйом штанги перед собою", "Barbell front raise", FrontDelts, 1.67, (Chest, .15), (SideDelts, .20)),
        E("Подъём рук перед собой в кроссовере", "Підйом рук перед собою в кросовері", "Cable front raise", FrontDelts, 1.66, (Chest, .15), (SideDelts, .20)),
        E("Подъём блина перед собой", "Підйом млинця перед собою", "Plate front raise", FrontDelts, 1.65, (Chest, .15), (SideDelts, .20)),
        E("Махи гантелями в стороны сидя", "Махи гантелями в сторони сидячи", "Seated dumbbell lateral raise", SideDelts, 1.71, (Traps, .15)),
        E("Махи в стороны в тренажёре", "Махи в сторони в тренажері", "Machine lateral raise", SideDelts, 1.73, (Traps, .15)),
        E("Тяга штанги к подбородку", "Тяга штанги до підборіддя", "Barbell upright row", SideDelts, 1.80, (Traps, .50), (FrontDelts, .20), (Biceps, .15)),
        E("Тяга к подбородку в кроссовере", "Тяга до підборіддя в кросовері", "Cable upright row", SideDelts, 1.77, (Traps, .45), (FrontDelts, .20), (Biceps, .15)),
        E("Махи гантелями в наклоне", "Махи гантелями в нахилі", "Bent-over dumbbell reverse fly", RearDelts, 1.71, (Traps, .20), (Rhomboids, .20)),
        E("Обратные разведения в кроссовере", "Зворотні розведення в кросовері", "Cable reverse fly", RearDelts, 1.72, (Traps, .20), (Rhomboids, .20)),
        E("Тяга каната к лицу", "Тяга канату до обличчя", "Face pull", RearDelts, 1.76, (Traps, .30), (Rhomboids, .30), (SideDelts, .20)),

        // Бицепс
        E("Подъём гантелей на бицепс на наклонной скамье", "Підйом гантелей на біцепс на похилій лаві", "Incline dumbbell curl", Biceps, 1.86, (Forearms, .20)),
        E("Концентрированный подъём на бицепс", "Концентрований підйом на біцепс", "Concentration curl", Biceps, 1.85, (Forearms, .15)),
        E("Сгибание рук в тренажёре", "Згинання рук у тренажері", "Machine biceps curl", Biceps, 1.82, (Forearms, .15)),
        E("Паучьи сгибания", "Павучі згинання", "Spider curl", Biceps, 1.84, (Forearms, .15)),
        E("Молотки с канатом в кроссовере", "Молотки з канатом у кросовері", "Cable rope hammer curl", Biceps, 1.79, (Forearms, .45)),
        E("Сгибание рук на верхних блоках", "Згинання рук на верхніх блоках", "Overhead cable curl", Biceps, 1.80, (Forearms, .10)),
        E("Сгибание руки в кроссовере из-за спины", "Згинання руки в кросовері з-за спини", "Bayesian cable curl", Biceps, 1.83, (Forearms, .15)),
        E("Сгибания Зоттмана", "Згинання Зоттмана", "Zottman curl", Biceps, 1.82, (Forearms, .45)),

        // Трицепс
        E("Жим штанги узким хватом", "Жим штанги вузьким хватом", "Close-grip bench press", Triceps, 1.90, (Chest, .40), (FrontDelts, .25)),
        E("Отжимания на брусьях (трицепс)", "Віджимання на брусах (трицепс)", "Triceps dip", Triceps, 1.90, (Chest, .30), (FrontDelts, .25)),
        E("Отжимания в тренажёре", "Віджимання в тренажері", "Machine dip", Triceps, 1.82, (Chest, .25), (FrontDelts, .20)),
        E("Обратные отжимания от скамьи", "Зворотні віджимання від лави", "Bench dip", Triceps, 1.74, (Chest, .20), (FrontDelts, .30)),
        E("Разгибание рук с канатом на верхнем блоке", "Розгинання рук з канатом на верхньому блоці", "Rope triceps pushdown", Triceps, 1.80),
        E("Разгибание одной руки на верхнем блоке", "Розгинання однієї руки на верхньому блоці", "Single-arm cable pushdown", Triceps, 1.79),
        E("Разгибание рук из-за головы в кроссовере", "Розгинання рук з-за голови в кросовері", "Overhead cable triceps extension", Triceps, 1.83, (FrontDelts, .10)),
        E("Разгибание руки с гантелью в наклоне", "Розгинання руки з гантеллю в нахилі", "Dumbbell triceps kickback", Triceps, 1.72),
        E("Французский жим с гантелями лёжа", "Французький жим з гантелями лежачи", "Lying dumbbell triceps extension", Triceps, 1.84, (FrontDelts, .10)),
        E("Разгибание рук в тренажёре", "Розгинання рук у тренажері", "Machine triceps extension", Triceps, 1.78),

        // Предплечья
        E("Подъём штанги обратным хватом", "Підйом штанги зворотним хватом", "Reverse barbell curl", Forearms, 1.52, (Biceps, .50)),
        E("Сгибание кистей со штангой за спиной", "Згинання кистей зі штангою за спиною", "Behind-the-back wrist curl", Forearms, 1.47),
        E("Кистевой ролик", "Кистьовий ролик", "Wrist roller", Forearms, 1.45),

        // Пресс
        E("Скручивания на верхнем блоке", "Скручування на верхньому блоці", "Cable crunch", Abs, 1.75, (Obliques, .20)),
        E("Скручивания на наклонной скамье", "Скручування на похилій лаві", "Decline crunch", Abs, 1.70, (HipFlexors, .20)),
        E("Обратные скручивания", "Зворотні скручування", "Reverse crunch", Abs, 1.72, (HipFlexors, .30)),
        E("Подъём ног лёжа", "Підйом ніг лежачи", "Lying leg raise", Abs, 1.72, (HipFlexors, .45)),
        E("Подъём ног в упоре", "Підйом ніг в упорі", "Captain's chair leg raise", Abs, 1.82, (HipFlexors, .45)),
        E("Подъём коленей в висе", "Підйом колін у висі", "Hanging knee raise", Abs, 1.78, (HipFlexors, .40)),
        E("Ролик для пресса", "Ролик для преса", "Ab wheel rollout", Abs, 1.82, (Lats, .15), (HipFlexors, .20)),
        E("Боковая планка", "Бічна планка", "Side plank", Obliques, 1.58),
        E("Русские скручивания", "Російські скручування", "Russian twist", Obliques, 1.62, (HipFlexors, .20)),
        E("Дровосек в кроссовере", "Дроворуб у кросовері", "Cable woodchop", Obliques, 1.68),
        E("Наклоны в сторону с гантелью", "Нахили в сторону з гантеллю", "Dumbbell side bend", Obliques, 1.60),
        E("Косые подъёмы ног в висе", "Косі підйоми ніг у висі", "Hanging oblique knee raise", Obliques, 1.76, (Abs, .30), (HipFlexors, .30)),

        // Квадрицепс
        E("Гакк-приседания", "Гак-присідання", "Hack squat", Quads, 1.92, (Hamstrings, .15), (Glutes, .30)),
        E("Приседания в Смите", "Присідання в Сміті", "Smith machine squat", Quads, 1.90, (Erectors, .10), (Hamstrings, .20), (Glutes, .40)),
        E("Гоблет-приседания", "Гоблет-присідання", "Goblet squat", Quads, 1.86, (Erectors, .10), (Glutes, .35)),
        E("Приседания в маятниковом тренажёре", "Присідання в маятниковому тренажері", "Pendulum squat", Quads, 1.90, (Glutes, .30)),
        E("Сисси-приседания", "Сіссі-присідання", "Sissy squat", Quads, 1.76),
        E("Горизонтальный жим ногами", "Горизонтальний жим ногами", "Seated leg press", Quads, 1.86, (Hamstrings, .20), (Glutes, .30)),
        E("Зашагивания на тумбу", "Зашагування на тумбу", "Step-up", Quads, 1.86, (Hamstrings, .20), (Glutes, .50)),
        E("Обратные выпады", "Зворотні випади", "Reverse lunge", Quads, 1.88, (Hamstrings, .25), (Glutes, .50)),
        E("Выпады в ходьбе", "Випади в ходьбі", "Walking lunge", Quads, 1.90, (Hamstrings, .30), (Glutes, .50)),
        E("Выпады в Смите", "Випади в Сміті", "Smith machine lunge", Quads, 1.88, (Hamstrings, .30), (Glutes, .45)),
        E("Болгарские выпады в Смите", "Болгарські випади в Сміті", "Smith machine Bulgarian split squat", Quads, 1.92, (Hamstrings, .30), (Glutes, .45)),
        E("Выпады в сторону", "Випади в сторону", "Lateral lunge", Quads, 1.84, (Glutes, .40), (Adductors, .40)),

        // Бицепс бедра
        E("Румынская тяга с гантелями", "Румунська тяга з гантелями", "Dumbbell Romanian deadlift", Hamstrings, 1.92, (Erectors, .25), (Glutes, .45)),
        E("Румынская тяга в Смите", "Румунська тяга в Сміті", "Smith machine Romanian deadlift", Hamstrings, 1.90, (Erectors, .25), (Glutes, .45)),
        E("Румынская тяга на одной ноге", "Румунська тяга на одній нозі", "Single-leg Romanian deadlift", Hamstrings, 1.88, (Erectors, .20), (Glutes, .50)),
        E("Гуд морнинг", "Гуд морнінг", "Good morning", Hamstrings, 1.86, (Erectors, .45), (Glutes, .35)),
        E("Сгибание ноги стоя в тренажёре", "Згинання ноги стоячи в тренажері", "Standing leg curl", Hamstrings, 1.72),
        E("Скандинавские сгибания", "Скандинавські згинання", "Nordic hamstring curl", Hamstrings, 1.85),

        // Ягодицы
        E("Хип-траст в тренажёре", "Хіп-траст у тренажері", "Machine hip thrust", Glutes, 1.88, (Hamstrings, .30)),
        E("Хип-траст в Смите", "Хіп-траст у Сміті", "Smith machine hip thrust", Glutes, 1.90, (Hamstrings, .30)),
        E("Ягодичный мост на одной ноге", "Сідничний міст на одній нозі", "Single-leg glute bridge", Glutes, 1.82, (Hamstrings, .35)),
        E("Отведение ноги назад в тренажёре", "Відведення ноги назад у тренажері", "Machine glute kickback", Glutes, 1.70, (Hamstrings, .20)),
        E("Разведение ног в тренажёре", "Розведення ніг у тренажері", "Seated hip abduction", Glutes, 1.65),
        E("Протяжка в кроссовере", "Протяжка в кросовері", "Cable pull-through", Glutes, 1.78, (Erectors, .15), (Hamstrings, .35)),
        E("Становая тяга сумо", "Станова тяга сумо", "Sumo deadlift", Glutes, 1.96, (Traps, .20), (Erectors, .40), (Quads, .35), (Hamstrings, .35), (Adductors, .35)),
        E("Становая тяга с трэп-грифом", "Станова тяга з треп-грифом", "Trap bar deadlift", Glutes, 1.95, (Traps, .30), (Erectors, .35), (Quads, .50), (Hamstrings, .30)),
        E("Выпады-реверанс", "Випади-реверанс", "Curtsy lunge", Glutes, 1.84, (Quads, .45), (Adductors, .20)),

        // Икры и приводящие
        E("Подъём на носки в жиме ногами", "Підйом на носки в жимі ногами", "Leg press calf raise", Calves, 1.74),
        E("Подъём на носки в Смите", "Підйом на носки в Сміті", "Smith machine calf raise", Calves, 1.75),
        E("Подъём на носки в гакк-тренажёре", "Підйом на носки в гак-тренажері", "Hack squat calf raise", Calves, 1.74),
        E("Подъём на носок с гантелью на одной ноге", "Підйом на носок з гантеллю на одній нозі", "Single-leg dumbbell calf raise", Calves, 1.72),
        E("Сведение ног в тренажёре", "Зведення ніг у тренажері", "Seated hip adduction", Adductors, 1.66),
        E("Сведение ноги в кроссовере", "Зведення ноги в кросовері", "Cable hip adduction", Adductors, 1.62),
        E("Приседания плие с гантелью", "Присідання пліє з гантеллю", "Dumbbell sumo squat", Adductors, 1.82, (Quads, .40), (Glutes, .50)),

        // Вторая партия, 2026-10: свой вес, воркаут, гири, резинки и петли.
        // Грудь
        E("Отжимания широким хватом", "Віджимання широким хватом", "Wide push-up", Chest, 1.74, (FrontDelts, .30), (Triceps, .30)),
        E("Отжимания с ногами на возвышении", "Віджимання з ногами на підвищенні", "Decline push-up", Chest, 1.76, (FrontDelts, .40), (Triceps, .40)),
        E("Отжимания с упором руками на скамью", "Віджимання з упором руками на лаву", "Incline push-up", Chest, 1.66, (FrontDelts, .25), (Triceps, .35)),
        E("Отжимания с колен", "Віджимання з колін", "Knee push-up", Chest, 1.60, (FrontDelts, .25), (Triceps, .35)),
        E("Отжимания с хлопком", "Віджимання з оплеском", "Clap push-up", Chest, 1.78, (FrontDelts, .30), (Triceps, .40)),
        E("Отжимания на кольцах", "Віджимання на кільцях", "Ring push-up", Chest, 1.78, (FrontDelts, .30), (Triceps, .40)),
        E("Отжимания на кольцах (брусья)", "Віджимання на кільцях (бруси)", "Ring dip", Chest, 1.88, (FrontDelts, .30), (Triceps, .50)),

        // Спина
        E("Австралийские подтягивания", "Австралійські підтягування", "Inverted row", Lats, 1.78, (Traps, .25), (Rhomboids, .25), (RearDelts, .30), (Biceps, .35)),
        E("Тяга на петлях TRX", "Тяга на петлях TRX", "TRX row", Lats, 1.72, (Traps, .25), (Rhomboids, .25), (RearDelts, .30), (Biceps, .35)),
        E("Подтягивания с резинкой", "Підтягування з гумою", "Band-assisted pull-up", Lats, 1.86, (Traps, .15), (RearDelts, .20), (Biceps, .45)),
        E("Негативные подтягивания", "Негативні підтягування", "Negative pull-up", Lats, 1.88, (Traps, .15), (RearDelts, .20), (Biceps, .50)),
        E("Выход силой", "Вихід силою", "Muscle-up", Lats, 1.95, (Chest, .30), (Traps, .20), (Biceps, .35), (Triceps, .45)),
        E("Передний вис", "Передній вис", "Front lever", Lats, 1.84, (RearDelts, .20), (Abs, .45)),
        E("Тяга резинки к поясу", "Тяга гуми до пояса", "Band row", Lats, 1.66, (Traps, .20), (Rhomboids, .25), (RearDelts, .25), (Biceps, .30)),
        E("Тяга гири в наклоне", "Тяга гирі в нахилі", "Kettlebell row", Lats, 1.84, (Traps, .25), (RearDelts, .25), (Biceps, .35)),
        E("Супермен", "Супермен", "Superman", Erectors, 1.60, (RearDelts, .10), (Glutes, .30)),

        // Плечи
        E("Отжимания в стойке на руках", "Віджимання в стійці на руках", "Handstand push-up", FrontDelts, 1.90, (Traps, .20), (SideDelts, .35), (Triceps, .50)),
        E("Отжимания домиком", "Віджимання будиночком", "Pike push-up", FrontDelts, 1.80, (SideDelts, .30), (Triceps, .45)),
        E("Стойка на руках у стены", "Стійка на руках біля стіни", "Wall handstand hold", FrontDelts, 1.60, (Traps, .30), (Triceps, .30)),
        E("Планш", "Планш", "Planche", FrontDelts, 1.86, (Chest, .35), (Triceps, .20), (Abs, .30)),
        E("Жим гири стоя", "Жим гирі стоячи", "Kettlebell overhead press", FrontDelts, 1.86, (SideDelts, .30), (Triceps, .40)),
        E("Толчок гири", "Поштовх гирі", "Kettlebell jerk", FrontDelts, 1.86, (Traps, .20), (Triceps, .35), (Quads, .35), (Glutes, .20)),
        E("Жим резинки над головой", "Жим гуми над головою", "Band overhead press", FrontDelts, 1.74, (SideDelts, .30), (Triceps, .35)),
        E("Махи с резинкой в стороны", "Махи з гумою в сторони", "Band lateral raise", SideDelts, 1.66, (Traps, .15)),
        E("Разведение резинки перед собой", "Розведення гуми перед собою", "Band pull-apart", RearDelts, 1.66, (Traps, .25), (Rhomboids, .35)),

        // Руки
        E("Алмазные отжимания", "Діамантові віджимання", "Diamond push-up", Triceps, 1.87, (Chest, .35), (FrontDelts, .25)),
        E("Разгибание рук в упоре на перекладину", "Розгинання рук в упорі на перекладину", "Bodyweight triceps extension", Triceps, 1.78, (FrontDelts, .10)),
        E("Разгибание рук с резинкой", "Розгинання рук з гумою", "Band triceps extension", Triceps, 1.70),
        E("Сгибание рук с резинкой", "Згинання рук з гумою", "Band biceps curl", Biceps, 1.72, (Forearms, .20)),
        E("Вис на перекладине", "Вис на турніку", "Dead hang", Forearms, 1.45, (Lats, .30)),
        E("Фермерская прогулка", "Фермерська прогулянка", "Farmer's walk", Forearms, 1.55, (Traps, .50), (Abs, .20)),

        // Пресс
        E("Подъём корпуса", "Підйом корпусу", "Sit-up", Abs, 1.66, (HipFlexors, .40)),
        E("Складка", "Складка", "V-up", Abs, 1.74, (HipFlexors, .40)),
        E("Уголок в упоре", "Кутик в упорі", "L-sit", Abs, 1.74, (Triceps, .15), (HipFlexors, .45)),
        E("Лодочка", "Човник", "Hollow body hold", Abs, 1.62, (HipFlexors, .25)),
        E("Скалолаз", "Скелелаз", "Mountain climber", Abs, 1.62, (FrontDelts, .15), (HipFlexors, .40)),
        E("Мёртвый жук", "Мертвий жук", "Dead bug", Abs, 1.58, (HipFlexors, .20)),
        E("Ножницы", "Ножиці", "Flutter kicks", Abs, 1.60, (HipFlexors, .45)),
        E("Турецкий подъём с гирей", "Турецький підйом з гирею", "Kettlebell Turkish get-up", Abs, 1.78, (FrontDelts, .35), (Obliques, .30), (Glutes, .30)),
        E("Велосипед", "Велосипед", "Bicycle crunch", Obliques, 1.64, (Abs, .50), (HipFlexors, .20)),
        E("Дворники", "Двірники", "Windshield wiper", Obliques, 1.78, (Abs, .40), (HipFlexors, .30)),
        E("Мельница с гирей", "Млин з гирею", "Kettlebell windmill", Obliques, 1.72, (FrontDelts, .20), (Glutes, .25)),

        // Ноги
        E("Приседания без веса", "Присідання без ваги", "Bodyweight squat", Quads, 1.70, (Hamstrings, .15), (Glutes, .40)),
        E("Приседания с выпрыгиванием", "Присідання з вистрибуванням", "Jump squat", Quads, 1.78, (Glutes, .40), (Calves, .25)),
        E("Пистолетик", "Пістолетик", "Pistol squat", Quads, 1.86, (Hamstrings, .15), (Glutes, .45)),
        E("Выпады с прыжком", "Випади зі стрибком", "Jump lunge", Quads, 1.78, (Hamstrings, .20), (Glutes, .45), (Calves, .20)),
        E("Стульчик у стены", "Стільчик біля стіни", "Wall sit", Quads, 1.62, (Glutes, .20)),
        E("Запрыгивания на тумбу", "Застрибування на тумбу", "Box jump", Quads, 1.76, (Hamstrings, .15), (Glutes, .40), (Calves, .30)),
        E("Бёрпи", "Берпі", "Burpee", Quads, 1.70, (Chest, .30), (FrontDelts, .20), (Triceps, .20), (Glutes, .20)),
        E("Сгибание ног на фитболе", "Згинання ніг на фітболі", "Stability ball leg curl", Hamstrings, 1.68, (Glutes, .35)),
        E("Подъём на носки на ступеньке", "Підйом на носки на сходинці", "Step calf raise", Calves, 1.66),

        // Ягодицы
        E("Махи гирей", "Махи гирею", "Kettlebell swing", Glutes, 1.86, (FrontDelts, .10), (Erectors, .30), (Hamstrings, .45)),
        E("Рывок гири", "Ривок гирі", "Kettlebell snatch", Glutes, 1.88, (Traps, .35), (FrontDelts, .30), (Hamstrings, .35)),
        E("Становая тяга с гирей", "Станова тяга з гирею", "Kettlebell deadlift", Glutes, 1.82, (Erectors, .30), (Quads, .25), (Hamstrings, .40)),
        E("Махи ногой назад на четвереньках", "Махи ногою назад рачки", "Donkey kick", Glutes, 1.62, (Hamstrings, .20)),
        E("Боковые шаги с резинкой", "Бічні кроки з гумою", "Banded lateral walk", Glutes, 1.62)
    ];

    private static ExerciseEntry E(
        string name,
        string nameUk,
        string nameEn,
        string primaryMuscle,
        double coefficient,
        params (string Muscle, double Coefficient)[] secondaryMuscles) =>
        new(
            name,
            nameUk,
            nameEn,
            primaryMuscle,
            coefficient,
            secondaryMuscles
                .Select(x => new SecondaryMuscleEntry(x.Muscle, x.Coefficient))
                .ToList());
}
