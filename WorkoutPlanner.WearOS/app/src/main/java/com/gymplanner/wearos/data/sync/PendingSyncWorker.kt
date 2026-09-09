package com.gymplanner.wearos.data.sync

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.gymplanner.wearos.WorkoutPlannerWearApplication

class PendingSyncWorker(
    appContext: Context,
    workerParameters: WorkerParameters,
) : CoroutineWorker(appContext, workerParameters) {
    override suspend fun doWork(): Result {
        val application = applicationContext as WorkoutPlannerWearApplication
        return when (application.appContainer.syncQueueProcessor.drain()) {
            QueueDrainResult.Complete -> Result.success()
            QueueDrainResult.Retry -> Result.retry()
        }
    }
}
