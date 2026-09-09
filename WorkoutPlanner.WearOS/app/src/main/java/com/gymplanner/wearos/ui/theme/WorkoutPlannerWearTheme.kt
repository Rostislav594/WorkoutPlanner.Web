package com.gymplanner.wearos.ui.theme

import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.wear.compose.material3.MaterialTheme

private val BrandGreen = Color(0xFF3FB950)
private val BrandOrange = Color(0xFFFF7A1A)
private val Background = Color(0xFF0F0F10)
private val Surface = Color(0xFF1E1E1F)
private val TextPrimary = Color(0xFFF7F7F8)
private val TextMuted = Color(0xFFA6A6AD)

@Composable
fun WorkoutPlannerWearTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = MaterialTheme.colorScheme.copy(
            primary = BrandGreen,
            secondary = BrandOrange,
            background = Background,
            surfaceContainer = Surface,
            onPrimary = TextPrimary,
            onBackground = TextPrimary,
            onSurface = TextPrimary,
            onSurfaceVariant = TextMuted,
        ),
        content = content,
    )
}

