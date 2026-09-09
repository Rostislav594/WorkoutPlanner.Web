package com.gymplanner.wearos.di

import android.content.Context
import com.google.gson.Gson
import com.google.gson.GsonBuilder
import com.gymplanner.wearos.BuildConfig
import com.gymplanner.wearos.data.local.WorkoutDatabase
import com.gymplanner.wearos.data.remote.RetrofitWatchRemoteDataSource
import com.gymplanner.wearos.data.remote.SafeNetworkLoggingInterceptor
import com.gymplanner.wearos.data.remote.WatchApiService
import com.gymplanner.wearos.data.repository.OfflineFirstWorkoutRepository
import com.gymplanner.wearos.data.security.AndroidKeystoreTokenStore
import com.gymplanner.wearos.data.security.DeviceIdentityStore
import com.gymplanner.wearos.data.security.WatchSessionManager
import com.gymplanner.wearos.data.sync.SyncQueueProcessor
import com.gymplanner.wearos.data.sync.WorkManagerSyncScheduler
import com.gymplanner.wearos.domain.repository.WorkoutRepository
import java.util.concurrent.TimeUnit
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory

class AppContainer(context: Context) {
    private val workoutDao = WorkoutDatabase.getInstance(context).workoutDao()
    private val gson: Gson = GsonBuilder().create()
    private val api = Retrofit.Builder()
        .baseUrl(BuildConfig.API_BASE_URL)
        .client(
            OkHttpClient.Builder()
                .connectTimeout(networkTimeoutSeconds, TimeUnit.SECONDS)
                .readTimeout(networkTimeoutSeconds, TimeUnit.SECONDS)
                .writeTimeout(networkTimeoutSeconds, TimeUnit.SECONDS)
                .addInterceptor(SafeNetworkLoggingInterceptor())
                .build(),
        )
        .addConverterFactory(GsonConverterFactory.create(gson))
        .build()
        .create(WatchApiService::class.java)
    private val sessionManager = WatchSessionManager(
        api = api,
        tokenStore = AndroidKeystoreTokenStore(context),
    )
    private val remoteDataSource = RetrofitWatchRemoteDataSource(
        api = api,
        sessionManager = sessionManager,
        deviceIdentityStore = DeviceIdentityStore(context),
        gson = gson,
    )
    private val syncScheduler = WorkManagerSyncScheduler(context)

    val syncQueueProcessor = SyncQueueProcessor(workoutDao, remoteDataSource)
    val workoutRepository: WorkoutRepository = OfflineFirstWorkoutRepository(
        workoutDao = workoutDao,
        remoteDataSource = remoteDataSource,
        syncScheduler = syncScheduler,
    )

    private companion object {
        const val networkTimeoutSeconds = 10L
    }
}
