package com.gymplanner.wearos.data.local

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.TypeConverter
import androidx.room.TypeConverters

@Database(
    entities = [
        LocalWorkout::class,
        LocalExercise::class,
        LocalWorkoutSet::class,
        PendingSyncOperation::class,
        DeviceSessionMetadata::class,
    ],
    version = 1,
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
                ).build().also { instance = it }
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
