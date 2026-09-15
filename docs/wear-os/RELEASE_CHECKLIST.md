# Wear OS release checklist

## Автоматически проверено

- [x] Server Release build компилируется.
- [x] Wear OS Release build компилируется с явным HTTPS API URL.
- [x] Release build отклоняется, если API URL отсутствует, локальный или placeholder.
- [x] Debug network logging защищён `BuildConfig.DEBUG` и не выводит headers/body.
- [x] Поиск в Wear OS source/config не обнаружил committed tokens, ключей или паролей.
- [x] EF model соответствует последней миграции.
- [x] Room migration `1 → 2` и повторное открытие persisted queue проверены.
- [x] Manifest требует watch hardware и содержит только network permissions.
- [x] `allowBackup=false`; refresh token защищён Android Keystore.
- [x] Добавлен launcher/round icon в текущей чёрно-зелёной стилистике.
- [x] Пользователь может отозвать одни или все часы; revoked access проверен тестами.
- [x] Offline, conflict, idempotency и finish покрыты автоматическими тестами.

## Обязательно перед публикацией

- [x] `applicationId` общий с телефоном: `com.gymplanner.mobile`. Того же имени
      требует Google, чтобы Play отдавал сборки часов и телефона как один продукт.
      `namespace` модуля остался `com.gymplanner.wearos` — он задаёт пакет R и
      BuildConfig и на идентификатор установки не влияет.
- [x] `versionCode` разведён по форм-факторам: часы нумеруются с 2 000 000,
      телефон остаётся ниже 1 000 000. Коды обязаны быть уникальны в пределах
      одного package name.
- [ ] Утвердить production `versionName`.
- [ ] Подписать обе сборки **одним** ключом. Это не пожелание Play, а условие
      работоспособности: при разных подписях установка падает с
      INSTALL_FAILED_UPDATE_INCOMPATIBLE. Отладочные сборки этим не страдают —
      у часов включён `applicationIdSuffix = ".watch"`, поэтому отладочные часы
      и телефон сосуществуют на одном устройстве.
  Текущая версия — pre-release `0.1.0` / `1`.
- [ ] Настроить release signing вне репозитория и сохранить keystore/passwords в
  защищённом CI secret store. Текущая `assembleRelease` создаёт unsigned APK.
- [ ] Передать реальный `WEAR_RELEASE_API_BASE_URL=https://.../api/watch/`.
- [ ] Проверить production TLS chain, Data Protection key persistence, issuer и
  audience на развёрнутом backend.
- [ ] Выполнить pairing, offline sync, conflict, revoke/re-pair и finish на минимум
  одной физической круглой и одной физической квадратной модели часов.
- [ ] Проверить haptic, Always-On, background/Doze и OEM battery policy.
- [ ] Обновить опубликованную privacy policy: account identity, device metadata,
  workout/set data и diagnostic request metadata. Health sensors не собираются.
- [ ] Заполнить Play Console Data safety и пройти Wear OS quality/store validation.
- [ ] Принять решение о minification/shrinking после real-backend проверки
  оптимизированной сборки; сейчас он намеренно выключен.

## Команда release-сборки

```powershell
cd WorkoutPlanner.WearOS
$env:JAVA_HOME = "C:\path\to\jdk-17"
$env:ANDROID_HOME = "C:\path\to\android-sdk"
.\gradlew.bat -PWEAR_RELEASE_API_BASE_URL=https://api.example.com/api/watch/ assembleRelease
```

`api.example.com` в примере нужно заменить production hostname. Не добавляйте URL,
keystore или credentials в отслеживаемые файлы.
