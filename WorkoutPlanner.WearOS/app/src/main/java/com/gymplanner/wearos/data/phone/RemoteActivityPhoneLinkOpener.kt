package com.gymplanner.wearos.data.phone

import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.wear.remote.interactions.RemoteActivityHelper
import com.google.common.util.concurrent.ListenableFuture
import com.gymplanner.wearos.domain.phone.PhoneLinkOpener
import com.gymplanner.wearos.domain.phone.PhoneLinkResult
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

/**
 * Открывает ссылку подтверждения на телефоне средствами Wear OS.
 *
 * RemoteActivityHelper сам находит сопряжённый телефон и передаёт ему интент.
 * Если телефона рядом нет, вызов завершается ошибкой — сообщаем об этом честно,
 * чтобы пользователь мог перейти к вводу кода руками.
 */
class RemoteActivityPhoneLinkOpener(
    context: Context,
) : PhoneLinkOpener {
    private val helper = RemoteActivityHelper(context.applicationContext)

    override suspend fun open(url: String): PhoneLinkResult {
        val intent = Intent(Intent.ACTION_VIEW)
            .addCategory(Intent.CATEGORY_BROWSABLE)
            .setData(Uri.parse(url))

        return try {
            helper.startRemoteActivity(intent).awaitCompletion()
            PhoneLinkResult.Success
        } catch (exception: CancellationException) {
            throw exception
        } catch (_: Exception) {
            // Единственный практический случай — рядом нет доступного телефона.
            PhoneLinkResult.NoPhoneAvailable
        }
    }

    /**
     * Ожидание ListenableFuture без kotlinx-coroutines-guava: нужен только
     * addListener, который есть у самого future.
     */
    private suspend fun ListenableFuture<Void>.awaitCompletion(): Unit =
        suspendCancellableCoroutine { continuation ->
            addListener(
                {
                    try {
                        get()
                        continuation.resume(Unit)
                    } catch (exception: Exception) {
                        continuation.resumeWithException(exception)
                    }
                },
                { runnable -> runnable.run() },
            )
            continuation.invokeOnCancellation { cancel(false) }
        }
}
