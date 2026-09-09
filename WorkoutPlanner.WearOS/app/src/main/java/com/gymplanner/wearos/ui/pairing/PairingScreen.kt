package com.gymplanner.wearos.ui.pairing

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
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
import androidx.wear.compose.material3.Button
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Text
import com.gymplanner.wearos.domain.model.MockWorkoutState
import com.gymplanner.wearos.domain.model.PairingStatus
import com.gymplanner.wearos.ui.common.WearScreen

@Composable
fun PairingScreen(
    state: MockWorkoutState.Pairing,
    pairingCode: String,
    onPairingCodeChange: (String) -> Unit,
    onConnect: () -> Unit,
    onRetry: () -> Unit,
) {
    val isConnecting = state.status == PairingStatus.Connecting
    val isError = state.status == PairingStatus.Error

    WearScreen {
        Text(
            text = "Введите код",
            color = MaterialTheme.colorScheme.primary,
            style = MaterialTheme.typography.titleLarge,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(6.dp))
        PairingCodeField(
            value = pairingCode,
            enabled = !isConnecting,
            isError = isError,
            onValueChange = onPairingCodeChange,
            onDone = onConnect,
        )
        Spacer(Modifier.height(4.dp))
        Text(
            text = state.errorMessage ?: if (isConnecting) "Проверяем код…" else "Демо-код: 123456",
            color = if (isError) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurfaceVariant,
            style = MaterialTheme.typography.bodySmall,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(6.dp))
        Button(
            onClick = if (isError) onRetry else onConnect,
            enabled = isError || (!isConnecting && pairingCode.length == pairingCodeLength),
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(min = 48.dp),
        ) {
            Text(
                text = when {
                    isError -> "Повторить"
                    isConnecting -> "Подключаем…"
                    else -> "Подключить"
                },
                textAlign = TextAlign.Center,
            )
        }
    }
}

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
        isError -> MaterialTheme.colorScheme.error
        isFocused -> MaterialTheme.colorScheme.primary
        else -> MaterialTheme.colorScheme.outline
    }

    BasicTextField(
        value = value,
        onValueChange = onValueChange,
        enabled = enabled,
        singleLine = true,
        textStyle = MaterialTheme.typography.titleLarge.copy(
            color = MaterialTheme.colorScheme.onSurface,
            fontWeight = FontWeight.SemiBold,
            textAlign = TextAlign.Center,
        ),
        keyboardOptions = KeyboardOptions(
            keyboardType = KeyboardType.NumberPassword,
            imeAction = ImeAction.Done,
        ),
        keyboardActions = KeyboardActions(onDone = { onDone() }),
        cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
        modifier = Modifier
            .fillMaxWidth()
            .height(48.dp)
            .onFocusChanged { isFocused = it.isFocused }
            .semantics { contentDescription = "Шестизначный код подключения" },
        decorationBox = { innerTextField ->
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(48.dp)
                    .background(MaterialTheme.colorScheme.surfaceContainer, RoundedCornerShape(28.dp))
                    .border(2.dp, borderColor, RoundedCornerShape(28.dp))
                    .padding(horizontal = 16.dp),
                contentAlignment = Alignment.Center,
            ) {
                if (value.isEmpty()) {
                    Text(
                        text = "••••••",
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
                innerTextField()
            }
        },
    )
}

private const val pairingCodeLength = 6
