package com.gymplanner.wearos.domain.model

sealed interface MockWorkoutState {
    data class Pairing(
        val status: PairingStatus = PairingStatus.Idle,
        val errorMessage: String? = null,
    ) : MockWorkoutState

    data class NoActiveWorkout(
        val lastCheckedAtMillis: Long,
        val isRefreshing: Boolean = false,
        val errorMessage: String? = null,
        val isFinished: Boolean = false,
    ) : MockWorkoutState

    data class CurrentSet(
        val exerciseName: String,
        val exerciseNumber: Int,
        val totalExercises: Int,
        val setNumber: Int,
        val totalSets: Int,
        val weightKilograms: Double?,
        val repetitions: Int?,
        val syncStatus: SyncStatus = SyncStatus.Synchronized,
    ) : MockWorkoutState

    data class Rest(
        val completedExerciseName: String,
        val completedSetNumber: Int,
        val durationSeconds: Int,
        val endsAtElapsedRealtimeMillis: Long,
        val nextSet: SetPreview?,
    ) : MockWorkoutState
}

enum class PairingStatus {
    Idle,
    Connecting,
    Error,
}

enum class SyncStatus {
    Synchronized,
    Pending,
    Failed,
    Conflict,
}

data class SetPreview(
    val exerciseName: String,
    val setNumber: Int,
)
