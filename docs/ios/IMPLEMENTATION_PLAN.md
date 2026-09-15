# iOS и Apple Watch — Implementation Plan

## 1. Назначение документа

Этот документ описывает поэтапный вывод проекта `WorkoutPlanner` на платформы Apple:
iPhone/iPad (`net10.0-ios`, существующий MAUI-клиент `GymPlanner.Mobile`) и Apple Watch
(новый нативный watchOS-клиент).

«GPlanner» — разговорное название того же продукта; в коде, структуре решения и во
всех технических документах используется `WorkoutPlanner`. Промпты для агентов должны
придерживаться `WorkoutPlanner`, чтобы не появлялись имена проектов и папок, не
соответствующие реальной структуре репозитория.

Документ построен по образцу `docs/wear-os/IMPLEMENTATION_PLAN.md` и предназначен для
пошаговой работы агента (Codex или Claude Code).

**Состояние на 2026-09-15:** этапы не начаты. Разделы 4 и 5 — результат аудита кода, а не
предположение. Сборка под iOS ни разу не запускалась, потому что для неё нужен macOS,
а разработка ведётся на Windows.

---

## 2. Общие правила выполнения

1. Выполнять только тот этап, который явно указан пользователем.
2. Не переходить к следующему этапу автоматически.
3. Перед изменением кода изучить существующую реализацию.
4. Не придумывать новые сущности, если в проекте уже есть подходящие модели или сервисы.
5. Не дублировать существующую бизнес-логику и не переносить логику тренировок в клиент.
6. Не ломать Android: любое изменение общего кода проверяется сборкой обоих таргетов.
7. После каждого этапа:
   - запустить сборку;
   - запустить доступные тесты;
   - перечислить изменённые файлы;
   - описать принятые решения;
   - сообщить о нерешённых проблемах;
   - предложить название Git-коммита.
8. Не выполнять `git commit` или `git push` без явной просьбы пользователя.
9. Не удалять существующий функционал ради упрощения портирования.
10. Не выполнять массовый рефакторинг, не связанный с текущим этапом.
11. Если сборка или тесты не проходят — не переходить к следующему этапу и не подгонять
    реализацию или сами тесты ради формального прохождения проверки; описать причину и
    предложить варианты.
12. **Не заявлять о проверке на устройстве, если устройства не было.** Сборка под iOS
    без macOS невозможна; любой отчёт обязан честно называть, что не проверялось.

---

## 3. Целевая архитектура

```text
WorkoutPlanner.Web (backend, единственный источник истины)
        │
        ├── /api/v1/...   ── GymPlanner.Mobile (MAUI)
        │                      ├── net10.0-android                 — готово
        │                      └── net10.0-ios        ← этапы 1-6
        │
        └── /api/watch/... ── WorkoutPlanner.WearOS (Kotlin)        — готово
                           └── WorkoutPlanner.WatchOS (Swift)  ← этапы 7-11
```

Ключевой факт аудита: `/api/watch` спроектирован платформенно-нейтральным. Это 17
endpoint-ов обычного REST (`WorkoutPlanner.Web/Api/WatchApiEndpoints.cs`), Wear OS-клиент
работает как standalone (`com.google.android.wearable.standalone = true`) и не использует
Wearable Data Layer. Поэтому **watchOS-клиент подключается к существующему контракту без
изменений на сервере**.

Второй ключевой факт: `WatchDevice.Platform` уже существует как строка со значением по
умолчанию `"WearOS"`, а `PushDeviceRegistrationService` уже принимает платформу `"ios"`.
Серверная модель готова к обеим платформам Apple.

---

## 4. Аудит: что уже есть под iOS

Проверено чтением кода.

