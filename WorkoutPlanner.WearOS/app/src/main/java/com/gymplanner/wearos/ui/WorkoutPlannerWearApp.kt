package com.gymplanner.wearos.ui

import android.view.HapticFeedbackConstants
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.platform.LocalView
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.wear.compose.foundation.AmbientMode
import androidx.wear.compose.foundation.LocalAmbientModeManager
import androidx.wear.compose.foundation.rememberAmbientModeManager
import com.gymplanner.wearos.di.AppContainer
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.ui.ambient.AmbientWorkoutScreen
import com.gymplanner.wearos.ui.currentset.CurrentSetScreen
import com.gymplanner.wearos.ui.finish.ReadyToFinishScreen
import com.gymplanner.wearos.ui.finish.WorkoutCompletedScreen
import com.gymplanner.wearos.ui.noactive.NoActiveWorkoutScreen
import com.gymplanner.wearos.ui.pairing.PairingScreen
import com.gymplanner.wearos.ui.rest.RestTimerScreen

/**
 * Навигации как таковой больше нет: экран однозначно определяется состоянием
 * тренировки. Это и было смыслом переделки — на часах в зале не листают меню,
 * а делают одно действие и убирают руку.
 */
@Composable
fun WorkoutPlannerWearApp(
    appContainer: AppContainer,
    viewModel: MinimalMvpViewModel = viewModel(
        factory = MinimalMvpViewModel.Factory(appContainer.workoutRepository, appContainer.strings),
    ),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val pairingCode by viewModel.pairingCode.collectAsStateWithLifecycle()
    val restSeconds by viewModel.restSeconds.collectAsStateWithLifecycle()
    val actionMessage by viewModel.actionMessage.collectAsStateWithLifecycle()
    val isFinishing by viewModel.isFinishing.collectAsStateWithLifecycle()
    val view = LocalView.current

    LaunchedEffect(viewModel, view) {
        viewModel.events.collect { event ->
            when (event) {
                MinimalMvpViewModel.UiEvent.SetCompleted,
                MinimalMvpViewModel.UiEvent.WorkoutFinished,
                -> view.performHapticFeedback(HapticFeedbackConstants.CONFIRM)

                // Конец отдыха вибрирует из ActiveWorkoutService: он работает и при
                // погашенном экране, а два источника дали бы двойной сигнал.
                MinimalMvpViewModel.UiEvent.RestFinished -> Unit
            }
        }
    }

    val ambientModeManager = rememberAmbientModeManager()
    val isAmbient = ambientModeManager?.currentAmbientMode is AmbientMode.Ambient

    CompositionLocalProvider(LocalAmbientModeManager provides ambientModeManager) {
        if (isAmbient) {
            AmbientWorkoutScreen(state)
            return@CompositionLocalProvider
        }

        when (val currentState = state) {
            is MockWorkoutState.Pairing -> PairingScreen(
                state = currentState,
                pairingCode = pairingCode,
                onPairingCodeChange = viewModel::updatePairingCode,
                onConnect = viewModel::submitPairingCode,
                onConfirmOnPhone = viewModel::confirmOnPhone,
                onRetry = viewModel::retryPairing,
            )

            is MockWorkoutState.NoActiveWorkout -> NoActiveWorkoutScreen(
                state = currentState,
                onRefresh = viewModel::refreshActiveWorkout,
            )

            is MockWorkoutState.CurrentSet -> CurrentSetScreen(
                state = currentState,
                onComplete = viewModel::completeCurrentSet,
                actionMessage = actionMessage,
            )

            is MockWorkoutState.Rest -> RestTimerScreen(
                state = currentState,
                remainingSeconds = restSeconds ?: currentState.durationSeconds,
                onSkip = viewModel::skipRest,
            )

            MockWorkoutState.ReadyToFinish -> ReadyToFinishScreen(
                isFinishing = isFinishing,
                actionMessage = actionMessage,
                onFinish = viewModel::finishWorkout,
            )

            is MockWorkoutState.Completed -> WorkoutCompletedScreen(kind = currentState.kind)
        }
    }
}
