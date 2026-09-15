package com.gymplanner.wearos.ui.common

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp

/**
 * Общий вид кнопки: заливка, внутренняя тень сверху и контур.
 *
 * Живёт отдельно от [NeonButton] и [HoldToConfirm], потому что кнопки должны
 * выглядеть одинаково, а раньше расходились — у одной была тень и заливка, у
 * другой нет. Пока вид задаётся здесь, разойтись они не могут.
 *
 * Тень рисуется только поверх заливки: на прозрачной кнопке чёрный градиент
 * лёг бы на чёрный фон и всё равно не был бы виден.
 */
internal fun Modifier.neonSurface(
    fill: Color,
    borderColor: Color,
): Modifier {
    val shape = RoundedCornerShape(percent = 50)
    return this
        .clip(shape)
        .background(color = fill, shape = shape)
        .then(
            if (fill.alpha > 0f) {
                Modifier.drawBehind {
                    drawRect(
                        brush = Brush.verticalGradient(
                            colors = listOf(
                                Color.Black.copy(alpha = innerShadowAlpha),
                                Color.Transparent,
                            ),
                            startY = 0f,
                            endY = size.height * innerShadowSpan,
                        ),
                    )
                }
            } else {
                Modifier
            },
        )
        .border(width = borderDp.dp, color = borderColor, shape = shape)
}

/** Заливка активной кнопки — из макета. */
internal const val neonFillAlpha = 0.35f

private const val innerShadowAlpha = 0.54f
private const val innerShadowSpan = 0.85f
private const val borderDp = 2
