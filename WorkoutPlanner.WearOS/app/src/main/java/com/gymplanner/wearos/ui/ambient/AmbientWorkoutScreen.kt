package com.gymplanner.wearos.ui.ambient

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import androidx.wear.compose.material3.TimeText
import com.gymplanner.wearos.domain.model.MockWorkoutState

/**
 * Экран для погашенного (ambient) состояния.
 *
 * Рекомендации Google для always-on: держать почти весь экран чёрным, показывать
 * только критичное, обходиться без крупных заливок и всегда показывать время.
 *
 * Обратный отсчёт отдыха здесь намеренно не тикает: в ambient система будит
 * приложение примерно раз в минуту, и посекундный таймер всё равно врал бы.
 * О конце отдыха сообщает вибрация из ActiveWorkoutService, ради неё экран
 * смотреть не нужно.
 */
@Composable
fun AmbientWorkoutScreen(state: MockWorkoutState) {
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color.Black),
    ) {
        // TimeText — оверлей во весь экран, поэтому он лежит отдельным слоем:
        // внутри Column он вытолкнул бы содержимое за пределы экрана.
        TimeText()

        AmbientContent(state)
    }
}

@Composable
private fun AmbientContent(state: MockWorkoutState) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 16.dp, vertical = 24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        when (state) {
            // Ровно одна строка на экран: подход — название упражнения, отдых —
            // слово «Отдых». Номер подхода, вес и «далее» убраны намеренно —
            // руку человек не поднял, и подробности ему сейчас не нужны.
            is MockWorkoutState.CurrentSet -> AmbientTitle(state.exerciseName)

            is MockWorkoutState.Rest -> AmbientTitle("Отдых")

            MockWorkoutState.ReadyToFinish -> AmbientTitle("Все подходы выполнены")

            is MockWorkoutState.Completed -> AmbientTitle("Тренировка завершена")

            is MockWorkoutState.NoActiveWorkout -> AmbientTitle("Нет тренировки")

            is MockWorkoutState.Pairing -> AmbientTitle("Не подключено")
        }
    }
}

@Composable
private fun AmbientTitle(text: String) {
    Text(
        text = text,
        // Приглушённый белый вместо чистого: у части часов это требование
        // защиты от выгорания, и в ambient так спокойнее для глаза.
        color = Color(0xFFBDBDBD),
        style = MaterialTheme.typography.titleMedium,
        textAlign = TextAlign.Center,
    )
}

