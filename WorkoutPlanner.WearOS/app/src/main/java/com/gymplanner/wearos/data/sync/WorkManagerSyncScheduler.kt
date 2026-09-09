package com.gymplanner.wearos.data.sync

import android.content.Context
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import java.util.concurrent.TimeUnit

class WorkManagerSyncScheduler(context: Context) : SyncScheduler {
    private val workManager = WorkManager.getInstance(context.applicationContext)

    override fun schedule() {
        val request = OneTimeWorkRequestBuilder<PendingSyncWorker>()
            .setConstraints(
                Constraints.Builder()
                    .setRequiredNetworkType(NetworkType.CONNECTED)
                    .build(),
            )
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 10, TimeUnit.SECONDS)
            .build()

        workManager.enqueueUniqueWork(
            uniqueWorkName,
            ExistingWorkPolicy.KEEP,
            request,
        )
    }

    companion object {
        const val uniqueWorkName = "pending-workout-sync"
    }
}
