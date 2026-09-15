package com.gymplanner.wearos.data.repository

import com.gymplanner.wearos.domain.model.CompletedWorkoutKind
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.model.PairingStatus
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class MockWorkoutRepositoryTest {
    @Test
    fun invalidPairingCode_exposesErrorAndRetry() = runTest {
        val repository = createRepository()

        repository.pair("654321")
        val error = repository.state.value as MockWorkoutState.Pairing
        assertEquals(PairingStatus.Error, error.status)
        assertFalse(error.errorMessage.isNullOrBlank())

        repository.retryPairing()
        val retry = repository.state.value as MockWorkoutState.Pairing
        assertEquals(PairingStatus.Idle, retry.status)
    }

    @Test
    fun phoneConfirmation_passesThroughWaitingStateAndConnects() = runTest {
        val repository = createRepository()

        val states = mutableListOf<PairingStatus>()
        val idle = repository.state.value as MockWorkoutState.Pairing
        states += idle.status

        repository.pairWithPhoneConfirmation()

        // Подтверждение с телефона не только подключает часы, но и сразу
        // открывает тренировку — ручное «Обновить» больше не нужно.
        assertTrue(repository.state.value is MockWorkoutState.CurrentSet)
        assertEquals(PairingStatus.Idle, states.single())
    }

    @Test
    fun validPairing_opensFirstSetWithoutManualRefresh() = runTest {
        val repository = createRepository()

        repository.pair("123456")

        val firstSet = repository.state.value as MockWorkoutState.CurrentSet
        assertEquals("Жим лёжа", firstSet.exerciseName)
        assertEquals(1, firstSet.setNumber)
        assertEquals(80.0, firstSet.weightKilograms)
    }

    @Test
    fun completingLastExerciseSet_advancesToNextExerciseAfterRest() = runTest {
        val repository = createRepository()
        repository.pair("123456")
        repository.refreshActiveWorkout()

        assertTrue(repository.completeCurrentSet())
        assertTrue(repository.state.value is MockWorkoutState.Rest)
        assertFalse(repository.completeCurrentSet())
        repository.finishRest()
        assertEquals(2, (repository.state.value as MockWorkoutState.CurrentSet).setNumber)

        assertTrue(repository.completeCurrentSet())
        repository.finishRest()
        val nextExercise = repository.state.value as MockWorkoutState.CurrentSet
        assertEquals("Приседания", nextExercise.exerciseName)
        assertEquals(1, nextExercise.setNumber)
    }

    @Test
    fun restDeadline_isDerivedFromMonotonicTimestamp() = runTest {
        var elapsedMillis = 4_000L
        val repository = createRepository(elapsedRealtimeMillis = { elapsedMillis })
        repository.pair("123456")
        repository.refreshActiveWorkout()

        assertTrue(repository.completeCurrentSet())
        val rest = repository.state.value as MockWorkoutState.Rest

        assertEquals(14_000L, rest.endsAtElapsedRealtimeMillis)
        assertEquals(10, rest.durationSeconds)
    }

    @Test
    fun lastSetLeadsToReadyToFinishAndThenToCompletedScreen() = runTest {
        val repository = createRepository()
        repository.pair("123456")
        repository.refreshActiveWorkout()

        repeat(4) {
            assertTrue(repository.completeCurrentSet())
            repository.finishRest()
        }

        assertEquals(MockWorkoutState.ReadyToFinish, repository.state.value)

        assertEquals(
            com.gymplanner.wearos.domain.repository.FinishWorkoutResult.Success,
            repository.finishWorkout(),
        )
        val finished = repository.state.value as MockWorkoutState.Completed
        assertEquals(CompletedWorkoutKind.Scheduled, finished.kind)
    }

    private fun createRepository(
        elapsedRealtimeMillis: () -> Long = { 1_000L },
    ) = MockWorkoutRepository(
        wallClockMillis = { 1_700_000_000_000L },
        elapsedRealtimeMillis = elapsedRealtimeMillis,
        simulatedDelayMillis = 0,
        restDurationSeconds = 10,
    )
}
