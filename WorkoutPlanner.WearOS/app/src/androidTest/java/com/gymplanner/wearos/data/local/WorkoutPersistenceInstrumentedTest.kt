package com.gymplanner.wearos.data.local

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class WorkoutPersistenceInstrumentedTest {
    private val context: Context = ApplicationProvider.getApplicationContext()
    private val databaseName = "stage16-process-restart.db"

    @After
    fun cleanUp() {
        context.deleteDatabase(databaseName)
    }

    @Test
    fun pendingCompletionAndRestDeadline_surviveDatabaseReopen() = runBlocking {
        val initial = openDatabase()
        try {
            val database = initial
            val dao = database.workoutDao()
            dao.upsertSession(DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = 900))
            dao.cacheActiveWorkout(
                LocalWorkout(workoutId, "Restart workout", true, 900),
                listOf(LocalExercise(exerciseId, workoutId, "Жим", 0)),
                listOf(LocalWorkoutSet(setId, exerciseId, 0, 75.0, 8, false, 3)),
            )
            assertTrue(
                dao.completeSetAtomically(
                    workoutId = workoutId,
                    setId = setId,
                    endsAtUtcMillis = 11_000,
                    durationSeconds = 10,
                    operationId = operationId,
                    payloadJson = "{\"clientVersion\":3}",
                    createdAtUtcMillis = 1_000,
                ),
            )
        } finally {
            initial.close()
        }

        val reopened = openDatabase()
        try {
            val dao = reopened.workoutDao()
            val snapshot = dao.observeActiveWorkout().first()!!
            val operation = dao.observeOperations().first().single()

            assertTrue(snapshot.exercises.single().sets.single().isCompleted)
            assertEquals(setId, snapshot.workout.restCompletedSetId)
            assertEquals(11_000L, snapshot.workout.restEndsAtUtcMillis)
            assertEquals(operationId, operation.operationId)
            assertEquals(SyncOperationStatus.Pending, operation.status)
            assertEquals(3L, dao.getSet(setId)!!.serverVersion)
        } finally {
            reopened.close()
        }
    }

    private fun openDatabase(): WorkoutDatabase = Room.databaseBuilder(
        context,
        WorkoutDatabase::class.java,
        databaseName,
    ).addMigrations(WorkoutDatabase.migration1To2).build()

    private companion object {
        const val workoutId = 16L
        const val exerciseId = 160L
        const val setId = 1_600L
        const val operationId = "stage16-persisted-operation"
    }
}
