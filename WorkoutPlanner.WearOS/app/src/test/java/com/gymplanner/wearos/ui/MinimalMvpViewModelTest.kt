package com.gymplanner.wearos.ui

import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.repository.FinishWorkoutResult
import com.gymplanner.wearos.domain.repository.WorkoutRepository
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class MinimalMvpViewModelTest {
    private val dispatcher: TestDispatcher = StandardTestDispatcher()

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun remainingSeconds_roundsUpAndNeverBecomesNegative() {
        assertEquals(10, MinimalMvpViewModel.remainingSeconds(10_000L))
        assertEquals(10, MinimalMvpViewModel.remainingSeconds(9_001L))
        assertEquals(1, MinimalMvpViewModel.remainingSeconds(1L))
        assertEquals(0, MinimalMvpViewModel.remainingSeconds(0L))
        assertEquals(0, MinimalMvpViewModel.remainingSeconds(-1L))
    }

    @Test
    fun successfulLocalCompletion_emitsHapticEventWithoutWaitingForStateFlow() = runTest(dispatcher) {
        val repository = CompletionRepository()
        val viewModel = MinimalMvpViewModel(repository)
        val event = async { viewModel.events.first() }

        viewModel.completeCurrentSet()
        advanceUntilIdle()

        assertEquals(MinimalMvpViewModel.UiEvent.SetCompleted, event.await())
    }

    @Test
    fun rejectedCompletion_explainsFailureInsteadOfSilentlySucceeding() = runTest(dispatcher) {
        val repository = CompletionRepository(completionAccepted = false)
        val viewModel = MinimalMvpViewModel(repository)

        viewModel.completeCurrentSet()
        advanceUntilIdle()

        assertTrue(viewModel.actionMessage.value!!.isNotBlank())
    }

    @Test
    fun finish_callsRepositoryAndEmitsConfirmation() = runTest(dispatcher) {
        val repository = CompletionRepository(initialState = MockWorkoutState.ReadyToFinish)
        val viewModel = MinimalMvpViewModel(repository)
        val event = async { viewModel.events.first() }

        viewModel.finishWorkout()
        advanceUntilIdle()

        assertTrue(repository.finishCalled)
        assertEquals(MinimalMvpViewModel.UiEvent.WorkoutFinished, event.await())
    }

    @Test
    fun unresolvedOperations_explainBlockerAndKeepWorkoutOpen() = runTest(dispatcher) {
        val repository = CompletionRepository(
            initialState = MockWorkoutState.ReadyToFinish,
            finishResult = FinishWorkoutResult.UnresolvedOperations,
        )
        val viewModel = MinimalMvpViewModel(repository)

        viewModel.finishWorkout()
        advanceUntilIdle()

        assertTrue(viewModel.actionMessage.value!!.contains("неотправленные"))
    }

    @Test
    fun confirmOnPhone_delegatesToRepository() = runTest(dispatcher) {
        val repository = CompletionRepository()
        val viewModel = MinimalMvpViewModel(repository)

        viewModel.confirmOnPhone()
        advanceUntilIdle()

        assertTrue(repository.phoneConfirmationRequested)
    }

    private class CompletionRepository(
        initialState: MockWorkoutState = currentSetState(),
        private val finishResult: FinishWorkoutResult = FinishWorkoutResult.Success,
        private val completionAccepted: Boolean = true,
    ) : WorkoutRepository {
        var finishCalled = false
        var phoneConfirmationRequested = false
        override val state: StateFlow<MockWorkoutState> = MutableStateFlow(initialState)

        override suspend fun completeCurrentSet(): Boolean = completionAccepted

        override suspend fun finishWorkout(): FinishWorkoutResult {
            finishCalled = true
            return finishResult
        }

        override suspend fun pair(pairingCode: String) = Unit

        override suspend fun pairWithPhoneConfirmation() {
            phoneConfirmationRequested = true
        }

        override suspend fun retryPairing() = Unit
        override suspend fun refreshActiveWorkout() = Unit
        override suspend fun finishRest() = Unit
    }

    private companion object {
        fun currentSetState() = MockWorkoutState.CurrentSet(
            setId = 1,
            exerciseName = "Жим штанги лежа",
            exerciseNumber = 1,
            totalExercises = 1,
            setNumber = 1,
            totalSets = 5,
            weightKilograms = 120.0,
        )
    }
}
