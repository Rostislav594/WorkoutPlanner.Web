package com.gymplanner.wearos.data.remote

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.Header
import retrofit2.http.POST
import retrofit2.http.Path

interface WatchApiService {
    @POST("pair")
    suspend fun pair(@Body request: PairWatchRequest): Response<WatchTokenResponse>

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
}
