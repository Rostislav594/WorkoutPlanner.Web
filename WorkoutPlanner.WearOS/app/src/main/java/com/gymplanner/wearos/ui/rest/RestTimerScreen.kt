package com.gymplanner.wearos.ui.rest

import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.res.stringResource
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import com.gymplanner.wearos.R
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.ui.common.GlowFrame
import com.gymplanner.wearos.ui.theme.WearColors

/**
 * Отдых между подходами.
 *
 * Экран сведён к одному числу: на него смотрят мельком, между вдохами.
 * Янтарная обводка вместо зелёной сразу говорит, что идёт пауза, даже если
 * цифры прочитать не успели.
 *
 * Кроме таймера на экране нет ничего: так решено по макету. Пропуск отдыха
 * остался — его делает касание по всему экрану, — но подписи об этом больше
 * нет, поэтому жест виден только через contentDescription для TalkBack.
 */
@Composable
fun RestTimerScreen(
    state: MockWorkoutState.Rest,
    remainingSeconds: Int,
    onSkip: () -> Unit,
) {
    val interactionSource = remember { MutableInteractionSource() }
    val skipHint = stringResource(R.string.rest_skip_description)

    GlowFrame(
        glowColor = WearColors.Amber,
        modifier = Modifier
            .clickable(
                interactionSource = interactionSource,
                indication = null,
                onClick = onSkip,
            )
            .semantics { contentDescription = skipHint },
    ) {
        Text(
            text = formatTimer(remainingSeconds),
            color = WearColors.TextPrimary,
            style = MaterialTheme.typography.displayLarge,
            textAlign = TextAlign.Center,
        )
    }
}

internal fun formatTimer(totalSeconds: Int): String {
    val safeSeconds = totalSeconds.coerceAtLeast(0)
    val minutes = safeSeconds / 60
    val seconds = safeSeconds % 60
    return "$minutes:${seconds.toString().padStart(2, '0')}"
}
