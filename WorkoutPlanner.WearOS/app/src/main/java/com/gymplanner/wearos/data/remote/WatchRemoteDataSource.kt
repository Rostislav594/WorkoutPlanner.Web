package com.gymplanner.wearos.data.remote

import com.gymplanner.wearos.data.local.PendingSyncOperation

interface WatchRemoteDataSource {
    suspend fun pair(pairingCode: String): PairingResult

    /// Создаёт заявку, которую пользователь подтверждает на телефоне.
    suspend fun startPhonePairing(): StartPhonePairingResult

    /// Опрос заявки. Токены приезжают внутри [PhonePairingPollResult.Approved].
    suspend fun pollPhonePairing(
        requestId: String,
        pollToken: String,
    ): PhonePairingPollResult

    suspend fun getActiveWorkout(): ActiveWorkoutResult

    suspend fun sendSetMutation(operation: PendingSyncOperation): CompletionResult

    suspend fun finishWorkout(workoutId: Long): FinishWorkoutRemoteResult

    suspend fun refreshSession(): SessionRefreshResult

    fun clearSession()
}

sealed interface FinishWorkoutRemoteResult {
    data class Success(val alreadyFinished: Boolean) : FinishWorkoutRemoteResult
    data class TransientFailure(val message: String) : FinishWorkoutRemoteResult
    data class Unauthorized(val message: String) : FinishWorkoutRemoteResult
    data class PermanentFailure(val message: String) : FinishWorkoutRemoteResult
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

sealed interface StartPhonePairingResult {
    data class Success(
        val requestId: String,
        val pollToken: String,
        val approveUrl: String,
    ) : StartPhonePairingResult

    data class Failure(val message: String, val retryable: Boolean) : StartPhonePairingResult
}

sealed interface PhonePairingPollResult {
    /// Пользователь ещё не ответил на телефоне.
    data object Pending : PhonePairingPollResult

    /// Токены уже приняты сессией: часы подключены.
    data object Approved : PhonePairingPollResult

    data object Rejected : PhonePairingPollResult

    /// Заявка истекла или уже была использована — нужна новая.
    data object Expired : PhonePairingPollResult

    data class Failure(val message: String, val retryable: Boolean) : PhonePairingPollResult
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
