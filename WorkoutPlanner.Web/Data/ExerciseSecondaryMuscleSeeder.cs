using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Data;

public static class ExerciseSecondaryMuscleSeeder
{
    public static void Seed(WorkoutDbContext db)
    {
        if (db.ExerciseSecondaryMuscles.Any())
            return;

        var exercises = db.ExerciseDefinitions
            .ToDictionary(x => x.Name);

        var muscles = db.Muscles
            .ToDictionary(x => x.Name);

        db.ExerciseSecondaryMuscles.AddRange(

            // Грудь
            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим лёжа"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.35
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим лёжа"].Id,
                MuscleId = muscles["Трицепс"].Id,
                Coefficient = 0.45
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим гантелей лёжа"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.35
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим гантелей лёжа"].Id,
                MuscleId = muscles["Трицепс"].Id,
                Coefficient = 0.45
            },
            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим в тренажёре"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим в тренажёре"].Id,
                MuscleId = muscles["Трицепс"].Id,
                Coefficient = 0.40
            },
            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим в Смите"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим в Смите"].Id,
                MuscleId = muscles["Трицепс"].Id,
                Coefficient = 0.40
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Разведения гантелей лёжа"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.15
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Сведения рук в кроссовере"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.15
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Пек-дек"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.15
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Отжимания от пола"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Отжимания от пола"].Id,
                MuscleId = muscles["Трицепс"].Id,
                Coefficient = 0.40
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Отжимания на брусьях (грудь)"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.25
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Отжимания на брусьях (грудь)"].Id,
                MuscleId = muscles["Трицепс"].Id,
                Coefficient = 0.50
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим гантелей на наклонной скамье"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.45
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим гантелей на наклонной скамье"].Id,
                MuscleId = muscles["Трицепс"].Id,
                Coefficient = 0.40
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим штанги на наклонной скамье"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.45
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим штанги на наклонной скамье"].Id,
                MuscleId = muscles["Трицепс"].Id,
                Coefficient = 0.40
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим в Hammer"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим в Hammer"].Id,
                MuscleId = muscles["Трицепс"].Id,
                Coefficient = 0.40
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Сведения рук в тренажёре"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.15
            },

            //Спина (широчайшие мышцы)
            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Подтягивания"].Id,
                MuscleId = muscles["Бицепс"].Id,
                Coefficient = 0.50
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Подтягивания"].Id,
                MuscleId = muscles["Задняя дельтовидная"].Id,
                Coefficient = 0.20
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Подтягивания"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.15
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Подтягивания обратным хватом"].Id,
                MuscleId = muscles["Бицепс"].Id,
                Coefficient = 0.65
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Подтягивания обратным хватом"].Id,
                MuscleId = muscles["Задняя дельтовидная"].Id,
                Coefficient = 0.15
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Подтягивания обратным хватом"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.10
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга верхнего блока"].Id,
                MuscleId = muscles["Бицепс"].Id,
                Coefficient = 0.45
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга верхнего блока"].Id,
                MuscleId = muscles["Задняя дельтовидная"].Id,
                Coefficient = 0.20
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга верхнего блока"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.15
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга верхнего блока обратным хватом"].Id,
                MuscleId = muscles["Бицепс"].Id,
                Coefficient = 0.60
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга верхнего блока обратным хватом"].Id,
                MuscleId = muscles["Задняя дельтовидная"].Id,
                Coefficient = 0.15
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга верхнего блока обратным хватом"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.10
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга горизонтального блока"].Id,
                MuscleId = muscles["Бицепс"].Id,
                Coefficient = 0.40
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга горизонтального блока"].Id,
                MuscleId = muscles["Задняя дельтовидная"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга горизонтального блока"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.25
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга Т-грифа"].Id,
                MuscleId = muscles["Бицепс"].Id,
                Coefficient = 0.35
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга Т-грифа"].Id,
                MuscleId = muscles["Задняя дельтовидная"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга Т-грифа"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга штанги в наклоне"].Id,
                MuscleId = muscles["Бицепс"].Id,
                Coefficient = 0.35
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга штанги в наклоне"].Id,
                MuscleId = muscles["Задняя дельтовидная"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга штанги в наклоне"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга штанги в наклоне"].Id,
                MuscleId = muscles["Разгибатели спины"].Id,
                Coefficient = 0.25
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга гантели одной рукой"].Id,
                MuscleId = muscles["Бицепс"].Id,
                Coefficient = 0.35
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга гантели одной рукой"].Id,
                MuscleId = muscles["Задняя дельтовидная"].Id,
                Coefficient = 0.25
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга гантели одной рукой"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.25
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга в Hammer"].Id,
                MuscleId = muscles["Бицепс"].Id,
                Coefficient = 0.35
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга в Hammer"].Id,
                MuscleId = muscles["Задняя дельтовидная"].Id,
                Coefficient = 0.25
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Тяга в Hammer"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.25
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Пуловер в кроссовере"].Id,
                MuscleId = muscles["Трицепс"].Id,
                Coefficient = 0.10
            },

            // Плечи
            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим штанги стоя"].Id,
                MuscleId = muscles["Трицепс"].Id,
                Coefficient = 0.45
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим штанги стоя"].Id,
                MuscleId = muscles["Средняя дельтовидная"].Id,
                Coefficient = 0.35
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим штанги стоя"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.15
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим гантелей сидя"].Id,
                MuscleId = muscles["Трицепс"].Id,
                Coefficient = 0.40
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим гантелей сидя"].Id,
                MuscleId = muscles["Средняя дельтовидная"].Id,
                Coefficient = 0.35
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим гантелей сидя"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.15
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Махи гантелями в стороны"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.20
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Махи в кроссовере"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.20
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Разведения в тренажёре"].Id,
                MuscleId = muscles["Трапециевидные мышцы"].Id,
                Coefficient = 0.20
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Разведения в тренажёре"].Id,
                MuscleId = muscles["Ромбовидные мышцы"].Id,
                Coefficient = 0.15
            },

            //Бицепс
            
            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Подъём штанги на бицепс стоя"].Id,
                MuscleId = muscles["Предплечья"].Id,
                Coefficient = 0.30
            },


            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Подъём EZ-штанги на бицепс"].Id,
                MuscleId = muscles["Предплечья"].Id,
                Coefficient = 0.25
            },


            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Подъём гантелей на бицепс"].Id,
                MuscleId = muscles["Предплечья"].Id,
                Coefficient = 0.30
            },


            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Молотки"].Id,
                MuscleId = muscles["Предплечья"].Id,
                Coefficient = 0.50
            },


            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Сгибание рук на скамье Скотта"].Id,
                MuscleId = muscles["Предплечья"].Id,
                Coefficient = 0.20
            },


            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Сгибание рук в кроссовере"].Id,
                MuscleId = muscles["Предплечья"].Id,
                Coefficient = 0.20
            },

            //Трицепс

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Французский жим лёжа"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.10
            },


            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Французский жим сидя"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.10
            },


            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Разгибание рук на верхнем блоке"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.10
            },


            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Разгибание руки с гантелью из-за головы"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.10
            },


            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Отжимания узким хватом"].Id,
                MuscleId = muscles["Грудные мышцы"].Id,
                Coefficient = 0.35
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Отжимания узким хватом"].Id,
                MuscleId = muscles["Передняя дельтовидная"].Id,
                Coefficient = 0.25
            },

            // Пресс
            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Подъём ног в висе"].Id,
                MuscleId = muscles["Сгибатели бедра"].Id,
                Coefficient = 0.40
            },

            //Квадрицепсы

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Приседания со штангой"].Id,
                MuscleId = muscles["Ягодичные мышцы"].Id,
                Coefficient = 0.45
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Приседания со штангой"].Id,
                MuscleId = muscles["Бицепс бедра"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Приседания со штангой"].Id,
                MuscleId = muscles["Разгибатели спины"].Id,
                Coefficient = 0.20
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Фронтальные приседания"].Id,
                MuscleId = muscles["Ягодичные мышцы"].Id,
                Coefficient = 0.25
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Фронтальные приседания"].Id,
                MuscleId = muscles["Разгибатели спины"].Id,
                Coefficient = 0.25
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим ногами"].Id,
                MuscleId = muscles["Ягодичные мышцы"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Жим ногами"].Id,
                MuscleId = muscles["Бицепс бедра"].Id,
                Coefficient = 0.20
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Болгарские выпады"].Id,
                MuscleId = muscles["Ягодичные мышцы"].Id,
                Coefficient = 0.45
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Болгарские выпады"].Id,
                MuscleId = muscles["Бицепс бедра"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Выпады со штангой"].Id,
                MuscleId = muscles["Ягодичные мышцы"].Id,
                Coefficient = 0.45
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Выпады со штангой"].Id,
                MuscleId = muscles["Бицепс бедра"].Id,
                Coefficient = 0.30
            },

            //Бицепс бедра
            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Румынская тяга"].Id,
                MuscleId = muscles["Ягодичные мышцы"].Id,
                Coefficient = 0.45
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Румынская тяга"].Id,
                MuscleId = muscles["Разгибатели спины"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Становая тяга на прямых ногах"].Id,
                MuscleId = muscles["Ягодичные мышцы"].Id,
                Coefficient = 0.40
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Становая тяга на прямых ногах"].Id,
                MuscleId = muscles["Разгибатели спины"].Id,
                Coefficient = 0.35
            },

            //Ягодичные мышцы
            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Ягодичный мост"].Id,
                MuscleId = muscles["Бицепс бедра"].Id,
                Coefficient = 0.35
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Хип Траст"].Id,
                MuscleId = muscles["Бицепс бедра"].Id,
                Coefficient = 0.35
            },

            //Разгибатели спины
            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Гиперэкстензия"].Id,
                MuscleId = muscles["Ягодичные мышцы"].Id,
                Coefficient = 0.35
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Гиперэкстензия"].Id,
                MuscleId = muscles["Бицепс бедра"].Id,
                Coefficient = 0.30
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Обратная гиперэкстензия"].Id,
                MuscleId = muscles["Ягодичные мышцы"].Id,
                Coefficient = 0.50
            },

            new ExerciseSecondaryMuscle
            {
                ExerciseDefinitionId = exercises["Обратная гиперэкстензия"].Id,
                MuscleId = muscles["Бицепс бедра"].Id,
                Coefficient = 0.35
            }

        );

        db.SaveChanges();
    }
}
