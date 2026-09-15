package com.gymplanner.wearos.domain.repository

import com.gymplanner.wearos.domain.model.MockWorkoutState
import kotlinx.coroutines.flow.StateFlow

/**
 * Часы умеют немного: подключиться, отметить подход и закрыть тренировку.
 *
 * Редактирование веса и повторов, отмена подхода и разбор конфликтов
 * сознательно убраны из клиента часов — это работа основного приложения.
 * Очередь синхронизации осталась внутри: она нужна, чтобы отметки подходов
 * доходили до сервера после потери связи, но наружу её больше не показываем.
 */
interface WorkoutRepository {
    val state: StateFlow<MockWorkoutState>

    suspend fun pair(pairingCode: String)

    /**
     * Основной способ подключения: заявка уходит на сервер, ссылка открывается
     * на телефоне, пользователь подтверждает одним нажатием.
     * Ввод кода остаётся запасным путём, когда телефон недоступен.
     */
    suspend fun pairWithPhoneConfirmation()

    suspend fun retryPairing()

    suspend fun refreshActiveWorkout()

    suspend fun completeCurrentSet(): Boolean

    suspend fun finishWorkout(): FinishWorkoutResult

    suspend fun finishRest()
}

sealed interface FinishWorkoutResult {
    data object Success : FinishWorkoutResult
    data object UnresolvedOperations : FinishWorkoutResult
    data class Failure(val message: String) : FinishWorkoutResult
}
