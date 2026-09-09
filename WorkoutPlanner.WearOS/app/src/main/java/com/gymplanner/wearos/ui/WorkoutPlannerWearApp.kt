package com.gymplanner.wearos.ui

import android.view.HapticFeedbackConstants
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.platform.LocalView
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.wear.compose.material3.AppScaffold
import androidx.wear.compose.material3.TimeText
import com.gymplanner.wearos.di.AppContainer
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.ui.currentset.CurrentSetScreen
import com.gymplanner.wearos.ui.noactive.NoActiveWorkoutScreen
import com.gymplanner.wearos.ui.pairing.PairingScreen
import com.gymplanner.wearos.ui.rest.RestTimerScreen

@Composable
fun WorkoutPlannerWearApp(
    appContainer: AppContainer,
    viewModel: MinimalMvpViewModel = viewModel(
        factory = MinimalMvpViewModel.Factory(appContainer.workoutRepository),
    ),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val pairingCode by viewModel.pairingCode.collectAsStateWithLifecycle()
    val restSeconds by viewModel.restSeconds.collectAsStateWithLifecycle()
    val view = LocalView.current

    LaunchedEffect(viewModel, view) {
        viewModel.events.collect { event ->
            when (event) {
                MinimalMvpViewModel.UiEvent.SetCompleted,
                MinimalMvpViewModel.UiEvent.RestFinished,
                -> view.performHapticFeedback(HapticFeedbackConstants.CONFIRM)
            }
        }
    }

    AppScaffold(timeText = { TimeText() }) {
        when (val currentState = state) {
            is MockWorkoutState.Pairing -> PairingScreen(
                state = currentState,
                pairingCode = pairingCode,
                onPairingCodeChange = viewModel::updatePairingCode,
                onConnect = viewModel::submitPairingCode,
                onRetry = viewModel::retryPairing,
            )

            is MockWorkoutState.NoActiveWorkout -> NoActiveWorkoutScreen(
                state = currentState,
                onRefresh = viewModel::refreshActiveWorkout,
            )

            is MockWorkoutState.CurrentSet -> CurrentSetScreen(
                state = currentState,
                onComplete = viewModel::completeCurrentSet,
            )

            is MockWorkoutState.Rest -> RestTimerScreen(
                state = currentState,
                remainingSeconds = restSeconds ?: currentState.durationSeconds,
                onSkip = viewModel::skipRest,
            )
        }
    }
}
