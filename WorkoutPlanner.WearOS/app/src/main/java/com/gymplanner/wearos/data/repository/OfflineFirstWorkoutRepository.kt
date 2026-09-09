package com.gymplanner.wearos.data.repository

import com.gymplanner.wearos.data.local.DeviceSessionMetadata
import com.gymplanner.wearos.data.local.LocalExercise
import com.gymplanner.wearos.data.local.LocalWorkout
import com.gymplanner.wearos.data.local.LocalWorkoutSet
import com.gymplanner.wearos.data.local.LocalWorkoutSnapshot
import com.gymplanner.wearos.data.local.PendingSyncOperation
import com.gymplanner.wearos.data.local.SyncOperationStatus
import com.gymplanner.wearos.data.local.WorkoutDao
import com.gymplanner.wearos.data.remote.ActiveWorkoutResult
import com.gymplanner.wearos.data.remote.PairingResult
import com.gymplanner.wearos.data.remote.WatchActiveWorkoutResponse
import com.gymplanner.wearos.data.remote.WatchRemoteDataSource
import com.gymplanner.wearos.data.sync.SyncScheduler
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.model.PairingStatus
import com.gymplanner.wearos.domain.model.SetPreview
import com.gymplanner.wearos.domain.model.SyncStatus
import com.gymplanner.wearos.domain.repository.WorkoutRepository
import java.util.UUID
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch

class OfflineFirstWorkoutRepository(
    private val workoutDao: WorkoutDao,
    private val remoteDataSource: WatchRemoteDataSource,
    private val syncScheduler: SyncScheduler,
    private val wallClockMillis: () -> Long = System::currentTimeMillis,
    private val elapsedRealtimeMillis: () -> Long = { System.nanoTime() / nanosPerMillisecond },
    private val restDurationSeconds: Int = 10,
    scope: CoroutineScope = CoroutineScope(SupervisorJob()),
) : WorkoutRepository {
    private val mutableState = MutableStateFlow<MockWorkoutState>(MockWorkoutState.Pairing())
    private var latestSnapshot: LocalWorkoutSnapshot? = null

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
            PairingResult.Success -> {
                val now = wallClockMillis()
                workoutDao.upsertSession(
                    DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = now),
                )
            }
            is PairingResult.Failure -> mutableState.value = MockWorkoutState.Pairing(
                status = PairingStatus.Error,
                errorMessage = result.message,
            )
        }
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
        when (val result = remoteDataSource.getActiveWorkout()) {
            is ActiveWorkoutResult.Success -> cacheWorkout(result.workout, now)
            is ActiveWorkoutResult.NoActiveWorkout -> {
                workoutDao.upsertSession(
                    DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = now),
                )
                mutableState.value = MockWorkoutState.NoActiveWorkout(
                    lastCheckedAtMillis = now,
                    isFinished = result.finished,
                )
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
            is ActiveWorkoutResult.Failure -> mutableState.value = currentState.copy(
                lastCheckedAtMillis = now,
                isRefreshing = false,
                errorMessage = result.message,
            )
        }
    }

    override suspend fun completeCurrentSet() {
        val snapshot = latestSnapshot ?: return
        if (snapshot.workout.restCompletedSetId != null) return
        val currentSet = orderedSets(snapshot).firstOrNull { !it.set.isCompleted } ?: return
        val now = wallClockMillis()
        val operationId = UUID.randomUUID().toString()
        val payload = "{\"setId\":${currentSet.set.setId},\"clientVersion\":${currentSet.set.serverVersion}}"
        val completed = workoutDao.completeSetAtomically(
            workoutId = snapshot.workout.workoutId,
            setId = currentSet.set.setId,
            endsAtUtcMillis = now + restDurationSeconds * millisPerSecond,
            durationSeconds = restDurationSeconds,
            operationId = operationId,
            payloadJson = payload,
            createdAtUtcMillis = now,
        )
        if (completed) syncScheduler.schedule()
    }

    override suspend fun finishRest() {
        val snapshot = latestSnapshot ?: return
        if (snapshot.workout.restCompletedSetId == null) return
        workoutDao.clearRest(snapshot.workout.workoutId)
        if (orderedSets(snapshot).none { !it.set.isCompleted }) {
            workoutDao.deactivateWorkout(snapshot.workout.workoutId)
        }
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
        if (snapshot == null) return MockWorkoutState.NoActiveWorkout(session.lastCheckedAtUtcMillis)

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
                    durationSeconds = snapshot.workout.restDurationSeconds ?: restDurationSeconds,
                    endsAtElapsedRealtimeMillis = elapsedRealtimeMillis() + remainingMillis,
                    nextSet = next?.let { SetPreview(it.exercise.name, it.set.orderIndex + 1) },
                )
            }
        }

        val current = orderedSets.firstOrNull { !it.set.isCompleted }
            ?: return MockWorkoutState.NoActiveWorkout(snapshot.workout.lastCheckedAtUtcMillis)
        val exerciseSets = orderedSets.filter { it.exercise.exerciseId == current.exercise.exerciseId }
        return MockWorkoutState.CurrentSet(
            exerciseName = current.exercise.name,
            exerciseNumber = current.exercise.orderIndex + 1,
            totalExercises = snapshot.exercises.size,
            setNumber = current.set.orderIndex + 1,
            totalSets = exerciseSets.size,
            weightKilograms = current.set.weightKilograms,
            repetitions = current.set.repetitions,
            syncStatus = syncStatus(operations),
        )
    }

    private fun syncStatus(operations: List<PendingSyncOperation>): SyncStatus = when {
        operations.any { it.status == SyncOperationStatus.Conflict } -> SyncStatus.Conflict
        operations.any { it.status == SyncOperationStatus.Failed } -> SyncStatus.Failed
        operations.any { it.status == SyncOperationStatus.Pending || it.status == SyncOperationStatus.Syncing } -> SyncStatus.Pending
        else -> SyncStatus.Synchronized
    }

    private fun orderedSets(snapshot: LocalWorkoutSnapshot): List<SetWithExercise> =
        snapshot.exercises.sortedBy { it.exercise.orderIndex }.flatMap { exercise ->
            exercise.sets.sortedBy { it.orderIndex }.map { SetWithExercise(exercise.exercise, it) }
        }

    private data class SetWithExercise(val exercise: LocalExercise, val set: LocalWorkoutSet)

    private suspend fun cacheWorkout(response: WatchActiveWorkoutResponse, checkedAtMillis: Long) {
        val exercises = response.exercises.map { exercise ->
            LocalExercise(
                exerciseId = exercise.exerciseId,
                workoutId = response.workoutId,
                name = exercise.name,
                orderIndex = exercise.order,
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
            workout = LocalWorkout(response.workoutId, true, checkedAtMillis),
            exercises = exercises,
            sets = sets,
        )
        workoutDao.upsertSession(
            DeviceSessionMetadata(isPaired = true, lastCheckedAtUtcMillis = checkedAtMillis),
        )
    }

    private companion object {
        const val millisPerSecond = 1_000L
        const val nanosPerMillisecond = 1_000_000L
    }
}