| Что | Где | Состояние |
|---|---|---|
| Таргет `net10.0-ios`, min 15.0, iPhone + iPad | `GymPlanner.Mobile/GymPlanner.Mobile.csproj` | есть |
| AppDelegate, делегат уведомлений | `GymPlanner.Mobile/Platforms/iOS/AppDelegate.cs` | есть |
| Точка входа | `GymPlanner.Mobile/Platforms/iOS/Program.cs` | есть |
| Локальные напоминания о тренировке | `GymPlanner.Mobile/Platforms/iOS/Notifications/PlatformLocalNotificationService.cs` | реализованы полностью, включая разбор push-payload и навигацию по тапу |
| Info.plist: камера, фото, ориентации | `GymPlanner.Mobile/Platforms/iOS/Info.plist` | есть |
| Privacy manifest | `GymPlanner.Mobile/Platforms/iOS/Resources/PrivacyInfo.xcprivacy` | есть, неполон — см. 5.1 |
| Вырезы экрана | `env(safe-area-inset-*)` в CSS, `viewport-fit=cover` в `wwwroot/index.html` | UI изначально писался с учётом iOS |
| Системный список выбора | `GymPlanner.Mobile/SystemControls/MauiSystemChoicePicker.cs:33` | ветка `#else` через `DisplayActionSheet` |
| Базовый адрес API | `GymPlanner.Mobile/Infrastructure/MobileApiOptions.cs:38` | ветка `#else` есть |
| Хранение токенов | `SecureMobileTokenStore` → `SecureStorage` → Keychain | работает без изменений |
| Камера и галерея | `GymPlanner.Mobile/Photos/MauiPhotoPicker.cs` → `MediaPicker` | работает без изменений |
| Сервер принимает платформу `ios` | `WorkoutPlanner.Web/Services/Push/PushDeviceRegistrationService.cs:14` | готово |
| Доставка push через FCM → APNs | `WorkoutPlanner.Web/Services/Push/FirebaseRemotePushProvider.cs` | готово, нужен только ключ APNs в Firebase |

**Вывод:** бэкенд под iOS переделывать не нужно. Каркас клиента есть. Но он ни разу не
собирался — «есть» здесь означает «код написан», а не «компилируется».

---

## 5. Аудит: что отсутствует или сломано под iOS

Семь пунктов. Все проверены по коду, для каждого указано точное место.

### 5.1. Privacy manifest неполон — сборку отклонит App Store

`GymPlanner.Mobile/Platforms/iOS/Resources/PrivacyInfo.xcprivacy:43` — блок
`NSPrivacyAccessedAPICategoryUserDefaults` закомментирован, а `Preferences.Default`
используется в 14 местах:

- `GymPlanner.Mobile/Localization/MauiAppLanguageService.cs` — выбранный язык;
- `GymPlanner.Mobile/Notifications/LocalWorkoutReminder.cs` — индекс напоминаний;
- `GymPlanner.Mobile/Notifications/PushRegistrationCoordinator.cs` — installation id.

Apple автоматически отклоняет сборки с необъявленным required-reason API. Правка на пять
минут, поэтому вынесена в отдельный ранний этап.

### 5.2. Платформа в регистрации push захардкожена

`GymPlanner.Mobile/Notifications/PushRegistrationCoordinator.cs:47`:

```csharp
new RegisterPushDeviceRequest(InstallationIdStore.Get(), "android", token)
```

Даже когда токен на iOS появится, устройство зарегистрируется как Android. Баг тихий:
регистрация пройдёт успешно, доставка — нет.

### 5.3. Удалённых push-уведомлений на iOS нет вообще

`GymPlanner.Mobile/MauiProgram.cs:65` регистрирует `UnavailableRemotePushTokenProvider`,
который возвращает `null`; реальный провайдер подключается только под `#if ANDROID`
(строка 66). Отсутствуют: `GoogleService-Info.plist`, entitlement `aps-environment`,
`UIBackgroundModes: remote-notification`, вызов `RegisterForRemoteNotifications`,
обработка `RegisteredForRemoteNotifications` и `FailedToRegisterForRemoteNotifications`.

Разбор входящего payload на iOS **уже написан** — `IosNotificationNavigation.OpenPush`
в `Platforms/iOS/Notifications/PlatformLocalNotificationService.cs`.

### 5.4. Подтверждение сопряжения часов с телефона на iOS не сработает

Схема `gymplanner://watch/approve` объявлена только Android-атрибутом
`GymPlanner.Mobile/Platforms/Android/MainActivity.cs:17`. В `Info.plist` нет
`CFBundleURLTypes`, в `AppDelegate` нет переопределения `OpenUrl`.

