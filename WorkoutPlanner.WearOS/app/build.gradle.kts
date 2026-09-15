import org.jetbrains.kotlin.gradle.dsl.JvmTarget
import org.gradle.api.GradleException
import java.net.URI

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
    id("org.jetbrains.kotlin.kapt")
}

val verifyReleaseConfiguration = tasks.register("verifyReleaseConfiguration") {
    group = "verification"
    description = "Rejects unsafe or missing Wear OS release API configuration."
    doLast {
        val value = providers.gradleProperty("WEAR_RELEASE_API_BASE_URL").orNull
            ?: throw GradleException(
                "WEAR_RELEASE_API_BASE_URL is required for release builds.",
            )
        val uri = runCatching { URI(value) }.getOrNull()
            ?: throw GradleException("WEAR_RELEASE_API_BASE_URL must be a valid absolute URI.")
        if (
            uri.scheme != "https" ||
            uri.host.isNullOrBlank() ||
            uri.rawUserInfo != null ||
            uri.rawQuery != null ||
            uri.rawFragment != null ||
            !value.endsWith("/")
        ) {
            throw GradleException(
                "WEAR_RELEASE_API_BASE_URL must use HTTPS, contain no credentials/query/fragment, " +
                    "and end with '/'.",
            )
        }
        val host = uri.host.lowercase()
        if (
            host.endsWith(".invalid") ||
            host.endsWith(".local") ||
            host == "localhost" ||
            host == "0.0.0.0" ||
            host == "::1" ||
            host.startsWith("127.") ||
            host == "10.0.2.2"
        ) {
            throw GradleException(
                "WEAR_RELEASE_API_BASE_URL must not point to a placeholder or local host.",
            )
        }
    }
}

tasks.matching { it.name == "preReleaseBuild" }.configureEach {
    dependsOn(verifyReleaseConfiguration)
}

android {
    namespace = "com.gymplanner.wearos"
    compileSdk = 36
    buildToolsVersion = "36.0.0"

    defaultConfig {
        // Тот же идентификатор, что у телефонного приложения. Google требует
        // одинаковый package name, чтобы Play отдавал часам и телефону сборки
        // одного продукта, а не двух независимых приложений с разными отзывами.
        // namespace при этом остаётся своим: он определяет пакет R и BuildConfig
        // и к идентификатору установки отношения не имеет.
        applicationId = "com.gymplanner.mobile"
        minSdk = 30
        targetSdk = 36

        // versionCode обязан быть уникальным среди всех форм-факторов одного
        // package name, поэтому у часов своя полоса нумерации: 2_000_000+.
        // Телефон остаётся в диапазоне до 1_000_000, и коды никогда не столкнутся.
        versionCode = 2_000_001
        versionName = "0.1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildTypes {
        debug {
            // Отладочная сборка живёт под своим идентификатором.
            //
            // В релизе часы и телефон обязаны делить package name и один ключ
            // подписи, но отладочные сборки подписаны разными ключами: MAUI
            // берёт свой debug keystore, Gradle — свой. С одинаковым id они не
            // могут ни сосуществовать, ни заменить друг друга: установка падает
            // с INSTALL_FAILED_UPDATE_INCOMPATIBLE. Суффикс убирает и это, и
            // риск случайно затереть телефонное приложение сборкой для часов.
            applicationIdSuffix = ".watch"

            val debugApiBaseUrl = providers.gradleProperty("WEAR_DEBUG_API_BASE_URL")
                .orElse("http://10.0.2.2:5121/api/watch/")
                .get()
            buildConfigField("String", "API_BASE_URL", "\"$debugApiBaseUrl\"")
        }
        release {
            isMinifyEnabled = false
            val releaseApiBaseUrl = providers.gradleProperty("WEAR_RELEASE_API_BASE_URL")
                .orElse("https://example.invalid/api/watch/")
                .get()
            buildConfigField("String", "API_BASE_URL", "\"$releaseApiBaseUrl\"")
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro",
            )
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    packaging {
        resources.excludes += "/META-INF/{AL2.0,LGPL2.1}"
    }
}

kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_17)
    }
}

dependencies {
    val composeBom = platform("androidx.compose:compose-bom:2026.03.00")

    implementation(composeBom)
    androidTestImplementation(composeBom)

    implementation("androidx.activity:activity-compose:1.13.0")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.10.0")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.10.0")

    implementation("androidx.wear.compose:compose-foundation:1.6.2")
    implementation("androidx.wear.compose:compose-material3:1.6.2")
    implementation("androidx.wear.compose:compose-navigation:1.6.2")

    // Открытие ссылки подтверждения на сопряжённом телефоне.
    implementation("androidx.wear:wear-remote-interactions:1.1.0")

    // Индикатор идущей тренировки на циферблате и в списке приложений.
    implementation("androidx.wear:wear-ongoing:1.0.0")
    implementation("androidx.core:core-ktx:1.17.0")

    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.10.2")

    implementation("com.squareup.retrofit2:retrofit:3.0.0")
    implementation("com.squareup.retrofit2:converter-gson:3.0.0")

    implementation("androidx.room:room-runtime:2.8.4")
    implementation("androidx.room:room-ktx:2.8.4")
    kapt("androidx.room:room-compiler:2.8.4")

    implementation("androidx.work:work-runtime:2.11.2")

    testImplementation("junit:junit:4.13.2")
    testImplementation("org.jetbrains.kotlinx:kotlinx-coroutines-test:1.10.2")

    androidTestImplementation("androidx.test.ext:junit:1.3.0")
    androidTestImplementation("androidx.test.espresso:espresso-core:3.7.0")
    androidTestImplementation("androidx.compose.ui:ui-test-junit4")
    androidTestImplementation("androidx.room:room-testing:2.8.4")

    debugImplementation("androidx.compose.ui:ui-tooling")
    debugImplementation("androidx.compose.ui:ui-test-manifest")
}
