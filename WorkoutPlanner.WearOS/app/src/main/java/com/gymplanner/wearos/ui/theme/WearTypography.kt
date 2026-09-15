package com.gymplanner.wearos.ui.theme

import android.graphics.Typeface as AndroidTypeface
import androidx.compose.ui.text.font.FontFamily
import androidx.wear.compose.material3.Typography

/**
 * Узкая гарнитура вместо Bahnschrift из макета.
 *
 * Bahnschrift — системный шрифт Windows, на Wear OS его нет и лицензия не
 * позволяет положить его в APK. Ближайшее, что есть на устройстве, — системное
 * семейство `sans-serif-condensed`: такой же сжатый гротеск, поэтому цифры веса
 * и длинные названия упражнений занимают столько же места, сколько в макете.
 *
 * Брать шрифт с устройства, а не класть в APK, — осознанный выбор: приложение
 * для часов и так весит немало, а собственный файл шрифта добавил бы к сборке
 * сотни килобайт ради небольшой разницы в рисунке букв.
 */
val CondensedFontFamily: FontFamily = FontFamily(
    AndroidTypeface.create("sans-serif-condensed", AndroidTypeface.NORMAL),
)

/**
 * Типографика часов поверх стандартной: меняется только гарнитура, размеры и
 * насыщенность остаются материаловскими — они рассчитаны под маленький экран.
 */
fun condensedTypography(base: Typography): Typography = base.copy(
    displayLarge = base.displayLarge.copy(fontFamily = CondensedFontFamily),
    displayMedium = base.displayMedium.copy(fontFamily = CondensedFontFamily),
    displaySmall = base.displaySmall.copy(fontFamily = CondensedFontFamily),
    titleLarge = base.titleLarge.copy(fontFamily = CondensedFontFamily),
    titleMedium = base.titleMedium.copy(fontFamily = CondensedFontFamily),
    titleSmall = base.titleSmall.copy(fontFamily = CondensedFontFamily),
    labelLarge = base.labelLarge.copy(fontFamily = CondensedFontFamily),
    labelMedium = base.labelMedium.copy(fontFamily = CondensedFontFamily),
    labelSmall = base.labelSmall.copy(fontFamily = CondensedFontFamily),
    bodyLarge = base.bodyLarge.copy(fontFamily = CondensedFontFamily),
    bodyMedium = base.bodyMedium.copy(fontFamily = CondensedFontFamily),
    bodySmall = base.bodySmall.copy(fontFamily = CondensedFontFamily),
)
