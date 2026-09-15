package com.gymplanner.wearos.ui

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.repository.FinishWorkoutResult
import com.gymplanner.wearos.domain.repository.WorkoutRepository
import com.gymplanner.wearos.domain.usecase.ObserveWorkoutStateUseCase
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlin.math.min

class MinimalMvpViewModel(
    private val repository: WorkoutRepository,
    private val elapsedRealtimeMillis: () -> Long = { System.nanoTime() / nanosPerMillisecond },
) : ViewModel() {
    private val mutablePairingCode = MutableStateFlow("")
    private val mutableRestSeconds = MutableStateFlow<Int?>(null)
    private val mutableEvents = MutableSharedFlow<UiEvent>(extraBufferCapacity = 2)
    private val mutableActionMessage = MutableStateFlow<String?>(null)
    private val mutableIsFinishing = MutableStateFlow(false)

    val pairingCode: StateFlow<String> = mutablePairingCode.asStateFlow()
    val restSeconds: StateFlow<Int?> = mutableRestSeconds.asStateFlow()
    val events = mutableEvents.asSharedFlow()
    val actionMessage: StateFlow<String?> = mutableActionMessage.asStateFlow()
    val isFinishing: StateFlow<Boolean> = mutableIsFinishing.asStateFlow()

    val state: StateFlow<MockWorkoutState> = ObserveWorkoutStateUseCase(repository)()
        .stateIn(
            scope = viewModelScope,
            started = SharingStarted.WhileSubscribed(5_000),
            initialValue = MockWorkoutState.Pairing(),
        )

    init {
        viewModelScope.launch {
            repository.state.collectLatest { currentState ->
                if (currentState is MockWorkoutState.Rest) {
                    runRestTimer(currentState)
                } else {
                    mutableRestSeconds.value = null
                }
            }
        }
    }

    fun updatePairingCode(value: String) {
        mutablePairingCode.value = value.filter(Char::isDigit).take(pairingCodeLength)
    }

    fun submitPairingCode() {
        if (mutablePairingCode.value.length != pairingCodeLength) return
        viewModelScope.launch { repository.pair(mutablePairingCode.value) }
    }

    fun confirmOnPhone() {
        viewModelScope.launch { repository.pairWithPhoneConfirmation() }
    }

    fun retryPairing() {
        mutablePairingCode.value = ""
        viewModelScope.launch { repository.retryPairing() }
    }

    fun refreshActiveWorkout() {
        viewModelScope.launch { repository.refreshActiveWorkout() }
    }

    fun completeCurrentSet() {
        viewModelScope.launch {
            if (repository.state.value !is MockWorkoutState.CurrentSet) return@launch
            if (repository.completeCurrentSet()) {
                mutableActionMessage.value = null
                mutableEvents.emit(UiEvent.SetCompleted)
            } else {
                mutableActionMessage.value = mutationRejectedMessage
            }
        }
    }

    fun finishWorkout() {
        if (mutableIsFinishing.value) return
        viewModelScope.launch {
            mutableIsFinishing.value = true
            when (val result = repository.finishWorkout()) {
                FinishWorkoutResult.Success -> {
                    mutableActionMessage.value = null
                    mutableEvents.emit(UiEvent.WorkoutFinished)
                }
                FinishWorkoutResult.UnresolvedOperations -> {
                    mutableActionMessage.value = unresolvedOperationsMessage
                }
                is FinishWorkoutResult.Failure -> {
                    mutableActionMessage.value = result.message
                }
            }
            mutableIsFinishing.value = false
        }
    }

    fun skipRest() {
        viewModelScope.launch { repository.finishRest() }
    }

    private suspend fun runRestTimer(rest: MockWorkoutState.Rest) {
        while (currentCoroutineContext().isActive) {
            val remainingMillis = rest.endsAtElapsedRealtimeMillis - elapsedRealtimeMillis()
            val seconds = remainingSeconds(remainingMillis)
            mutableRestSeconds.value = seconds

            if (seconds == 0) {
                mutableEvents.emit(UiEvent.RestFinished)
                repository.finishRest()
                return
            }

            delay(min(timerTickMillis, remainingMillis.coerceAtLeast(1L)))
        }
    }

    sealed interface UiEvent {
        data object SetCompleted : UiEvent
        data object RestFinished : UiEvent
        data object WorkoutFinished : UiEvent
    }

    class Factory(
        private val repository: WorkoutRepository,
    ) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(MinimalMvpViewModel::class.java))
            return MinimalMvpViewModel(repository) as T
        }
    }

    companion object {
        private const val pairingCodeLength = 6
        private const val timerTickMillis = 250L
        private const val nanosPerMillisecond = 1_000_000L
        private const val mutationRejectedMessage =
            "Не удалось сохранить. Попробуйте ещё раз"
        private const val unresolvedOperationsMessage =
            "Есть неотправленные изменения. Дождитесь связи"

        internal fun remainingSeconds(remainingMillis: Long): Int =
            ((remainingMillis.coerceAtLeast(0L) + 999L) / 1_000L).toInt()
    }
}
