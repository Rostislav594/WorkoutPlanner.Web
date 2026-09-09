package com.gymplanner.wearos.ui.noactive

import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.wear.compose.material3.Button
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.ui.common.WearScreen
import java.text.DateFormat
import java.util.Date

@Composable
fun NoActiveWorkoutScreen(
    state: MockWorkoutState.NoActiveWorkout,
    onRefresh: () -> Unit,
) {
    val checkedAt = remember(state.lastCheckedAtMillis) {
        DateFormat.getTimeInstance(DateFormat.SHORT).format(Date(state.lastCheckedAtMillis))
    }

    WearScreen {
        Text(
            text = "Тренировка",
            color = MaterialTheme.colorScheme.primary,
            style = MaterialTheme.typography.labelMedium,
        )
        Spacer(Modifier.height(2.dp))
        Text(
            text = when {
                state.errorMessage != null -> state.errorMessage
                state.isFinished -> "Тренировка уже завершена"
                else -> "Активной тренировки нет"
            },
            color = if (state.errorMessage != null) {
                MaterialTheme.colorScheme.error
            } else {
                MaterialTheme.colorScheme.onSurface
            },
            style = MaterialTheme.typography.titleLarge,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(4.dp))
        Text(
            text = if (state.isRefreshing) "Проверяем…" else "Последняя проверка: $checkedAt",
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            style = MaterialTheme.typography.bodySmall,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(8.dp))
        Button(
            onClick = onRefresh,
            enabled = !state.isRefreshing,
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(min = 48.dp),
        ) {
            Text(
                when {
                    state.isRefreshing -> "Обновляем…"
                    state.errorMessage != null -> "Повторить"
                    else -> "Обновить"
                },
            )
        }
    }
}
