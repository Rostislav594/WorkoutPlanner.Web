package com.gymplanner.wearos.data.remote

import android.os.Build
import com.google.gson.Gson
import com.gymplanner.wearos.BuildConfig
import com.gymplanner.wearos.data.local.PendingSyncOperation
import com.gymplanner.wearos.data.local.SetMutationPayload
import com.gymplanner.wearos.data.local.SyncOperationType
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

    override suspend fun startPhonePairing(): StartPhonePairingResult {
        val response = try {
            api.startPairing(
                StartWatchPairingRequest(
                    deviceId = deviceIdentityStore.getOrCreateDeviceId(),
                    displayName = "WorkoutPlanner Wear",
                    deviceModel = Build.MODEL,
                    appVersion = BuildConfig.VERSION_NAME,
                ),
            )
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: IOException) {
            return StartPhonePairingResult.Failure("Нет связи с сервером", retryable = true)
        }

        val body = response.body()
        if (response.isSuccessful && body != null) {
            return StartPhonePairingResult.Success(
                requestId = body.requestId,
                pollToken = body.pollToken,
                approveUrl = body.approveUrl,
            )
        }

        val problem = parseProblem(response)
        return when (response.code()) {
            408, 429 -> StartPhonePairingResult.Failure(
                problem.detail ?: "Сервер временно недоступен",
                retryable = true,
            )
            in 500..599 -> StartPhonePairingResult.Failure(
                "Сервер временно недоступен",
                retryable = true,
            )
            else -> StartPhonePairingResult.Failure(
                problem.detail ?: "Не удалось начать подключение",
                retryable = false,
            )
        }
    }

    override suspend fun pollPhonePairing(
        requestId: String,
        pollToken: String,
    ): PhonePairingPollResult {
        val response = try {
            api.pairingStatus(WatchPairingStatusRequest(requestId, pollToken))
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: IOException) {
            return PhonePairingPollResult.Failure("Нет связи с сервером", retryable = true)
        }

        val body = response.body()
        if (response.isSuccessful && body != null) {
            return when (body.status) {
                WatchPairingStatuses.PENDING -> PhonePairingPollResult.Pending
                WatchPairingStatuses.REJECTED -> PhonePairingPollResult.Rejected
                WatchPairingStatuses.EXPIRED -> PhonePairingPollResult.Expired
                WatchPairingStatuses.APPROVED -> acceptTokens(body.tokens)
                else -> PhonePairingPollResult.Failure(
                    "Неизвестный ответ сервера",
                    retryable = false,
                )
            }
        }

        val problem = parseProblem(response)
        return when (response.code()) {
            404 -> PhonePairingPollResult.Expired
            409 -> PhonePairingPollResult.Failure(
                "Часы уже подключены к другому аккаунту",
                retryable = false,
            )
            408, 429 -> PhonePairingPollResult.Failure(
                problem.detail ?: "Сервер временно недоступен",
                retryable = true,
            )
            in 500..599 -> PhonePairingPollResult.Failure(
                "Сервер временно недоступен",
                retryable = true,
            )
            else -> PhonePairingPollResult.Failure(
                problem.detail ?: "Не удалось подключить часы",
                retryable = false,
            )
        }
    }

    private fun acceptTokens(tokens: WatchTokenResponse?): PhonePairingPollResult {
        if (tokens == null) {
            return PhonePairingPollResult.Failure(
                "Сервер не вернул доступ",
                retryable = false,
            )
        }
        return try {
            sessionManager.accept(tokens)
            PhonePairingPollResult.Approved
        } catch (_: Exception) {
            sessionManager.clear()
            PhonePairingPollResult.Failure(
                "Не удалось безопасно сохранить доступ",
                retryable = true,
            )
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

    override suspend fun sendSetMutation(operation: PendingSyncOperation): CompletionResult {
        val token = try {
            sessionManager.getAccessToken()
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: IOException) {
            return CompletionResult.TransientFailure("Нет связи с сервером")
        } ?: return CompletionResult.Unauthorized("Сессия часов недействительна")
        val payload = runCatching {
            gson.fromJson(operation.payloadJson, SetMutationPayload::class.java)
        }.getOrNull() ?: return CompletionResult.PermanentFailure("Некорректный payload операции")

        return try {
            val authorization = "Bearer $token"
            val changedAtUtc = Instant.ofEpochMilli(operation.createdAtUtcMillis).toString()
            when (operation.operationType) {
                SyncOperationType.CompleteSet -> {
                    val apiResponse = api.completeSet(
                        authorization = authorization,
                        setId = operation.entityId,
                        request = CompleteWatchSetRequest(
                            operationId = operation.operationId,
                            changedAtUtc = changedAtUtc,
                            clientVersion = payload.clientVersion,
                        ),
                    )
                    handleMutationResponse(apiResponse, apiResponse.body()?.set)
                }
                SyncOperationType.UndoSet -> {
                    val apiResponse = api.undoSet(
                        authorization = authorization,
                        setId = operation.entityId,
                        request = UndoWatchSetRequest(
                            operationId = operation.operationId,
                            changedAtUtc = changedAtUtc,
                            clientVersion = payload.clientVersion,
                        ),
                    )
                    handleMutationResponse(apiResponse, apiResponse.body()?.set)
                }
                SyncOperationType.UpdateWeight,
                SyncOperationType.UpdateReps,
                -> {
                    val weight = payload.weightKilograms
                        ?: return CompletionResult.PermanentFailure("В update отсутствует вес")
                    val repetitions = payload.repetitions
                        ?: return CompletionResult.PermanentFailure("В update отсутствуют повторы")
                    val apiResponse = api.updateSet(
                        authorization = authorization,
                        setId = operation.entityId,
                        request = UpdateWatchSetRequest(
                            operationId = operation.operationId,
                            actualWeight = weight,
                            actualReps = repetitions,
                            changedAtUtc = changedAtUtc,
                            clientVersion = payload.clientVersion,
                        ),
                    )
                    handleMutationResponse(apiResponse, apiResponse.body()?.set)
                }
            }
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: IOException) {
            CompletionResult.TransientFailure("Нет связи с сервером")
        }
    }

    override suspend fun finishWorkout(workoutId: Long): FinishWorkoutRemoteResult {
        val token = try {
            sessionManager.getAccessToken()
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: IOException) {
            return FinishWorkoutRemoteResult.TransientFailure("Нет связи с сервером")
        } ?: return FinishWorkoutRemoteResult.Unauthorized("Сессия часов недействительна")

        var response = executeFinishWorkout(workoutId, token)
            ?: return FinishWorkoutRemoteResult.TransientFailure("Нет связи с сервером")
        if (response.code() == 401) {
            when (refreshSession()) {
                SessionRefreshResult.TransientFailure ->
                    return FinishWorkoutRemoteResult.TransientFailure("Не удалось обновить сессию")
                SessionRefreshResult.Invalid ->
                    return FinishWorkoutRemoteResult.Unauthorized("Сессия часов истекла")
                SessionRefreshResult.Success -> Unit
            }
            val refreshedToken = sessionManager.getAccessToken()
                ?: return FinishWorkoutRemoteResult.Unauthorized("Сессия часов истекла")
            response = executeFinishWorkout(workoutId, refreshedToken)
                ?: return FinishWorkoutRemoteResult.TransientFailure("Нет связи с сервером")
        }

        response.body()?.takeIf { response.isSuccessful }?.let {
            return FinishWorkoutRemoteResult.Success(it.alreadyFinished)
        }
        return when (response.code()) {
            401 -> FinishWorkoutRemoteResult.Unauthorized("Сессия часов истекла")
            403 -> {
                sessionManager.clear()
                FinishWorkoutRemoteResult.Unauthorized("Доступ часов отозван")
            }
            408, 429 -> FinishWorkoutRemoteResult.TransientFailure(
                "Сервер временно недоступен",
            )
            in 500..599 -> FinishWorkoutRemoteResult.TransientFailure(
                "Сервер временно недоступен",
            )
            409 -> FinishWorkoutRemoteResult.PermanentFailure(
                parseProblem(response).detail ?: "Тренировка ещё не готова к завершению",
            )
            else -> FinishWorkoutRemoteResult.PermanentFailure(
                parseProblem(response).detail ?: "Не удалось завершить тренировку",
            )
        }
    }

    private fun handleMutationResponse(
        response: Response<*>,
        set: WatchSetResponse?,
    ): CompletionResult {
        if (response.isSuccessful && set != null) {
            return CompletionResult.Success(set.toAuthoritativeState())
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
                CompletionResult.Unauthorized("Доступ часов отозван")
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

    private suspend fun executeFinishWorkout(
        workoutId: Long,
        token: String,
    ): Response<FinishWatchWorkoutResponse>? = try {
        api.finishWorkout("Bearer $token", workoutId)
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

}
