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

    @Query(
        """
        SELECT * FROM pending_sync_operations
        WHERE entityId = :setId
          AND (status = 'Pending' OR status = 'Syncing' OR (status = 'Failed' AND canRetry = 1))
        ORDER BY sequenceNumber
        """,
    )
    abstract suspend fun getUnresolvedOperationsForSet(setId: Long): List<PendingSyncOperation>

    @Query("SELECT COUNT(*) FROM pending_sync_operations WHERE status != 'Synced'")
    abstract suspend fun countOutstandingOperations(): Int

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

    @Query("UPDATE local_workout_sets SET isCompleted = 0 WHERE setId = :setId AND isCompleted = 1")
    abstract suspend fun markSetIncomplete(setId: Long): Int

    @Query(
        """
        UPDATE local_workout_sets
        SET weightKilograms = :weightKilograms, repetitions = :repetitions
        WHERE setId = :setId
        """,
    )
    abstract suspend fun updateSetValues(
        setId: Long,
        weightKilograms: Double,
        repetitions: Int,
    ): Int

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

    @Query(
        """
        UPDATE local_workouts
        SET restCompletedSetId = NULL,
            restEndsAtUtcMillis = NULL,
            restDurationSeconds = NULL
        WHERE restCompletedSetId = :setId
        """,
    )
    abstract suspend fun clearRestForSet(setId: Long)

    @Query("UPDATE local_workouts SET isActive = 0 WHERE workoutId = :workoutId")
    abstract suspend fun deactivateWorkout(workoutId: Long)

    /**
     * Закрывает подход и при необходимости запускает отдых.
     *
     * [durationSeconds] равен null, когда отдыхать не нужно: внутри суперсета
     * подход одного упражнения сразу сменяется подходом другого, а после
     * последнего подхода тренировки отдыхать уже не от чего.
     */
    @Transaction
    open suspend fun completeSetAtomically(
        workoutId: Long,
        setId: Long,
        endsAtUtcMillis: Long?,
        durationSeconds: Int?,
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
        if (durationSeconds != null && endsAtUtcMillis != null) {
            startRest(workoutId, setId, endsAtUtcMillis, durationSeconds)
        }
        return true
    }

    @Query(
        """
        DELETE FROM pending_sync_operations
        WHERE operationId IN (:operationIds) AND status = 'Pending'
        """,
    )
    abstract suspend fun deletePendingOperations(operationIds: List<String>): Int

    @Query(
        """
        SELECT COUNT(*) FROM pending_sync_operations
        WHERE operationId IN (:operationIds) AND status = 'Pending'
        """,
    )
    abstract suspend fun countPendingOperations(operationIds: List<String>): Int

    @Query("DELETE FROM pending_sync_operations WHERE operationId = :operationId")
    abstract suspend fun deleteOperation(operationId: String): Int

    @Transaction
    open suspend fun updateSetAndEnqueueAtomically(
        setId: Long,
        weightKilograms: Double,
        repetitions: Int,
        operation: PendingSyncOperation,
        coalescedOperationIds: List<String>,
    ): Boolean {
        if (getSet(setId) == null) return false
        if (coalescedOperationIds.isNotEmpty()) {
            if (countPendingOperations(coalescedOperationIds) != coalescedOperationIds.size) {
                return false
            }
            check(deletePendingOperations(coalescedOperationIds) == coalescedOperationIds.size)
        }
        if (updateSetValues(setId, weightKilograms, repetitions) != 1) return false
        insertOperation(operation)
        return true
    }

    @Transaction
    open suspend fun undoSetAtomically(
        setId: Long,
        operation: PendingSyncOperation?,
        cancelledCompleteOperationId: String?,
    ): Boolean {
        val set = getSet(setId) ?: return false
        if (!set.isCompleted) return false
        if (cancelledCompleteOperationId != null) {
            if (countPendingOperations(listOf(cancelledCompleteOperationId)) != 1) return false
            check(deletePendingOperations(listOf(cancelledCompleteOperationId)) == 1)
        } else if (operation != null) {
            insertOperation(operation)
        } else {
            return false
        }
        check(markSetIncomplete(setId) == 1)
        clearRestForSet(setId)
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

    @Query(
        """
        UPDATE pending_sync_operations
        SET status = 'Pending', lastError = NULL
        WHERE operationId = :operationId AND status = 'Failed' AND canRetry = 1
        """,
    )
    abstract suspend fun retryOperation(operationId: String): Int

    @Query(
        """
        DELETE FROM pending_sync_operations
        WHERE operationId = :operationId
          AND (status = 'Conflict' OR (status = 'Failed' AND canRetry = 0))
        """,
    )
    abstract suspend fun dismissTerminalOperation(operationId: String): Int

    @Query(
        """
        UPDATE pending_sync_operations
        SET status = 'Conflict', lastError = :message, canRetry = 0
        WHERE entityId = :entityId
          AND sequenceNumber > :afterSequenceNumber
          AND (status = 'Pending' OR status = 'Syncing' OR (status = 'Failed' AND canRetry = 1))
        """,
    )
    abstract suspend fun markFollowingOperationsConflicted(
        entityId: Long,
        afterSequenceNumber: Long,
        message: String,
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
    open suspend fun applySuccessAndDelete(
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
        deleteOperation(operation.operationId)
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

    @Query("DELETE FROM local_workouts")
    abstract suspend fun deleteAllWorkouts()

    @Query("DELETE FROM pending_sync_operations")
    abstract suspend fun deleteAllOperations()

    /**
     * Полная очистка кэша тренировок.
     *
     * Вызывается при каждом новом сопряжении: часы не знают, чей аккаунт был
     * раньше, а оставлять на устройстве чужую тренировку нельзя. Упражнения и
     * подходы уходят каскадом вслед за тренировкой.
     */
    @Transaction
    open suspend fun clearWorkoutCache() {
        deleteAllOperations()
        deleteAllWorkouts()
    }
}