Сам парсер `GymPlanner.Mobile/Notifications/WatchPairingLink.cs` платформенно-нейтрален —
переписывать его не нужно, нужно только зарегистрировать схему и прокинуть вызов в
`NotificationNavigationService.OpenWatchApproval`.

### 5.5. Поворот экрана для графиков — заглушка

`GymPlanner.Mobile/Orientation/ScreenOrientationService.cs` целиком внутри `#if ANDROID`;
на iOS `LockLandscape()` не делает ничего. Ломается альбомный просмотр графиков:

- `GymPlanner.Mobile/Components/Pages/ProgressExercises.razor:259`
- `GymPlanner.Mobile/Components/Pages/ProgressWorkouts.razor:245`

### 5.6. Зум WebView настраивается только под Android

`GymPlanner.Mobile/MainPage.xaml.cs:16` — `SetSupportZoom` для `Android.Webkit.WebView`.
Для `WKWebView` эквивалент не настроен, поведение при щипке будет отличаться.

### 5.7. Тексты разрешений в Info.plist только по-русски

`NSCameraUsageDescription` и `NSPhotoLibraryUsageDescription` захардкожены русским.
В проекте три языка (ru/uk/en), нужны `InfoPlist.strings` для uk и en. App Review
проверяет локализацию запросов разрешений.

### Мелочи, не блокирующие релиз

- `GymPlanner.Mobile/wwwroot/index.html` — `lang="ru"` захардкожен.
- Анимация выхода сплеша реализована только под Android
  (`SplashExitAnimationListener` в `MainActivity.cs`) — косметическое расхождение.
- `GymPlanner.Mobile/Infrastructure/MobileHttpMessageHandlerFactory.cs` — обход
  dev-сертификата только под `#if DEBUG && ANDROID`; для отладки на симуляторе против
  локального бэкенда понадобится аналог или доверенный сертификат на Mac.

---

## 6. Решения, которые обязан принять владелец продукта

Эти четыре решения блокируют этапы. Агент **не должен** выбирать за пользователя.

### 6.1. Стратегия сборки под Apple

Сборка iOS и watchOS невозможна без macOS и Xcode. Варианты: физический Mac, облачный
Mac, macOS-раннер в CI, либо Pair to Mac из Visual Studio на Windows. Конкретный способ
надо перепроверить по актуальной документации .NET MAUI — набор поддерживаемых сценариев
менялся между версиями.

### 6.2. Форма watchOS-приложения

**Вариант A — встроенное приложение-компаньон.** watchOS-приложение лежит внутри бандла
iOS-приложения в папке `Watch/`. Плюсы: одна карточка в App Store; доступен
`WatchConnectivity`, то есть подтверждение сопряжения ссылкой на телефоне работает так же,
как на Wear OS. Минус: **MAUI не умеет встраивать watchOS-таргет**, потребуется
собственная post-build интеграция на Mac — сборка watch-app в Xcode, вкладывание `.app`
внутрь бандла MAUI и переподписывание. Это работает, но ломается при обновлениях MAUI.

**Вариант B — отдельное watch-only приложение.** Своя карточка в App Store, свой bundle
id, никакой связи со сборкой MAUI. Минусы: пользователь ставит два приложения;
`WatchConnectivity` недоступен, поэтому остаётся только ввод шестизначного кода на часах.
Этот запасной путь **уже реализован** на сервере (`POST /api/watch/pairing-codes` и
`POST /api/watch/pair`) и в UX мобильного приложения.

Рекомендация плана — **вариант B**. Ввод кода поддержан сервером, а кастомная пересборка
бандла MAUI — постоянный источник поломок в CI и при каждом обновлении .NET. Переход к
варианту A позже не потребует изменений сервера и API.

Дальнейшие этапы 7-11 написаны в расчёте на вариант B. При выборе варианта A меняются
этап 7 (структура проекта и интеграция сборки) и этап 8 (доступен путь подтверждения
с телефона); этапы 9-11 не меняются.

### 6.3. Поведение таймера отдыха при погасшем экране на часах

