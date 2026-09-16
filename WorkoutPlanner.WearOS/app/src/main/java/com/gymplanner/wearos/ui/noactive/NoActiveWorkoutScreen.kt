package com.gymplanner.wearos.ui.noactive

import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.res.stringResource
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import com.gymplanner.wearos.R
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.ui.common.GlowFrame
import com.gymplanner.wearos.ui.common.NeonButton
import com.gymplanner.wearos.ui.theme.WearColors
import java.text.DateFormat
import java.util.Date

/**
 * Часы подключены, но тренировки на сегодня нет.
 *
 * Обводка янтарная: как и отдых, это состояние ожидания — делать на часах
 * нечего, тренировка начинается в основном приложении.
 */
@Composable
fun NoActiveWorkoutScreen(
    state: MockWorkoutState.NoActiveWorkout,
    onRefresh: () -> Unit,
) {
    val checkedAt = remember(state.lastCheckedAtMillis) {
        DateFormat.getTimeInstance(DateFormat.SHORT).format(Date(state.lastCheckedAtMillis))
    }
    val hasError = state.errorMessage != null

    GlowFrame {
        Text(
            text = state.errorMessage ?: stringResource(R.string.no_active_workout),
            color = if (hasError) WearColors.Error else WearColors.TextPrimary,
            style = MaterialTheme.typography.titleSmall,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(6.dp))
        Text(
            text = if (state.isRefreshing) {
                stringResource(R.string.no_active_checking)
            } else {
                stringResource(R.string.no_active_last_check, checkedAt)
            },
            color = WearColors.TextMuted,
            style = MaterialTheme.typography.labelSmall,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(10.dp))
        NeonButton(
            label = when {
                state.isRefreshing -> stringResource(R.string.no_active_refreshing)
                hasError -> stringResource(R.string.common_retry)
                else -> stringResource(R.string.no_active_refresh)
            },
            accent = WearColors.Amber,
            enabled = !state.isRefreshing,
            onClick = onRefresh,
        )
    }
}
