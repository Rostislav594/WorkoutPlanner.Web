package com.gymplanner.wearos.data.security

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

class AndroidKeystoreTokenStore(context: Context) : SecureTokenStore {
    private val preferences = context.getSharedPreferences(preferencesName, Context.MODE_PRIVATE)

    override fun readRefreshToken(): String? {
        val encryptedToken = preferences.getString(encryptedTokenKey, null) ?: return null
        val initializationVector = preferences.getString(initializationVectorKey, null) ?: return null
        val cipher = Cipher.getInstance(transformation)
        cipher.init(
            Cipher.DECRYPT_MODE,
            getOrCreateKey(),
            GCMParameterSpec(tagLengthBits, Base64.decode(initializationVector, Base64.NO_WRAP)),
        )
        return cipher.doFinal(Base64.decode(encryptedToken, Base64.NO_WRAP)).decodeToString()
    }

    override fun saveRefreshToken(refreshToken: String) {
        require(refreshToken.isNotBlank())
        val cipher = Cipher.getInstance(transformation)
        cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey())
        val encryptedToken = cipher.doFinal(refreshToken.encodeToByteArray())
        check(preferences.edit()
            .putString(encryptedTokenKey, Base64.encodeToString(encryptedToken, Base64.NO_WRAP))
            .putString(initializationVectorKey, Base64.encodeToString(cipher.iv, Base64.NO_WRAP))
            .commit()) { "Unable to persist encrypted refresh token." }
    }

    override fun clear() {
        preferences.edit()
            .remove(encryptedTokenKey)
            .remove(initializationVectorKey)
            .commit()
    }

    private fun getOrCreateKey(): SecretKey {
        val keyStore = KeyStore.getInstance(keyStoreProvider).apply { load(null) }
        (keyStore.getKey(keyAlias, null) as? SecretKey)?.let { return it }

        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, keyStoreProvider).run {
            init(
                KeyGenParameterSpec.Builder(
                    keyAlias,
                    KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT,
                )
                    .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                    .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                    .build(),
            )
            generateKey()
        }
    }

    private companion object {
        const val keyStoreProvider = "AndroidKeyStore"
        const val keyAlias = "gymplanner.watch.refresh-token"
        const val transformation = "AES/GCM/NoPadding"
        const val tagLengthBits = 128
        const val preferencesName = "watch_credentials"
        const val encryptedTokenKey = "refresh_token_ciphertext"
        const val initializationVectorKey = "refresh_token_iv"
    }
}
