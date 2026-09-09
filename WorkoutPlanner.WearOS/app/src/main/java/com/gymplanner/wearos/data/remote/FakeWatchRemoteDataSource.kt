package com.gymplanner.wearos.data.remote

import android.content.Context
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import com.gymplanner.wearos.data.local.PendingSyncOperation
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

    override suspend fun pair(pairingCode: String): PairingResult =
        if (pairingCode == "123456") PairingResult.Success
        else PairingResult.Failure("Неверный код", retryable = false)

    override suspend fun getActiveWorkout(): ActiveWorkoutResult =
        ActiveWorkoutResult.Failure("Fake active workout is not available", retryable = false)

    override suspend fun completeSet(operation: PendingSyncOperation): CompletionResult {
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
                CompletionResult.Success(
                    AuthoritativeSetState(true, 0.0, 0, storedVersion),
                )
            } else {
                CompletionResult.PermanentFailure("Operation ID уже относится к другой операции")
            }
        }

        val clientVersion = clientVersionPattern.find(operation.payloadJson)
            ?.groupValues
            ?.getOrNull(1)
            ?.toLongOrNull()
            ?: return CompletionResult.PermanentFailure("Некорректный payload CompleteSet")
        val serverVersion = clientVersion + 1
        val persisted = processedOperations.edit()
            .putString(operation.operationId, "${operation.entityId}:$serverVersion")
            .commit()
        if (!persisted) {
            return CompletionResult.TransientFailure("Не удалось сохранить fake server response")
        }
        return CompletionResult.Success(
            AuthoritativeSetState(true, 0.0, 0, serverVersion),
        )
    }

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

    private companion object {
        const val processedOperationsPreferences = "stage8_fake_server_operations"
        val clientVersionPattern = Regex("\\\"clientVersion\\\"\\s*:\\s*(\\d+)")
    }
}
