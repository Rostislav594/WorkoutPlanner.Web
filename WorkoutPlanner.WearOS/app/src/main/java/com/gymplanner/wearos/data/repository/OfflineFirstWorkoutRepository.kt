package com.gymplanner.wearos.data.repository

import com.google.gson.Gson
import com.gymplanner.wearos.R
import com.gymplanner.wearos.data.localization.WatchStrings
import com.gymplanner.wearos.data.local.DeviceSessionMetadata
import com.gymplanner.wearos.data.local.LocalExercise
import com.gymplanner.wearos.data.local.LocalWorkout
import com.gymplanner.wearos.data.local.LocalWorkoutSet
import com.gymplanner.wearos.data.local.LocalWorkoutSnapshot
import com.gymplanner.wearos.data.local.PendingSyncOperation
import com.gymplanner.wearos.data.local.SetMutationPayload
import com.gymplanner.wearos.data.local.SyncOperationStatus
import com.gymplanner.wearos.data.local.SyncOperationType
import com.gymplanner.wearos.data.local.WorkoutDao
import com.gymplanner.wearos.data.remote.ActiveWorkoutResult
import com.gymplanner.wearos.data.remote.PairingResult
import com.gymplanner.wearos.data.remote.PhonePairingPollResult
import com.gymplanner.wearos.data.remote.StartPhonePairingResult
import com.gymplanner.wearos.data.remote.FinishWorkoutRemoteResult
import com.gymplanner.wearos.data.remote.WatchActiveWorkoutResponse
import com.gymplanner.wearos.data.remote.WatchRemoteDataSource
import com.gymplanner.wearos.data.sync.SyncScheduler
import com.gymplanner.wearos.data.sync.SyncQueueProcessor
import com.gymplanner.wearos.data.sync.QueueDrainResult
import com.gymplanner.wearos.domain.model.CompletedWorkoutKind
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.model.PairingStatus
import com.gymplanner.wearos.domain.model.SetPreview
import com.gymplanner.wearos.domain.phone.PhoneLinkOpener
import com.gymplanner.wearos.domain.phone.PhoneLinkResult
import com.gymplanner.wearos.domain.repository.WorkoutRepository
import com.gymplanner.wearos.domain.repository.FinishWorkoutResult
import java.util.UUID
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

