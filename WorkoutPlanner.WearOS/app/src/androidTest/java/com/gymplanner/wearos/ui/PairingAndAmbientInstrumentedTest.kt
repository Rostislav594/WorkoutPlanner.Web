package com.gymplanner.wearos.ui

import android.graphics.Bitmap
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onRoot
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import androidx.wear.compose.material3.MaterialTheme
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.model.PairingStatus
import com.gymplanner.wearos.domain.model.SetPreview
import com.gymplanner.wearos.ui.ambient.AmbientWorkoutScreen
import com.gymplanner.wearos.ui.pairing.PairingScreen
import com.gymplanner.wearos.ui.theme.WorkoutPlannerWearTheme
import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Экраны, добавленные вместе с подтверждением на телефоне и ambient-режимом.
 */
@RunWith(AndroidJUnit4::class)
class PairingAndAmbientInstrumentedTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun pairing_offersPhoneConfirmationFirstAndKeepsCodeAsFallback() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                TestSurface {
                    PairingScreen(
                        state = MockWorkoutState.Pairing(),
                        pairingCode = "",
                        onPairingCodeChange = {},
                        onConnect = {},
                        onConfirmOnPhone = {},
                        onRetry = {},
                    )
                }
            }
        }

        composeRule.onNodeWithText("Подтвердить на телефоне").assertIsDisplayed()
        composeRule.onNodeWithText("Ввести код").assertIsDisplayed()
        saveScreenshot("pairing-phone-confirmation.png")
    }

    @Test
    fun pairing_manualEntryIsReachableAndReversible() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                TestSurface {
                    PairingScreen(
                        state = MockWorkoutState.Pairing(),
                        pairingCode = "",
                        onPairingCodeChange = {},
                        onConnect = {},
                        onConfirmOnPhone = {},
                        onRetry = {},
                    )
                }
            }
        }

        composeRule.onNodeWithText("Ввести код").performClick()
        composeRule.onNodeWithText("Введите код").assertIsDisplayed()
        saveScreenshot("pairing-manual-code.png")

        // На круглом 384px «Назад» уходит ниже границы экрана: без прокрутки
        // нажатие улетает мимо узла и экран не меняется.
        composeRule.onNodeWithText("Назад").performScrollTo().performClick()
        composeRule.onNodeWithText("Подтвердить на телефоне").assertIsDisplayed()
        saveScreenshot("pairing-back-from-manual.png")
    }

    @Test
    fun pairing_waitingForPhoneExplainsWhatToDo() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                TestSurface {
                    PairingScreen(
                        state = MockWorkoutState.Pairing(
                            status = PairingStatus.WaitingForPhone,
                        ),
                        pairingCode = "",
                        onPairingCodeChange = {},
                        onConnect = {},
                        onConfirmOnPhone = {},
                        onRetry = {},
                    )
                }
            }
        }

        composeRule.onNodeWithText("Подтвердите на телефоне").assertIsDisplayed()
        saveScreenshot("pairing-waiting-for-phone.png")
    }

    @Test
    fun pairing_noPhoneFallsBackToCodeWithExplanation() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                TestSurface {
                    PairingScreen(
                        state = MockWorkoutState.Pairing(
                            status = PairingStatus.Error,
                            errorMessage = "Телефон недоступен",
                        ),
                        pairingCode = "",
                        onPairingCodeChange = {},
                        onConnect = {},
                        onConfirmOnPhone = {},
                        onRetry = {},
                    )
                }
            }
        }

        composeRule.onNodeWithText("Телефон недоступен").assertIsDisplayed()
        composeRule.onNodeWithText("Ввести код").assertIsDisplayed()
    }

    @Test
    fun ambient_currentSetStaysReadableAndMostlyBlack() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                AmbientWorkoutScreen(currentSetState())
            }
        }

        composeRule.onNodeWithText("Жим лёжа").assertIsDisplayed()
        // Кроме названия упражнения в ambient не остаётся ничего: ни номера
        // подхода, ни веса — подробности нужны, только когда подняли руку.
        composeRule.onNodeWithText("Подход 2 / 4").assertDoesNotExist()
        composeRule.onNodeWithText("120 кг").assertDoesNotExist()
        saveScreenshot("ambient-current-set.png")
        assertMostlyBlack()
    }

    @Test
    fun ambient_restShowsOnlyTheWordRestWithoutCountdown() {
        composeRule.setContent {
            WorkoutPlannerWearTheme {
                AmbientWorkoutScreen(
                    MockWorkoutState.Rest(
                        completedExerciseName = "Жим лёжа",
                        completedSetNumber = 2,
                        durationSeconds = 90,
                        endsAtElapsedRealtimeMillis = 90_000,
                        nextSet = SetPreview("Жим лёжа", 3),
                    ),
                )
            }
        }

        composeRule.onNodeWithText("Отдых").assertIsDisplayed()
        // Ни следующего подхода, ни отсчёта: в ambient приложение просыпается
        // примерно раз в минуту, и посекундный таймер всё равно врал бы.
        composeRule.onNodeWithText("Далее: Жим лёжа").assertDoesNotExist()
        composeRule.onNodeWithText("1:29").assertDoesNotExist()
        saveScreenshot("ambient-rest.png")
        assertMostlyBlack()
    }

    /**
     * Рекомендация Google для always-on — держать не меньше 85% экрана чёрным.
     */
    private fun assertMostlyBlack() {
        composeRule.waitForIdle()
        val bitmap = composeRule.onRoot().captureToImage().asAndroidBitmap()
        var dark = 0
        var total = 0
        for (x in 0 until bitmap.width step samplingStep) {
            for (y in 0 until bitmap.height step samplingStep) {
                val pixel = bitmap.getPixel(x, y)
                val luminance = (
                    (pixel shr 16 and 0xFF) * 299 +
                        (pixel shr 8 and 0xFF) * 587 +
                        (pixel and 0xFF) * 114
                    ) / 1000
                if (luminance < darkLuminanceThreshold) dark++
                total++
            }
        }

        val darkShare = dark.toDouble() / total
        assertTrue(
            "Ambient-экран должен быть почти чёрным, а тёмных пикселей лишь " + darkShare,
            darkShare >= 0.85,
        )
    }

    private fun saveScreenshot(fileName: String) {
        composeRule.waitForIdle()
        val targetContext = InstrumentationRegistry.getInstrumentation().targetContext
        File(requireNotNull(targetContext.getExternalFilesDir(null)), fileName).outputStream().use { output ->
            composeRule.onRoot().captureToImage().asAndroidBitmap()
                .compress(Bitmap.CompressFormat.PNG, 100, output)
        }
    }

    @Composable
    private fun TestSurface(content: @Composable () -> Unit) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background),
        ) {
            content()
        }
    }

    private fun currentSetState() = MockWorkoutState.CurrentSet(
        setId = 101,
        exerciseName = "Жим лёжа",
        exerciseNumber = 1,
        totalExercises = 2,
        setNumber = 2,
        totalSets = 4,
        weightKilograms = 82.5,
    )

    private companion object {
        const val samplingStep = 4
        const val darkLuminanceThreshold = 40
    }
}
