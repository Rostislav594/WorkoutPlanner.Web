package com.gymplanner.wearos.ui.common

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import com.gymplanner.wearos.ui.theme.WearColors

/**
 * Кнопка обычного действия в оформлении часов.
 *
 * В отличие от [HoldToConfirm] срабатывает от касания: подключиться или
 * обновить список — действия обратимые, защищать их удержанием незачем.
 *
 * Вторичный вариант отличается только приглушённым контуром: на чёрном фоне
 * этого достаточно, чтобы взгляд сразу нашёл главное действие.
 */
@Composable
fun NeonButton(
    label: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    primary: Boolean = true,
    accent: Color = WearColors.NeonGreen,
) {
    val contentColor = when {
        !enabled -> accent.copy(alpha = disabledAlpha)
        primary -> accent
        else -> WearColors.TextMuted
    }
    val borderColor = when {
        !enabled -> accent.copy(alpha = disabledAlpha)
        primary -> accent
        else -> WearColors.TextMuted.copy(alpha = secondaryBorderAlpha)
    }

    Box(
        modifier = modifier
            .fillMaxWidth()
            .heightIn(min = minTouchTargetDp.dp)
            .neonSurface(
                fill = if (primary) accent.copy(alpha = neonFillAlpha) else Color.Transparent,
                borderColor = borderColor,
            )
            .clickable(enabled = enabled, onClick = onClick)
            .padding(horizontal = 10.dp, vertical = 6.dp),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            text = label,
            color = contentColor,
            style = MaterialTheme.typography.labelMedium,
            maxLines = 2,
            textAlign = TextAlign.Center,
        )
    }
}

private const val minTouchTargetDp = 48
private const val secondaryBorderAlpha = 0.45f
private const val disabledAlpha = 0.35f
