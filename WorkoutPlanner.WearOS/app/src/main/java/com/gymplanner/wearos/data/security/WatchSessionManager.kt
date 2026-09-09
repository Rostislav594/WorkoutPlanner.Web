package com.gymplanner.wearos.data.security

import com.gymplanner.wearos.data.remote.RefreshWatchTokenRequest
import com.gymplanner.wearos.data.remote.WatchApiService
import com.gymplanner.wearos.data.remote.WatchTokenResponse
import java.io.IOException
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

class WatchSessionManager(
    private val api: WatchApiService,
    private val tokenStore: SecureTokenStore,
    private val elapsedRealtimeMillis: () -> Long = { System.nanoTime() / nanosPerMillisecond },
) {
    private val refreshMutex = Mutex()
    private var accessToken: String? = null
    private var accessTokenExpiresAtElapsedMillis: Long = 0

    fun accept(tokens: WatchTokenResponse) {
        require(tokens.accessToken.isNotBlank() && tokens.refreshToken.isNotBlank())
        require(tokens.expiresIn > 0)
        tokenStore.saveRefreshToken(tokens.refreshToken)
        accessToken = tokens.accessToken
        accessTokenExpiresAtElapsedMillis = elapsedRealtimeMillis() +
            (tokens.expiresIn * millisPerSecond - expirySkewMillis).coerceAtLeast(millisPerSecond)
    }

    suspend fun getAccessToken(): String? {
        val cached = accessToken
        if (cached != null && elapsedRealtimeMillis() < accessTokenExpiresAtElapsedMillis) {
            return cached
        }
        return refreshMutex.withLock {
            val refreshedCached = accessToken
            if (refreshedCached != null && elapsedRealtimeMillis() < accessTokenExpiresAtElapsedMillis) {
                refreshedCached
            } else if (refreshLocked()) {
                accessToken
            } else {
                null
            }
        }
    }

    suspend fun forceRefresh(): Boolean = refreshMutex.withLock {
        accessToken = null
        accessTokenExpiresAtElapsedMillis = 0
        refreshLocked()
    }

    fun clear() {
        accessToken = null
        accessTokenExpiresAtElapsedMillis = 0
        tokenStore.clear()
    }

    private suspend fun refreshLocked(): Boolean {
        val refreshToken = try {
            tokenStore.readRefreshToken()
        } catch (_: Exception) {
            clear()
            return false
        } ?: return false

        val response = try {
            api.refresh(RefreshWatchTokenRequest(refreshToken))
        } catch (exception: CancellationException) {
            throw exception
        } catch (exception: IOException) {
            throw exception
        }
        val tokens = response.body()
        if (!response.isSuccessful || tokens == null) {
            if (response.code() == 400 || response.code() == 401 || response.code() == 403) clear()
            return false
        }
        return try {
            accept(tokens)
            true
        } catch (_: Exception) {
            clear()
            false
        }
    }

    private companion object {
        const val millisPerSecond = 1_000L
        const val expirySkewMillis = 30_000L
        const val nanosPerMillisecond = 1_000_000L
    }
}
