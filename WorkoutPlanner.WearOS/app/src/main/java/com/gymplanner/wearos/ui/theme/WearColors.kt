package com.gymplanner.wearos.ui.theme

import androidx.compose.ui.graphics.Color

/**
 * Палитра часов.
 *
 * Значения взяты из токенов мобильного клиента (`GymPlanner.Mobile/wwwroot/app.css`):
 * макеты в Figma нарисованы именно ими, и расхождение цветов между телефоном и
 * часами было бы заметно человеку, который пользуется обоими.
 *
 * Роли те же, что и раньше: зелёный — действие доступно, оранжевый — пауза и
 * данные подхода. Поменялись только сами цвета.
 *
 * Фон остаётся чистым чёрным, а не `--color-bg` (#0F0F10) с телефона. На OLED-экране
 * часов чёрный пиксель не светится: это и экономия батареи, и единственный способ
 * заставить светящуюся обводку читаться как свечение, а не как рамка на сером.
 */
object WearColors {
    val Background = Color(0xFF000000)

    /** --color-success-hover: основной зелёный макета. */
    val NeonGreen = Color(0xFF4FC46B)

    /** --color-success: приглушённый зелёный для заливок и вторых планов. */
    val NeonGreenDim = Color(0xFF3FB950)

    /** --color-accent: оранжевый телефона, им же нарисованы плашки в макете. */
    val Amber = Color(0xFFFF7A1A)

    /** Затемнённый оранжевый для подложек — аналог --color-accent-soft. */
    val AmberDim = Color(0xFF5C2D08)

    /** --color-text */
    val TextPrimary = Color(0xFFF7F7F8)

    /** --color-text-muted */
    val TextMuted = Color(0xFFA6A6AD)

    /** --color-danger */
    val Error = Color(0xFFF85149)

    /** Заливка экранов завершения: сверху светлее, снизу глубже. */
    val CompletedTop = Color(0xFF3FB950)
    val CompletedBottom = Color(0xFF17512F)
}
