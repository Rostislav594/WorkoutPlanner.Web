package com.gymplanner.wearos.data.local

import androidx.room.Entity
import androidx.room.ForeignKey
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(tableName = "local_workouts")
data class LocalWorkout(
    @PrimaryKey val workoutId: Long,
    val isActive: Boolean,
    val lastCheckedAtUtcMillis: Long,
    val restCompletedSetId: Long? = null,
    val restEndsAtUtcMillis: Long? = null,
    val restDurationSeconds: Int? = null,
)

@Entity(
    tableName = "local_exercises",
    foreignKeys = [
        ForeignKey(
            entity = LocalWorkout::class,
            parentColumns = ["workoutId"],
            childColumns = ["workoutId"],
            onDelete = ForeignKey.CASCADE,
        ),
    ],
    indices = [Index("workoutId")],
)
data class LocalExercise(
    @PrimaryKey val exerciseId: Long,
    val workoutId: Long,
    val name: String,
    val orderIndex: Int,
)

@Entity(
    tableName = "local_workout_sets",
    foreignKeys = [
        ForeignKey(
            entity = LocalExercise::class,
            parentColumns = ["exerciseId"],
            childColumns = ["exerciseId"],
            onDelete = ForeignKey.CASCADE,
        ),
    ],
    indices = [Index("exerciseId"), Index(value = ["exerciseId", "orderIndex"], unique = true)],
)
data class LocalWorkoutSet(
    @PrimaryKey val setId: Long,
    val exerciseId: Long,
    val orderIndex: Int,
    val weightKilograms: Double?,
    val repetitions: Int?,
    val isCompleted: Boolean,
    val serverVersion: Long,
)

@Entity(
    tableName = "pending_sync_operations",
    indices = [
        Index(value = ["entityId", "sequenceNumber"], unique = true),
        Index(value = ["status", "sequenceNumber"]),
    ],
)
data class PendingSyncOperation(
    @PrimaryKey val operationId: String,
    val operationType: SyncOperationType,
    val entityId: Long,
    val payloadJson: String,
    val createdAtUtcMillis: Long,
    val sequenceNumber: Long,
    val attemptCount: Int = 0,
    val lastAttemptAtUtcMillis: Long? = null,
    val lastError: String? = null,
    val status: SyncOperationStatus = SyncOperationStatus.Pending,
    val canRetry: Boolean = true,
)

@Entity(tableName = "device_session_metadata")
data class DeviceSessionMetadata(
    @PrimaryKey val metadataId: Int = singletonId,
    val isPaired: Boolean,
    val lastCheckedAtUtcMillis: Long,
) {
    companion object {
        const val singletonId = 1
    }
}

enum class SyncOperationType {
    CompleteSet,
}

enum class SyncOperationStatus {
    Pending,
    Syncing,
    Synced,
    Failed,
    Conflict,
}
