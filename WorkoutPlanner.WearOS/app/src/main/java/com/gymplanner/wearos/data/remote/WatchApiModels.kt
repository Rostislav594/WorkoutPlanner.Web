package com.gymplanner.wearos.data.remote

data class PairWatchRequest(
    val code: String,
    val deviceId: String,
    val displayName: String,
    val deviceModel: String?,
    val appVersion: String?,
)

// --- Сопряжение подтверждением на телефоне ---

data class StartWatchPairingRequest(
    val deviceId: String,
    val displayName: String,
    val deviceModel: String?,
    val appVersion: String?,
)

data class StartWatchPairingResponse(
    val requestId: String,
    val pollToken: String,
    val approveUrl: String,
    val expiresAtUtc: String,
)

data class WatchPairingStatusRequest(
    val requestId: String,
    val pollToken: String,
)

data class WatchPairingStatusResponse(
    val status: String,
    val tokens: WatchTokenResponse?,
)

object WatchPairingStatuses {
    const val PENDING = "pending"
    const val APPROVED = "approved"
    const val REJECTED = "rejected"
    const val EXPIRED = "expired"
}

data class RefreshWatchTokenRequest(val refreshToken: String)

data class WatchTokenResponse(
    val tokenType: String,
    val accessToken: String,
    val expiresIn: Long,
    val refreshToken: String,
)

data class WatchActiveWorkoutResponse(
    val workoutId: Long,
    val workoutName: String,
    val scheduledDate: String,
    val startedAtUtc: String?,
    val exercises: List<WatchExerciseResponse>,
    val currentExerciseId: Long?,
    val currentSetId: Long?,
    // Длительности отдыха приходят с сервера — их задаёт человек в приложении.
    // null означает старый сервер, который их ещё не отдаёт.
    val restBetweenSetsSeconds: Int? = null,
    val restBetweenExercisesSeconds: Int? = null,
    /** Свободная тренировка: в конце человека нужно отправить в приложение. */
    val isFreeWorkout: Boolean = false,
)

data class WatchExerciseResponse(
    val exerciseId: Long,
    val name: String,
    val order: Int,
    val supersetGroupId: Int? = null,
    val sets: List<WatchSetResponse>,
)

data class WatchSetResponse(
    val setId: Long,
    val setNumber: Int,
    val weight: Double,
    val repetitions: Int,
    val isCompleted: Boolean,
    val isWarmup: Boolean,
    val version: Long,
)

data class CompleteWatchSetRequest(
    val operationId: String,
    val changedAtUtc: String,
    val clientVersion: Long,
)

data class CompleteWatchSetResponse(
    val set: WatchSetResponse,
    val currentExerciseId: Long?,
    val currentSetId: Long?,
    val processedAtUtc: String,
)

data class UpdateWatchSetRequest(
    val operationId: String,
    val actualWeight: Double,
    val actualReps: Int,
    val changedAtUtc: String,
    val clientVersion: Long,
)

data class UndoWatchSetRequest(
    val operationId: String,
    val changedAtUtc: String,
    val clientVersion: Long,
)

data class WatchSetMutationResponse(
    val set: WatchSetResponse,
    val currentExerciseId: Long?,
    val currentSetId: Long?,
    val processedAtUtc: String,
)

data class FinishWatchWorkoutResponse(
    val workoutId: Long,
    val alreadyFinished: Boolean,
)

data class WatchSetConflictResponse(
    val code: String?,
    val detail: String?,
    val set: WatchSetResponse?,
    val currentExerciseId: Long?,
    val currentSetId: Long?,
    // Длительности отдыха приходят с сервера — их задаёт человек в приложении.
    // null означает старый сервер, который их ещё не отдаёт.
    val restBetweenSetsSeconds: Int? = null,
    val restBetweenExercisesSeconds: Int? = null,
)

data class ApiProblemDetails(
    val code: String?,
    val title: String?,
    val detail: String?,
)
