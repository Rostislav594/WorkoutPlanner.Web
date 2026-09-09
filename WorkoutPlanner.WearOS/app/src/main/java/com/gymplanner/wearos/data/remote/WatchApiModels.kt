package com.gymplanner.wearos.data.remote

data class PairWatchRequest(
    val code: String,
    val deviceId: String,
    val displayName: String,
    val deviceModel: String?,
    val appVersion: String?,
)

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
)

data class WatchExerciseResponse(
    val exerciseId: Long,
    val name: String,
    val order: Int,
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

data class WatchSetConflictResponse(
    val code: String?,
    val detail: String?,
    val set: WatchSetResponse?,
    val currentExerciseId: Long?,
    val currentSetId: Long?,
)

data class ApiProblemDetails(
    val code: String?,
    val title: String?,
    val detail: String?,
)
