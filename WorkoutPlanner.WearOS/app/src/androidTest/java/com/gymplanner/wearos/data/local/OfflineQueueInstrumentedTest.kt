package com.gymplanner.wearos.data.local

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.google.gson.Gson
import com.gymplanner.wearos.data.repository.OfflineFirstWorkoutRepository
import com.gymplanner.wearos.data.remote.CompletionResult
import com.gymplanner.wearos.data.remote.ActiveWorkoutResult
import com.gymplanner.wearos.data.remote.AuthoritativeSetState
import com.gymplanner.wearos.data.remote.PairingResult
import com.gymplanner.wearos.data.remote.PhonePairingPollResult
import com.gymplanner.wearos.data.remote.StartPhonePairingResult
import com.gymplanner.wearos.data.remote.SessionRefreshResult
import com.gymplanner.wearos.data.remote.WatchActiveWorkoutResponse
import com.gymplanner.wearos.data.remote.WatchExerciseResponse
import com.gymplanner.wearos.data.remote.WatchSetResponse
import com.gymplanner.wearos.data.remote.WatchRemoteDataSource
import com.gymplanner.wearos.data.remote.FinishWorkoutRemoteResult
import com.gymplanner.wearos.data.sync.QueueDrainResult
import com.gymplanner.wearos.data.sync.SyncQueueProcessor
import com.gymplanner.wearos.data.sync.SyncScheduler
import com.gymplanner.wearos.domain.model.CompletedWorkoutKind
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.phone.PhoneLinkOpener
import com.gymplanner.wearos.domain.phone.PhoneLinkResult
import com.gymplanner.wearos.domain.repository.FinishWorkoutResult
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.cancel
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class OfflineQueueInstrumentedTest {
    private lateinit var database: WorkoutDatabase
    private lateinit var dao: WorkoutDao

    /**
     * Репозиторий держит фоновую подписку на DAO. Без отмены она переживает
     * закрытие базы и падает уже в следующем тесте на закрытом пуле соединений.
     */
    private val repositoryScopes = mutableListOf<CoroutineScope>()

    @Before
    fun setUp() {
        database = Room.inMemoryDatabaseBuilder(
            ApplicationProvider.getApplicationContext(),
            WorkoutDatabase::class.java,
        ).build()
        dao = database.workoutDao()
    }

    @After
    fun tearDown() {
        repositoryScopes.forEach { it.cancel() }
        repositoryScopes.clear()
        database.close()
    }

    /** Репозиторий с подпиской, которую тест обязательно отменит. */
    private fun createRepository(remote: WatchRemoteDataSource): OfflineFirstWorkoutRepository {
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.Unconfined)
        repositoryScopes += scope
        return OfflineFirstWorkoutRepository(
            workoutDao = dao,
            remoteDataSource = remote,
            syncScheduler = NoOpSyncScheduler,
            syncQueueProcessor = SyncQueueProcessor(dao, remote),
            phoneLinkOpener = NoPhoneLinkOpener,
            gson = Gson(),
            scope = scope,
        )
    }

    @Test
    fun completionPersistsSetAndOneOperationAtomically() = runBlocking {
        seedWorkout()

        assertTrue(completeFirstSet("operation-one"))
        assertFalse(completeFirstSet("operation-two"))

        assertTrue(dao.getSet(firstSetId)!!.isCompleted)
        val operations = dao.observeOperations().first()
        assertEquals(1, operations.size)
        assertEquals("operation-one", operations.single().operationId)
        assertEquals(SyncOperationStatus.Pending, operations.single().status)
    }

    @Test
    fun transientRetryReusesOperationAndDoesNotResendAfterSuccess() = runBlocking {
        seedWorkout()
        completeFirstSet("stable-operation-id")
        val remote = RecordingRemoteDataSource(
            ArrayDeque(
                listOf(
                    CompletionResult.TransientFailure("offline"),
                    CompletionResult.Success(AuthoritativeSetState(true, 80.0, 8, 1)),
                ),
            ),
        )
        val processor = SyncQueueProcessor(dao, remote, wallClockMillis = { 2_000 })

        assertEquals(QueueDrainResult.Retry, processor.drain())
        val failedAttempt = dao.observeOperations().first().single()
        assertEquals(SyncOperationStatus.Failed, failedAttempt.status)
        assertEquals(1, failedAttempt.attemptCount)
        assertEquals(QueueDrainResult.Complete, processor.drain())
        assertEquals(QueueDrainResult.Complete, processor.drain())

        assertEquals(listOf("stable-operation-id", "stable-operation-id"), remote.operationIds)
        assertTrue(dao.observeOperations().first().isEmpty())
        assertEquals(1, dao.getSet(firstSetId)!!.serverVersion)
    }

    @Test
    fun conflictAppliesAuthoritativeSetAndStopsRetrying() = runBlocking {
        seedWorkout()
        completeFirstSet("conflicted-operation")
        val remote = RecordingRemoteDataSource(
            ArrayDeque(
                listOf(
                    CompletionResult.Conflict(
                        set = AuthoritativeSetState(false, 82.5, 6, 4),
                        message = "stale version",
                    ),
                ),
            ),
        )

        assertEquals(QueueDrainResult.Complete, SyncQueueProcessor(dao, remote).drain())
        assertFalse(dao.getSet(firstSetId)!!.isCompleted)
        assertEquals(4, dao.getSet(firstSetId)!!.serverVersion)
        assertEquals(SyncOperationStatus.Conflict, dao.observeOperations().first().single().status)
        assertEquals(QueueDrainResult.Complete, SyncQueueProcessor(dao, remote).drain())
        assertEquals(1, remote.operationIds.size)
    }

    @Test
    fun compatibleUpdatesCoalesceIntoOnePendingOperation() = runBlocking {
        seedWorkout()
        val first = pendingOperation(
            operationId = "weight-update",
            operationType = SyncOperationType.UpdateWeight,
            sequenceNumber = 1,
            payloadJson = "{\"clientVersion\":0,\"weightKilograms\":82.5,\"repetitions\":8}",
        )
        assertTrue(
            dao.updateSetAndEnqueueAtomically(
                firstSetId,
                82.5,
                8,
                first,
                emptyList(),
            ),
        )
        val replacement = pendingOperation(
            operationId = "repetitions-update",
            operationType = SyncOperationType.UpdateReps,
            sequenceNumber = first.sequenceNumber,
            payloadJson = "{\"clientVersion\":0,\"weightKilograms\":82.5,\"repetitions\":10}",
        )
        assertTrue(
            dao.updateSetAndEnqueueAtomically(
                firstSetId,
                82.5,
                10,
                replacement,
                listOf(first.operationId),
            ),
        )

        val operation = dao.observeOperations().first().single()
        assertEquals("repetitions-update", operation.operationId)
        assertEquals(SyncOperationType.UpdateReps, operation.operationType)
        assertEquals(1, operation.sequenceNumber)
        assertEquals(82.5, dao.getSet(firstSetId)!!.weightKilograms)
        assertEquals(10, dao.getSet(firstSetId)!!.repetitions)
    }

    @Test
    fun pendingCompletionAndUndoCancelWithoutServerMutation() = runBlocking {
        seedWorkout()
        assertTrue(completeFirstSet("pending-completion"))

        assertTrue(
            dao.undoSetAtomically(
                setId = firstSetId,
                operation = null,
                cancelledCompleteOperationId = "pending-completion",
            ),
        )

        assertFalse(dao.getSet(firstSetId)!!.isCompleted)
        assertTrue(dao.observeOperations().first().isEmpty())
    }

    @Test
    fun successfulExtendedMutationAppliesServerStateAndLeavesNoAcknowledgedRow() = runBlocking {
        seedWorkout()
        val operation = pendingOperation(
            operationId = "update-online",
            operationType = SyncOperationType.UpdateWeight,
            sequenceNumber = 1,
            payloadJson = "{\"clientVersion\":0,\"weightKilograms\":85.0,\"repetitions\":7}",
        )
        dao.insertOperation(operation)
        val remote = RecordingRemoteDataSource(
            ArrayDeque(
                listOf(
                    CompletionResult.Success(AuthoritativeSetState(false, 85.0, 7, 1)),
                ),
            ),
        )

        assertEquals(QueueDrainResult.Complete, SyncQueueProcessor(dao, remote).drain())

        assertEquals(listOf("update-online"), remote.operationIds)
        assertEquals(listOf(SyncOperationType.UpdateWeight), remote.operationTypes)
        assertEquals(85.0, dao.getSet(firstSetId)!!.weightKilograms)
        assertEquals(7, dao.getSet(firstSetId)!!.repetitions)
        assertEquals(1, dao.getSet(firstSetId)!!.serverVersion)
        assertTrue(dao.observeOperations().first().isEmpty())
    }

    @Test
    fun conflictMarksLaterOperationsForSameSetAndDoesNotSendThem() = runBlocking {
        seedWorkout()
        dao.insertOperation(
            pendingOperation("first-conflict", SyncOperationType.UndoSet, 1),
        )
        dao.insertOperation(
            pendingOperation("stale-after-undo", SyncOperationType.UpdateWeight, 2),
        )
        val remote = RecordingRemoteDataSource(
            ArrayDeque(
                listOf(
                    CompletionResult.Conflict(
                        set = AuthoritativeSetState(true, 80.0, 8, 4),
                        message = "stale version",
                    ),
                ),
            ),
        )

        assertEquals(QueueDrainResult.Complete, SyncQueueProcessor(dao, remote).drain())

        assertEquals(listOf("first-conflict"), remote.operationIds)
        val operations = dao.observeOperations().first()
        assertEquals(2, operations.size)
        assertTrue(operations.all { it.status == SyncOperationStatus.Conflict })
        assertFalse(operations.last().canRetry)
    }

    @Test
    fun invalidSessionReturnsClientToPairing() = runBlocking {
        seedWorkout()
        dao.upsertSession(DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = 1_000))
        completeFirstSet("unauthorized-operation")
        val remote = RecordingRemoteDataSource(
            ArrayDeque(listOf(CompletionResult.Unauthorized("revoked"))),
            refreshResult = SessionRefreshResult.Invalid,
        )

        assertEquals(QueueDrainResult.Complete, SyncQueueProcessor(dao, remote).drain())
        assertFalse(dao.observeDeviceSession().first()!!.isPaired)
        assertEquals(SyncOperationStatus.Failed, dao.observeOperations().first().single().status)
    }

    @Test
    fun finishDrainsPendingMutationsBeforeServerFinishAndThenClearsLocalWorkout() = runBlocking {
        seedReadyWorkout("finish-pending")
        val remote = RecordingRemoteDataSource(
            ArrayDeque(
                listOf(
                    CompletionResult.Success(AuthoritativeSetState(true, 80.0, 8, 1)),
                ),
            ),
        )
        val processor = SyncQueueProcessor(dao, remote)
        val repository = createRepository(remote)
        withTimeout(2_000) {
            repository.state.first { it is MockWorkoutState.ReadyToFinish }
        }

        assertEquals(FinishWorkoutResult.Success, repository.finishWorkout())

        assertEquals(listOf("set:finish-pending", "finish:$workoutId"), remote.calls)
        assertFalse(dao.getWorkout(workoutId)!!.isActive)
        assertTrue(dao.observeOperations().first().isEmpty())
    }

    @Test
    fun finishKeepsWorkoutActiveWhenSynchronizationHasConflict() = runBlocking {
        seedReadyWorkout("finish-conflict")
        val remote = RecordingRemoteDataSource(
            ArrayDeque(
                listOf(
                    CompletionResult.Conflict(
                        AuthoritativeSetState(true, 80.0, 8, 2),
                        "stale version",
                    ),
                ),
            ),
        )
        val repository = createRepository(remote)
        withTimeout(2_000) {
            repository.state.first { it is MockWorkoutState.ReadyToFinish }
        }

        assertEquals(FinishWorkoutResult.UnresolvedOperations, repository.finishWorkout())

        assertEquals(listOf("set:finish-conflict"), remote.calls)
        assertTrue(dao.getWorkout(workoutId)!!.isActive)
        assertEquals(SyncOperationStatus.Conflict, dao.observeOperations().first().single().status)
    }

    @Test
    fun pairing_clearsPreviousOwnersCacheAndLoadsWorkoutWithoutManualRefresh() = runBlocking {
        // На часах лежит тренировка прошлого владельца вместе с её очередью.
        seedReadyWorkout("stale-operation")

        val remote = PairingRemoteDataSource(
            workout = WatchActiveWorkoutResponse(
                workoutId = 777,
                workoutName = "День груди",
                scheduledDate = "2026-09-14T00:00:00",
                startedAtUtc = null,
                exercises = listOf(
                    WatchExerciseResponse(
                        exerciseId = 778,
                        name = "Жим штанги лежа",
                        order = 0,
                        sets = listOf(
                            WatchSetResponse(779, 1, 120.0, 8, false, false, 1),
                        ),
                    ),
                ),
                currentExerciseId = 778,
                currentSetId = 779,
            ),
        )
        val repository = createRepository(remote)
        withTimeout(2_000) {
            repository.state.first { it is MockWorkoutState.Pairing }
        }

        repository.pair("123456")

        // Чужая тренировка и её неотправленные операции стёрты.
        assertNull(dao.getWorkout(workoutId))
        assertTrue(dao.observeOperations().first().none { it.operationId == "stale-operation" })

        // Новая тренировка подтянулась сама, без нажатия «Обновить».
        val current = withTimeout(2_000) {
            repository.state.first { it is MockWorkoutState.CurrentSet }
        } as MockWorkoutState.CurrentSet
        assertEquals("Жим штанги лежа", current.exerciseName)
        assertEquals(777, dao.getWorkout(777)!!.workoutId)
    }

    @Test
    fun finishingFreeWorkout_reportsFreeCompletionSoPersonIsSentToThePhone() = runBlocking {
        // Свободная тренировка отличается только видом: подходы уже закрыты,
        // очередь пуста, остаётся нажать «Завершить».
        dao.cacheActiveWorkout(
            LocalWorkout(workoutId, "Свободная тренировка", true, 1_000, isFree = true),
            listOf(LocalExercise(exerciseId, workoutId, "Жим лёжа", 0)),
            listOf(LocalWorkoutSet(firstSetId, exerciseId, 0, 80.0, 8, true, 0)),
        )
        dao.upsertSession(DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = 1_000))

        val remote = RecordingRemoteDataSource(ArrayDeque())
        val repository = createRepository(remote)
        withTimeout(2_000) {
            repository.state.first { it is MockWorkoutState.ReadyToFinish }
        }

        assertEquals(FinishWorkoutResult.Success, repository.finishWorkout())

        val completed = withTimeout(2_000) {
            repository.state.first { it is MockWorkoutState.Completed }
        } as MockWorkoutState.Completed
        assertEquals(CompletedWorkoutKind.Free, completed.kind)
    }

    @Test
    fun finishingScheduledWorkout_reportsScheduledCompletion() = runBlocking {
        dao.cacheActiveWorkout(
            LocalWorkout(workoutId, "День груди", true, 1_000),
            listOf(LocalExercise(exerciseId, workoutId, "Жим лёжа", 0)),
            listOf(LocalWorkoutSet(firstSetId, exerciseId, 0, 80.0, 8, true, 0)),
        )
        dao.upsertSession(DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = 1_000))

        val remote = RecordingRemoteDataSource(ArrayDeque())
        val repository = createRepository(remote)
        withTimeout(2_000) {
            repository.state.first { it is MockWorkoutState.ReadyToFinish }
        }

        assertEquals(FinishWorkoutResult.Success, repository.finishWorkout())

        val completed = withTimeout(2_000) {
            repository.state.first { it is MockWorkoutState.Completed }
        } as MockWorkoutState.Completed
        assertEquals(CompletedWorkoutKind.Scheduled, completed.kind)
    }

    private suspend fun seedWorkout() {
        dao.cacheActiveWorkout(
            LocalWorkout(workoutId, "Тренировка", true, 1_000),
            listOf(LocalExercise(exerciseId, workoutId, "Жим лёжа", 0)),
            listOf(LocalWorkoutSet(firstSetId, exerciseId, 0, 80.0, 8, false, 0)),
        )
    }

    private suspend fun seedReadyWorkout(operationId: String) {
        seedWorkout()
        dao.upsertSession(DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = 1_000))
        check(dao.markSetCompleted(firstSetId) == 1)
        dao.insertOperation(
            pendingOperation(
                operationId = operationId,
                operationType = SyncOperationType.CompleteSet,
                sequenceNumber = 1,
            ),
        )
    }

    private suspend fun completeFirstSet(operationId: String): Boolean = dao.completeSetAtomically(
        workoutId = workoutId,
        setId = firstSetId,
        endsAtUtcMillis = 11_000,
        durationSeconds = 10,
        operationId = operationId,
        payloadJson = "{\"setId\":$firstSetId,\"clientVersion\":0}",
        createdAtUtcMillis = 1_000,
    )

    private fun pendingOperation(
        operationId: String,
        operationType: SyncOperationType,
        sequenceNumber: Long,
        payloadJson: String = "{\"clientVersion\":0}",
    ) = PendingSyncOperation(
        operationId = operationId,
        operationType = operationType,
        entityId = firstSetId,
        payloadJson = payloadJson,
        createdAtUtcMillis = 1_000,
        sequenceNumber = sequenceNumber,
    )

    /** Отдаёт готовую тренировку сразу после успешного сопряжения. */
    private class PairingRemoteDataSource(
        private val workout: WatchActiveWorkoutResponse,
    ) : WatchRemoteDataSource {
        override suspend fun pair(pairingCode: String): PairingResult = PairingResult.Success

        override suspend fun startPhonePairing(): StartPhonePairingResult =
            StartPhonePairingResult.Failure("Не используется", retryable = false)

        override suspend fun pollPhonePairing(
            requestId: String,
            pollToken: String,
        ): PhonePairingPollResult =
            PhonePairingPollResult.Failure("Не используется", retryable = false)

        override suspend fun getActiveWorkout(): ActiveWorkoutResult =
            ActiveWorkoutResult.Success(workout)

        override suspend fun sendSetMutation(operation: PendingSyncOperation): CompletionResult =
            CompletionResult.TransientFailure("Не используется")

        override suspend fun finishWorkout(workoutId: Long): FinishWorkoutRemoteResult =
            FinishWorkoutRemoteResult.TransientFailure("Не используется")

        override suspend fun refreshSession(): SessionRefreshResult = SessionRefreshResult.Success

        override fun clearSession() = Unit
    }

    /** Телефон в инструментальных тестах не участвует. */
    private object NoPhoneLinkOpener : PhoneLinkOpener {
        override suspend fun open(url: String): PhoneLinkResult =
            PhoneLinkResult.NoPhoneAvailable
    }

    private class RecordingRemoteDataSource(
        private val results: ArrayDeque<CompletionResult>,
        private val refreshResult: SessionRefreshResult = SessionRefreshResult.Success,
    ) : WatchRemoteDataSource {
        val operationIds = mutableListOf<String>()
        val operationTypes = mutableListOf<SyncOperationType>()
        val calls = mutableListOf<String>()

        override suspend fun pair(pairingCode: String): PairingResult = PairingResult.Success

        override suspend fun startPhonePairing(): StartPhonePairingResult =
            StartPhonePairingResult.Failure("Не используется", retryable = false)

        override suspend fun pollPhonePairing(
            requestId: String,
            pollToken: String,
        ): PhonePairingPollResult =
            PhonePairingPollResult.Failure("Не используется", retryable = false)

        override suspend fun getActiveWorkout(): ActiveWorkoutResult =
            ActiveWorkoutResult.NoActiveWorkout()

        override suspend fun sendSetMutation(operation: PendingSyncOperation): CompletionResult {
            operationIds += operation.operationId
            operationTypes += operation.operationType
            calls += "set:${operation.operationId}"
            return results.removeFirst()
        }

        override suspend fun finishWorkout(workoutId: Long): FinishWorkoutRemoteResult {
            calls += "finish:$workoutId"
            return FinishWorkoutRemoteResult.Success(alreadyFinished = false)
        }

        override suspend fun refreshSession(): SessionRefreshResult = refreshResult

        override fun clearSession() = Unit
    }

    private companion object {
        val NoOpSyncScheduler = object : SyncScheduler {
            override fun schedule() = Unit
        }
        const val workoutId = 1L
        const val exerciseId = 11L
        const val firstSetId = 101L
    }
}
