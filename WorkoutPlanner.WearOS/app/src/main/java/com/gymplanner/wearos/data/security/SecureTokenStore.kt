package com.gymplanner.wearos.data.security

interface SecureTokenStore {
    fun readRefreshToken(): String?

    fun saveRefreshToken(refreshToken: String)

    fun clear()
}

