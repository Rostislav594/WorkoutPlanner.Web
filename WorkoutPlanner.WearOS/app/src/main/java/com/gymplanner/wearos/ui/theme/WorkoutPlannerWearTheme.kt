package com.gymplanner.wearos.ui.theme

import androidx.compose.runtime.Composable
import androidx.wear.compose.material3.MaterialTheme

@Composable
fun WorkoutPlannerWearTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = MaterialTheme.colorScheme.copy(
            primary = WearColors.NeonGreen,
            secondary = WearColors.Amber,
            background = WearColors.Background,
            surfaceContainer = WearColors.AmberDim,
            onPrimary = WearColors.TextPrimary,
            onBackground = WearColors.TextPrimary,
            onSurface = WearColors.TextPrimary,
            onSurfaceVariant = WearColors.TextMuted,
            error = WearColors.Error,
        ),
        typography = condensedTypography(MaterialTheme.typography),
        content = content,
    )
}
