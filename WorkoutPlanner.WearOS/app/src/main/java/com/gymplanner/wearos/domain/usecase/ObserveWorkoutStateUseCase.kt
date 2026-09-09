package com.gymplanner.wearos.domain.usecase

import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.repository.WorkoutRepository
import kotlinx.coroutines.flow.StateFlow

class ObserveWorkoutStateUseCase(
    private val repository: WorkoutRepository,
) {
    operator fun invoke(): StateFlow<MockWorkoutState> = repository.state
}

