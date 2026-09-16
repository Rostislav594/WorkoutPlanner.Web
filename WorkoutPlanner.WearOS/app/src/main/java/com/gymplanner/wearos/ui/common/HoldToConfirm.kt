package com.gymplanner.wearos.ui.common

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.tween
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import com.gymplanner.wearos.R
import com.gymplanner.wearos.ui.theme.WearColors

/**
 * Кнопка, срабатывающая по удержанию.
 *
 * Почему удержание, а не нажатие: в зале часы задевают штангой, рукавом и
 * собственным запястьем. Случайный тап не должен закрывать подход и тем более
 * тренировку. Удержание же нельзя выполнить нечаянно.
 *
 * Прогресс отдаётся наружу через [onProgress], чтобы экран мог рисовать свою
 * анимацию — галочку у подхода, полосу по контуру у завершения тренировки.
 * Отпустили раньше времени — прогресс плавно откатывается, действие не наступает.
 */
@Composable
fun HoldToConfirm(
    label: String,
    onConfirmed: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    accent: Color = WearColors.NeonGreen,
    holdMillis: Int = defaultHoldMillis,
    onProgress: (Float) -> Unit = {},
) {
    val progress = remember { Animatable(0f) }
    var pressed by remember { mutableStateOf(false) }
    val currentOnConfirmed by rememberUpdatedState(onConfirmed)
    val currentOnProgress by rememberUpdatedState(onProgress)

    LaunchedEffect(progress.value) { currentOnProgress(progress.value) }

    LaunchedEffect(pressed, enabled) {
        if (!enabled) {
            progress.snapTo(0f)
            return@LaunchedEffect
        }
        if (pressed) {
            val remaining = ((1f - progress.value) * holdMillis).toInt().coerceAtLeast(1)
            progress.animateTo(1f, tween(durationMillis = remaining, easing = { it }))
            if (progress.value >= 1f) {
                pressed = false
                currentOnConfirmed()
                progress.snapTo(0f)
            }
        } else {
            // Откат быстрее набора: отмена должна ощущаться мгновенной.
            progress.animateTo(0f, tween(durationMillis = releaseMillis, easing = { it }))
        }
    }

    val holdHint = stringResource(R.string.hold_to_confirm_description, label)

    Box(
        modifier = modifier
            .fillMaxWidth()
            .heightIn(min = minTouchTargetDp.dp)
            // Заливка постоянная, как в макете, а не привязана к удержанию: без
            // неё внутренняя тень оказывалась чёрным по чёрному и пропадала.
            // Прогресс при этом не теряется — его показывают галочка на экране
            // подхода и полоса по контуру на завершении тренировки.
            .neonSurface(
                fill = accent.copy(alpha = neonFillAlpha),
                borderColor = if (enabled) accent else accent.copy(alpha = disabledAlpha),
            )
            .pointerInput(enabled) {
                if (!enabled) return@pointerInput
                detectTapGestures(
                    onPress = {
                        pressed = true
                        tryAwaitRelease()
                        pressed = false
                    },
                )
            }
            .semantics { contentDescription = holdHint }
            .padding(horizontal = labelPaddingDp.dp, vertical = 8.dp),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            text = label,
            color = if (enabled) accent else accent.copy(alpha = disabledAlpha),
            style = MaterialTheme.typography.labelMedium,
            maxLines = 2,
            textAlign = TextAlign.Center,
        )
    }
}

private const val defaultHoldMillis = 900
private const val releaseMillis = 160
private const val minTouchTargetDp = 52
private const val labelPaddingDp = 12
private const val disabledAlpha = 0.4f
