package com.gymplanner.wearos.data.remote

import android.os.Build
import com.google.gson.Gson
import com.gymplanner.wearos.BuildConfig
import com.gymplanner.wearos.data.local.PendingSyncOperation
import com.gymplanner.wearos.data.security.DeviceIdentityStore
import com.gymplanner.wearos.data.security.WatchSessionManager
import java.io.IOException
import java.time.Instant
import kotlinx.coroutines.CancellationException
import retrofit2.Response

class RetrofitWatchRemoteDataSource(
    private val api: WatchApiService,
    private val sessionManager: WatchSessionManager,
    private val deviceIdentityStore: DeviceIdentityStore,
    private val gson: Gson,
) : WatchRemoteDataSource {
    override suspend fun pair(pairingCode: String): PairingResult {
        val response = try {
            api.pair(
                PairWatchRequest(
                    code = pairingCode,
                    deviceId = deviceIdentityStore.getOrCreateDeviceId(),
                    displayName = "WorkoutPlanner Wear",
                    deviceModel = Build.MODEL,
                    appVersion = BuildConfig.VERSION_NAME,
                ),
            )
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: IOException) {
            return PairingResult.Failure("Нет связи с сервером", retryable = true)
        }

        val tokens = response.body()
        if (response.isSuccessful && tokens != null) {
            return try {
                sessionManager.accept(tokens)
                PairingResult.Success
            } catch (_: Exception) {
                sessionManager.clear()
                PairingResult.Failure("Не удалось безопасно сохранить доступ", retryable = true)
            }
        }

        val problem = parseProblem(response)
        return when (response.code()) {
            400 -> PairingResult.Failure("Неверный код", retryable = false)
            409 -> PairingResult.Failure("Часы уже подключены к другому аккаунту", retryable = false)
            410 -> PairingResult.Failure("Срок действия кода истёк", retryable = false)
            408, 429 -> PairingResult.Failure(problem.detail ?: "Сервер временно недоступен", true)
            in 500..599 -> PairingResult.Failure("Сервер временно недоступен", true)
            else -> PairingResult.Failure(problem.detail ?: "Не удалось подключить часы", false)
        }
    }

    override suspend fun getActiveWorkout(): ActiveWorkoutResult {
        val token = try {
            sessionManager.getAccessToken()
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: IOException) {
            return ActiveWorkoutResult.Failure("Нет связи с сервером", true)
        } ?: return ActiveWorkoutResult.Unauthorized("Сессия часов недействительна")

        var response = executeActiveWorkout(token)
        if (response == null) return ActiveWorkoutResult.Failure("Нет связи с сервером", true)
        if (response.code() == 401) {
            val refreshed = refreshSession()
            if (refreshed == SessionRefreshResult.TransientFailure) {
                return ActiveWorkoutResult.Failure("Не удалось обновить сессию", true)
            }
            if (refreshed == SessionRefreshResult.Invalid) {
                return ActiveWorkoutResult.Unauthorized("Сессия часов истекла")
            }
            val refreshedToken = sessionManager.getAccessToken()
                ?: return ActiveWorkoutResult.Unauthorized("Сессия часов истекла")
            response = executeActiveWorkout(refreshedToken)
                ?: return ActiveWorkoutResult.Failure("Нет связи с сервером", true)
        }

        response.body()?.takeIf { response.isSuccessful }?.let {
            return ActiveWorkoutResult.Success(it)
        }
        return when (response.code()) {
            404 -> ActiveWorkoutResult.NoActiveWorkout()
            410 -> ActiveWorkoutResult.NoActiveWorkout(finished = true)
            401 -> ActiveWorkoutResult.Unauthorized("Сессия часов истекла")
            403 -> {
                sessionManager.clear()
                ActiveWorkoutResult.Unauthorized("Доступ часов отозван")
            }
            408, 429 -> ActiveWorkoutResult.Failure("Сервер временно недоступен", true)
            in 500..599 -> ActiveWorkoutResult.Failure("Сервер временно недоступен", true)
            else -> ActiveWorkoutResult.Failure(parseProblem(response).detail ?: "Ошибка загрузки", false)
        }
    }

    override suspend fun completeSet(operation: PendingSyncOperation): CompletionResult {
        val token = try {
            sessionManager.getAccessToken()
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: IOException) {
            return CompletionResult.TransientFailure("Нет связи с сервером")
        } ?: return CompletionResult.Unauthorized("Сессия часов недействительна")
        val clientVersion = runCatching {
            gson.fromJson(operation.payloadJson, CompletePayload::class.java).clientVersion
        }.getOrNull() ?: return CompletionResult.PermanentFailure("Некорректный payload CompleteSet")

        val response = try {
            api.completeSet(
                authorization = "Bearer $token",
                setId = operation.entityId,
                request = CompleteWatchSetRequest(
                    operationId = operation.operationId,
                    changedAtUtc = Instant.ofEpochMilli(operation.createdAtUtcMillis).toString(),
                    clientVersion = clientVersion,
                ),
            )
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: IOException) {
            return CompletionResult.TransientFailure("Нет связи с сервером")
        }

        response.body()?.takeIf { response.isSuccessful }?.let {
            return CompletionResult.Success(it.set.toAuthoritativeState())
        }
        if (response.code() == 409) {
            val conflict = parseConflict(response)
            return if (conflict?.code == "WORKOUT_SET_CONFLICT") {
                CompletionResult.Conflict(
                    set = conflict.set?.toAuthoritativeState(),
                    message = conflict.detail ?: "Подход изменён на сервере",
                )
            } else {
                CompletionResult.PermanentFailure(
                    conflict?.detail ?: "Operation ID уже использован",
                )
            }
        }
        return when (response.code()) {
            401 -> CompletionResult.Unauthorized("Сессия часов истекла")
            408, 429 -> CompletionResult.TransientFailure("Сервер временно недоступен")
            in 500..599 -> CompletionResult.TransientFailure("Сервер временно недоступен")
            403 -> {
                sessionManager.clear()
                CompletionResult.PermanentFailure("Доступ часов отозван")
            }
            400, 404, 410 -> CompletionResult.PermanentFailure(
                parseProblem(response).detail ?: "Операция отклонена сервером",
            )
            else -> CompletionResult.PermanentFailure("Не удалось синхронизировать подход")
        }
    }

    override suspend fun refreshSession(): SessionRefreshResult = try {
        if (sessionManager.forceRefresh()) SessionRefreshResult.Success
        else SessionRefreshResult.Invalid
    } catch (exception: CancellationException) {
        throw exception
    } catch (_: IOException) {
        SessionRefreshResult.TransientFailure
    }

    override fun clearSession() = sessionManager.clear()

    private suspend fun executeActiveWorkout(token: String): Response<WatchActiveWorkoutResponse>? =
        try {
            api.getActiveWorkout("Bearer $token")
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: IOException) {
            null
        }

    private fun parseProblem(response: Response<*>): ApiProblemDetails =
        parseError(response, ApiProblemDetails::class.java) ?: ApiProblemDetails(null, null, null)

    private fun parseConflict(response: Response<*>): WatchSetConflictResponse? =
        parseError(response, WatchSetConflictResponse::class.java)

    private fun <T> parseError(response: Response<*>, type: Class<T>): T? = runCatching {
        response.errorBody()?.charStream()?.use { gson.fromJson(it, type) }
    }.getOrNull()

    private fun WatchSetResponse.toAuthoritativeState() = AuthoritativeSetState(
        isCompleted = isCompleted,
        weightKilograms = weight,
        repetitions = repetitions,
        serverVersion = version,
    )

    private data class CompletePayload(val clientVersion: Long)
}
