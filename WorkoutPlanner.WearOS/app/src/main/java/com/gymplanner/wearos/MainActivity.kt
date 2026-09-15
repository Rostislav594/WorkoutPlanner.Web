package com.gymplanner.wearos

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.ContextCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.ui.WorkoutPlannerWearApp
import com.gymplanner.wearos.ui.theme.WorkoutPlannerWearTheme
import com.gymplanner.wearos.workout.ActiveWorkoutService
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch

class MainActivity : ComponentActivity() {
    private val appContainer by lazy {
        (application as WorkoutPlannerWearApplication).appContainer
    }

    private val notificationPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission(),
    ) { /* Отказ не критичен: сервис и вибрация работают и без уведомления. */ }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        requestNotificationPermissionIfNeeded()
        observeWorkoutForService()

        setContent {
            WorkoutPlannerWearTheme {
                WorkoutPlannerWearApp(appContainer)
            }
        }
    }

    /**
     * Сервис живёт ровно столько, сколько идёт тренировка: он нужен, чтобы
     * отсчёт отдыха и вибрация пережили погасший экран и уход приложения в фон.
     */
    private fun observeWorkoutForService() {
        lifecycleScope.launch {
            repeatOnLifecycle(Lifecycle.State.STARTED) {
                appContainer.workoutRepository.state
                    .map { it.requiresBackgroundService() }
                    .distinctUntilChanged()
                    .collect { required ->
                        if (required) {
                            ActiveWorkoutService.start(this@MainActivity)
                        } else {
                            ActiveWorkoutService.stop(this@MainActivity)
                        }
                    }
            }
        }
    }

    private fun requestNotificationPermissionIfNeeded() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU) return
        val granted = ContextCompat.checkSelfPermission(
            this,
            Manifest.permission.POST_NOTIFICATIONS,
        ) == PackageManager.PERMISSION_GRANTED
        if (!granted) {
            notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }

    private fun MockWorkoutState.requiresBackgroundService(): Boolean = when (this) {
        is MockWorkoutState.CurrentSet,
        is MockWorkoutState.Rest,
        is MockWorkoutState.ReadyToFinish,
        -> true

        is MockWorkoutState.Pairing,
        is MockWorkoutState.NoActiveWorkout,
        is MockWorkoutState.Completed,
        -> false
    }
}
