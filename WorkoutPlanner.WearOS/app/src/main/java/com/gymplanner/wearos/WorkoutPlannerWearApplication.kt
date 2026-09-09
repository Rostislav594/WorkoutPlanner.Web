package com.gymplanner.wearos

import android.app.Application
import com.gymplanner.wearos.di.AppContainer

class WorkoutPlannerWearApplication : Application() {
    val appContainer by lazy { AppContainer(this) }
}
