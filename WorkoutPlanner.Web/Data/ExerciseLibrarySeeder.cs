using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Data;

public static class ExerciseLibrarySeeder
{
    public static void Seed(WorkoutDbContext db)
    {
        db.SaveChanges();

        if (db.ExerciseDefinitions.Any())
            return;

        var chest = db.Muscles.Single(x => x.Name == "Грудные мышцы");
       
        db.ExerciseDefinitions.AddRange(

    new ExerciseDefinition
    {
        Name = "Жим лёжа",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.92,

    },

    new ExerciseDefinition
    {
        Name = "Жим гантелей лёжа",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.90,

    },

    new ExerciseDefinition
    {
        Name = "Жим в тренажёре",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.80,

    },

    new ExerciseDefinition
    {
        Name = "Жим в Смите",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.76,

    },

    new ExerciseDefinition
    {
        Name = "Разведения гантелей лёжа",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.63,

    },

    new ExerciseDefinition
    {
        Name = "Сведения рук в кроссовере",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.69,

    },

    new ExerciseDefinition
    {
        Name = "Пек-дек",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.70,

    },

    new ExerciseDefinition
    {
        Name = "Отжимания от пола",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.73,
       
    },

    new ExerciseDefinition
    {
        Name = "Отжимания на брусьях (грудь)",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.88,
        
    },

    new ExerciseDefinition
    {
        Name = "Жим гантелей на наклонной скамье",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.91,
    },

    new ExerciseDefinition
    {
        Name = "Жим штанги на наклонной скамье",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.90,
        
    },

    new ExerciseDefinition
    {
        Name = "Жим в Hammer",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.84,
        
    },

    new ExerciseDefinition
    {
        Name = "Сведения рук в тренажёре",
        PrimaryMuscleId = chest.Id,
        ExerciseCoefficient = 1.69,
        
    }

);
        var back = db.Muscles.Single(x =>
        x.Name == "Широчайшие мышцы спины");

        db.ExerciseDefinitions.AddRange(

            new ExerciseDefinition
            {
                Name = "Подтягивания",
                PrimaryMuscleId = back.Id,
                ExerciseCoefficient = 1.95,
               
            },

            new ExerciseDefinition
            {
                Name = "Подтягивания обратным хватом",
                PrimaryMuscleId = back.Id,
                ExerciseCoefficient = 1.93,
               
            },

            new ExerciseDefinition
            {
                Name = "Тяга верхнего блока",
                PrimaryMuscleId = back.Id,
                ExerciseCoefficient = 1.82,
                
            },

            new ExerciseDefinition
            {
                Name = "Тяга верхнего блока обратным хватом",
                PrimaryMuscleId = back.Id,
                ExerciseCoefficient = 1.81,
                
            },

            new ExerciseDefinition
            {
                Name = "Тяга горизонтального блока",
                PrimaryMuscleId = back.Id,
                ExerciseCoefficient = 1.84,
                
            },

            new ExerciseDefinition
            {
                Name = "Тяга Т-грифа",
                PrimaryMuscleId = back.Id,
                ExerciseCoefficient = 1.90,
                
            },

            new ExerciseDefinition
            {
                Name = "Тяга штанги в наклоне",
                PrimaryMuscleId = back.Id,
                ExerciseCoefficient = 1.94,
                
            },

            new ExerciseDefinition
            {
                Name = "Тяга гантели одной рукой",
                PrimaryMuscleId = back.Id,
                ExerciseCoefficient = 1.88,
                
            },

            new ExerciseDefinition
            {
                Name = "Тяга в Hammer",
                PrimaryMuscleId = back.Id,
                ExerciseCoefficient = 1.84,
               
            },

            new ExerciseDefinition
            {
                Name = "Пуловер в кроссовере",
                PrimaryMuscleId = back.Id,
                ExerciseCoefficient = 1.67,
                
            }

        );

        var muscles = db.Muscles.ToDictionary(x => x.Name);

        db.ExerciseDefinitions.AddRange(

            new ExerciseDefinition
            {
                Name = "Жим штанги стоя",
                PrimaryMuscleId = muscles["Передняя дельтовидная"].Id,
                ExerciseCoefficient = 1.91,
                
            },

            new ExerciseDefinition
            {
                Name = "Жим гантелей сидя",
                PrimaryMuscleId = muscles["Передняя дельтовидная"].Id,
                ExerciseCoefficient = 1.89,
                
            },

            new ExerciseDefinition
            {
                Name = "Махи гантелями в стороны",
                PrimaryMuscleId = muscles["Средняя дельтовидная"].Id,
                ExerciseCoefficient = 1.70,
                
            },

            new ExerciseDefinition
            {
                Name = "Махи в кроссовере",
                PrimaryMuscleId = muscles["Средняя дельтовидная"].Id,
                ExerciseCoefficient = 1.75,
                
            },

            new ExerciseDefinition
            {
                Name = "Разведения в тренажёре",
                PrimaryMuscleId = muscles["Задняя дельтовидная"].Id,
                ExerciseCoefficient = 1.69,
                
            },

            new ExerciseDefinition
            {
                Name = "Обратный пек-дек",
                PrimaryMuscleId = muscles["Задняя дельтовидная"].Id,
                ExerciseCoefficient = 1.74,
               
            }

        );

        db.ExerciseDefinitions.AddRange(

    new ExerciseDefinition
    {
        Name = "Подъём штанги на бицепс стоя",
        PrimaryMuscleId = muscles["Бицепс"].Id,
        ExerciseCoefficient = 1.86,
        
    },

    new ExerciseDefinition
    {
        Name = "Подъём EZ-штанги на бицепс",
        PrimaryMuscleId = muscles["Бицепс"].Id,
        ExerciseCoefficient = 1.88,
        
    },

    new ExerciseDefinition
    {
        Name = "Подъём гантелей на бицепс",
        PrimaryMuscleId = muscles["Бицепс"].Id,
        ExerciseCoefficient = 1.84,
        
    },

    new ExerciseDefinition
    {
        Name = "Молотки",
        PrimaryMuscleId = muscles["Бицепс"].Id,
        ExerciseCoefficient = 1.81,
        
    },

    new ExerciseDefinition
    {
        Name = "Сгибание рук на скамье Скотта",
        PrimaryMuscleId = muscles["Бицепс"].Id,
        ExerciseCoefficient = 1.87,
        
    },

    new ExerciseDefinition
    {
        Name = "Сгибание рук в кроссовере",
        PrimaryMuscleId = muscles["Бицепс"].Id,
        ExerciseCoefficient = 1.85,
        
    }

);
        db.ExerciseDefinitions.AddRange(

    new ExerciseDefinition
    {
        Name = "Французский жим лёжа",
        PrimaryMuscleId = muscles["Трицепс"].Id,
        ExerciseCoefficient = 1.86,
        
    },

    new ExerciseDefinition
    {
        Name = "Французский жим сидя",
        PrimaryMuscleId = muscles["Трицепс"].Id,
        ExerciseCoefficient = 1.84,
        
    },

    new ExerciseDefinition
    {
        Name = "Разгибание рук на верхнем блоке",
        PrimaryMuscleId = muscles["Трицепс"].Id,
        ExerciseCoefficient = 1.81,
        
    },

    new ExerciseDefinition
    {
        Name = "Разгибание руки с гантелью из-за головы",
        PrimaryMuscleId = muscles["Трицепс"].Id,
        ExerciseCoefficient = 1.83,
        
    },

    new ExerciseDefinition
    {
        Name = "Отжимания узким хватом",
        PrimaryMuscleId = muscles["Трицепс"].Id,
        ExerciseCoefficient = 1.89,
        
    },

    new ExerciseDefinition
    {
        Name = "Разгибание рук в кроссовере обратным хватом",
        PrimaryMuscleId = muscles["Трицепс"].Id,
        ExerciseCoefficient = 1.79,
        
    }

);
        db.ExerciseDefinitions.AddRange(

    new ExerciseDefinition
    {
        Name = "Сгибание кистей со штангой",
        PrimaryMuscleId = muscles["Предплечья"].Id,
        ExerciseCoefficient = 1.48,
        
    },

    new ExerciseDefinition
    {
        Name = "Разгибание кистей со штангой",
        PrimaryMuscleId = muscles["Предплечья"].Id,
        ExerciseCoefficient = 1.42,
        
    },

    new ExerciseDefinition
    {
        Name = "Сгибание кистей с гантелями",
        PrimaryMuscleId = muscles["Предплечья"].Id,
        ExerciseCoefficient = 1.46,
        
    },

    new ExerciseDefinition
    {
        Name = "Разгибание кистей с гантелями",
        PrimaryMuscleId = muscles["Предплечья"].Id,
        ExerciseCoefficient = 1.41,
        
    },

    new ExerciseDefinition
    {
        Name = "Сгибание кистей в кроссовере",
        PrimaryMuscleId = muscles["Предплечья"].Id,
        ExerciseCoefficient = 1.44,
        
    }

);
        db.ExerciseDefinitions.AddRange(

    new ExerciseDefinition
    {
        Name = "Скручивания",
        PrimaryMuscleId = muscles["Прямая мышца живота"].Id,
        ExerciseCoefficient = 1.66,
        
    },

    new ExerciseDefinition
    {
        Name = "Подъём ног в висе",
        PrimaryMuscleId = muscles["Прямая мышца живота"].Id,
        ExerciseCoefficient = 1.86,
        
    },

    new ExerciseDefinition
    {
        Name = "Подъём коленей в упоре",
        PrimaryMuscleId = muscles["Прямая мышца живота"].Id,
        ExerciseCoefficient = 1.80,
    },

    new ExerciseDefinition
    {
        Name = "Скручивания в тренажёре",
        PrimaryMuscleId = muscles["Прямая мышца живота"].Id,
        ExerciseCoefficient = 1.70,

    },

    new ExerciseDefinition
    {
        Name = "Планка",
        PrimaryMuscleId = muscles["Прямая мышца живота"].Id,
        ExerciseCoefficient = 1.58,
        
    },

    new ExerciseDefinition
    {
        Name = "Боковые скручивания",
        PrimaryMuscleId = muscles["Косые мышцы живота"].Id,
        ExerciseCoefficient = 1.64,
        
    }

);
        db.ExerciseDefinitions.AddRange(

    new ExerciseDefinition
    {
        Name = "Приседания со штангой",
        PrimaryMuscleId = muscles["Квадрицепс"].Id,
        ExerciseCoefficient = 2.00,
        
    },

    new ExerciseDefinition
    {
        Name = "Фронтальные приседания",
        PrimaryMuscleId = muscles["Квадрицепс"].Id,
        ExerciseCoefficient = 1.97,
        
    },

    new ExerciseDefinition
    {
        Name = "Жим ногами",
        PrimaryMuscleId = muscles["Квадрицепс"].Id,
        ExerciseCoefficient = 1.90,
        
    },

    new ExerciseDefinition
    {
        Name = "Болгарские выпады",
        PrimaryMuscleId = muscles["Квадрицепс"].Id,
        ExerciseCoefficient = 1.95,
        
    },

    new ExerciseDefinition
    {
        Name = "Выпады со штангой",
        PrimaryMuscleId = muscles["Квадрицепс"].Id,
        ExerciseCoefficient = 1.93,
        
    },

    new ExerciseDefinition
    {
        Name = "Выпады с гантелями",
        PrimaryMuscleId = muscles["Квадрицепс"].Id,
        ExerciseCoefficient = 1.90,
       
    },

    new ExerciseDefinition
    {
        Name = "Разгибание ног в тренажёре",
        PrimaryMuscleId = muscles["Квадрицепс"].Id,
        ExerciseCoefficient = 1.73,
        
    }

);
        db.ExerciseDefinitions.AddRange(

    new ExerciseDefinition
    {
        Name = "Румынская тяга",
        PrimaryMuscleId = muscles["Бицепс бедра"].Id,
        ExerciseCoefficient = 1.96,
       
    },

    new ExerciseDefinition
    {
        Name = "Становая тяга на прямых ногах",
        PrimaryMuscleId = muscles["Бицепс бедра"].Id,
        ExerciseCoefficient = 1.95,
        
    },

    new ExerciseDefinition
    {
        Name = "Сгибание ног лёжа",
        PrimaryMuscleId = muscles["Бицепс бедра"].Id,
        ExerciseCoefficient = 1.74,
        
    },

    new ExerciseDefinition
    {
        Name = "Сгибание ног сидя",
        PrimaryMuscleId = muscles["Бицепс бедра"].Id,
        ExerciseCoefficient = 1.73,
        
    }

);
        db.ExerciseDefinitions.AddRange(

    new ExerciseDefinition
    {
        Name = "Ягодичный мост",
        PrimaryMuscleId = muscles["Ягодичные мышцы"].Id,
        ExerciseCoefficient = 1.87,
        
    },

    new ExerciseDefinition
    {
        Name = "Хип Траст",
        PrimaryMuscleId = muscles["Ягодичные мышцы"].Id,
        ExerciseCoefficient = 1.92,
        
    },

    new ExerciseDefinition
    {
        Name = "Отведение ноги в кроссовере",
        PrimaryMuscleId = muscles["Ягодичные мышцы"].Id,
        ExerciseCoefficient = 1.65,
        
    },

    new ExerciseDefinition
    {
        Name = "Отведение ноги в тренажёре",
        PrimaryMuscleId = muscles["Ягодичные мышцы"].Id,
        ExerciseCoefficient = 1.63,
        
    }

);
        db.ExerciseDefinitions.AddRange(

    new ExerciseDefinition
    {
        Name = "Подъём на носки стоя",
        PrimaryMuscleId = muscles["Икроножные мышцы"].Id,
        ExerciseCoefficient = 1.76,
        
    },

    new ExerciseDefinition
    {
        Name = "Подъём на носки сидя",
        PrimaryMuscleId = muscles["Икроножные мышцы"].Id,
        ExerciseCoefficient = 1.73,
        
    },

    new ExerciseDefinition
    {
        Name = "Подъём на носки в тренажёре",
        PrimaryMuscleId = muscles["Икроножные мышцы"].Id,
        ExerciseCoefficient = 1.75,
       
    }

);
        db.ExerciseDefinitions.AddRange(

    new ExerciseDefinition
    {
        Name = "Гиперэкстензия",
        PrimaryMuscleId = muscles["Разгибатели спины"].Id,
        ExerciseCoefficient = 1.79,
        
    },

    new ExerciseDefinition
    {
        Name = "Обратная гиперэкстензия",
        PrimaryMuscleId = muscles["Разгибатели спины"].Id,
        ExerciseCoefficient = 1.77,
        
    }

);
        db.ExerciseDefinitions.AddRange(

    new ExerciseDefinition
    {
        Name = "Шраги со штангой",
        PrimaryMuscleId = muscles["Трапециевидные мышцы"].Id,
        ExerciseCoefficient = 1.83,
        
    },

    new ExerciseDefinition
    {
        Name = "Шраги с гантелями",
        PrimaryMuscleId = muscles["Трапециевидные мышцы"].Id,
        ExerciseCoefficient = 1.81,
        
    },

    new ExerciseDefinition
    {
        Name = "Шраги в тренажёре",
        PrimaryMuscleId = muscles["Трапециевидные мышцы"].Id,
        ExerciseCoefficient = 1.79,
        
    }

);

        db.SaveChanges();
    }

}
