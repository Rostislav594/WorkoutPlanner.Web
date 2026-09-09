package com.gymplanner.wearos.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction
import kotlinx.coroutines.flow.Flow

@Dao
abstract class WorkoutDao {
    @Query("SELECT * FROM device_session_metadata WHERE metadataId = 1")
    abstract fun observeDeviceSession(): Flow<DeviceSessionMetadata?>

    @Transaction
    @Query("SELECT * FROM local_workouts WHERE isActive = 1 LIMIT 1")
    abstract fun observeActiveWorkout(): Flow<LocalWorkoutSnapshot?>

    @Query("SELECT * FROM pending_sync_operations ORDER BY sequenceNumber")
    abstract fun observeOperations(): Flow<List<PendingSyncOperation>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    abstract suspend fun upsertSession(metadata: DeviceSessionMetadata)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    abstract suspend fun upsertWorkout(workout: LocalWorkout)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    abstract suspend fun upsertExercises(exercises: List<LocalExercise>)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    abstract suspend fun upsertSets(sets: List<LocalWorkoutSet>)

    @Query("UPDATE local_workouts SET isActive = 0")
    abstract suspend fun deactivateAllWorkouts()

    @Query("SELECT * FROM local_workout_sets WHERE setId = :setId")
    abstract suspend fun getSet(setId: Long): LocalWorkoutSet?

    @Query("SELECT * FROM local_workouts WHERE workoutId = :workoutId")
    abstract suspend fun getWorkout(workoutId: Long): LocalWorkout?

    @Query("SELECT COALESCE(MAX(sequenceNumber), 0) + 1 FROM pending_sync_operations")
    abstract suspend fun nextSequenceNumber(): Long

    @Insert(onConflict = OnConflictStrategy.ABORT)
    abstract suspend fun insertOperation(operation: PendingSyncOperation)

    @Query("UPDATE local_workout_sets SET isCompleted = 1 WHERE setId = :setId AND isCompleted = 0")
    abstract suspend fun markSetCompleted(setId: Long): Int

    @Query(
        """
        UPDATE local_workouts
        SET restCompletedSetId = :setId,
            restEndsAtUtcMillis = :endsAtUtcMillis,
            restDurationSeconds = :durationSeconds
        WHERE workoutId = :workoutId
        """,
    )
    abstract suspend fun startRest(
        workoutId: Long,
        setId: Long,
        endsAtUtcMillis: Long,
        durationSeconds: Int,
    )

    @Query(
        """
        UPDATE local_workouts
        SET restCompletedSetId = NULL,
            restEndsAtUtcMillis = NULL,
            restDurationSeconds = NULL
        WHERE workoutId = :workoutId
        """,
    )
    abstract suspend fun clearRest(workoutId: Long)

    @Query("UPDATE local_workouts SET isActive = 0 WHERE workoutId = :workoutId")
    abstract suspend fun deactivateWorkout(workoutId: Long)

    @Transaction
    open suspend fun completeSetAtomically(
        workoutId: Long,
        setId: Long,
        endsAtUtcMillis: Long,
        durationSeconds: Int,
        operationId: String,
        payloadJson: String,
        createdAtUtcMillis: Long,
    ): Boolean {
        val set = getSet(setId) ?: return false
        if (set.isCompleted || markSetCompleted(setId) != 1) return false

        insertOperation(
            PendingSyncOperation(
                operationId = operationId,
                operationType = SyncOperationType.CompleteSet,
                entityId = setId,
                payloadJson = payloadJson,
                createdAtUtcMillis = createdAtUtcMillis,
                sequenceNumber = nextSequenceNumber(),
            ),
        )
        startRest(workoutId, setId, endsAtUtcMillis, durationSeconds)
        return true
    }

    @Query(
        """
        SELECT * FROM pending_sync_operations
        WHERE status = 'Pending' OR (status = 'Failed' AND canRetry = 1)
        ORDER BY sequenceNumber
        LIMIT 1
        """,
    )
    abstract suspend fun getNextSendableOperation(): PendingSyncOperation?

    @Query(
        """
        UPDATE pending_sync_operations
        SET status = 'Syncing',
            attemptCount = attemptCount + 1,
            lastAttemptAtUtcMillis = :attemptedAtUtcMillis,
            lastError = NULL
        WHERE operationId = :operationId
          AND (status = 'Pending' OR (status = 'Failed' AND canRetry = 1))
        """,
    )
    abstract suspend fun markSyncing(operationId: String, attemptedAtUtcMillis: Long): Int

    @Transaction
    open suspend fun claimNextOperation(attemptedAtUtcMillis: Long): PendingSyncOperation? {
        val candidate = getNextSendableOperation() ?: return null
        return if (markSyncing(candidate.operationId, attemptedAtUtcMillis) == 1) {
            candidate.copy(
                status = SyncOperationStatus.Syncing,
                attemptCount = candidate.attemptCount + 1,
                lastAttemptAtUtcMillis = attemptedAtUtcMillis,
                lastError = null,
            )
        } else {
            null
        }
    }

    @Query(
        """
        UPDATE pending_sync_operations
        SET status = :status, lastError = :lastError, canRetry = :canRetry
        WHERE operationId = :operationId
        """,
    )
    abstract suspend fun updateOperationStatus(
        operationId: String,
        status: SyncOperationStatus,
        lastError: String?,
        canRetry: Boolean,
    )

    @Query("UPDATE pending_sync_operations SET status = 'Pending' WHERE status = 'Syncing'")
    abstract suspend fun resetInterruptedOperations()

    @Query("UPDATE local_workout_sets SET serverVersion = :serverVersion WHERE setId = :setId")
    abstract suspend fun updateServerVersion(setId: Long, serverVersion: Long)

    @Query(
        """
        UPDATE local_workout_sets
        SET isCompleted = :isCompleted,
            weightKilograms = :weightKilograms,
            repetitions = :repetitions,
            serverVersion = :serverVersion
        WHERE setId = :setId
        """,
    )
    abstract suspend fun applyAuthoritativeSet(
        setId: Long,
        isCompleted: Boolean,
        weightKilograms: Double,
        repetitions: Int,
        serverVersion: Long,
    )

    @Transaction
    open suspend fun markSynced(
        operation: PendingSyncOperation,
        isCompleted: Boolean,
        weightKilograms: Double,
        repetitions: Int,
        serverVersion: Long,
    ) {
        applyAuthoritativeSet(
            operation.entityId,
            isCompleted,
            weightKilograms,
            repetitions,
            serverVersion,
        )
        updateOperationStatus(operation.operationId, SyncOperationStatus.Synced, null, false)
    }

    @Transaction
    open suspend fun cacheActiveWorkout(
        workout: LocalWorkout,
        exercises: List<LocalExercise>,
        sets: List<LocalWorkoutSet>,
    ) {
        deactivateAllWorkouts()
        upsertWorkout(workout)
        upsertExercises(exercises)
        upsertSets(sets)
    }
}
