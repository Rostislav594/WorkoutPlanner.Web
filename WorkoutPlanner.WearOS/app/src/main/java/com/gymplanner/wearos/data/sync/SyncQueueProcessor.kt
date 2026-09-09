package com.gymplanner.wearos.data.sync

import com.gymplanner.wearos.data.local.PendingSyncOperation
import com.gymplanner.wearos.data.local.SyncOperationStatus
import com.gymplanner.wearos.data.local.WorkoutDao
import com.gymplanner.wearos.data.remote.CompletionResult
import com.gymplanner.wearos.data.remote.SessionRefreshResult
import com.gymplanner.wearos.data.remote.WatchRemoteDataSource

class SyncQueueProcessor(
    private val workoutDao: WorkoutDao,
    private val remoteDataSource: WatchRemoteDataSource,
    private val wallClockMillis: () -> Long = System::currentTimeMillis,
) {
    suspend fun drain(): QueueDrainResult {
        workoutDao.resetInterruptedOperations()

        while (true) {
            val operation = workoutDao.claimNextOperation(wallClockMillis())
                ?: return QueueDrainResult.Complete
            when (val result = sendWithSingleRefresh(operation)) {
                is CompletionResult.Success -> workoutDao.markSynced(
                    operation,
                    result.set.isCompleted,
                    result.set.weightKilograms,
                    result.set.repetitions,
                    result.set.serverVersion,
                )
                is CompletionResult.TransientFailure -> {
                    workoutDao.updateOperationStatus(
                        operation.operationId,
                        SyncOperationStatus.Failed,
                        result.message,
                        true,
                    )
                    return QueueDrainResult.Retry
                }
                is CompletionResult.Conflict -> {
                    result.set?.let { set ->
                        workoutDao.applyAuthoritativeSet(
                            operation.entityId,
                            set.isCompleted,
                            set.weightKilograms,
                            set.repetitions,
                            set.serverVersion,
                        )
                    }
                    workoutDao.updateOperationStatus(
                        operation.operationId,
                        SyncOperationStatus.Conflict,
                        result.message,
                        false,
                    )
                }
                is CompletionResult.Unauthorized -> workoutDao.updateOperationStatus(
                    operation.operationId,
                    SyncOperationStatus.Failed,
                    result.message,
                    false,
                )
                is CompletionResult.PermanentFailure -> workoutDao.updateOperationStatus(
                    operation.operationId,
                    SyncOperationStatus.Failed,
                    result.message,
                    false,
                )
            }
        }
    }

    private suspend fun sendWithSingleRefresh(
        operation: PendingSyncOperation,
    ): CompletionResult {
        val firstResult = remoteDataSource.completeSet(operation)
        if (firstResult !is CompletionResult.Unauthorized) return firstResult
        return when (remoteDataSource.refreshSession()) {
            SessionRefreshResult.Success -> remoteDataSource.completeSet(operation)
            SessionRefreshResult.TransientFailure ->
                CompletionResult.TransientFailure("Не удалось обновить сессию")
            SessionRefreshResult.Invalid -> firstResult
        }
    }
}

enum class QueueDrainResult {
    Complete,
    Retry,
}
