# WorkoutPlanner Wear OS

Отдельный Kotlin/Compose for Wear OS клиент GymPlanner. Minimal MVP прошёл
end-to-end контрольную точку Этапа 10, а Этап 14 добавил обзор тренировки,
редактирование текущего подхода, undo и подробный статус синхронизации. Этап 15
добавил подтверждаемое завершение всей тренировки. Все записи по-прежнему проходят
через offline-first Room-очередь и реальный WorkoutPlanner API.

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
.\gradlew.bat lintDebug
```

macOS/Linux:

```bash
export JAVA_HOME=/path/to/jdk-17
export ANDROID_HOME=/path/to/android-sdk
./gradlew assembleDebug
./gradlew testDebugUnitTest
./gradlew lintDebug
```

Backend для локальной проверки запускается из корня репозитория:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project WorkoutPlanner.Web/WorkoutPlanner.Web.csproj --urls http://127.0.0.1:5121
```

## Запуск на Wear OS Emulator

1. В Android Studio откройте каталог `WorkoutPlanner.WearOS`.
2. В Device Manager создайте Wear OS virtual device с API 30+.
3. Запустите AVD и дождитесь полной загрузки.
4. Выберите конфигурацию `app` и нажмите Run.
5. В мобильном приложении откройте `Профиль → Смарт-часы Wear OS` и создайте
   одноразовый код подключения. Веб-профиль можно использовать как запасной путь.
6. Введите этот шестизначный код на часах и нажмите «Подключить».
7. На экране отсутствующей тренировки нажмите «Обновить».
8. Проверьте цикл `Текущий подход → Выполнить → Отдых → следующий подход`.

Для Android Emulator debug-сборка по умолчанию обращается к
`http://10.0.2.2:5121/api/watch/`. Другой локальный адрес можно передать без
изменения исходников:

```powershell
.\gradlew.bat -PWEAR_DEBUG_API_BASE_URL=http://10.0.2.2:5131/api/watch/ assembleDebug
```

Release URL задаётся через обязательный `WEAR_RELEASE_API_BASE_URL`. Он должен быть
абсолютным HTTPS URL с завершающим `/` и без credentials, query или fragment;
loopback, `.local`, `10.0.2.2` и `.invalid` отклоняются release-проверкой:

```powershell
.\gradlew.bat -PWEAR_RELEASE_API_BASE_URL=https://api.example.com/api/watch/ assembleRelease
```

Замените example hostname на production. Release signing в репозитории намеренно
не хранится; без внешней signing-конфигурации Gradle создаёт unsigned artifact.

Из командной строки после запуска emulator:

```powershell
.\gradlew.bat installDebug
adb shell am start -n com.gymplanner.wearos/.MainActivity
```

## Архитектура клиента после Этапа 15

- `data/repository/OfflineFirstWorkoutRepository` — local-first источник состояния;
- Room хранит локальную тренировку, упражнения, подходы, session metadata и
  `PendingSyncOperation`;
- completion, undo и редактирование вместе с pending operation записываются
  атомарными Room-транзакциями;
- один unique WorkManager job последовательно обрабатывает общую очередь;
- `RetrofitWatchRemoteDataSource` реализует pairing, refresh, загрузку активной
  тренировки, complete, undo и update через реальный API;
- `domain` — состояния pairing, no-active, current-set, rest и ready-to-finish;
- `ui` — Minimal MVP flow на реальных данных с offline-first completion;
- refresh token хранится через Android Keystore, access token — только в памяти;
- debug-логирование выводит только method/path/status/duration и не выводит body,
  headers или credentials;
- `FakeWatchRemoteDataSource` остаётся только тестовым double и production DI его
  не использует.

Главный путь остаётся `Текущий подход → Выполнить → Отдых → следующий подход`.
Вторичные экраны позволяют открыть обзор, изменить индивидуальный вес/повторы,
отменить последний выполненный подход и увидеть pending/error/conflict операции.
Подтверждённые операции удаляются, конфликтные не перезаписываются молча, а очередь
ограничена 100 нерешёнными операциями. Room schema версии 2 добавляет только имя
тренировки в локальный кэш; серверная схема на Этапе 14 не менялась.

После последнего подхода часы не удаляют локальную тренировку автоматически.
Пользователь видит итоговый экран, явно подтверждает завершение, после чего клиент
сначала отправляет все pending set-операции. При конфликте или ошибке тренировка
остаётся локально активной. Только подтверждённый ответ
`POST /api/watch/workouts/{workoutId}/finish` очищает активный экран; повторный
запрос безопасен, а сервер использует общий pipeline создания истории.

Результаты контрольной точки приведены в
[`docs/wear-os/STAGE_10_MILESTONE.md`](../docs/wear-os/STAGE_10_MILESTONE.md).

Итоговая архитектура и подготовка выпуска описаны в
[`docs/wear-os/ARCHITECTURE.md`](../docs/wear-os/ARCHITECTURE.md) и
[`docs/wear-os/RELEASE_CHECKLIST.md`](../docs/wear-os/RELEASE_CHECKLIST.md).
