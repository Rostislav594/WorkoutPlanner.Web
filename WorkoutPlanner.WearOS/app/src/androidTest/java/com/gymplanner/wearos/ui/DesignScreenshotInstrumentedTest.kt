package com.gymplanner.wearos.ui

import android.graphics.Bitmap
import androidx.annotation.StringRes
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onRoot
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTouchInput
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import androidx.wear.compose.material3.MaterialTheme
import com.gymplanner.wearos.R
import com.gymplanner.wearos.domain.model.CompletedWorkoutKind
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.model.PairingStatus
import com.gymplanner.wearos.domain.model.SetPreview
import com.gymplanner.wearos.ui.ambient.AmbientWorkoutScreen
import com.gymplanner.wearos.ui.currentset.CurrentSetScreen
import com.gymplanner.wearos.ui.finish.ReadyToFinishScreen
import com.gymplanner.wearos.ui.finish.WorkoutCompletedScreen
import com.gymplanner.wearos.ui.noactive.NoActiveWorkoutScreen
import com.gymplanner.wearos.ui.pairing.PairingScreen
import com.gymplanner.wearos.ui.rest.RestTimerScreen
import com.gymplanner.wearos.ui.theme.WorkoutPlannerWearTheme
import java.io.File
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Снимает экраны для сверки с макетами Figma.
 *
 * Отличие от [WorkoutScreensInstrumentedTest] только в месте сохранения: тот
 * пишет во внешний каталог приложения, который на Android 16 недоступен ни из
 * adb shell, ни через run-as. Внутренний каталог читается через run-as, поэтому
 * картинки можно забрать на машину и сравнить с макетом.
 */