На Wear OS это решено foreground-сервисом типа `specialUse`; в манифесте прямо записано,
что тип `health` не взят намеренно, чтобы не просить доступ к датчикам тела. На watchOS
прямого аналога нет.

- **Простой путь:** локальное уведомление с тактильным сигналом на момент окончания
  отдыха. Без разрешений, но приложение в фоне не живёт.
- **Полный путь:** `HKWorkoutSession` — приложение работает в фоне всю тренировку, есть
  пульс и калории. Это HealthKit: разрешение, обоснование для App Review и **полная
  переработка раздела 2 `docs/legal/privacy-policy.md`**, потому что Apple добавляет к
  политике собственные обязательные условия.

Рекомендация плана — начать с простого пути, HealthKit вынести в отдельное решение.

### 6.4. Провайдер push для iOS

Рекомендация плана — **остаться на FCM**. `FirebaseRemotePushProvider` на бэкенде уже
написан и работает, FCM маршрутизирует в APNs по загруженному ключу `.p8`. Прямой APNs
означает второй провайдер и второй путь доставки на сервере без выигрыша.

---

## 7. Инфраструктура и доступы

| Что | Зачем | Примечание |
|---|---|---|
| Apple Developer Program | TestFlight, устройства, публикация | блокирует всё остальное |
| Mac с Xcode | сборка iOS и watchOS | см. 6.1 |
| APNs Authentication Key (.p8) | push; загружается в Firebase | входит в Developer Program |
| iOS-приложение в проекте Firebase | `GoogleService-Info.plist` | bundle id `com.gymplanner.mobile` |
| Физический iPhone | симулятор не даёт push и камеру | — |
| Физические Apple Watch | симулятор не даёт тактильные сигналы и Always-On | — |
| App Store Connect: анкета App Privacy | **отдельная** от Data Safety в Google Play | должна совпадать с политикой |

`GoogleService-Info.plist`, как и `google-services.json`, — клиентская конфигурация,
а не приватный ключ, но в репозиторий не коммитится. Правило и путь описаны в
`docs/firebase-setup.md`; его надо будет дополнить разделом про iOS.

---

## 8. Этапы

### Этап 0 — доступы и решения

**Блокирует все остальные этапы.** Выполняется владельцем продукта, не агентом.

1. Регистрация в Apple Developer Program.
2. Выбор стратегии сборки (6.1).
3. Решение по форме watchOS-приложения (6.2).
4. Решение по таймеру отдыха и HealthKit (6.3).
5. Подтверждение провайдера push (6.4).
6. Добавление iOS-приложения в проект Firebase, выпуск ключа APNs `.p8`, загрузка его
   в Firebase Console.

**Контрольная точка:** доступен Mac с Xcode, решения 6.1-6.4 зафиксированы в этом
документе, `GoogleService-Info.plist` получен.

---

### Этап 1 — iOS-таргет собирается и запускается

Первая задача — выяснить, что на самом деле происходит при `dotnet build -f net10.0-ios`.
Код каркаса писался без единой сборки, поэтому ошибки компиляции ожидаемы.

1. `dotnet restore` и `dotnet build -f net10.0-ios` на Mac; вычистить ошибки компиляции.
2. Запуск на симуляторе iPhone.
3. Запуск на физическом iPhone с developer-профилем.
4. Настроить отладку против локального бэкенда — аналог ветки `10.0.2.2` в
   `MobileApiOptions`, но для симулятора (см. 5.7, «мелочи»).
5. Пройти основные сценарии: регистрация, вход, приветственный гид, план тренировок,
   тренировка, история, прогресс, профиль, поддержка, фото упражнения.
6. Зафиксировать список расхождений с Android, обнаруженных на этом проходе.

**Не делать на этом этапе:** push, deep link, ориентацию. Цель — только зелёная сборка и
базовая работоспособность.

**Контрольная точка:** приложение запускается на физическом iPhone и проходит сценарий
тренировки от начала до конца.

---

### Этап 2 — быстрые исправления: privacy manifest и платформа push

Два независимых исправления из раздела 5, обе правки маленькие, обе критичные.

