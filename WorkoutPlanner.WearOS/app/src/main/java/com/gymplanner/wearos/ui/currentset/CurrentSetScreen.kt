package com.gymplanner.wearos.ui.currentset

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.layout.positionInRoot
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.ui.common.AnimatedCheckmark
import com.gymplanner.wearos.ui.common.GlowFrame
import com.gymplanner.wearos.ui.common.HoldToConfirm
import com.gymplanner.wearos.ui.theme.WearColors
import java.text.DecimalFormat
import kotlin.math.roundToInt

/**
 * Текущий подход — единственный рабочий экран тренировки.
 *
 * На экране ровно одно действие: удержать кнопку и закрыть подход. Всё
 * остальное убрано намеренно — в зале рука потная, экран маленький, а лишние
 * кнопки рядом с необратимым действием только мешают.
 */
@Composable
fun CurrentSetScreen(
    state: MockWorkoutState.CurrentSet,
    onComplete: () -> Unit,
    actionMessage: String?,
) {
    var holdProgress by remember { mutableFloatStateOf(0f) }
    var pillCenterY by remember { mutableFloatStateOf(0f) }

    Box(Modifier.fillMaxSize()) {
    GlowFrame {
        Text(
            text = state.exerciseName,
            color = WearColors.TextPrimary,
            style = MaterialTheme.typography.labelMedium,
            textAlign = TextAlign.Center,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis,
        )
        Spacer(Modifier.height(4.dp))
        SetMetrics(
            weightKilograms = state.weightKilograms,
            totalSets = state.totalSets,
            // Галочка потом встаёт ровно сюда, поэтому пилюля сообщает,
            // где она оказалась после раскладки.
            modifier = Modifier.onGloballyPositioned {
                pillCenterY = it.positionInRoot().y + it.size.height / 2f
            },
        )
        Spacer(Modifier.height(2.dp))
        Text(
            text = "Подход ${state.setNumber}",
            color = WearColors.TextPrimary,
            style = MaterialTheme.typography.labelMedium,
            textAlign = TextAlign.Center,
            maxLines = 1,
        )

        Spacer(Modifier.height(10.dp))

        actionMessage?.let {
            Text(
                text = it,
                color = WearColors.Error,
                style = MaterialTheme.typography.labelSmall,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(4.dp))
        }

        HoldToConfirm(
            label = "Закончить подход",
            accent = WearColors.NeonGreen,
            onProgress = { holdProgress = it },
            onConfirmed = onComplete,
        )
    }

    // Затемнение под галочкой: пока идёт удержание, остальное на экране гаснет,
    // и подтверждение остаётся единственным, что видно.
    if (holdProgress > 0f) {
        Box(
            Modifier
                .fillMaxSize()
                .background(Color.Black.copy(alpha = scrimAlpha * holdProgress)),
        )
    }

    // Галочка рисуется поверх экрана, пока держат кнопку: так она может быть
    // крупной, как в макете, и не занимает место в раскладке, когда её нет.
    //
    // Встаёт она не по центру экрана, а ровно на пилюлю с весом: кнопку в этот
    // момент закрывает палец, и под ним галочку было бы не видно. Координата
    // берётся у самой пилюли, а не подбирается константой, — иначе на другом
    // размере экрана или при крупном системном шрифте промахнётся.
    val glyphCenterOffsetPx = with(LocalDensity.current) {
        checkmarkOverlayDp.dp.toPx() * checkmarkGlyphCenterFraction
    }
    Box(Modifier.fillMaxSize()) {
        AnimatedCheckmark(
            progress = holdProgress,
            color = WearColors.TextPrimary,
            sizeDp = checkmarkOverlayDp,
            strokeDp = 12,
            modifier = Modifier
                .align(Alignment.TopCenter)
                .offset { IntOffset(0, (pillCenterY - glyphCenterOffsetPx).roundToInt()) },
        )
    }
    }
}

/**
 * Вес и общее число подходов — та пара чисел, ради которой человек вообще
 * смотрит на часы между повторениями.
 */
@Composable
private fun SetMetrics(
    weightKilograms: Double?,
    totalSets: Int,
    modifier: Modifier = Modifier,
) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .border(
                width = 1.dp,
                color = WearColors.Amber,
                shape = RoundedCornerShape(10.dp),
            )
            .background(
                color = WearColors.AmberDim.copy(alpha = 0.25f),
                shape = RoundedCornerShape(10.dp),
            ),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        MetricCell(
            text = weightKilograms?.let { "${DecimalFormat("0.#").format(it)}кг" } ?: "—кг",
            modifier = Modifier.weight(1f),
        )
        Box(
            modifier = Modifier
                .width(1.dp)
                .height(dividerHeightDp.dp)
                .background(WearColors.Amber),
        )
        MetricCell(
            text = "$totalSets ${setsWord(totalSets)}",
            modifier = Modifier.weight(1.5f),
        )
    }
}

@Composable
private fun MetricCell(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        modifier = modifier.padding(vertical = 4.dp),
        color = WearColors.Amber,
        style = MaterialTheme.typography.labelSmall,
        textAlign = TextAlign.Center,
        maxLines = 1,
        softWrap = false,
    )
}

/** Русские числительные: 1 подход, 2 подхода, 5 подходов. */
private fun setsWord(count: Int): String {
    val mod100 = count % 100
    if (mod100 in 11..14) return "подходов"
    return when (count % 10) {
        1 -> "подход"
        2, 3, 4 -> "подхода"
        else -> "подходов"
    }
}

private const val checkmarkOverlayDp = 120
// Где внутри своего квадрата лежит зрительный центр штриха: путь идёт от 0.26
// до 0.76 высоты, середина — 0.51.
private const val checkmarkGlyphCenterFraction = 0.51f
private const val scrimAlpha = 0.72f
private const val dividerHeightDp = 14
