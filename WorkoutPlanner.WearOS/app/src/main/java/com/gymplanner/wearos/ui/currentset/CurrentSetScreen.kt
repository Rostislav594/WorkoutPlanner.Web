package com.gymplanner.wearos.ui.currentset

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.wear.compose.material3.Button
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.model.SyncStatus
import com.gymplanner.wearos.ui.common.WearScreen
import java.text.DecimalFormat

@Composable
fun CurrentSetScreen(
    state: MockWorkoutState.CurrentSet,
    onComplete: () -> Unit,
) {
    WearScreen {
        Text(
            text = state.exerciseName,
            color = MaterialTheme.colorScheme.primary,
            style = MaterialTheme.typography.titleMedium,
            textAlign = TextAlign.Center,
        )
        Text(
            text = "Подход ${state.setNumber} / ${state.totalSets}",
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            style = MaterialTheme.typography.bodyMedium,
        )
        Spacer(Modifier.height(4.dp))
        val weight = state.weightKilograms?.let { "${DecimalFormat("0.#").format(it)} кг" } ?: "— кг"
        val repetitions = state.repetitions?.let { "$it повт." } ?: "— повт."
        val metricsDescription = "${weight.replace("—", "не указан")}, " +
            (state.repetitions?.let { "$it повторов" } ?: "повторы не указаны")
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .background(MaterialTheme.colorScheme.surfaceContainer, RoundedCornerShape(18.dp))
                .padding(horizontal = 10.dp, vertical = 6.dp),
            contentAlignment = Alignment.Center,
        ) {
            Text(
                text = "$weight · $repetitions",
                modifier = Modifier.semantics { contentDescription = metricsDescription },
                style = MaterialTheme.typography.titleSmall,
                textAlign = TextAlign.Center,
            )
        }
        Spacer(Modifier.height(4.dp))
        Text(
            text = when (state.syncStatus) {
                SyncStatus.Synchronized -> "Синхронизировано"
                SyncStatus.Pending -> "Ожидает синхронизации"
                SyncStatus.Failed -> "Синхронизация отложена"
                SyncStatus.Conflict -> "Конфликт данных"
            },
            color = when (state.syncStatus) {
                SyncStatus.Failed,
                SyncStatus.Conflict,
                -> MaterialTheme.colorScheme.error
                else -> MaterialTheme.colorScheme.onSurfaceVariant
            },
            style = MaterialTheme.typography.labelSmall,
        )
        Spacer(Modifier.height(4.dp))
        Button(
            onClick = onComplete,
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(min = 48.dp),
        ) {
            Text("Выполнить подход", textAlign = TextAlign.Center)
        }
    }
}