1. Раскомментировать `NSPrivacyAccessedAPICategoryUserDefaults` в `PrivacyInfo.xcprivacy`
   (5.1). Проверить по списку из 5.1, не используются ли другие required-reason API.
2. Убрать хардкод `"android"` в `PushRegistrationCoordinator.cs:47` (5.2). Платформу
   передавать явно — например, отдельной абстракцией, регистрируемой в `MauiProgram`
   рядом с `IRemotePushTokenProvider`. Значения должны совпадать с
   `PushDeviceRegistrationService.SupportedPlatforms` (`"android"` / `"ios"`).
3. Проверить, что Android-регистрация по-прежнему присылает `"android"`.

**Контрольная точка:** сборка Android не изменила поведение; сборка iOS проходит;
в privacy-манифесте объявлены все используемые required-reason API.

---

### Этап 3 — удалённые push-уведомления на iOS

1. Положить `GoogleService-Info.plist` в `GymPlanner.Mobile/`, добавить его в csproj
   под условием iOS по образцу `GoogleServicesJson`, внести в `.gitignore`.
2. Добавить пакет FCM для iOS. Проверить совместимость с `net10.0-ios` до того, как
   закладываться на него; при отсутствии рабочей биндинг-библиотеки — вернуться к
   решению 6.4 и обсудить прямой APNs.
3. Entitlements: `aps-environment` (`development` / `production`).
4. `Info.plist`: `UIBackgroundModes` со значением `remote-notification`.
5. `AppDelegate`: вызвать `RegisterForRemoteNotifications`, реализовать
   `RegisteredForRemoteNotifications` и `FailedToRegisterForRemoteNotifications`.
6. Реализовать iOS-провайдер `IRemotePushTokenProvider` и зарегистрировать его в
   `MauiProgram` под `#if IOS` рядом с существующей веткой `#if ANDROID` (строка 66).
7. Проверить, что существующий `IosNotificationNavigation.OpenPush` корректно разбирает
   payload, который присылает `PushNotificationService` — ключи в
   `WorkoutPlanner.Api.Contracts.PushNotificationPayloadKeys`.

**Проверка на устройстве** по образцу чек-листа `docs/firebase-setup.md` («Device E2E
checklist»): доставка в foreground, background и при полностью закрытом приложении; тап
открывает нужное сообщение Inbox; смена аккаунта на том же устройстве; обновление токена
не плодит дубли регистраций.

**Контрольная точка:** push приходит на физический iPhone во всех трёх состояниях
приложения и открывает правильный экран.

---

### Этап 4 — подтверждение сопряжения часов на iOS

Нужен для Wear OS уже сейчас: пользователь с iPhone и часами Wear OS сегодня не сможет
подтвердить сопряжение. Не ждёт watchOS.

1. `CFBundleURLTypes` в `Info.plist` со схемой из `WatchPairingLink.Scheme`
   (`gymplanner`).
2. Переопределить `OpenUrl` в `AppDelegate`, разобрать ссылку существующим
   `WatchPairingLink.TryReadRequestId` и вызвать
   `NotificationNavigationService.OpenWatchApproval`.
3. Учесть холодный старт: ссылка может прийти в `launchOptions` в `FinishedLaunching`,
   когда навигация ещё не готова — повторить приём, использованный в
   `MainActivity.OnCreate` для Android.
4. Проверить экран `Components/Pages/WatchApprove.razor` на iOS.

**Контрольная точка:** часы Wear OS открывают ссылку на iPhone, экран подтверждения
показывает модель часов, подтверждение выдаёт токены.

---

### Этап 5 — ориентация, зум, локализация системных строк

1. Реализовать `ScreenOrientationService` под iOS (5.5). На iOS 16+ смена ориентации
   идёт через `UIWindowScene.RequestGeometryUpdate`, на более ранних — через
   `UIDevice.SetValueForKey`; минимальная версия проекта 15.0, поэтому нужны обе ветки
   или подъём минимальной версии — это отдельное решение, его надо вынести пользователю.
2. Проверить альбомный режим на `ProgressExercises` и `ProgressWorkouts`, включая
   `LandscapeChartViewport`.
