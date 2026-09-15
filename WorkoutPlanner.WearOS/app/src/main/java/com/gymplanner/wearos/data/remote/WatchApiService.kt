package com.gymplanner.wearos.data.remote

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.Header
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.PUT

interface WatchApiService {
    @POST("pair")
    suspend fun pair(@Body request: PairWatchRequest): Response<WatchTokenResponse>

    @POST("pair/request")
    suspend fun startPairing(
        @Body request: StartWatchPairingRequest,
    ): Response<StartWatchPairingResponse>

    @POST("pair/status")
    suspend fun pairingStatus(
        @Body request: WatchPairingStatusRequest,
    ): Response<WatchPairingStatusResponse>

    @POST("token/refresh")
    suspend fun refresh(@Body request: RefreshWatchTokenRequest): Response<WatchTokenResponse>

    @GET("workouts/active")
    suspend fun getActiveWorkout(
        @Header("Authorization") authorization: String,
    ): Response<WatchActiveWorkoutResponse>

    @POST("sets/{setId}/complete")
    suspend fun completeSet(
        @Header("Authorization") authorization: String,
        @Path("setId") setId: Long,
        @Body request: CompleteWatchSetRequest,
    ): Response<CompleteWatchSetResponse>

    @PUT("sets/{setId}")
    suspend fun updateSet(
        @Header("Authorization") authorization: String,
        @Path("setId") setId: Long,
        @Body request: UpdateWatchSetRequest,
    ): Response<WatchSetMutationResponse>

    @POST("sets/{setId}/undo")
    suspend fun undoSet(
        @Header("Authorization") authorization: String,
        @Path("setId") setId: Long,
        @Body request: UndoWatchSetRequest,
    ): Response<WatchSetMutationResponse>

    @POST("workouts/{workoutId}/finish")
    suspend fun finishWorkout(
        @Header("Authorization") authorization: String,
        @Path("workoutId") workoutId: Long,
    ): Response<FinishWatchWorkoutResponse>
}
