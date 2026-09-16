package com.gymplanner.wearos.ui.finish

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import com.gymplanner.wearos.R
import com.gymplanner.wearos.domain.model.CompletedWorkoutKind
import com.gymplanner.wearos.ui.theme.WearColors

/**
 * Итог тренировки.
 *
 * Единственный экран приложения с зелёной заливкой: он появляется один раз за
 * тренировку и должен читаться как точка, а не как очередное состояние.
 *
 * Текст зависит от того, что человеку делать дальше. У шаблонной тренировки
 * делать нечего — она уже в истории. У свободной на телефоне ждёт вопрос,
 * сохранять ли её шаблоном, и об этом надо предупредить.
 */
@Composable
fun WorkoutCompletedScreen(kind: CompletedWorkoutKind) {
    val isRound = LocalConfiguration.current.isScreenRound

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(
                Brush.verticalGradient(
                    listOf(WearColors.CompletedTop, WearColors.CompletedBottom),
                ),
            )
            .verticalScroll(rememberScrollState())
            .padding(
                horizontal = if (isRound) 26.dp else 16.dp,
                vertical = 12.dp,
            ),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text(
            text = stringResource(R.string.completed_title),
            color = WearColors.TextPrimary,
            style = MaterialTheme.typography.titleSmall,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = when (kind) {
                CompletedWorkoutKind.Scheduled ->
                    stringResource(R.string.completed_with_charts)
                CompletedWorkoutKind.Free ->
                    stringResource(R.string.completed_open_app)
            },
            modifier = Modifier
                .fillMaxWidth()
                // Рамка без заливки: в макете внутри неё виден тот же градиент,
                // что и на всём экране.
                .border(1.dp, WearColors.Amber, RoundedCornerShape(15.dp))
                .padding(horizontal = 8.dp, vertical = 6.dp),
            color = WearColors.TextPrimary,
            style = MaterialTheme.typography.labelSmall,
            textAlign = TextAlign.Center,
        )
    }
}
