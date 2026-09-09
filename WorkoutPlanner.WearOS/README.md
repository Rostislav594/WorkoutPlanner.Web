# WorkoutPlanner Wear OS

Отдельный Kotlin/Compose for Wear OS клиент GymPlanner. Текущая реализация относится
к Этапу 8: Minimal Wear OS flow работает на mock data, а локальное выполнение
подхода и очередь синхронизации сохраняются в Room. Production API пока не
подключён.

## Требования

- JDK 17 или новее;
- Android SDK Platform 36 и Build Tools 36.0.0;
- Wear OS emulator с API 30 или новее;
- `ANDROID_HOME` или `ANDROID_SDK_ROOT`, указывающий на Android SDK.

## Сборка и тесты

Windows:

```powershell
$env:JAVA_HOME = "C:\path\to\jdk-17"
$env:ANDROID_HOME = "C:\path\to\android-sdk"
.\gradlew.bat assembleDebug
.\gradlew.bat testDebugUnitTest
```

macOS/Linux:

```bash
export JAVA_HOME=/path/to/jdk-17
export ANDROID_HOME=/path/to/android-sdk
./gradlew assembleDebug
./gradlew testDebugUnitTest
```

## Запуск на Wear OS Emulator

1. В Android Studio откройте каталог `WorkoutPlanner.WearOS`.
2. В Device Manager создайте Wear OS virtual device с API 30+.
3. Запустите AVD и дождитесь полной загрузки.
4. Выберите конфигурацию `app` и нажмите Run.
5. Введите demo-код `123456` и нажмите «Подключить».
6. На экране отсутствующей тренировки нажмите «Обновить».
7. Проверьте цикл `Текущий подход → Выполнить → Отдых → следующий подход`.

Из командной строки после запуска emulator:

```powershell
.\gradlew.bat installDebug
adb shell am start -n com.gymplanner.wearos/.MainActivity
```

## Архитектурная граница Этапа 8

- `data/repository/OfflineFirstWorkoutRepository` — local-first источник состояния;
- Room хранит локальную тренировку, упражнения, подходы, session metadata и
  `PendingSyncOperation`;
- completion и pending operation записываются одной Room-транзакцией;
- один unique WorkManager job последовательно обрабатывает общую очередь;
- `FakeWatchRemoteDataSource` имитирует идемпотентный сервер и учитывает фактическое
  состояние сети устройства;
- `domain` — состояния pairing, no-active, current-set и rest;
- `ui` — полноценный Minimal MVP flow на mock data;
- `AndroidKeystoreTokenStore` — безопасная заготовка для refresh token;
- Retrofit настроен как граница будущего production API.

Pairing с backend и реальный API относятся к следующим этапам плана. Вес и повторы
на часах остаются только для чтения; расширенный conflict UI не реализован.