3. Настроить зум `WKWebView` (5.6) — эквивалент `SetSupportZoom` для iOS.
4. Завести `InfoPlist.strings` для uk и en (5.7), тексты согласовать с уже переведёнными
   строками в `WorkoutPlanner.Localization`.
5. Мелочи по желанию: `lang` в `index.html`, анимация сплеша.

**Контрольная точка:** графики поворачиваются, запросы разрешений приходят на языке
интерфейса.

---

### Этап 6 — релизная готовность iPhone

1. Иконки и сплеш: проверить, что `MauiIcon` и `MauiSplashScreen` дают корректные
   ассеты под iOS во всех размерах.
2. Создать запись приложения в App Store Connect.
3. Заполнить анкету App Privacy. **Обязательно отметить данные о здоровье** — тренировочные
   данные под неё попадают; это уже зафиксировано в `docs/legal/privacy-policy.md`.
   Анкета должна совпадать с разделом 2 политики.
4. Export compliance: приложение использует только HTTPS, но декларацию заполнить нужно.
5. Обновить `docs/legal/privacy-policy.md` по чек-листу «Когда выйдет версия для iOS»,
   который уже лежит в служебной шапке документа: перечисление платформ в подзаголовке и
   разделе 1, Apple как получатель данных в разделе 4, оговорка про Wear OS только для
   Android в разделе 2. Правку делать в трёх языковых версиях.
6. Проверить, что удаление аккаунта доступно внутри приложения — требование App Store
   5.1.1(v). По результатам прошлого аудита оно уже выполнено, но перед подачей
   перепроверить на самом iOS-билде.
7. Скриншоты для всех обязательных размеров, описание, ключевые слова.
8. Сборка в TestFlight, внешнее тестирование.

**Контрольная точка:** сборка принята App Store Connect и раздаётся через TestFlight.
**После этого этапа iPhone можно выпускать; этапы 7-11 — отдельный продукт.**

---

### Этап 7 — watchOS: фундамент проекта и сеть

Начало нового клиента. Каталог — `WorkoutPlanner.WatchOS` в корне репозитория, рядом с
`WorkoutPlanner.WearOS`.

1. Создать Xcode-проект watchOS-приложения (при варианте B из 6.2 — watch-only).
2. Слой сети под `/api/watch`: перенести модели из
   `WorkoutPlanner.WearOS/.../data/remote/WatchApiModels.kt` и описание вызовов из
   `WatchApiService.kt` в `Codable` и `URLSession`. Источник истины по контракту —
   `WorkoutPlanner.Api.Contracts/WatchPairingContracts.cs` и `WatchWorkoutContracts.cs`,
   а не Kotlin-код.
3. Конфигурация базового адреса: debug и release по образцу
   `WEAR_DEBUG_API_BASE_URL` / `WEAR_RELEASE_API_BASE_URL`, с такой же release-проверкой
   на абсолютный HTTPS без loopback.
4. Хранение refresh-токена в Keychain (аналог `AndroidKeystoreTokenStore.kt`),
   access-токен — только в памяти, как на Wear.
5. Идентичность устройства: аналог `DeviceIdentityStore.kt`.
6. Ротация токена: аналог `WatchSessionManager.kt`, `POST /api/watch/token/refresh`.
7. Экран ввода шестизначного кода сопряжения, `POST /api/watch/pair`,
   `Platform = "watchOS"` при регистрации устройства.
8. Проверить, что устройство появляется в списке `Профиль → Смарт-часы` мобильного
   приложения и корректно отзывается.

**Контрольная точка:** часы сопрягаются с аккаунтом, переживают перезапуск приложения и
показывают активную тренировку в виде сырых данных.

---

### Этап 8 — watchOS: цикл тренировки

Функциональный объём соответствует Minimal MVP Wear OS (`docs/wear-os/STAGE_10_MILESTONE.md`)
плюс редактирование и undo из Extended.

