package com.gymplanner.wearos.ui

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.gymplanner.wearos.MainActivity
import com.gymplanner.wearos.data.local.SyncOperationStatus
import com.gymplanner.wearos.data.local.WorkoutDatabase
import com.gymplanner.wearos.data.sync.WorkManagerSyncScheduler
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertTrue
import org.junit.Assume.assumeTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Opt-in smoke test against a running real WorkoutPlanner backend.
 *
 * Run with `-e pairingCode 123456`. Without that argument the test is skipped,
 * so normal connected checks never depend on a developer machine backend.
 */
@RunWith(AndroidJUnit4::class)
class RealApiSmokeInstrumentedTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<MainActivity>()

    @Test
    fun pair_loadActiveWorkout_andCompleteRealSet() {
        val pairingCode = InstrumentationRegistry.getArguments().getString("pairingCode").orEmpty()
        assumeTrue("A six-digit pairingCode instrumentation argument is required.", pairingCode.length == 6)
        val expectedExercise = InstrumentationRegistry.getArguments()
            .getString("expectedExercise")
            .orEmpty()
            .ifBlank { "Жим на часах" }

        if (composeRule.onAllNodesWithText(expectedExercise, substring = true).fetchSemanticsNodes().isEmpty()) {
            composeRule.onNodeWithContentDescription("Шестизначный код подключения")
                .performTextInput(pairingCode)
            composeRule.onNodeWithText("Подключить").performClick()

            composeRule.waitUntil(timeoutMillis = 15_000) {
                composeRule.onAllNodesWithText("Обновить").fetchSemanticsNodes().isNotEmpty()
            }
            composeRule.onNodeWithText("Обновить").performClick()
        }

        composeRule.waitUntil(timeoutMillis = 15_000) {
            composeRule.onAllNodesWithText(expectedExercise, substring = true).fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText(expectedExercise, substring = true).assertIsDisplayed()

        composeRule.onNodeWithText("Выполнить подход").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            composeRule.onAllNodesWithText("Подход выполнен").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText("Подход выполнен").assertExists()

        composeRule.waitUntil(timeoutMillis = 15_000) {
            val workManager = androidx.work.WorkManager.getInstance(
                InstrumentationRegistry.getInstrumentation().targetContext,
            )
            val states = workManager.getWorkInfosForUniqueWork(
                WorkManagerSyncScheduler.uniqueWorkName,
            ).get()
            states.isNotEmpty() && states.all { it.state == androidx.work.WorkInfo.State.SUCCEEDED }
        }
    }

    @Test
    fun offlineCompletion_isPersistedBeforeNetworkSync() {
        assumeTrue("Run only with -e milestoneStep offline.", milestoneStep() == "offline")

        composeRule.waitUntil(timeoutMillis = 15_000) {
            composeRule.onAllNodesWithText("Выполнить подход").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText("Выполнить подход").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            composeRule.onAllNodesWithText("Подход выполнен").fetchSemanticsNodes().isNotEmpty()
        }

        val operations = runBlocking {
            WorkoutDatabase.getInstance(
                InstrumentationRegistry.getInstrumentation().targetContext,
            ).workoutDao().observeOperations().first()
        }
        assertTrue(operations.any {
            it.status == SyncOperationStatus.Pending ||
                it.status == SyncOperationStatus.Syncing ||
                (it.status == SyncOperationStatus.Failed && it.canRetry)
        })
    }

    @Test
    fun revokedDevice_returnsToPairingAfterNextWrite() {
        assumeTrue("Run only with -e milestoneStep revoked.", milestoneStep() == "revoked")

        composeRule.waitUntil(timeoutMillis = 15_000) {
            composeRule.onAllNodesWithText("Выполнить подход").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText("Выполнить подход").performClick()
        composeRule.waitUntil(timeoutMillis = 20_000) {
            composeRule.onAllNodesWithText("Введите код").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText("Введите код").assertIsDisplayed()
    }

    @Test
    fun completeAndFinishWorkout_isConfirmedByRealBackend() {
        assumeTrue("Run only with -e milestoneStep finish.", milestoneStep() == "finish")
        val pairingCode = InstrumentationRegistry.getArguments().getString("pairingCode").orEmpty()
        assumeTrue("A six-digit pairingCode instrumentation argument is required.", pairingCode.length == 6)
        val expectedExercise = InstrumentationRegistry.getArguments()
            .getString("expectedExercise")
            .orEmpty()
            .ifBlank { "Финиш с часов" }

        composeRule.onNodeWithContentDescription("Шестизначный код подключения")
            .performTextInput(pairingCode)
        composeRule.onNodeWithText("Подключить").performClick()
        composeRule.waitUntil(timeoutMillis = 15_000) {
            composeRule.onAllNodesWithText("Обновить").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText("Обновить").performClick()
        composeRule.waitUntil(timeoutMillis = 15_000) {
            composeRule.onAllNodesWithText(expectedExercise, substring = true)
                .fetchSemanticsNodes().isNotEmpty()
        }

        composeRule.onNodeWithText("Выполнить подход").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            composeRule.onAllNodesWithText("Подход выполнен").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText("Пропустить").performClick()
        composeRule.waitUntil(timeoutMillis = 5_000) {
            composeRule.onAllNodesWithText("Все подходы выполнены").fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText("Завершить тренировку").performClick()
        composeRule.onNodeWithText("Да, завершить").performClick()

        composeRule.waitUntil(timeoutMillis = 20_000) {
            composeRule.onAllNodesWithText("Тренировка уже завершена")
                .fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText("Тренировка уже завершена").assertIsDisplayed()
    }

    private fun milestoneStep(): String =
        InstrumentationRegistry.getArguments().getString("milestoneStep").orEmpty()
}
