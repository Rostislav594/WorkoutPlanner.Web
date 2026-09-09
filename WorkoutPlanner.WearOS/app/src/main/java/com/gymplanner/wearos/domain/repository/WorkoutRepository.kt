package com.gymplanner.wearos.domain.repository

import com.gymplanner.wearos.domain.model.MockWorkoutState
import kotlinx.coroutines.flow.StateFlow

interface WorkoutRepository {
    val state: StateFlow<MockWorkoutState>

    suspend fun pair(pairingCode: String)

    suspend fun retryPairing()

    suspend fun refreshActiveWorkout()

    suspend fun completeCurrentSet()

    suspend fun finishRest()
}