1. Экран «нет активной тренировки» с обновлением.
2. Экран текущего подхода: упражнение, номер подхода, вес, повторы.
3. Крупный элемент выполнения подхода, `POST /api/watch/sets/{setId}/complete`.
4. Таймер отдыха и переход к следующему подходу и упражнению.
5. Редактирование фактического веса и повторов, `PUT /api/watch/sets/{setId}`.
6. Отмена подхода, `POST /api/watch/sets/{setId}/undo`.
7. Обзор тренировки со списком упражнений.
8. Завершение тренировки с подтверждением удержанием — аналог `HoldToConfirm.kt`,
   `POST /api/watch/workouts/{workoutId}/finish`.
9. Учесть флаг `IsFreeWorkout` в `WatchActiveWorkoutResponse`: после свободной тренировки
   на телефоне пользователя ждёт вопрос, сохранять ли её шаблоном — текст завершения на
   часах должен это отражать, как на Wear.
10. Digital Crown для навигации и изменения чисел, тактильная обратная связь.

**Контрольная точка:** тренировка проходится на физических часах от первого подхода до
финиша, результат виден в мобильном приложении и в истории.

---

### Этап 9 — watchOS: offline-first и разрешение конфликтов

Контракт конфликтов уже существует на сервере, изобретать не нужно:
`CompleteWatchSetRequest` / `UpdateWatchSetRequest` / `UndoWatchSetRequest` несут
`OperationId`, `ChangedAtUtc` и `ClientVersion`, а сервер отвечает
`WatchSetConflictResponse`. Семантика описана в `docs/wear-os/OFFLINE_SYNC.md`.

1. Локальное хранилище снимка тренировки и очереди отложенных операций — аналог Room
   (`WorkoutDao.kt`, `WorkoutEntities.kt`).
2. Запись локального состояния и операции в одной транзакции, как на Wear.
3. Фоновая отправка очереди: `BGTaskScheduler` и background `URLSession` вместо
   WorkManager, с экспоненциальной задержкой и глобальным порядком операций.
4. Идемпотентность по `OperationId`.
5. Разрешение конфликтов по `WatchSetConflictResponse`.
6. Явные состояния синхронизации в UI — аналог этапа 14 Wear OS.

**Контрольная точка:** подходы, выполненные без сети, доезжают до сервера после её
появления; повторная отправка не создаёт дублей; конфликт показывается пользователю.

---

### Этап 10 — watchOS: фон, таймер отдыха, Always-On

Реализуется по решению 6.3.

1. Поведение таймера отдыха при погасшем экране.
2. Тактильный сигнал об окончании отдыха.
3. Always-On Display для экрана тренировки — аналог `AmbientWorkoutScreen.kt`.
4. По желанию: индикатор идущей тренировки в Smart Stack (WidgetKit) — аналог
   `wear-ongoing`.

**Контрольная точка:** отдых корректно завершается при погасшем экране и опущенной руке.

---

### Этап 11 — релиз watchOS

1. Иконки, скриншоты watchOS.
2. Запись в App Store Connect (при варианте B — отдельная).
3. Анкета App Privacy для watch-приложения; при выборе HealthKit — переработанный
   раздел 2 политики конфиденциальности.
4. TestFlight на физических часах.
5. Обновить `docs/legal/privacy-policy.md`: раздел 2 сейчас описывает часы как Wear OS,
   нужно учесть watchOS.
6. Строка `Смарт-часы Wear OS` в `WorkoutPlanner.Localization/AppStrings*.resx` и
   соответствующий раздел профиля должны перестать называть единственную платформу.

**Контрольная точка:** watch-приложение раздаётся через TestFlight и проходит полный
сценарий тренировки.

---

## 9. Соответствие Wear OS → watchOS

Переиспользовать код Wear OS нельзя: это 4872 строки Kotlin в 44 production-файлах.
Переиспользуются архитектурные решения и серверный контракт.

| Wear OS | watchOS |
|---|---|
| Compose for Wear, 8 экранов | SwiftUI |
| Room, `PendingSyncOperation` | SwiftData или GRDB |
| WorkManager, `SyncQueueProcessor` | `BGTaskScheduler` + background `URLSession` |
| Retrofit + Gson | `URLSession` + `Codable` |
| Android Keystore (`AndroidKeystoreTokenStore.kt`) | Keychain |
| `ActiveWorkoutService` (foreground service) | решение 6.3 |
| `wear-ongoing` | WidgetKit, Smart Stack |
| Ambient-режим | Always-On Display |
| Поворотная рамка | Digital Crown |
| `RemoteActivityHelper` (ссылка на телефон) | `WatchConnectivity` только в варианте A; в варианте B — ввод кода |
| `HoldToConfirm.kt` | эквивалент на SwiftUI |

