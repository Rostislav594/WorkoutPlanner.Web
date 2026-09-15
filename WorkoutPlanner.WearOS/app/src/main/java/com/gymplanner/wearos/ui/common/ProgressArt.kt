package com.gymplanner.wearos.ui.common

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.RoundRect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathMeasure
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.unit.dp

/**
 * Галочка, которая прорисовывается по мере удержания кнопки.
 *
 * Рисуется одной линией через PathMeasure: так штрих идёт от начала к концу,
 * как будто его ведут рукой, а не появляется целиком. Человек видит, сколько
 * ещё держать, и может отпустить, если передумал.
 */
@Composable
fun AnimatedCheckmark(
    progress: Float,
    color: Color,
    modifier: Modifier = Modifier,
    sizeDp: Int = 96,
    strokeDp: Int = 10,
) {
    val pathMeasure = remember { PathMeasure() }
    val source = remember { Path() }
    val visible = remember { Path() }

    Canvas(modifier.size(sizeDp.dp)) {
        if (progress <= 0f) return@Canvas

        source.reset()
        source.moveTo(size.width * 0.18f, size.height * 0.52f)
        source.lineTo(size.width * 0.42f, size.height * 0.76f)
        source.lineTo(size.width * 0.84f, size.height * 0.26f)

        pathMeasure.setPath(source, false)
        visible.reset()
        pathMeasure.getSegment(
            0f,
            pathMeasure.length * progress.coerceIn(0f, 1f),
            visible,
            true,
        )

        drawPath(
            path = visible,
            color = color,
            style = Stroke(width = strokeDp.dp.toPx(), cap = androidx.compose.ui.graphics.StrokeCap.Round),
        )
    }
}

/**
 * Полоса, заполняющая контур экрана по мере удержания.
 *
 * Используется при завершении тренировки: действие необратимо, поэтому оно
 * занимает весь экран и показывает, сколько осталось держать.
 *
 * Форма повторяет форму часов — как и у [GlowFrame]. На круглых рисуется дугой
 * от двенадцати часов по ходу стрелки: прямоугольный контур там срезало бы
 * углами, и полоса выглядела бы оборванной.
 */
@Composable
fun PerimeterProgress(
    progress: Float,
    color: Color,
    modifier: Modifier = Modifier,
    strokeDp: Int = 6,
) {
    val pathMeasure = remember { PathMeasure() }
    val source = remember { Path() }
    val visible = remember { Path() }
    val isRound = LocalConfiguration.current.isScreenRound

    Canvas(modifier) {
        if (progress <= 0f) return@Canvas

        val stroke = strokeDp.dp.toPx()
        val inset = stroke / 2f
        val corner = size.minDimension * cornerFraction

        if (isRound) {
            val diameter = size.minDimension - stroke
            drawArc(
                color = color,
                startAngle = -90f,
                sweepAngle = 360f * progress.coerceIn(0f, 1f),
                useCenter = false,
                topLeft = Offset(
                    (size.width - diameter) / 2f,
                    (size.height - diameter) / 2f,
                ),
                size = Size(diameter, diameter),
                style = Stroke(
                    width = stroke,
                    cap = androidx.compose.ui.graphics.StrokeCap.Round,
                ),
            )
            return@Canvas
        }

        source.reset()
        source.addRoundRect(
            RoundRect(
                rect = Rect(
                    offset = Offset(inset, inset),
                    size = Size(size.width - stroke, size.height - stroke),
                ),
                cornerRadius = CornerRadius(corner, corner),
            ),
        )

        pathMeasure.setPath(source, true)
        visible.reset()
        pathMeasure.getSegment(
            0f,
            pathMeasure.length * progress.coerceIn(0f, 1f),
            visible,
            true,
        )

        drawPath(
            path = visible,
            color = color,
            style = Stroke(width = stroke, cap = androidx.compose.ui.graphics.StrokeCap.Round),
        )
    }
}

private const val cornerFraction = 0.22f
