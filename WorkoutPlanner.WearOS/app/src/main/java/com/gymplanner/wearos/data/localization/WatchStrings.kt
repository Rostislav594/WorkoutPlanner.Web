package com.gymplanner.wearos.data.localization

import android.content.Context
import androidx.annotation.StringRes

/**
 * Тексты для слоёв без Compose: репозитория, очереди синхронизации и сетевого
 * источника. В Compose для того же набора ресурсов используется stringResource.
 *
 * Язык на часах выбирает система: Android сам подставляет values-uk или
 * values-en. Язык из профиля в телефоне сюда не приходит — у часов свои
 * настройки локали.
 */
interface WatchStrings {
    fun get(@StringRes id: Int, vararg args: Any): String
}

class AndroidWatchStrings(context: Context) : WatchStrings {
    // Держим applicationContext: источник данных живёт дольше любой активности.
    private val appContext = context.applicationContext

    override fun get(@StringRes id: Int, vararg args: Any): String =
        if (args.isEmpty()) appContext.getString(id) else appContext.getString(id, *args)
}
