package com.gymplanner.wearos.data.remote

import com.gymplanner.wearos.data.local.PendingSyncOperation

interface WatchRemoteDataSource {
    suspend fun pair(pairingCode: String): PairingResult

    suspend fun getActiveWorkout(): ActiveWorkoutResult

    suspend fun completeSet(operation: PendingSyncOperation): CompletionResult

    suspend fun refreshSession(): SessionRefreshResult

    fun clearSession()
}

enum class SessionRefreshResult {
    Success,
    TransientFailure,
    Invalid,
}

sealed interface PairingResult {
    data object Success : PairingResult
    data class Failure(val message: String, val retryable: Boolean) : PairingResult
}

sealed interface ActiveWorkoutResult {
    data class Success(val workout: WatchActiveWorkoutResponse) : ActiveWorkoutResult
    data class NoActiveWorkout(val finished: Boolean = false) : ActiveWorkoutResult
    data class Unauthorized(val message: String) : ActiveWorkoutResult
    data class Failure(val message: String, val retryable: Boolean) : ActiveWorkoutResult
}

data class AuthoritativeSetState(
    val isCompleted: Boolean,
    val weightKilograms: Double,
    val repetitions: Int,
    val serverVersion: Long,
)

sealed interface CompletionResult {
    data class Success(val set: AuthoritativeSetState) : CompletionResult

    data class TransientFailure(val message: String) : CompletionResult

    data class Unauthorized(val message: String) : CompletionResult

    data class Conflict(
        val set: AuthoritativeSetState?,
        val message: String,
    ) : CompletionResult

    data class PermanentFailure(val message: String) : CompletionResult
}
