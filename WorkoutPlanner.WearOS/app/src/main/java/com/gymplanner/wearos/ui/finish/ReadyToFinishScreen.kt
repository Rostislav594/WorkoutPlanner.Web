package com.gymplanner.wearos.ui.finish

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import com.gymplanner.wearos.ui.common.GlowFrame
import com.gymplanner.wearos.ui.common.HoldToConfirm
import com.gymplanner.wearos.ui.common.PerimeterProgress
import com.gymplanner.wearos.ui.theme.WearColors

/**
 * Все подходы выполнены — остаётся закрыть тренировку.
 *
 * Отдельного экрана-вопроса «точно завершить?» больше нет: вместо него сам
 * жест удержания, а полоса по контуру показывает, сколько держать. Отпустил —
 * ничего не произошло.
 */
@Composable
fun ReadyToFinishScreen(
    isFinishing: Boolean,
    actionMessage: String?,
    onFinish: () -> Unit,
) {
    var holdProgress by remember { mutableFloatStateOf(0f) }

    Box(Modifier.fillMaxSize()) {
        GlowFrame {
            Text(
                text = "Все подходы выполнены",
                color = WearColors.TextPrimary,
                style = MaterialTheme.typography.titleSmall,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(12.dp))

            actionMessage?.let {
                Text(
                    text = it,
                    color = WearColors.Error,
                    style = MaterialTheme.typography.labelSmall,
                    textAlign = TextAlign.Center,
                )
                Spacer(Modifier.height(8.dp))
            }

            HoldToConfirm(
                label = if (isFinishing) "Завершаем…" else "Завершить тренировку",
                accent = WearColors.NeonGreen,
                enabled = !isFinishing,
                holdMillis = finishHoldMillis,
                onProgress = { holdProgress = it },
                onConfirmed = onFinish,
            )
        }

        // Полоса поверх всего экрана: пока держат, она обегает контур.
        PerimeterProgress(
            progress = holdProgress,
            color = WearColors.NeonGreen,
            modifier = Modifier.fillMaxSize().padding(2.dp),
        )
    }
}

private const val finishHoldMillis = 1400
