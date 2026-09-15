# Этап 17. Документация и подготовка к выпуску

## Результат

Создана итоговая `ARCHITECTURE.md`; актуализированы API, pairing, offline sync и
Wear OS README. Добавлены release checklist и privacy data-flow notes. Документы
разделяют Minimal MVP и Extended Wear OS, описывают конфигурацию, security boundary,
конфликты, timer/energy behavior, revocation и ограничения.

Release-конфигурация теперь fail-closed: Gradle требует явный абсолютный HTTPS
`WEAR_RELEASE_API_BASE_URL` с завершающим `/`, без credentials/query/fragment, и
отклоняет loopback, `.local`, emulator host и `.invalid`. В manifest добавлен
launcher/round icon, `allowBackup=false` сохранён, лишние permissions не добавлялись.

## Статус публикации

Код и документация подготовлены к формированию release artifact, но проект нельзя
называть готовым к публикации в магазине до выполнения внешних действий:

- production signing и защищённое хранение ключей;
- утверждение store version/application identity;
- реальный production API hostname/TLS;
- физические Wear OS устройства и Play Console validation;
- опубликованная privacy policy/Data safety declaration.

Эти пункты требуют production credentials, устройств и решений владельца продукта;
они перечислены в `RELEASE_CHECKLIST.md` и не подменяются тестовым сертификатом или
выдуманными значениями.

## Проверка release-конфигурации

- Server Release build завершён без ошибок; осталось одно существующее
  предупреждение об устаревшем Firebase `Message.Token`, не относящееся к Wear OS.
- Release validation без `WEAR_RELEASE_API_BASE_URL` останавливает сборку.
- `assembleRelease` с синтаксически корректным HTTPS URL завершён успешно, включая
  `lintVitalRelease`; примерный hostname использовался только для проверки конфигурации.
- Поиск по Wear OS source/config и документации не обнаружил committed токенов,
  API keys, private keys или паролей.
- Полученный release APK не подписан production-ключом и не является магазинным
  артефактом; signing остаётся внешним обязательным пунктом checklist.

## Стоп-условие

Этап 17 — последний этап `IMPLEMENTATION_PLAN.md`. Более поздних этапов в плане нет.
