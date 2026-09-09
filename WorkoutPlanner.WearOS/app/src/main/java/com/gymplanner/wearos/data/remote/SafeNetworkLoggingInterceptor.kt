package com.gymplanner.wearos.data.remote

import android.util.Log
import com.gymplanner.wearos.BuildConfig
import java.util.concurrent.TimeUnit
import okhttp3.Interceptor
import okhttp3.Response

class SafeNetworkLoggingInterceptor : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        val startedAt = System.nanoTime()
        return try {
            chain.proceed(request).also { response ->
                if (BuildConfig.DEBUG) {
                    Log.d(
                        logTag,
                        "${request.method} ${request.url.encodedPath} -> ${response.code} " +
                            "(${elapsedMillis(startedAt)} ms)",
                    )
                }
            }
        } catch (exception: Exception) {
            if (BuildConfig.DEBUG) {
                Log.d(
                    logTag,
                    "${request.method} ${request.url.encodedPath} -> network error " +
                        "(${elapsedMillis(startedAt)} ms)",
                )
            }
            throw exception
        }
    }

    private fun elapsedMillis(startedAt: Long): Long =
        TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - startedAt)

    private companion object {
        const val logTag = "WatchApi"
    }
}
