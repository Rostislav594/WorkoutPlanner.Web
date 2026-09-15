package com.gymplanner.wearos.data.repository

import com.gymplanner.wearos.domain.model.CompletedWorkoutKind
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.model.PairingStatus
import com.gymplanner.wearos.domain.model.SetPreview
import com.gymplanner.wearos.domain.repository.WorkoutRepository
import com.gymplanner.wearos.domain.repository.FinishWorkoutResult
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

class MockWorkoutRepository(
    private val wallClockMillis: () -> Long = System::currentTimeMillis,
    private val elapsedRealtimeMillis: () -> Long = { System.nanoTime() / nanosPerMillisecond },
    private val simulatedDelayMillis: Long = 500,
    private val restDurationSeconds: Int = 10,
) : WorkoutRepository {
    private val mutableState = MutableStateFlow<MockWorkoutState>(MockWorkoutState.Pairing())
    private var currentSetIndex = 0
    private val workoutSets = initialWorkoutSets.toMutableList()

    override val state: StateFlow<MockWorkoutState> = mutableState.asStateFlow()

    override suspend fun pair(pairingCode: String) {
        val pairingState = mutableState.value as? MockWorkoutState.Pairing ?: return
        if (pairingState.status != PairingStatus.Idle) return

        mutableState.value = MockWorkoutState.Pairing(status = PairingStatus.Connecting)
        delay(simulatedDelayMillis)

        if (pairingCode == demoPairingCode) {
            // Как и в боевом репозитории: после подключения тренировка
            // подтягивается сама, без ручного «Обновить».
            adoptNewSession()
        } else {
            mutableState.value = MockWorkoutState.Pairing(
                status = PairingStatus.Error,
                errorMessage = "Неверный код",
            )
        }
    }

    /// В демо-режиме телефон «подтверждает» заявку сам после короткой паузы.
    override suspend fun pairWithPhoneConfirmation() {
        val pairingState = mutableState.value as? MockWorkoutState.Pairing ?: return
        if (pairingState.status != PairingStatus.Idle) return

        mutableState.value = MockWorkoutState.Pairing(status = PairingStatus.Connecting)
        delay(simulatedDelayMillis)
        mutableState.value = MockWorkoutState.Pairing(status = PairingStatus.WaitingForPhone)
        delay(simulatedDelayMillis)
        adoptNewSession()
    }

    /** Новое подключение начинает демо-тренировку с первого подхода. */
    private fun adoptNewSession() {
        currentSetIndex = 0
        mutableState.value = workoutSets[0].toUiState()
    }

    override suspend fun retryPairing() {
        val pairingState = mutableState.value as? MockWorkoutState.Pairing
        if (pairingState?.status == PairingStatus.Error) {
            mutableState.value = MockWorkoutState.Pairing()
        }
    }

    override suspend fun refreshActiveWorkout() {
        val currentState = mutableState.value as? MockWorkoutState.NoActiveWorkout ?: return
        if (currentState.isRefreshing) return
        mutableState.value = currentState.copy(
            lastCheckedAtMillis = wallClockMillis(),
            isRefreshing = true,
        )
        delay(simulatedDelayMillis)
        currentSetIndex = 0
        mutableState.value = workoutSets[currentSetIndex].toUiState()
    }

    override suspend fun completeCurrentSet(): Boolean {
        val completedSet = workoutSets.getOrNull(currentSetIndex) ?: return false
        if (mutableState.value !is MockWorkoutState.CurrentSet) return false

        val nextSet = workoutSets.getOrNull(currentSetIndex + 1)
        mutableState.value = MockWorkoutState.Rest(
            completedExerciseName = completedSet.exerciseName,
            completedSetNumber = completedSet.setNumber,
            durationSeconds = restDurationSeconds,
            endsAtElapsedRealtimeMillis = elapsedRealtimeMillis() + restDurationSeconds * millisPerSecond,
            nextSet = nextSet?.let { SetPreview(it.exerciseName, it.setNumber) },
        )
        return true
    }

    override suspend fun finishWorkout(): FinishWorkoutResult {
        if (mutableState.value !is MockWorkoutState.ReadyToFinish) {
            return FinishWorkoutResult.Failure("Сначала завершите все подходы")
        }
        mutableState.value = MockWorkoutState.Completed(
            kind = CompletedWorkoutKind.Scheduled,
        )
        return FinishWorkoutResult.Success
    }

    override suspend fun finishRest() {
        if (mutableState.value !is MockWorkoutState.Rest) return

        val nextIndex = currentSetIndex + 1
        val nextSet = workoutSets.getOrNull(nextIndex)
        if (nextSet == null) {
            mutableState.value = MockWorkoutState.ReadyToFinish
            return
        }

        currentSetIndex = nextIndex
        mutableState.value = nextSet.toUiState()
    }

    private fun MockSet.toUiState() = MockWorkoutState.CurrentSet(
        setId = currentSetIndex.toLong() + 1,
        exerciseName = exerciseName,
        exerciseNumber = exerciseNumber,
        totalExercises = totalExercises,
        setNumber = setNumber,
        totalSets = totalSets,
        weightKilograms = weightKilograms,
    )

    private data class MockSet(
        val exerciseName: String,
        val exerciseNumber: Int,
        val totalExercises: Int,
        val setNumber: Int,
        val totalSets: Int,
        val weightKilograms: Double?,
        val repetitions: Int?,
    )

    private companion object {
        const val demoPairingCode = "123456"
        const val millisPerSecond = 1_000L
        const val nanosPerMillisecond = 1_000_000L

        val initialWorkoutSets = listOf(
            MockSet("Жим лёжа", 1, 2, 1, 2, 80.0, 8),
            MockSet("Жим лёжа", 1, 2, 2, 2, 82.5, 6),
            MockSet("Приседания", 2, 2, 1, 2, 100.0, 8),
            MockSet("Приседания", 2, 2, 2, 2, 105.0, 6),
        )
    }
}
