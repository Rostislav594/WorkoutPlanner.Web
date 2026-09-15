package com.gymplanner.wearos.domain.phone

/**
 * Открытие ссылки на сопряжённом телефоне.
 *
 * Вынесено в абстракцию, потому что репозиторий не должен знать про Android-интенты,
 * а тестам нужен подменяемый вариант.
 */
interface PhoneLinkOpener {
    suspend fun open(url: String): PhoneLinkResult
}

sealed interface PhoneLinkResult {
    data object Success : PhoneLinkResult

    /// Телефон не сопряжён или недоступен — остаётся запасной путь с вводом кода.
    data object NoPhoneAvailable : PhoneLinkResult

    data class Failure(val message: String) : PhoneLinkResult
}
