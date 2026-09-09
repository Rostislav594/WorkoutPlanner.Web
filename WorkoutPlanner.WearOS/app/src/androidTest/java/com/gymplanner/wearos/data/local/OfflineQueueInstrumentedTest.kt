package com.gymplanner.wearos.data.local

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.gymplanner.wearos.data.remote.CompletionResult
import com.gymplanner.wearos.data.remote.ActiveWorkoutResult
import com.gymplanner.wearos.data.remote.AuthoritativeSetState
import com.gymplanner.wearos.data.remote.PairingResult
import com.gymplanner.wearos.data.remote.SessionRefreshResult
import com.gymplanner.wearos.data.remote.WatchRemoteDataSource
import com.gymplanner.wearos.data.sync.QueueDrainResult
import com.gymplanner.wearos.data.sync.SyncQueueProcessor
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class OfflineQueueInstrumentedTest {
    private lateinit var database: WorkoutDatabase
    private lateinit var dao: WorkoutDao

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
        database.close()
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
        assertEquals(QueueDrainResult.Complete, processor.drain())
        assertEquals(QueueDrainResult.Complete, processor.drain())

        assertEquals(listOf("stable-operation-id", "stable-operation-id"), remote.operationIds)
        val operation = dao.observeOperations().first().single()
        assertEquals(SyncOperationStatus.Synced, operation.status)
        assertEquals(2, operation.attemptCount)
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

    private suspend fun seedWorkout() {
        dao.cacheActiveWorkout(
            LocalWorkout(workoutId, true, 1_000),
            listOf(LocalExercise(exerciseId, workoutId, "Жим лёжа", 0)),
            listOf(LocalWorkoutSet(firstSetId, exerciseId, 0, 80.0, 8, false, 0)),
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

    private class RecordingRemoteDataSource(
        private val results: ArrayDeque<CompletionResult>,
    ) : WatchRemoteDataSource {
        val operationIds = mutableListOf<String>()

        override suspend fun pair(pairingCode: String): PairingResult = PairingResult.Success

        override suspend fun getActiveWorkout(): ActiveWorkoutResult =
            ActiveWorkoutResult.NoActiveWorkout()

        override suspend fun completeSet(operation: PendingSyncOperation): CompletionResult {
            operationIds += operation.operationId
            return results.removeFirst()
        }

        override suspend fun refreshSession(): SessionRefreshResult = SessionRefreshResult.Success

        override fun clearSession() = Unit
    }

    private companion object {
        const val workoutId = 1L
        const val exerciseId = 11L
        const val firstSetId = 101L
    }
}
