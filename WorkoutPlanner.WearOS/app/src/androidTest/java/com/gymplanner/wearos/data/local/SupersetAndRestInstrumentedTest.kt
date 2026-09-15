package com.gymplanner.wearos.data.local

import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.google.gson.Gson
import com.gymplanner.wearos.data.remote.ActiveWorkoutResult
import com.gymplanner.wearos.data.remote.CompletionResult
import com.gymplanner.wearos.data.remote.FinishWorkoutRemoteResult
import com.gymplanner.wearos.data.remote.PairingResult
import com.gymplanner.wearos.data.remote.PhonePairingPollResult
import com.gymplanner.wearos.data.remote.SessionRefreshResult
import com.gymplanner.wearos.data.remote.StartPhonePairingResult
import com.gymplanner.wearos.data.remote.WatchRemoteDataSource
import com.gymplanner.wearos.data.repository.OfflineFirstWorkoutRepository
import com.gymplanner.wearos.data.sync.SyncQueueProcessor
import com.gymplanner.wearos.data.sync.SyncScheduler
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.phone.PhoneLinkOpener
import com.gymplanner.wearos.domain.phone.PhoneLinkResult
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Порядок подходов в суперсете и выбор длительности отдыха.
 *
 * Тренировка: суперсет из «Жим лёжа» и «Тяга» по два подхода, следом обычный
 * «Присед». Ожидаемый путь и паузы между подходами:
 *
 * Жим 1 → (без отдыха) Тяга 1 → (между подходами) Жим 2 → (без отдыха)
 * Тяга 2 → (между упражнениями) Присед 1 → (конец, отдыха нет).
 */
@RunWith(AndroidJUnit4::class)
class SupersetAndRestInstrumentedTest {
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
    fun supersetAlternatesSetsAndPicksMatchingRest() = runBlocking {
        seedSupersetWorkout()
        val repository = newRepository()

        assertCurrent(repository, "Жим лёжа", setNumber = 1)
        repository.completeCurrentSet()

        // Сразу второе упражнение суперсета, экрана отдыха между ними нет.
        assertCurrent(repository, "Тяга", setNumber = 1)
        repository.completeCurrentSet()

        // Круг закрыт — отдых между подходами, длительность из профиля.
        assertRest(repository, expectedSeconds = restBetweenSets)
        repository.finishRest()

        assertCurrent(repository, "Жим лёжа", setNumber = 2)
        repository.completeCurrentSet()

        assertCurrent(repository, "Тяга", setNumber = 2)
        repository.completeCurrentSet()

        // Суперсет закончился, дальше другое упражнение — отдых длиннее.
        assertRest(repository, expectedSeconds = restBetweenExercises)
        repository.finishRest()

        assertCurrent(repository, "Присед", setNumber = 1)
        repository.completeCurrentSet()

        // Последний подход тренировки: отдыхать уже не от чего.
        withTimeout(timeoutMillis) {
            repository.state.first { it is MockWorkoutState.ReadyToFinish }
        }
        Unit
    }

    @Test
    fun plainExerciseUsesBetweenSetsThenBetweenExercises() = runBlocking {
        seedPlainWorkout()
        val repository = newRepository()

        assertCurrent(repository, "Присед", setNumber = 1)
        repository.completeCurrentSet()
        assertRest(repository, expectedSeconds = restBetweenSets)
        repository.finishRest()

        assertCurrent(repository, "Присед", setNumber = 2)
        repository.completeCurrentSet()
        assertRest(repository, expectedSeconds = restBetweenExercises)
    }

    /**
     * Ждёт именно ожидаемый подход, а не любой.
     *
     * Состояние — StateFlow: проверка «просто дождаться CurrentSet» проходила бы
     * мгновенно на ещё не обновлённом прежнем значении, и тест ничего бы не
     * стерёг. Промах даёт таймаут.
     */
    private suspend fun assertCurrent(
        repository: OfflineFirstWorkoutRepository,
        exerciseName: String,
        setNumber: Int,
    ) {
        val state = withTimeout(timeoutMillis) {
            repository.state.first {
                it is MockWorkoutState.CurrentSet &&
                    it.exerciseName == exerciseName &&
                    it.setNumber == setNumber
            }
        } as MockWorkoutState.CurrentSet
        assertEquals(exerciseName, state.exerciseName)
        assertEquals(setNumber, state.setNumber)
    }

