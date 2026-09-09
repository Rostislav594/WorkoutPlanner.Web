package com.gymplanner.wearos.ui.rest

import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import androidx.wear.compose.material3.TextButton
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.ui.common.WearScreen

@Composable
fun RestTimerScreen(
    state: MockWorkoutState.Rest,
    remainingSeconds: Int,
    onSkip: () -> Unit,
) {
    WearScreen {
        Text(
            text = "Подход выполнен",
            color = MaterialTheme.colorScheme.primary,
            style = MaterialTheme.typography.labelMedium,
        )
        Spacer(Modifier.height(2.dp))
        Text(
            text = formatTimer(remainingSeconds),
            style = MaterialTheme.typography.displayMedium.copy(fontWeight = FontWeight.Bold),
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(4.dp))
        Text(
            text = state.nextSet?.let { "Далее: ${it.exerciseName}, подход ${it.setNumber}" }
                ?: "Это последний mock-подход",
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            style = MaterialTheme.typography.bodySmall,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(8.dp))
        TextButton(
            onClick = onSkip,
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(min = 48.dp),
        ) {
            Text("Пропустить")
        }
    }
}

private fun formatTimer(seconds: Int): String = "%02d:%02d".format(seconds / 60, seconds % 60)
