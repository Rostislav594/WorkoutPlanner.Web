package com.gymplanner.wearos.data.security

import android.content.Context
import java.util.UUID

class DeviceIdentityStore(context: Context) {
    private val preferences = context.applicationContext.getSharedPreferences(
        preferencesName,
        Context.MODE_PRIVATE,
    )

    fun getOrCreateDeviceId(): String {
        preferences.getString(deviceIdKey, null)?.let { return it }
        val deviceId = UUID.randomUUID().toString()
        check(preferences.edit().putString(deviceIdKey, deviceId).commit()) {
            "Unable to persist watch device identifier."
        }
        return deviceId
    }

    private companion object {
        const val preferencesName = "watch_device_identity"
        const val deviceIdKey = "device_id"
    }
}
