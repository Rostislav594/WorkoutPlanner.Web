package com.gymplanner.wearos.data.remote

import android.os.Build
import com.google.gson.Gson
import com.gymplanner.wearos.BuildConfig
import com.gymplanner.wearos.R
import com.gymplanner.wearos.data.localization.WatchStrings
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
    private val strings: WatchStrings,
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
            return PairingResult.Failure(strings.get(R.string.error_no_connection), retryable = true)
        }

        val tokens = response.body()
        if (response.isSuccessful && tokens != null) {
            return try {
                sessionManager.accept(tokens)
                PairingResult.Success
            } catch (_: Exception) {
                sessionManager.clear()
                PairingResult.Failure(strings.get(R.string.error_secure_store_failed), retryable = true)
            }
        }

        val problem = parseProblem(response)
        return when (response.code()) {
            400 -> PairingResult.Failure(strings.get(R.string.error_invalid_code), retryable = false)
            409 -> PairingResult.Failure(strings.get(R.string.error_watch_other_account), retryable = false)
            410 -> PairingResult.Failure(strings.get(R.string.error_code_expired), retryable = false)
            408, 429 -> PairingResult.Failure(problem.detail ?: strings.get(R.string.error_server_unavailable), true)
            in 500..599 -> PairingResult.Failure(strings.get(R.string.error_server_unavailable), true)
            else -> PairingResult.Failure(problem.detail ?: strings.get(R.string.error_pairing_failed), false)
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
            return StartPhonePairingResult.Failure(strings.get(R.string.error_no_connection), retryable = true)
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
                problem.detail ?: strings.get(R.string.error_server_unavailable),
                retryable = true,
            )
            in 500..599 -> StartPhonePairingResult.Failure(
                strings.get(R.string.error_server_unavailable),
                retryable = true,
            )
            else -> StartPhonePairingResult.Failure(
                problem.detail ?: strings.get(R.string.error_pairing_start_failed),
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
            return PhonePairingPollResult.Failure(strings.get(R.string.error_no_connection), retryable = true)
        }

        val body = response.body()
        if (response.isSuccessful && body != null) {
            return when (body.status) {
                WatchPairingStatuses.PENDING -> PhonePairingPollResult.Pending
                WatchPairingStatuses.REJECTED -> PhonePairingPollResult.Rejected
                WatchPairingStatuses.EXPIRED -> PhonePairingPollResult.Expired
                WatchPairingStatuses.APPROVED -> acceptTokens(body.tokens)
                else -> PhonePairingPollResult.Failure(
                    strings.get(R.string.error_unknown_response),
                    retryable = false,
                )
            }
        }

        val problem = parseProblem(response)
        return when (response.code()) {
            404 -> PhonePairingPollResult.Expired
            409 -> PhonePairingPollResult.Failure(
                strings.get(R.string.error_watch_other_account),
                retryable = false,
            )
            408, 429 -> PhonePairingPollResult.Failure(
                problem.detail ?: strings.get(R.string.error_server_unavailable),
                retryable = true,
            )
            in 500..599 -> PhonePairingPollResult.Failure(
                strings.get(R.string.error_server_unavailable),
                retryable = true,
            )
            else -> PhonePairingPollResult.Failure(
                problem.detail ?: strings.get(R.string.error_pairing_failed),
                retryable = false,
            )
        }
    }

    private fun acceptTokens(tokens: WatchTokenResponse?): PhonePairingPollResult {
        if (tokens == null) {
            return PhonePairingPollResult.Failure(
                strings.get(R.string.error_no_access_returned),
                retryable = false,
            )
        }
        return try {
            sessionManager.accept(tokens)
            PhonePairingPollResult.Approved
        } catch (_: Exception) {
            sessionManager.clear()
            PhonePairingPollResult.Failure(
                strings.get(R.string.error_secure_store_failed),
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
            return ActiveWorkoutResult.Failure(strings.get(R.string.error_no_connection), true)
        } ?: return ActiveWorkoutResult.Unauthorized(strings.get(R.string.error_session_invalid))

        var response = executeActiveWorkout(token)
        if (response == null) return ActiveWorkoutResult.Failure(strings.get(R.string.error_no_connection), true)
        if (response.code() == 401) {
            val refreshed = refreshSession()
            if (refreshed == SessionRefreshResult.TransientFailure) {
                return ActiveWorkoutResult.Failure(strings.get(R.string.error_session_refresh_failed), true)
            }
            if (refreshed == SessionRefreshResult.Invalid) {
                return ActiveWorkoutResult.Unauthorized(strings.get(R.string.error_session_expired))
            }
            val refreshedToken = sessionManager.getAccessToken()
                ?: return ActiveWorkoutResult.Unauthorized(strings.get(R.string.error_session_expired))
            response = executeActiveWorkout(refreshedToken)
                ?: return ActiveWorkoutResult.Failure(strings.get(R.string.error_no_connection), true)
        }

        response.body()?.takeIf { response.isSuccessful }?.let {
            return ActiveWorkoutResult.Success(it)
        }
        return when (response.code()) {
            404 -> ActiveWorkoutResult.NoActiveWorkout()
            410 -> ActiveWorkoutResult.NoActiveWorkout(finished = true)
            401 -> ActiveWorkoutResult.Unauthorized(strings.get(R.string.error_session_expired))
            403 -> {
                sessionManager.clear()
                ActiveWorkoutResult.Unauthorized(strings.get(R.string.error_access_revoked))
            }
            408, 429 -> ActiveWorkoutResult.Failure(strings.get(R.string.error_server_unavailable), true)
            in 500..599 -> ActiveWorkoutResult.Failure(strings.get(R.string.error_server_unavailable), true)
            else -> ActiveWorkoutResult.Failure(parseProblem(response).detail ?: strings.get(R.string.error_load_failed), false)
        }
    }

    override suspend fun sendSetMutation(operation: PendingSyncOperation): CompletionResult {
        val token = try {
            sessionManager.getAccessToken()
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: IOException) {
            return CompletionResult.TransientFailure(strings.get(R.string.error_no_connection))
        } ?: return CompletionResult.Unauthorized(strings.get(R.string.error_session_invalid))
        val payload = runCatching {
            gson.fromJson(operation.payloadJson, SetMutationPayload::class.java)
        }.getOrNull() ?: return CompletionResult.PermanentFailure(strings.get(R.string.error_bad_payload))

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
                        ?: return CompletionResult.PermanentFailure(strings.get(R.string.error_update_missing_weight))
                    val repetitions = payload.repetitions
                        ?: return CompletionResult.PermanentFailure(strings.get(R.string.error_update_missing_reps))
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
            CompletionResult.TransientFailure(strings.get(R.string.error_no_connection))
        }
    }

    override suspend fun finishWorkout(workoutId: Long): FinishWorkoutRemoteResult {
        val token = try {
            sessionManager.getAccessToken()
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: IOException) {
            return FinishWorkoutRemoteResult.TransientFailure(strings.get(R.string.error_no_connection))
        } ?: return FinishWorkoutRemoteResult.Unauthorized(strings.get(R.string.error_session_invalid))

        var response = executeFinishWorkout(workoutId, token)
            ?: return FinishWorkoutRemoteResult.TransientFailure(strings.get(R.string.error_no_connection))
        if (response.code() == 401) {
            when (refreshSession()) {
                SessionRefreshResult.TransientFailure ->
                    return FinishWorkoutRemoteResult.TransientFailure(strings.get(R.string.error_session_refresh_failed))
                SessionRefreshResult.Invalid ->
                    return FinishWorkoutRemoteResult.Unauthorized(strings.get(R.string.error_session_expired))
                SessionRefreshResult.Success -> Unit
            }
            val refreshedToken = sessionManager.getAccessToken()
                ?: return FinishWorkoutRemoteResult.Unauthorized(strings.get(R.string.error_session_expired))
            response = executeFinishWorkout(workoutId, refreshedToken)
                ?: return FinishWorkoutRemoteResult.TransientFailure(strings.get(R.string.error_no_connection))
        }

        response.body()?.takeIf { response.isSuccessful }?.let {
            return FinishWorkoutRemoteResult.Success(it.alreadyFinished)
        }
        return when (response.code()) {
            401 -> FinishWorkoutRemoteResult.Unauthorized(strings.get(R.string.error_session_expired))
            403 -> {
                sessionManager.clear()
                FinishWorkoutRemoteResult.Unauthorized(strings.get(R.string.error_access_revoked))
            }
            408, 429 -> FinishWorkoutRemoteResult.TransientFailure(
                strings.get(R.string.error_server_unavailable),
            )
            in 500..599 -> FinishWorkoutRemoteResult.TransientFailure(
                strings.get(R.string.error_server_unavailable),
            )
            409 -> FinishWorkoutRemoteResult.PermanentFailure(
                parseProblem(response).detail ?: strings.get(R.string.error_workout_not_ready),
            )
            else -> FinishWorkoutRemoteResult.PermanentFailure(
                parseProblem(response).detail ?: strings.get(R.string.error_finish_failed),
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
                    message = conflict.detail ?: strings.get(R.string.error_set_changed_on_server),
                )
            } else {
                CompletionResult.PermanentFailure(
                    conflict?.detail ?: strings.get(R.string.error_operation_id_used),
                )
            }
        }
        return when (response.code()) {
            401 -> CompletionResult.Unauthorized(strings.get(R.string.error_session_expired))
            408, 429 -> CompletionResult.TransientFailure(strings.get(R.string.error_server_unavailable))
            in 500..599 -> CompletionResult.TransientFailure(strings.get(R.string.error_server_unavailable))
            403 -> {
                sessionManager.clear()
                CompletionResult.Unauthorized(strings.get(R.string.error_access_revoked))
            }
            400, 404, 410 -> CompletionResult.PermanentFailure(
                parseProblem(response).detail ?: strings.get(R.string.error_operation_rejected),
            )
            else -> CompletionResult.PermanentFailure(strings.get(R.string.error_set_sync_failed))
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
