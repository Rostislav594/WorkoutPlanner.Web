package com.gymplanner.wearos.ui

import android.graphics.Bitmap
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onRoot
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.gymplanner.wearos.domain.model.CompletedWorkoutKind
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.model.SetPreview
import com.gymplanner.wearos.ui.currentset.CurrentSetScreen
import com.gymplanner.wearos.ui.finish.ReadyToFinishScreen
import com.gymplanner.wearos.ui.finish.WorkoutCompletedScreen
import com.gymplanner.wearos.ui.rest.RestTimerScreen
import com.gymplanner.wearos.ui.theme.WorkoutPlannerWearTheme
import java.io.File
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Экраны тренировки после переделки оформления.
 *
 * Главное, что здесь проверяется, — необратимые действия защищены удержанием:
 * короткое касание не должно ни закрывать подход, ни завершать тренировку.
 */
@RunWith(AndroidJUnit4::class)
class WorkoutScreensInstrumentedTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun currentSet_showsWeightSetsAndSingleAction() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                CurrentSetScreen(
                    state = currentSetState(),
                    onComplete = {},
                    actionMessage = null,
                )
            }
        }

        composeRule.onNodeWithText("Жим штанги лежа").assertIsDisplayed()
        composeRule.onNodeWithText("120кг").assertIsDisplayed()
        composeRule.onNodeWithText("5 подходов").assertIsDisplayed()
        composeRule.onNodeWithText("Подход 1").assertIsDisplayed()
        composeRule.onNodeWithText("Закончить подход").assertIsDisplayed()
        saveScreenshot("redesign-current-set.png")
    }

    @Test
    fun currentSet_shortTapDoesNotCompleteTheSet() {
        var completed = false
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                CurrentSetScreen(
                    state = currentSetState(),
                    onComplete = { completed = true },
                    actionMessage = null,
                )
            }
        }

        composeRule.onNodeWithText("Закончить подход").performClick()
        composeRule.waitForIdle()

        assertFalse("Случайное касание не должно закрывать подход", completed)
    }

    @Test
    fun rest_showsTimerAndSkipsOnTap() {
        var skipped = false
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                RestTimerScreen(
                    state = MockWorkoutState.Rest(
                        completedExerciseName = "Жим штанги лежа",
                        completedSetNumber = 1,
                        durationSeconds = 120,
                        endsAtElapsedRealtimeMillis = 120_000,
                        nextSet = SetPreview("Жим штанги лежа", 2),
                    ),
                    remainingSeconds = 119,
                    onSkip = { skipped = true },
                )
            }
        }

        composeRule.onNodeWithText("1:59").assertIsDisplayed()
        saveScreenshot("redesign-rest.png")

        composeRule.onNodeWithText("1:59").performClick()
        composeRule.waitForIdle()
        assertTrue("Отдых должен пропускаться касанием экрана", skipped)
    }

    @Test
    fun readyToFinish_shortTapDoesNotFinishWorkout() {
        var finished = false
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                ReadyToFinishScreen(
                    isFinishing = false,
                    actionMessage = null,
                    onFinish = { finished = true },
                )
            }
        }

        composeRule.onNodeWithText("Завершить тренировку").assertIsDisplayed()
        saveScreenshot("redesign-ready-to-finish.png")

        composeRule.onNodeWithText("Завершить тренировку").performClick()
        composeRule.waitForIdle()
        assertFalse("Тренировка не должна завершаться одним касанием", finished)
    }

    @Test
    fun completed_scheduledWorkoutPointsAtHistory() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                WorkoutCompletedScreen(kind = CompletedWorkoutKind.Scheduled)
            }
        }

        composeRule.onNodeWithText("Тренировка завершена").assertIsDisplayed()
        composeRule
            .onNodeWithText("Тренировка добавлена в историю, а также в графики прогресса!")
            .assertIsDisplayed()
        saveScreenshot("redesign-completed-scheduled.png")
    }

    @Test
    fun completed_freeWorkoutSendsUserToThePhone() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                WorkoutCompletedScreen(kind = CompletedWorkoutKind.Free)
            }
        }

        composeRule
            .onNodeWithText("Перейдите в приложение, чтобы сделать следующее действие!")
            .assertIsDisplayed()
        saveScreenshot("redesign-completed-free.png")
    }

    private fun currentSetState() = MockWorkoutState.CurrentSet(
        setId = 101,
        exerciseName = "Жим штанги лежа",
        exerciseNumber = 1,
        totalExercises = 2,
        setNumber = 1,
        totalSets = 5,
        weightKilograms = 120.0,
    )

    private fun saveScreenshot(fileName: String) {
        composeRule.waitForIdle()
        val targetContext = InstrumentationRegistry.getInstrumentation().targetContext
        File(requireNotNull(targetContext.getExternalFilesDir(null)), fileName).outputStream().use { output ->
            composeRule.onRoot().captureToImage().asAndroidBitmap()
                .compress(Bitmap.CompressFormat.PNG, 100, output)
        }
    }
}
