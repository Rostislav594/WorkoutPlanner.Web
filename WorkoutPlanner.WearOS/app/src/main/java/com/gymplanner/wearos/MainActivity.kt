package com.gymplanner.wearos

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import com.gymplanner.wearos.ui.WorkoutPlannerWearApp
import com.gymplanner.wearos.ui.theme.WorkoutPlannerWearTheme

class MainActivity : ComponentActivity() {
    private val appContainer by lazy {
        (application as WorkoutPlannerWearApplication).appContainer
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            WorkoutPlannerWearTheme {
                WorkoutPlannerWearApp(appContainer)
            }
        }
    }
}
