package com.gymplanner.wearos.data.sync

import com.gymplanner.wearos.R
import com.gymplanner.wearos.data.local.DeviceSessionMetadata
import com.gymplanner.wearos.data.local.PendingSyncOperation
import com.gymplanner.wearos.data.local.SyncOperationStatus
import com.gymplanner.wearos.data.local.WorkoutDao
import com.gymplanner.wearos.data.localization.WatchStrings
import com.gymplanner.wearos.data.remote.CompletionResult
import com.gymplanner.wearos.data.remote.SessionRefreshResult
import com.gymplanner.wearos.data.remote.WatchRemoteDataSource
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

class SyncQueueProcessor(
    private val workoutDao: WorkoutDao,
    private val remoteDataSource: WatchRemoteDataSource,
    private val strings: WatchStrings,
    private val wallClockMillis: () -> Long = System::currentTimeMillis,
) {
    private val drainMutex = Mutex()

    suspend fun drain(): QueueDrainResult = drainMutex.withLock { drainLocked() }

    private suspend fun drainLocked(): QueueDrainResult {
        workoutDao.resetInterruptedOperations()

        while (true) {
            val operation = workoutDao.claimNextOperation(wallClockMillis())
                ?: return QueueDrainResult.Complete
            when (val result = sendWithSingleRefresh(operation)) {
                is CompletionResult.Success -> workoutDao.applySuccessAndDelete(
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
                    workoutDao.markFollowingOperationsConflicted(
                        operation.entityId,
                        operation.sequenceNumber,
                        strings.get(R.string.error_previous_operation_rejected),
                    )
                }
                is CompletionResult.Unauthorized -> {
                    workoutDao.updateOperationStatus(
                        operation.operationId,
                        SyncOperationStatus.Failed,
                        result.message,
                        false,
                    )
                    workoutDao.upsertSession(
                        DeviceSessionMetadata(
                            isPaired = false,
                            lastCheckedAtUtcMillis = wallClockMillis(),
                        ),
                    )
                    return QueueDrainResult.Complete
                }
                is CompletionResult.PermanentFailure -> {
                    workoutDao.updateOperationStatus(
                        operation.operationId,
                        SyncOperationStatus.Failed,
                        result.message,
                        false,
                    )
                    workoutDao.markFollowingOperationsConflicted(
                        operation.entityId,
                        operation.sequenceNumber,
                        strings.get(R.string.error_previous_operation_rejected),
                    )
                }
            }
        }
    }

    private suspend fun sendWithSingleRefresh(
        operation: PendingSyncOperation,
    ): CompletionResult {
        val firstResult = remoteDataSource.sendSetMutation(operation)
        if (firstResult !is CompletionResult.Unauthorized) return firstResult
        return when (remoteDataSource.refreshSession()) {
            SessionRefreshResult.Success -> remoteDataSource.sendSetMutation(operation)
            SessionRefreshResult.TransientFailure ->
                CompletionResult.TransientFailure(strings.get(R.string.error_session_refresh_failed))
            SessionRefreshResult.Invalid -> firstResult
        }
    }
}

enum class QueueDrainResult {
    Complete,
    Retry,
}
