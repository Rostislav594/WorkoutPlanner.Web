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
    ) : MockWorkoutState

    data class CurrentSet(
        val setId: Long,
        val exerciseName: String,
        val exerciseNumber: Int,
        val totalExercises: Int,
        val setNumber: Int,
        val totalSets: Int,
        val weightKilograms: Double?,
    ) : MockWorkoutState

    data class Rest(
        val completedExerciseName: String,
        val completedSetNumber: Int,
        val durationSeconds: Int,
        val endsAtElapsedRealtimeMillis: Long,
        val nextSet: SetPreview?,
    ) : MockWorkoutState

    /** Все подходы закрыты, остаётся удержать кнопку завершения. */
    data object ReadyToFinish : MockWorkoutState

    /** Итоговый экран. Показывается один раз, пока человек не ушёл с него. */
    data class Completed(
        val kind: CompletedWorkoutKind,
    ) : MockWorkoutState
}

enum class PairingStatus {
    Idle,
    Connecting,
    Error,

    /// Заявка открыта на телефоне, ждём ответа пользователя там.
    WaitingForPhone,
}

/**
 * От вида тренировки зависит, что человеку делать дальше.
 *
 * Scheduled — запланированная по шаблону: она уже в истории и в графиках,
 * делать больше нечего. Free — свободная: на телефоне ждёт вопрос, сохранять
 * ли её шаблоном.
 */
enum class CompletedWorkoutKind {
    Scheduled,
    Free,
}

data class SetPreview(
    val exerciseName: String,
    val setNumber: Int,
)
