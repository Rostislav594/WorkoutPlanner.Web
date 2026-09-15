package com.gymplanner.wearos.ui.common

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.unit.dp
import com.gymplanner.wearos.ui.theme.WearColors

/**
 * Чёрный экран с необязательной светящейся обводкой по краю.
 *
 * Обводка не постоянная: по макетам она появляется только там, где несёт
 * смысл, — оранжевая во время отдыха. На рабочих экранах края чистые, а о
 * ходе удержания кнопки говорит полоса из [PerimeterProgress]. Поэтому
 * [glowColor] равен null, когда свечение не нужно.
 *
 * Форма обводки повторяет форму часов. На круглых это окружность: рисовать
 * там прямоугольник нельзя — углы уходят за стекло, и свечение превращается в
 * две полосы по бокам. Горизонтальные отступы на круглых тоже больше, иначе
 * текст обрезается скруглением.
 */
@Composable
fun GlowFrame(
    glowColor: Color? = null,
    modifier: Modifier = Modifier,
    verticalArrangement: Arrangement.Vertical = Arrangement.Center,
    content: @Composable ColumnScope.() -> Unit,
) {
    val isRound = LocalConfiguration.current.isScreenRound

    Box(
        modifier = modifier
            .fillMaxSize()
            .background(WearColors.Background),
    ) {
        if (glowColor != null) Canvas(Modifier.fillMaxSize()) {
            for (layer in glowLayers downTo 1) {
                val inset = layer * glowStepPx
                // Квадратичное затухание вместо 1/layer: у прежней кривой первые
                // слои отличались по яркости слишком резко, и свечение читалось
                // как несколько отдельных колец вместо одного мягкого ореола.
                val falloff = 1f - (layer - 1f) / glowLayers
                val alpha = glowBaseAlpha * falloff * falloff
                if (isRound) {
                    drawCircle(
                        color = glowColor.copy(alpha = alpha),
                        radius = size.minDimension / 2f - inset,
                        style = Stroke(width = glowStrokePx),
                    )
                } else {
                    // Радиус сжимается вместе с контуром. С постоянным радиусом
                    // слои в углах расходятся неравномерно, и ровно там снова
                    // появляются полосы — на круглых часах этого не видно.
                    val corner = (size.minDimension * cornerFraction - inset)
                        .coerceAtLeast(0f)
                    drawRoundRect(
                        color = glowColor.copy(alpha = alpha),
                        topLeft = Offset(inset, inset),
                        size = Size(size.width - inset * 2, size.height - inset * 2),
                        cornerRadius = CornerRadius(corner, corner),
                        style = Stroke(width = glowStrokePx),
                    )
                }
            }
        }

        Column(
            modifier = Modifier
                .fillMaxSize()
                // Прокрутка как страховка: при крупном системном шрифте или на
                // меньшем экране содержимое не должно обрезаться.
                .verticalScroll(rememberScrollState())
                .padding(
                    horizontal = if (isRound) roundHorizontalDp.dp else squareHorizontalDp.dp,
                    vertical = verticalPaddingDp.dp,
                ),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = verticalArrangement,
            content = content,
        )
    }
}

private const val cornerFraction = 0.22f
// Много тонких слоёв с малым шагом: по отдельности они не различимы и дают
// сплошной градиент, тогда как пять толстых читались полосами.
private const val glowLayers = 28
private const val glowStepPx = 1.1f
private const val glowStrokePx = 2.5f
private const val glowBaseAlpha = 0.5f
private const val roundHorizontalDp = 26
private const val squareHorizontalDp = 14
private const val verticalPaddingDp = 10