class OfflineFirstWorkoutRepository(
    private val workoutDao: WorkoutDao,
    private val remoteDataSource: WatchRemoteDataSource,
    private val syncScheduler: SyncScheduler,
    private val syncQueueProcessor: SyncQueueProcessor,
    private val phoneLinkOpener: PhoneLinkOpener,
    private val gson: Gson,
    private val strings: WatchStrings,
    private val wallClockMillis: () -> Long = System::currentTimeMillis,
    private val elapsedRealtimeMillis: () -> Long = { System.nanoTime() / nanosPerMillisecond },
    scope: CoroutineScope = CoroutineScope(SupervisorJob()),
) : WorkoutRepository {
    private val mutableState = MutableStateFlow<MockWorkoutState>(MockWorkoutState.Pairing())
    private var latestSnapshot: LocalWorkoutSnapshot? = null
    private val mutationMutex = Mutex()
    private var confirmedFinishedWorkoutId: Long? = null
    private var confirmedFinishedKind: CompletedWorkoutKind =
        CompletedWorkoutKind.Scheduled

    override val state: StateFlow<MockWorkoutState> = mutableState.asStateFlow()

    init {
        scope.launch {
            combine(
                workoutDao.observeDeviceSession(),
                workoutDao.observeActiveWorkout(),
                workoutDao.observeOperations(),
            ) { session, snapshot, operations -> Triple(session, snapshot, operations) }
                .collect { (session, snapshot, operations) ->
                    latestSnapshot = snapshot
                    mutableState.value = mapState(session, snapshot, operations)
                    if (operations.any {
                            it.status == SyncOperationStatus.Pending ||
                                (it.status == SyncOperationStatus.Failed && it.canRetry)
                        }
                    ) {
                        syncScheduler.schedule()
                    }
                }
        }
    }

    override suspend fun pair(pairingCode: String) {
        if (mutableState.value !is MockWorkoutState.Pairing) return
        mutableState.value = MockWorkoutState.Pairing(status = PairingStatus.Connecting)
        when (val result = remoteDataSource.pair(pairingCode)) {
            PairingResult.Success -> adoptNewSession()
            is PairingResult.Failure -> mutableState.value = MockWorkoutState.Pairing(
                status = PairingStatus.Error,
                errorMessage = result.message,
            )
        }
    }

    override suspend fun pairWithPhoneConfirmation() {
        if (mutableState.value !is MockWorkoutState.Pairing) return
        mutableState.value = MockWorkoutState.Pairing(status = PairingStatus.Connecting)

        val start = when (val result = remoteDataSource.startPhonePairing()) {
            is StartPhonePairingResult.Success -> result
            is StartPhonePairingResult.Failure -> {
                failPairing(result.message)
                return
            }
        }

        when (val opened = phoneLinkOpener.open(start.approveUrl)) {
            PhoneLinkResult.Success -> Unit
            PhoneLinkResult.NoPhoneAvailable -> {
                failPairing(strings.get(R.string.error_phone_unavailable))
                return
            }
            is PhoneLinkResult.Failure -> {
                failPairing(opened.message)
                return
            }
        }

        mutableState.value = MockWorkoutState.Pairing(status = PairingStatus.WaitingForPhone)
        awaitPhoneApproval(start.requestId, start.pollToken)
    }

    /**
     * Опрашивает заявку, пока пользователь не ответит на телефоне.
     *
     * Сетевые сбои во время ожидания не обрывают процесс: телефон мог просто
     * потерять связь на секунду. Ожидание ограничено сроком жизни заявки.
     */
    private suspend fun awaitPhoneApproval(requestId: String, pollToken: String) {
        val deadline = elapsedRealtimeMillis() + phonePairingTimeoutMillis
        while (elapsedRealtimeMillis() < deadline) {
            delay(phonePairingPollIntervalMillis)
            when (val result = remoteDataSource.pollPhonePairing(requestId, pollToken)) {
                PhonePairingPollResult.Pending -> Unit
                PhonePairingPollResult.Approved -> {
                    adoptNewSession()
                    return
                }
                PhonePairingPollResult.Rejected -> {
                    failPairing(strings.get(R.string.error_pairing_declined))
                    return
                }
                PhonePairingPollResult.Expired -> {
                    failPairing(strings.get(R.string.error_confirmation_timeout))
                    return
                }
                is PhonePairingPollResult.Failure -> {
                    if (!result.retryable) {
                        failPairing(result.message)
                        return
                    }
                }
            }
        }
        failPairing(strings.get(R.string.error_confirmation_timeout))
    }

    /**
     * Принять новую сессию часов.
     *
     * Кэш прошлого владельца стирается всегда: часы не знают, к какому аккаунту
     * их подключили, а держать на устройстве чужую тренировку нельзя. Заодно
     * это избавляет от рассинхронизации, если аккаунт тот же, но тренировка
     * успела измениться, пока часы были отключены.
     *
     * Сразу за этим тренировка подтягивается с сервера: раньше человеку после
     * подключения показывали «активной тренировки нет», пока он не нажмёт
     * «Обновить» вручную.
     */
    private suspend fun adoptNewSession() {
        val now = wallClockMillis()
        confirmedFinishedWorkoutId = null
        workoutDao.clearWorkoutCache()
        workoutDao.upsertSession(
            DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = now),
        )
        loadActiveWorkout(now)
    }

    private fun failPairing(message: String) {
        mutableState.value = MockWorkoutState.Pairing(
            status = PairingStatus.Error,
            errorMessage = message,
        )
    }

    override suspend fun retryPairing() {
        val pairingState = mutableState.value as? MockWorkoutState.Pairing ?: return
        if (pairingState.status == PairingStatus.Error) mutableState.value = MockWorkoutState.Pairing()
    }

    override suspend fun refreshActiveWorkout() {
        val currentState = mutableState.value as? MockWorkoutState.NoActiveWorkout ?: return
        if (currentState.isRefreshing) return
        val now = wallClockMillis()
        mutableState.value = currentState.copy(
            lastCheckedAtMillis = now,
            isRefreshing = true,
            errorMessage = null,
        )
        loadActiveWorkout(now)
    }

    /** Загрузка активной тренировки. Общая для сопряжения и ручного обновления. */
    private suspend fun loadActiveWorkout(now: Long) {
        when (val result = remoteDataSource.getActiveWorkout()) {
            is ActiveWorkoutResult.Success -> cacheWorkout(result.workout, now)
            is ActiveWorkoutResult.NoActiveWorkout -> {
                workoutDao.upsertSession(
                    DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = now),
                )
                // Сервер говорит, что тренировка на сегодня уже закрыта — это тот
                // же итог, что и после завершения с часов.
                mutableState.value = if (result.finished) {
                    MockWorkoutState.Completed(kind = CompletedWorkoutKind.Scheduled)
                } else {
                    MockWorkoutState.NoActiveWorkout(lastCheckedAtMillis = now)
                }
            }
            is ActiveWorkoutResult.Unauthorized -> {
                remoteDataSource.clearSession()
                workoutDao.upsertSession(
                    DeviceSessionMetadata(isPaired = false, lastCheckedAtUtcMillis = now),
                )
                mutableState.value = MockWorkoutState.Pairing(
                    status = PairingStatus.Error,
                    errorMessage = result.message,
                )
            }
            is ActiveWorkoutResult.Failure -> mutableState.value =
                MockWorkoutState.NoActiveWorkout(
                    lastCheckedAtMillis = now,
                    errorMessage = result.message,
                )
        }
    }

    override suspend fun completeCurrentSet(): Boolean = mutationMutex.withLock {
        val snapshot = latestSnapshot ?: return false
        if (snapshot.workout.restCompletedSetId != null) return false
        val ordered = orderedSets(snapshot)
        val currentIndex = ordered.indexOfFirst { !it.set.isCompleted }
        if (currentIndex < 0) return false
        val currentSet = ordered[currentIndex]
        if (workoutDao.countOutstandingOperations() >= maxOutstandingOperations) return false
        val storedSet = workoutDao.getSet(currentSet.set.setId) ?: return false
        val expectedVersion = storedSet.serverVersion +
            workoutDao.getUnresolvedOperationsForSet(storedSet.setId).size
        val now = wallClockMillis()
        val operationId = UUID.randomUUID().toString()
        val payload = gson.toJson(SetMutationPayload(clientVersion = expectedVersion))
        val restSeconds = restSecondsAfter(
            current = currentSet,
            next = ordered.getOrNull(currentIndex + 1),
            workout = snapshot.workout,
        )
        val completed = workoutDao.completeSetAtomically(
            workoutId = snapshot.workout.workoutId,
            setId = currentSet.set.setId,
            endsAtUtcMillis = restSeconds?.let { now + it * millisPerSecond },
            durationSeconds = restSeconds,
            operationId = operationId,
            payloadJson = payload,
            createdAtUtcMillis = now,
        )
        if (completed) syncScheduler.schedule()
        return completed
    }

    override suspend fun finishWorkout(): FinishWorkoutResult = mutationMutex.withLock {
        val snapshot = latestSnapshot ?: return FinishWorkoutResult.Failure(
            strings.get(R.string.error_no_active_workout),
        )
        if (mutableState.value !is MockWorkoutState.ReadyToFinish) {
            return FinishWorkoutResult.Failure(strings.get(R.string.error_finish_sets_first))
        }

        if (syncQueueProcessor.drain() == QueueDrainResult.Retry) {
            return FinishWorkoutResult.Failure(
                strings.get(R.string.error_sync_changes_failed),
            )
        }
        if (workoutDao.countOutstandingOperations() > 0) {
            return FinishWorkoutResult.UnresolvedOperations
        }

        when (val result = remoteDataSource.finishWorkout(snapshot.workout.workoutId)) {
            is FinishWorkoutRemoteResult.Success -> {
                confirmedFinishedWorkoutId = snapshot.workout.workoutId
                confirmedFinishedKind = if (snapshot.workout.isFree) {
                    CompletedWorkoutKind.Free
                } else {
                    CompletedWorkoutKind.Scheduled
                }
                workoutDao.deactivateWorkout(snapshot.workout.workoutId)
                FinishWorkoutResult.Success
            }
            is FinishWorkoutRemoteResult.TransientFailure ->
                FinishWorkoutResult.Failure(result.message)
            is FinishWorkoutRemoteResult.PermanentFailure ->
                FinishWorkoutResult.Failure(result.message)
            is FinishWorkoutRemoteResult.Unauthorized -> {
                remoteDataSource.clearSession()
                workoutDao.upsertSession(
                    DeviceSessionMetadata(
                        isPaired = false,
                        lastCheckedAtUtcMillis = wallClockMillis(),
                    ),
                )
                FinishWorkoutResult.Failure(result.message)
            }
        }
    }

    override suspend fun finishRest() {
        val snapshot = latestSnapshot ?: return
        if (snapshot.workout.restCompletedSetId == null) return
        workoutDao.clearRest(snapshot.workout.workoutId)
    }

    private fun mapState(
        session: DeviceSessionMetadata?,
        snapshot: LocalWorkoutSnapshot?,
        operations: List<PendingSyncOperation>,
    ): MockWorkoutState {
        if (session?.isPaired != true) {
            return mutableState.value.takeIf {
                it is MockWorkoutState.Pairing && it.status != PairingStatus.Idle
            } ?: MockWorkoutState.Pairing()
        }
        if (snapshot == null) {
            // Тренировку только что закрыли — показываем итог, а не «нет тренировки».
            return confirmedFinishedWorkoutId?.let {
                MockWorkoutState.Completed(kind = confirmedFinishedKind)
            } ?: MockWorkoutState.NoActiveWorkout(
                lastCheckedAtMillis = session.lastCheckedAtUtcMillis,
            )
        }

        val orderedSets = orderedSets(snapshot)
        val restCompletedSetId = snapshot.workout.restCompletedSetId
        if (restCompletedSetId != null) {
            val completed = orderedSets.firstOrNull { it.set.setId == restCompletedSetId }
            if (completed != null) {
                val next = orderedSets.firstOrNull { !it.set.isCompleted }
                val remainingMillis = (
                    (snapshot.workout.restEndsAtUtcMillis ?: wallClockMillis()) - wallClockMillis()
                ).coerceAtLeast(0L)
                return MockWorkoutState.Rest(
                    completedExerciseName = completed.exercise.name,
                    completedSetNumber = completed.set.orderIndex + 1,
                    durationSeconds = snapshot.workout.restDurationSeconds
                        ?: defaultRestBetweenSetsSeconds,
                    endsAtElapsedRealtimeMillis = elapsedRealtimeMillis() + remainingMillis,
                    nextSet = next?.let { SetPreview(it.exercise.name, it.set.orderIndex + 1) },
                )
            }
        }

        val current = orderedSets.firstOrNull { !it.set.isCompleted }
            ?: return MockWorkoutState.ReadyToFinish
        val exerciseSets = orderedSets.filter { it.exercise.exerciseId == current.exercise.exerciseId }
        return MockWorkoutState.CurrentSet(
            setId = current.set.setId,
            exerciseName = current.exercise.name,
            exerciseNumber = current.exercise.orderIndex + 1,
            totalExercises = snapshot.exercises.size,
            setNumber = current.set.orderIndex + 1,
            totalSets = exerciseSets.size,
            weightKilograms = current.set.weightKilograms,
        )
    }

    /**
     * Порядок, в котором часы ведут человека по тренировке.
     *
     * Обычные упражнения идут подряд: все подходы одного, затем следующего.
     * Упражнения, объединённые в суперсет, чередуются кругами — первый подход
     * каждого из них, затем второй и так далее. Порядок должен совпадать с тем,
     * что считает сервер, иначе офлайн и онлайн разойдутся.
     */
    private fun orderedSets(snapshot: LocalWorkoutSnapshot): List<SetWithExercise> {
        val groups = snapshot.exercises
            .groupBy { entry ->
                entry.exercise.supersetGroupId?.let { "s:$it" } ?: "e:${entry.exercise.exerciseId}"
            }
            .values
            .map { group -> group.sortedBy { it.exercise.orderIndex } }
            .sortedBy { group -> group.first().exercise.orderIndex }

        return groups.flatMap { group ->
            if (group.size == 1) {
                val entry = group.first()
                entry.sets.sortedBy { it.orderIndex }
                    .map { SetWithExercise(entry.exercise, it) }
            } else {
                val rounds = group.maxOf { it.sets.size }
                (0 until rounds).flatMap { round ->
                    group.mapNotNull { entry ->
                        entry.sets.sortedBy { it.orderIndex }.getOrNull(round)
                            ?.let { SetWithExercise(entry.exercise, it) }
                    }
                }
            }
        }
    }

    /**
     * Сколько отдыхать после закрытого подхода, или null, если отдыха нет.
     *
     * Длительности приходят с сервера из профиля пользователя — те самые два
     * поля, что он настраивает в приложении.
     */
    private fun restSecondsAfter(
        current: SetWithExercise,
        next: SetWithExercise?,
        workout: LocalWorkout,
    ): Int? {
        // Последний подход тренировки: дальше экран завершения, отдыхать не от чего.
        if (next == null) return null

        val group = current.exercise.supersetGroupId
        val sameGroup = group != null && group == next.exercise.supersetGroupId
        val sameExercise = current.exercise.exerciseId == next.exercise.exerciseId

        // Середина круга суперсета: подход одного упражнения сразу сменяется
        // подходом другого, пауза между ними не предусмотрена.
        if (sameGroup && !sameExercise && next.set.orderIndex == current.set.orderIndex) {
            return null
        }

        return if (sameExercise || sameGroup) {
            workout.restBetweenSetsSeconds ?: defaultRestBetweenSetsSeconds
        } else {
            workout.restBetweenExercisesSeconds ?: defaultRestBetweenExercisesSeconds
        }
    }

    private data class SetWithExercise(val exercise: LocalExercise, val set: LocalWorkoutSet)

    private suspend fun cacheWorkout(response: WatchActiveWorkoutResponse, checkedAtMillis: Long) {
        confirmedFinishedWorkoutId = null
        val exercises = response.exercises.map { exercise ->
            LocalExercise(
                exerciseId = exercise.exerciseId,
                workoutId = response.workoutId,
                name = exercise.name,
                orderIndex = exercise.order,
                supersetGroupId = exercise.supersetGroupId,
            )
        }
        val sets = response.exercises.flatMap { exercise ->
            exercise.sets.map { set ->
                LocalWorkoutSet(
                    setId = set.setId,
                    exerciseId = exercise.exerciseId,
                    orderIndex = set.setNumber - 1,
                    weightKilograms = set.weight,
                    repetitions = set.repetitions,
                    isCompleted = set.isCompleted,
                    serverVersion = set.version,
                )
            }
        }
        workoutDao.cacheActiveWorkout(
            workout = LocalWorkout(
                workoutId = response.workoutId,
                workoutName = response.workoutName,
                isActive = true,
                lastCheckedAtUtcMillis = checkedAtMillis,
                restBetweenSetsSeconds = response.restBetweenSetsSeconds,
                restBetweenExercisesSeconds = response.restBetweenExercisesSeconds,
                isFree = response.isFreeWorkout,
            ),
            exercises = exercises,
            sets = sets,
        )
        workoutDao.upsertSession(
            DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = checkedAtMillis),
        )
    }

    private companion object {
        // Запасные значения на случай старого сервера: совпадают со значениями
        // по умолчанию в профиле пользователя.
        const val defaultRestBetweenSetsSeconds = 90
        const val defaultRestBetweenExercisesSeconds = 120
        const val millisPerSecond = 1_000L
        const val nanosPerMillisecond = 1_000_000L
        const val maxOutstandingOperations = 100
        const val phonePairingPollIntervalMillis = 2_000L
        // Чуть больше серверного срока жизни заявки (3 минуты).
        const val phonePairingTimeoutMillis = 200_000L
        val updateOperationTypes = setOf(
            SyncOperationType.UpdateWeight,
            SyncOperationType.UpdateReps,
        )
    }
}
