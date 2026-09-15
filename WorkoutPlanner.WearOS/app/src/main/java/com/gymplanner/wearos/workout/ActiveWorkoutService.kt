package com.gymplanner.wearos.workout

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import androidx.core.app.NotificationCompat
import androidx.wear.ongoing.OngoingActivity
import androidx.wear.ongoing.Status
import com.gymplanner.wearos.MainActivity
import com.gymplanner.wearos.R
import com.gymplanner.wearos.WorkoutPlannerWearApplication
import com.gymplanner.wearos.data.local.LocalWorkoutSnapshot
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch

/**
 * Держит тренировку живой, пока экран погашен.
 *
 * Зачем сервис, а не таймер в ViewModel: в зале человек опускает руку, экран
 * гаснет, приложение уходит в фон — и вибрация из composable до него уже не
 * дойдёт, а процесс может быть выгружен. Foreground-сервис переживает и то,
 * и другое, а заодно даёт Ongoing Activity, чтобы вернуться в тренировку
 * одним касанием с циферблата.
 *
 * Источник истины остаётся прежним: дедлайн отдыха читается из Room, сервис
 * ничего не решает сам и не дублирует бизнес-логику.
 */
class ActiveWorkoutService : Service() {
    private val scope = CoroutineScope(SupervisorJob())
    private var restJob: Job? = null
    private var notifiedRestSetId: Long? = null

    private val workoutDao by lazy {
        (application as WorkoutPlannerWearApplication).appContainer.workoutDao
    }

    private val vibrator: Vibrator? by lazy {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            getSystemService(VibratorManager::class.java)?.defaultVibrator
        } else {
            @Suppress("DEPRECATION")
            getSystemService(Vibrator::class.java)
        }
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        createChannel()
        startForegroundCompat(buildNotification(getString(R.string.workout_in_progress)))
        observeWorkout()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int = START_STICKY

    override fun onDestroy() {
        scope.cancel()
        super.onDestroy()
    }

    private fun observeWorkout() {
        scope.launch {
            workoutDao.observeActiveWorkout()
                .map { it?.let(::RestSnapshot) }
                .distinctUntilChanged()
                .collect { rest ->
                    if (rest == null) {
                        // Активной тренировки больше нет — сервису нечего держать.
                        stopSelf()
                        return@collect
                    }
                    scheduleRest(rest)
                }
        }
    }

    private fun scheduleRest(rest: RestSnapshot) {
        restJob?.cancel()
        restJob = null

        val completedSetId = rest.completedSetId
        val endsAtUtcMillis = rest.endsAtUtcMillis
        if (completedSetId == null || endsAtUtcMillis == null) {
            // Отдых закончился или был пропущен вручную.
            notifiedRestSetId = null
            updateNotification(getString(R.string.workout_in_progress))
            return
        }

        if (notifiedRestSetId == completedSetId) return

        updateNotification(getString(R.string.workout_rest_in_progress))
        restJob = scope.launch {
            val remaining = endsAtUtcMillis - System.currentTimeMillis()
            if (remaining > 0) delay(remaining)

            notifiedRestSetId = completedSetId
            vibrateRestFinished()
            updateNotification(getString(R.string.workout_rest_finished))
        }
    }

    private fun vibrateRestFinished() {
        val effect = VibrationEffect.createWaveform(
            longArrayOf(0, 400, 200, 400),
            NO_REPEAT,
        )
        vibrator?.vibrate(effect)
    }

    private fun createChannel() {
        val channel = NotificationChannel(
            channelId,
            getString(R.string.workout_channel_name),
            NotificationManager.IMPORTANCE_LOW,
        )
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }

    private fun buildNotification(text: String): Notification {
        val openApp = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java)
                .addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )

        val builder = NotificationCompat.Builder(this, channelId)
            .setSmallIcon(R.drawable.ic_notification_workout)
            .setContentTitle(getString(R.string.app_name))
            .setContentText(text)
            .setContentIntent(openApp)
            .setOngoing(true)
            .setCategory(NotificationCompat.CATEGORY_WORKOUT)
            .setPriority(NotificationCompat.PRIORITY_LOW)

        OngoingActivity.Builder(applicationContext, ongoingActivityId, builder)
            .setStaticIcon(R.drawable.ic_notification_workout)
            .setTouchIntent(openApp)
            .setStatus(Status.forPart(Status.TextPart(text)))
            .build()
            .apply(applicationContext)

        return builder.build()
    }

    private fun updateNotification(text: String) {
        getSystemService(NotificationManager::class.java)
            .notify(notificationId, buildNotification(text))
    }

    private fun startForegroundCompat(notification: Notification) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            startForeground(
                notificationId,
                notification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE,
            )
        } else {
            startForeground(notificationId, notification)
        }
    }

    /** Только то из снимка, что влияет на отдых: остальные правки сервису безразличны. */
    private data class RestSnapshot(
        val workoutId: Long,
        val completedSetId: Long?,
        val endsAtUtcMillis: Long?,
    ) {
        constructor(snapshot: LocalWorkoutSnapshot) : this(
            workoutId = snapshot.workout.workoutId,
            completedSetId = snapshot.workout.restCompletedSetId,
            endsAtUtcMillis = snapshot.workout.restEndsAtUtcMillis,
        )
    }

    companion object {
        private const val channelId = "active-workout"
        private const val notificationId = 1001
        private const val ongoingActivityId = 1001
        private const val NO_REPEAT = -1

        fun start(context: Context) {
            context.startForegroundService(
                Intent(context, ActiveWorkoutService::class.java),
            )
        }

        fun stop(context: Context) {
            context.stopService(Intent(context, ActiveWorkoutService::class.java))
        }
    }
}