@RunWith(AndroidJUnit4::class)
class DesignScreenshotInstrumentedTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun captureCurrentSet() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                CurrentSetScreen(
                    state = currentSetState(),
                    onComplete = {},
                    actionMessage = null,
                )
            }
        }
        capture("shot-current-set.png")
    }

    @Test
    fun captureRest() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                RestTimerScreen(
                    state = MockWorkoutState.Rest(
                        completedExerciseName = "Жим штанги лежачи",
                        completedSetNumber = 1,
                        durationSeconds = 120,
                        endsAtElapsedRealtimeMillis = 120_000,
                        nextSet = SetPreview("Жим штанги лежачи", 2),
                    ),
                    remainingSeconds = 119,
                    onSkip = {},
                )
            }
        }
        capture("shot-rest.png")
    }

    @Test
    fun captureReadyToFinish() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                ReadyToFinishScreen(
                    isFinishing = false,
                    actionMessage = null,
                    onFinish = {},
                )
            }
        }
        capture("shot-ready-to-finish.png")
    }

    @Test
    fun captureCompletedScheduled() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                WorkoutCompletedScreen(kind = CompletedWorkoutKind.Scheduled)
            }
        }
        capture("shot-completed-scheduled.png")
    }

    @Test
    fun captureCompletedFree() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                WorkoutCompletedScreen(kind = CompletedWorkoutKind.Free)
            }
        }
        capture("shot-completed-free.png")
    }

    /**
     * Подход в момент удержания кнопки: именно здесь видна галочка, которой в
     * покое на экране нет.
     */
    @Test
    fun captureCurrentSetWhileHolding() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                CurrentSetScreen(
                    state = currentSetState(),
                    onComplete = {},
                    actionMessage = null,
                )
            }
        }
        holdAndCapture(string(R.string.current_set_finish), 850, "shot-current-set-holding.png")
    }

    /** Завершение тренировки в момент удержания: полоса обегает контур экрана. */
    @Test
    fun captureReadyToFinishWhileHolding() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                ReadyToFinishScreen(
                    isFinishing = false,
                    actionMessage = null,
                    onFinish = {},
                )
            }
        }
        holdAndCapture(string(R.string.ready_finish_workout), 900, "shot-ready-to-finish-holding.png")
    }

    @Test
    fun capturePairingPhoneConfirmation() {
        setPairingContent(MockWorkoutState.Pairing())
        capture("shot-pairing-phone.png")
    }

    @Test
    fun capturePairingManualCode() {
        setPairingContent(MockWorkoutState.Pairing())
        composeRule.onNodeWithText(string(R.string.pairing_enter_code)).performClick()
        capture("shot-pairing-manual.png")
    }

    @Test
    fun capturePairingWaitingForPhone() {
        setPairingContent(MockWorkoutState.Pairing(status = PairingStatus.WaitingForPhone))
        capture("shot-pairing-waiting.png")
    }

    @Test
    fun capturePairingError() {
        setPairingContent(
            MockWorkoutState.Pairing(
                status = PairingStatus.Error,
                errorMessage = "Телефон недоступний",
            ),
        )
        capture("shot-pairing-error.png")
    }

    @Test
    fun captureNoActiveWorkout() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                NoActiveWorkoutScreen(
                    state = MockWorkoutState.NoActiveWorkout(lastCheckedAtMillis = 1_600_000_000_000),
                    onRefresh = {},
                )
            }
        }
        capture("shot-no-active-workout.png")
    }

    @Test
    fun captureAmbientCurrentSet() {
        composeRule.setContent {
            WorkoutPlannerWearTheme { AmbientWorkoutScreen(currentSetState()) }
        }
        capture("shot-ambient-current-set.png")
    }

    @Test
    fun captureAmbientRest() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                AmbientWorkoutScreen(
                    MockWorkoutState.Rest(
                        completedExerciseName = "Жим штанги лежачи",
                        completedSetNumber = 1,
                        durationSeconds = 120,
                        endsAtElapsedRealtimeMillis = 120_000,
                        nextSet = SetPreview("Жим штанги лежачи", 2),
                    ),
                )
            }
        }
        capture("shot-ambient-rest.png")
    }

    /**
     * Экран сопряжения рисуется на подложке цвета фона: сам по себе он её не
     * задаёт, а на прозрачном фоне снимок получился бы нечитаемым.
     */
    private fun setPairingContent(state: MockWorkoutState.Pairing) {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(MaterialTheme.colorScheme.background),
                ) {
                    PairingScreen(
                        state = state,
                        pairingCode = "",
                        onPairingCodeChange = {},
                        onConnect = {},
                        onConfirmOnPhone = {},
                        onRetry = {},
                    )
                }
            }
        }
    }

    private fun currentSetState() = MockWorkoutState.CurrentSet(
        setId = 101,
        exerciseName = "Жим штанги лежачи",
        exerciseNumber = 1,
        totalExercises = 2,
        setNumber = 1,
        totalSets = 5,
        weightKilograms = 120.0,
    )

    /**
     * Останавливает часы теста, чтобы снять кадр в середине удержания: при
     * обычном ходе времени анимация успела бы дойти до конца, сработать и
     * откатить прогресс в ноль.
     */
    private fun holdAndCapture(label: String, holdMillis: Long, fileName: String) {
        composeRule.waitForIdle()
        composeRule.mainClock.autoAdvance = false
        composeRule.onNodeWithText(label).performTouchInput { down(center) }
        composeRule.mainClock.advanceTimeBy(holdMillis)
        writeBitmap(fileName)
        composeRule.onNodeWithText(label).performTouchInput { up() }
        composeRule.mainClock.autoAdvance = true
    }

    /**
     * Подписи кнопок берём из ресурсов, а не из литералов: язык на часах
     * выбирает система, и на украинской локали поиск по русскому тексту
     * не находит узел.
     */
    private fun string(@StringRes id: Int): String =
        InstrumentationRegistry.getInstrumentation().targetContext.getString(id)

    private fun capture(fileName: String) {
        composeRule.waitForIdle()
        writeBitmap(fileName)
    }

    private fun writeBitmap(fileName: String) {
        val targetContext = InstrumentationRegistry.getInstrumentation().targetContext
        File(targetContext.filesDir, fileName).outputStream().use { output ->
            composeRule.onRoot().captureToImage().asAndroidBitmap()
                .compress(Bitmap.CompressFormat.PNG, 100, output)
        }
    }
}
