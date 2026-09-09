package com.gymplanner.wearos.data.local

import androidx.room.Embedded
import androidx.room.Relation

data class LocalExerciseWithSets(
    @Embedded val exercise: LocalExercise,
    @Relation(
        parentColumn = "exerciseId",
        entityColumn = "exerciseId",
    )
    val sets: List<LocalWorkoutSet>,
)

data class LocalWorkoutSnapshot(
    @Embedded val workout: LocalWorkout,
    @Relation(
        entity = LocalExercise::class,
        parentColumn = "workoutId",
        entityColumn = "workoutId",
    )
    val exercises: List<LocalExerciseWithSets>,
)
