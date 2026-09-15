package com.gymplanner.wearos.ui.pairing

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.model.PairingStatus
import com.gymplanner.wearos.ui.common.GlowFrame
import com.gymplanner.wearos.ui.common.NeonButton
import com.gymplanner.wearos.ui.theme.WearColors

/**
 * Подключение часов в оформлении тренировки: тот же чёрный фон и те же кнопки.
 * Светящейся обводки здесь нет — по макетам она осталась только на отдыхе.
 */
@Composable
fun PairingScreen(
    state: MockWorkoutState.Pairing,
    pairingCode: String,
    onPairingCodeChange: (String) -> Unit,
    onConnect: () -> Unit,
    onConfirmOnPhone: () -> Unit,
    onRetry: () -> Unit,
) {
    // Ручной ввод — запасной путь, поэтому он прячется за отдельным действием
    // и не мешает основному сценарию с подтверждением на телефоне.
    var manualEntry by rememberSaveable { mutableStateOf(false) }

    // key по режиму: у экранов общий слот, а значит и общее состояние прокрутки.
    // Без него возврат из ручного ввода открывал бы экран прокрученным вниз.
    key(manualEntry, state.status == PairingStatus.WaitingForPhone) {
        when {
            state.status == PairingStatus.WaitingForPhone -> WaitingForPhoneScreen()
            manualEntry -> ManualCodeScreen(
                state = state,
                pairingCode = pairingCode,
                onPairingCodeChange = onPairingCodeChange,
                onConnect = onConnect,
                onRetry = onRetry,
                onBack = { manualEntry = false },
            )
            else -> PhoneConfirmationScreen(
                state = state,
                onConfirmOnPhone = onConfirmOnPhone,
                onRetry = onRetry,
                onManualEntry = { manualEntry = true },
            )
        }
    }
}

@Composable
private fun PhoneConfirmationScreen(
    state: MockWorkoutState.Pairing,
    onConfirmOnPhone: () -> Unit,
    onRetry: () -> Unit,
    onManualEntry: () -> Unit,
) {
    val isConnecting = state.status == PairingStatus.Connecting
    val isError = state.status == PairingStatus.Error

    GlowFrame {
        Text(
            text = "Подключение",
            color = WearColors.NeonGreen,
            style = MaterialTheme.typography.titleSmall,
            textAlign = TextAlign.Center,
            maxLines = 1,
        )
        // Подсказку показываем, только когда есть что сказать: на круглом
        // экране каждая лишняя строка выдавливает кнопки за кромку.
        val hint = state.errorMessage ?: if (isConnecting) "Открываем на телефоне…" else null
        hint?.let {
            Spacer(Modifier.height(4.dp))
            Text(
                text = it,
                color = if (isError) WearColors.Error else WearColors.TextMuted,
                style = MaterialTheme.typography.labelSmall,
                textAlign = TextAlign.Center,
            )
        }
        Spacer(Modifier.height(8.dp))
        NeonButton(
            label = when {
                isError -> "Повторить"
                isConnecting -> "Открываем…"
                else -> "Подтвердить на телефоне"
            },
            enabled = !isConnecting,
            onClick = if (isError) onRetry else onConfirmOnPhone,
        )
        Spacer(Modifier.height(6.dp))
        NeonButton(
            label = "Ввести код",
            primary = false,
            enabled = !isConnecting,
            onClick = onManualEntry,
        )
    }
}

@Composable
private fun WaitingForPhoneScreen() {
    GlowFrame {
        Text(
            text = "Подтвердите на телефоне",
            color = WearColors.Amber,
            style = MaterialTheme.typography.titleSmall,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = "Откройте GymPlanner на телефоне и нажмите «Подключить часы».",
            color = WearColors.TextPrimary,
            style = MaterialTheme.typography.labelSmall,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = "Ждём ответа…",
            color = WearColors.TextMuted,
            style = MaterialTheme.typography.labelSmall,
            textAlign = TextAlign.Center,
        )
    }
}