    private suspend fun assertRest(
        repository: OfflineFirstWorkoutRepository,
        expectedSeconds: Int,
    ) {
        val state = withTimeout(timeoutMillis) {
            repository.state.first { it is MockWorkoutState.Rest }
        } as MockWorkoutState.Rest
        assertEquals(expectedSeconds, state.durationSeconds)
    }

    private fun newRepository() = OfflineFirstWorkoutRepository(
        workoutDao = dao,
        remoteDataSource = OfflineRemoteDataSource,
        syncScheduler = NoOpSyncScheduler,
        syncQueueProcessor = SyncQueueProcessor(dao, OfflineRemoteDataSource),
        phoneLinkOpener = NoPhoneLinkOpener,
        gson = Gson(),
        scope = CoroutineScope(SupervisorJob() + Dispatchers.Unconfined),
    )

    private suspend fun seedSupersetWorkout() {
        dao.cacheActiveWorkout(
            workout(),
            listOf(
                LocalExercise(benchId, workoutId, "Жим лёжа", 0, supersetGroupId = 1),
                LocalExercise(rowId, workoutId, "Тяга", 1, supersetGroupId = 1),
                LocalExercise(squatId, workoutId, "Присед", 2),
            ),
            listOf(
                set(1, benchId, 0),
                set(2, benchId, 1),
                set(3, rowId, 0),
                set(4, rowId, 1),
                set(5, squatId, 0),
            ),
        )
        dao.upsertSession(DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = 1_000))
    }

    private suspend fun seedPlainWorkout() {
        dao.cacheActiveWorkout(
            workout(),
            listOf(
                LocalExercise(squatId, workoutId, "Присед", 0),
                LocalExercise(benchId, workoutId, "Жим лёжа", 1),
            ),
            listOf(
                set(1, squatId, 0),
                set(2, squatId, 1),
                set(3, benchId, 0),
            ),
        )
        dao.upsertSession(DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = 1_000))
    }

    private fun workout() = LocalWorkout(
        workoutId = workoutId,
        workoutName = "Тренировка",
        isActive = true,
        lastCheckedAtUtcMillis = 1_000,
        restBetweenSetsSeconds = restBetweenSets,
        restBetweenExercisesSeconds = restBetweenExercises,
    )

    private fun set(setId: Long, exerciseId: Long, orderIndex: Int) = LocalWorkoutSet(
        setId = setId,
        exerciseId = exerciseId,
        orderIndex = orderIndex,
        weightKilograms = 80.0,
        repetitions = 8,
        isCompleted = false,
        serverVersion = 0,
    )

    /**
     * Сеть всегда недоступна: тест проверяет офлайн-логику часов, и ответ
     * сервера не должен перезаписывать кэш посреди проверки.
     */
    private object OfflineRemoteDataSource : WatchRemoteDataSource {
        override suspend fun pair(pairingCode: String): PairingResult = PairingResult.Success

        override suspend fun startPhonePairing(): StartPhonePairingResult =
            StartPhonePairingResult.Failure("Не используется", retryable = false)

        override suspend fun pollPhonePairing(
            requestId: String,
            pollToken: String,
        ): PhonePairingPollResult =
            PhonePairingPollResult.Failure("Не используется", retryable = false)

        override suspend fun getActiveWorkout(): ActiveWorkoutResult =
            ActiveWorkoutResult.Failure("offline", retryable = true)

        override suspend fun sendSetMutation(operation: PendingSyncOperation): CompletionResult =
            CompletionResult.TransientFailure("offline")

        override suspend fun finishWorkout(workoutId: Long): FinishWorkoutRemoteResult =
            FinishWorkoutRemoteResult.TransientFailure("offline")

        override suspend fun refreshSession(): SessionRefreshResult = SessionRefreshResult.Success

        override fun clearSession() = Unit
    }

    private object NoPhoneLinkOpener : PhoneLinkOpener {
        override suspend fun open(url: String): PhoneLinkResult = PhoneLinkResult.NoPhoneAvailable
    }

    private companion object {
        val NoOpSyncScheduler = object : SyncScheduler {
            override fun schedule() = Unit
        }
        const val timeoutMillis = 3_000L
        const val workoutId = 1L
        const val benchId = 11L
        const val rowId = 12L
        const val squatId = 13L
        const val restBetweenSets = 75
        const val restBetweenExercises = 150
    }
}
