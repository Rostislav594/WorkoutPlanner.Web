package com.gymplanner.wearos.data.local

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.TypeConverter
import androidx.room.TypeConverters
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

@Database(
    entities = [
        LocalWorkout::class,
        LocalExercise::class,
        LocalWorkoutSet::class,
        PendingSyncOperation::class,
        DeviceSessionMetadata::class,
    ],
    version = 4,
    exportSchema = false,
)
@TypeConverters(WorkoutConverters::class)
abstract class WorkoutDatabase : RoomDatabase() {
    abstract fun workoutDao(): WorkoutDao

    companion object {
        @Volatile
        private var instance: WorkoutDatabase? = null

        fun getInstance(context: Context): WorkoutDatabase =
            instance ?: synchronized(this) {
                instance ?: Room.databaseBuilder(
                    context.applicationContext,
                    WorkoutDatabase::class.java,
                    "workout-planner-wear.db",
                ).addMigrations(migration1To2, migration2To3, migration3To4).build().also { instance = it }
            }

        /**
         * Суперсеты и настройки отдыха.
         *
         * Группа суперсета нужна, чтобы часы чередовали подходы двух упражнений,
         * а длительности отдыха приходят из профиля пользователя: раньше они
         * были зашиты в коде и не совпадали с тем, что человек видит в
         * приложении. NULL означает «сервер ещё не прислал» — тогда действуют
         * значения по умолчанию.
         */
        /**
         * Вид тренировки. Нужен только для финального экрана: после свободной
         * тренировки человека отправляют в приложение, потому что там ждёт
         * вопрос о сохранении шаблона.
         */
        internal val migration3To4 = object : Migration(3, 4) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL(
                    "ALTER TABLE local_workouts " +
                        "ADD COLUMN isFree INTEGER NOT NULL DEFAULT 0",
                )
            }
        }

        internal val migration2To3 = object : Migration(2, 3) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE local_exercises ADD COLUMN supersetGroupId INTEGER")
                db.execSQL("ALTER TABLE local_workouts ADD COLUMN restBetweenSetsSeconds INTEGER")
                db.execSQL("ALTER TABLE local_workouts ADD COLUMN restBetweenExercisesSeconds INTEGER")
            }
        }

        internal val migration1To2 = object : Migration(1, 2) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL(
                    "ALTER TABLE local_workouts " +
                        "ADD COLUMN workoutName TEXT NOT NULL DEFAULT ''",
                )
            }
        }
    }
}

class WorkoutConverters {
    @TypeConverter
    fun operationTypeToString(value: SyncOperationType): String = value.name

    @TypeConverter
    fun stringToOperationType(value: String): SyncOperationType = SyncOperationType.valueOf(value)

    @TypeConverter
    fun operationStatusToString(value: SyncOperationStatus): String = value.name

    @TypeConverter
    fun stringToOperationStatus(value: String): SyncOperationStatus = SyncOperationStatus.valueOf(value)
}