@Composable
private fun ManualCodeScreen(
    state: MockWorkoutState.Pairing,
    pairingCode: String,
    onPairingCodeChange: (String) -> Unit,
    onConnect: () -> Unit,
    onRetry: () -> Unit,
    onBack: () -> Unit,
) {
    val isConnecting = state.status == PairingStatus.Connecting
    val isError = state.status == PairingStatus.Error

    GlowFrame {
        Text(
            text = "Введите код",
            color = WearColors.NeonGreen,
            style = MaterialTheme.typography.titleSmall,
            textAlign = TextAlign.Center,
            maxLines = 1,
        )
        // Отступы здесь теснее, чем на других экранах: поле ввода и две кнопки
        // вместе выше круглого экрана, и «Назад» уходила краями за кромку.
        Spacer(Modifier.height(4.dp))
        PairingCodeField(
            value = pairingCode,
            enabled = !isConnecting,
            isError = isError,
            onValueChange = onPairingCodeChange,
            onDone = onConnect,
        )
        val hint = state.errorMessage ?: if (isConnecting) "Проверяем код…" else null
        hint?.let {
            Spacer(Modifier.height(4.dp))
            Text(
                text = it,
                color = if (isError) WearColors.Error else WearColors.TextMuted,
                style = MaterialTheme.typography.labelSmall,
                textAlign = TextAlign.Center,
            )
        }
        Spacer(Modifier.height(2.dp))
        NeonButton(
            label = when {
                isError -> "Повторить"
                isConnecting -> "Подключаем…"
                else -> "Подключить"
            },
            enabled = isError || (!isConnecting && pairingCode.length == pairingCodeLength),
            onClick = if (isError) onRetry else onConnect,
        )
        Spacer(Modifier.height(2.dp))
        NeonButton(
            label = "Назад",
            primary = false,
            enabled = !isConnecting,
            onClick = onBack,
        )
    }
}

/**
 * Поле кода оформлено той же янтарной плашкой, что и вес с подходами: и там,
 * и там это данные, а не действие.
 */
@Composable
private fun PairingCodeField(
    value: String,
    enabled: Boolean,
    isError: Boolean,
    onValueChange: (String) -> Unit,
    onDone: () -> Unit,
) {
    var isFocused by remember { mutableStateOf(false) }
    val borderColor = when {
        isError -> WearColors.Error
        isFocused -> WearColors.NeonGreen
        else -> WearColors.Amber
    }

    BasicTextField(
        value = value,
        onValueChange = onValueChange,
        enabled = enabled,
        singleLine = true,
        textStyle = MaterialTheme.typography.titleMedium.copy(
            color = WearColors.Amber,
            fontWeight = FontWeight.SemiBold,
            textAlign = TextAlign.Center,
        ),
        keyboardOptions = KeyboardOptions(
            keyboardType = KeyboardType.NumberPassword,
            imeAction = ImeAction.Done,
        ),
        keyboardActions = KeyboardActions(onDone = { onDone() }),
        cursorBrush = SolidColor(WearColors.NeonGreen),
        modifier = Modifier
            .fillMaxWidth()
            .height(fieldHeightDp.dp)
            .semantics { contentDescription = "Шестизначный код подключения" }
            .onFocusChanged { isFocused = it.isFocused },
        decorationBox = { innerTextField ->
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(fieldHeightDp.dp)
                    .background(
                        WearColors.AmberDim.copy(alpha = 0.25f),
                        RoundedCornerShape(12.dp),
                    )
                    .border(2.dp, borderColor, RoundedCornerShape(12.dp))
                    .padding(horizontal = 12.dp),
                contentAlignment = Alignment.Center,
            ) {
                if (value.isEmpty()) {
                    Text(
                        text = "••••••",
                        color = WearColors.TextMuted,
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
                innerTextField()
            }
        },
    )
}

private const val pairingCodeLength = 6
private const val fieldHeightDp = 38