**MAUI под watchOS не работает.** Биндинги .NET для watchOS прекращены после .NET 7 —
это следует перепроверить по актуальной документации, но рассчитывать нужно на нативный
Swift/SwiftUI в Xcode.

---

## 10. Проверка и приёмка

Правила совпадают с `AGENTS.md`:

- сборка обоих таргетов MAUI после любого изменения общего кода;
- `dotnet test` для `WorkoutPlanner.Web.Tests`, если затронут сервер или локализация;
- проверка на физическом устройстве обязательна там, где поведение можно проверить
  на устройстве; успешная компиляция проверкой не считается;
- всё, что не проверено, называется в отчёте явно.

Для iOS и watchOS ни одна проверка на устройстве невозможна с Windows-машины. Агент,
работающий на Windows, может выполнять только статическую часть этапа и обязан сказать
об этом в отчёте.

---

## 11. Риски

1. **Сборка iOS ни разу не запускалась.** Этап 1 может вскрыть больше ошибок, чем
   предполагает раздел 5. Оценки сроков до его завершения ненадёжны.
2. **Биндинг FCM под `net10.0-ios`.** Если рабочей библиотеки нет, решение 6.4 придётся
   пересматривать, а на сервере появится второй путь доставки.
3. **Встраивание watchOS-таргета в MAUI (вариант A).** Неподдерживаемый сценарий;
   риск поломки при каждом обновлении MAUI.
4. **HealthKit.** Выбор полного пути в 6.3 переписывает раздел 2 политики
   конфиденциальности и добавляет обоснование для App Review.
5. **Минимальная версия iOS 15.0.** Часть современных API ориентации и фона требует
   iOS 16+; подъём минимальной версии — продуктовое решение.
6. **Расхождение анкеты App Privacy и политики.** Самая частая причина снятия
   приложения с публикации; та же ошибка возможна и в Google Play Data Safety.

---

## 12. Рекомендуемые Git-коммиты

```text
ios: document iOS and Apple Watch implementation plan
ios: build and run the MAUI client on iOS
ios: declare UserDefaults in the privacy manifest
ios: send the real platform when registering push devices
ios: add remote push notifications on iOS
ios: handle the watch pairing deep link on iOS
ios: implement screen orientation locking on iOS
ios: localize iOS permission prompts
ios: prepare the iPhone App Store release
watchos: scaffold the watchOS client and watch API layer
watchos: implement pairing and watch credentials
watchos: implement the current set and rest timer
watchos: add set editing, undo and workout finish
watchos: add the offline-first sync queue
watchos: handle background rest timer and always-on
watchos: prepare the watchOS App Store release
```

---

## 13. Промпты для агента

### Для очередного этапа

```text
Выполни только этап N из docs/ios/IMPLEMENTATION_PLAN.md.
Не затрагивай более поздние этапы.
Перед изменениями изучи текущую реализацию и проверь, что предыдущий этап завершён.
Запусти применимые сборки и тесты.
Перечисли изменённые файлы, решения, риски и предложи название коммита.
Если проверка на устройстве невозможна в текущей среде — скажи об этом прямо.
```

### Для этапа 1

```text
Выполни только этап 1 из docs/ios/IMPLEMENTATION_PLAN.md.
Цель — зелёная сборка net10.0-ios и запуск на устройстве, ничего больше.
Не трогай push, deep link и ориентацию: это этапы 3, 4 и 5.
Составь точный список ошибок компиляции и расхождений с Android, которые обнаружились.
```

### Для контрольной точки iPhone

```text
Проверь готовность iPhone-версии к TestFlight по этапу 6 из docs/ios/IMPLEMENTATION_PLAN.md.
Отдельно сверь анкету App Privacy с разделом 2 docs/legal/privacy-policy.md.
После проверки остановись и не начинай этапы watchOS.
```
