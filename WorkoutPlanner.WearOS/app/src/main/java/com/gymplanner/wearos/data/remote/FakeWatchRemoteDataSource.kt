package com.gymplanner.wearos.data.remote

import android.content.Context
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import com.gymplanner.wearos.data.local.PendingSyncOperation
import com.gymplanner.wearos.data.local.SyncOperationType
import kotlinx.coroutines.delay

class FakeWatchRemoteDataSource(
    context: Context,
    private val simulatedDelayMillis: Long = 250,
) : WatchRemoteDataSource {
    private val connectivityManager =
        context.applicationContext.getSystemService(ConnectivityManager::class.java)
    private val processedOperations = context.applicationContext.getSharedPreferences(
        processedOperationsPreferences,
        Context.MODE_PRIVATE,
    )
    private var pollsBeforeApproval = 0

    override suspend fun pair(pairingCode: String): PairingResult =
        if (pairingCode == "123456") PairingResult.Success
        else PairingResult.Failure("Неверный код", retryable = false)

    /// Fake-заявка подтверждается сразу на втором опросе: первый отдаёт Pending,
    /// чтобы экран ожидания успел отрисоваться.
    override suspend fun startPhonePairing(): StartPhonePairingResult {
        pollsBeforeApproval = 1
        return StartPhonePairingResult.Success(
            requestId = "fake-request",
            pollToken = "fake-token",
            approveUrl = "gymplanner://watch/approve?request=fake-request",
        )
    }

    override suspend fun pollPhonePairing(
        requestId: String,
        pollToken: String,
    ): PhonePairingPollResult {
        delay(simulatedDelayMillis)
        if (pollsBeforeApproval > 0) {
            pollsBeforeApproval--
            return PhonePairingPollResult.Pending
        }
        return PhonePairingPollResult.Approved
    }

    override suspend fun getActiveWorkout(): ActiveWorkoutResult =
        ActiveWorkoutResult.Failure("Fake active workout is not available", retryable = false)

    override suspend fun sendSetMutation(operation: PendingSyncOperation): CompletionResult {
        if (!hasValidatedNetwork()) {
            return CompletionResult.TransientFailure("Нет подключения к сети")
        }

        delay(simulatedDelayMillis)
        val storedResult = processedOperations.getString(operation.operationId, null)
        if (storedResult != null) {
            val parts = storedResult.split(':')
            val storedEntityId = parts.getOrNull(0)?.toLongOrNull()
            val storedVersion = parts.getOrNull(1)?.toLongOrNull()
            return if (storedEntityId == operation.entityId && storedVersion != null) {
                CompletionResult.Success(authoritativeState(operation, storedVersion))
            } else {
                CompletionResult.PermanentFailure("Operation ID уже относится к другой операции")
            }
        }

        val clientVersion = clientVersionPattern.find(operation.payloadJson)
            ?.groupValues
            ?.getOrNull(1)
            ?.toLongOrNull()
            ?: return CompletionResult.PermanentFailure("Некорректный payload операции")
        val serverVersion = clientVersion + 1
        val persisted = processedOperations.edit()
            .putString(operation.operationId, "${operation.entityId}:$serverVersion")
            .commit()
        if (!persisted) {
            return CompletionResult.TransientFailure("Не удалось сохранить fake server response")
        }
        return CompletionResult.Success(authoritativeState(operation, serverVersion))
    }

    override suspend fun finishWorkout(workoutId: Long): FinishWorkoutRemoteResult =
        FinishWorkoutRemoteResult.Success(alreadyFinished = false)

    override suspend fun refreshSession(): SessionRefreshResult =
        if (hasValidatedNetwork()) SessionRefreshResult.Success
        else SessionRefreshResult.TransientFailure

    override fun clearSession() = Unit

    private fun hasValidatedNetwork(): Boolean {
        val network = connectivityManager.activeNetwork ?: return false
        val capabilities = connectivityManager.getNetworkCapabilities(network) ?: return false
        return capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) &&
            capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED)
    }

    private fun authoritativeState(
        operation: PendingSyncOperation,
        serverVersion: Long,
    ): AuthoritativeSetState {
        val weight = weightPattern.find(operation.payloadJson)?.groupValues?.getOrNull(1)?.toDoubleOrNull()
        val repetitions = repetitionsPattern.find(operation.payloadJson)?.groupValues?.getOrNull(1)?.toIntOrNull()
        return AuthoritativeSetState(
            isCompleted = operation.operationType != SyncOperationType.UndoSet,
            weightKilograms = weight ?: 0.0,
            repetitions = repetitions ?: 0,
            serverVersion = serverVersion,
        )
    }

    private companion object {
        const val processedOperationsPreferences = "stage8_fake_server_operations"
        val clientVersionPattern = Regex("\\\"clientVersion\\\"\\s*:\\s*(\\d+)")
        val weightPattern = Regex("\\\"weightKilograms\\\"\\s*:\\s*([0-9.]+)")
        val repetitionsPattern = Regex("\\\"repetitions\\\"\\s*:\\s*(\\d+)")
    }
}
