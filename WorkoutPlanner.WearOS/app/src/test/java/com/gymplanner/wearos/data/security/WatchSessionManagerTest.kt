package com.gymplanner.wearos.data.security

import com.gymplanner.wearos.data.remote.CompleteWatchSetRequest
import com.gymplanner.wearos.data.remote.CompleteWatchSetResponse
import com.gymplanner.wearos.data.remote.PairWatchRequest
import com.gymplanner.wearos.data.remote.RefreshWatchTokenRequest
import com.gymplanner.wearos.data.remote.StartWatchPairingRequest
import com.gymplanner.wearos.data.remote.StartWatchPairingResponse
import com.gymplanner.wearos.data.remote.WatchPairingStatusRequest
import com.gymplanner.wearos.data.remote.WatchPairingStatusResponse
import com.gymplanner.wearos.data.remote.WatchActiveWorkoutResponse
import com.gymplanner.wearos.data.remote.WatchApiService
import com.gymplanner.wearos.data.remote.WatchTokenResponse
import com.gymplanner.wearos.data.remote.UpdateWatchSetRequest
import com.gymplanner.wearos.data.remote.UndoWatchSetRequest
import com.gymplanner.wearos.data.remote.WatchSetMutationResponse
import com.gymplanner.wearos.data.remote.FinishWatchWorkoutResponse
import java.io.IOException
import kotlinx.coroutines.test.runTest
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Test
import retrofit2.Response

class WatchSessionManagerTest {
    @Test
    fun expiredAccessToken_rotatesRefreshToken() = runTest {
        var elapsedMillis = 0L
        val tokenStore = FakeTokenStore()
        val api = FakeWatchApiService(
            ArrayDeque(
                listOf(
                    Response.success(tokens("access-2", "refresh-2")),
                ),
            ),
        )
        val manager = WatchSessionManager(api, tokenStore) { elapsedMillis }
        manager.accept(tokens("access-1", "refresh-1", expiresIn = 31))

        assertEquals("access-1", manager.getAccessToken())
        elapsedMillis = 2_000
        assertEquals("access-2", manager.getAccessToken())
        assertEquals("refresh-2", tokenStore.token)
        assertEquals(listOf("refresh-1"), api.refreshTokens)
    }

    @Test
    fun invalidRefreshToken_clearsStoredCredentials() = runTest {
        val tokenStore = FakeTokenStore("invalid-refresh")
        val api = FakeWatchApiService(
            ArrayDeque(listOf(Response.error(401, "{}".toResponseBody()))),
        )
        val manager = WatchSessionManager(api, tokenStore) { 0 }

        assertNull(manager.getAccessToken())
        assertNull(tokenStore.token)
    }

    @Test
    fun transientRefreshFailure_keepsRefreshTokenForRetry() = runTest {
        val tokenStore = FakeTokenStore("retry-refresh")
        val api = FakeWatchApiService(
            ArrayDeque(listOf(Response.error(503, "{}".toResponseBody()))),
        )
        val manager = WatchSessionManager(api, tokenStore) { 0 }

        assertThrows(IOException::class.java) {
            kotlinx.coroutines.runBlocking { manager.getAccessToken() }
        }
        assertEquals("retry-refresh", tokenStore.token)
    }

    private fun tokens(
        accessToken: String,
        refreshToken: String,
        expiresIn: Long = 900,
    ) = WatchTokenResponse("Bearer", accessToken, expiresIn, refreshToken)

    private class FakeTokenStore(initial: String? = null) : SecureTokenStore {
        var token: String? = initial

        override fun readRefreshToken(): String? = token
        override fun saveRefreshToken(refreshToken: String) {
            token = refreshToken
        }
        override fun clear() {
            token = null
        }
    }

    private class FakeWatchApiService(
        private val refreshResponses: ArrayDeque<Response<WatchTokenResponse>>,
    ) : WatchApiService {
        val refreshTokens = mutableListOf<String>()

        override suspend fun refresh(
            request: RefreshWatchTokenRequest,
        ): Response<WatchTokenResponse> {
            refreshTokens += request.refreshToken
            return refreshResponses.removeFirst()
        }

        override suspend fun pair(request: PairWatchRequest): Response<WatchTokenResponse> =
            error("Not used")

        override suspend fun startPairing(
            request: StartWatchPairingRequest,
        ): Response<StartWatchPairingResponse> = error("Not used")

        override suspend fun pairingStatus(
            request: WatchPairingStatusRequest,
        ): Response<WatchPairingStatusResponse> = error("Not used")

        override suspend fun getActiveWorkout(
            authorization: String,
        ): Response<WatchActiveWorkoutResponse> = error("Not used")

        override suspend fun completeSet(
            authorization: String,
            setId: Long,
            request: CompleteWatchSetRequest,
        ): Response<CompleteWatchSetResponse> = error("Not used")

        override suspend fun updateSet(
            authorization: String,
            setId: Long,
            request: UpdateWatchSetRequest,
        ): Response<WatchSetMutationResponse> = error("Not used")

        override suspend fun undoSet(
            authorization: String,
            setId: Long,
            request: UndoWatchSetRequest,
        ): Response<WatchSetMutationResponse> = error("Not used")

        override suspend fun finishWorkout(
            authorization: String,
            workoutId: Long,
        ): Response<FinishWatchWorkoutResponse> = error("Not used")
    }
}
