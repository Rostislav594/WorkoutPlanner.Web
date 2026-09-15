# Мультиязычность GPlanner

Поддерживаемые языки: **ru** (исходный), **uk**, **en**.

Инфраструктура готова и работает сквозняком. Массовый перевод экранов —
отдельная задача, см. «Что осталось».

## Где что лежит

| Что | Где |
|---|---|
| Список языков, разбор тегов культуры | `WorkoutPlanner.Localization/AppLanguage.cs` |
| Формы множественного числа (CLDR) | `WorkoutPlanner.Localization/PluralRules.cs` |
| Строки интерфейса | `WorkoutPlanner.Localization/AppStrings*.resx` |
| Коды ошибок API | `WorkoutPlanner.Localization/ApiErrorCodes.cs` |
| Тексты ошибок API | `WorkoutPlanner.Localization/ApiErrors*.resx` |
| Культура запроса на сервере | `WorkoutPlanner.Web/Services/Localization/GymPlannerLocalization.cs` |
| Ответы об ошибке с кодом | `WorkoutPlanner.Web/Api/ApiProblems.cs` |
| Язык в мобильном приложении | `GymPlanner.Mobile/Localization/` |
| Тесты-инварианты | `WorkoutPlanner.Web.Tests/Unit/LocalizationTests.cs` |

Русский объявлен `NeutralResourcesLanguage`, поэтому его строки лежат в
`AppStrings.resx` без суффикса и попадают в основную сборку. `uk` и `en` —
в сателлитных сборках. В `GymPlanner.Mobile.csproj` стоит
`SatelliteResourceLanguages`: без него MAUI вырежет сателлиты из APK
и приложение молча останется русским.

## Как добавить строку

1. Добавить ключ во **все три** файла `AppStrings.resx` / `.uk.resx` / `.en.resx`.
2. В компоненте: `@inject IAppText Text`, затем `@Text["Ключ"]`.

> **Не использовать `IStringLocalizer`.** Он разрешает строки по
> `CultureInfo.CurrentUICulture`, а в MAUI BlazorWebView культура потока
> рендерера фиксируется при старте процесса. Проверено на устройстве:
> после смены языка текст не менялся ни от перерисовки, ни от перехода между
> страницами, ни от полной перезагрузки WebView — только перезапуск приложения.
> `IAppText` берёт язык из сервиса явно и потому работает на лету.
> Инвариант закреплён тестом `IgnoresAmbientThreadCulture`.

Тест `EveryRussianKeyHasATranslation` упадёт, если перевод забыт, а
`TranslationsAreNotLeftAsRussianCopies` — если строку скопировали, не переведя.

## Как добавить строку с числом

Никогда не склеивать число с текстом вручную: в ru и uk три формы
(1 подход / 2 подхода / 5 подходов), в en две.

Заводятся три ключа с суффиксами `_One`, `_Few`, `_Many`, само число
подставляется на место `{0}`:

```xml
<data name="Sets_One"><value>{0} подход</value></data>
<data name="Sets_Few"><value>{0} подхода</value></data>
<data name="Sets_Many"><value>{0} подходов</value></data>
```

```csharp
Text.Plural("Sets", count)   // "3 подхода"
```

## Контракт ошибок API

Сервер отдаёт **код**, клиент показывает свой перевод. Так сообщение не
зависит от языка сервера, а старые сборки приложения продолжают работать.

На сервере:

```csharp
return ApiProblems.ValidationProblem(
    nameof(request.PreferredLanguage),
    ApiErrorCodes.ProfileLanguageUnsupported,
    "Supported languages: ru, uk, en.");   // текст для отладки, пользователь его не видит
```

В ответе появляется `"errorCodes": ["profile.language_unsupported"]` рядом с
привычным `"errors"`.

Клиент разбирает это в единственном месте —
`GymPlanner.Mobile/Authentication/MobileApiErrorReader.cs`. Приоритет такой:

1. `errorCodes` → перевод из `ApiErrors.resx`;
2. если кода нет или он неизвестен этой сборке → текст из `errors`;
3. если и его нет → текст из `title`;
4. иначе → общее сообщение по HTTP-статусу.

Благодаря пункту 2 **эндпоинты можно переводить на коды по одному**, ничего
не ломая: непереведённые продолжают отдавать русский текст, как раньше.

Коды неизменяемы. Значение выпущенной константы менять нельзя — у
пользователей на старой версии сообщение превратится в сырой код.
Устаревшие помечать `[Obsolete]`, но не удалять.

## Язык пользователя

Хранится в профиле (`UserProfiles.PreferredLanguage`, миграция
`AddPreferredLanguage`), а не на устройстве — потому что push-уведомления
формируются на сервере и должны приходить на языке пользователя.

Мобильный клиент шлёт `Accept-Language` на каждом запросе
(`LanguageHttpMessageHandler`). Порядок определения языка при старте:

1. явный выбор пользователя (`Preferences`, ключ `app.language`);
2. язык устройства, если он поддерживается;
3. русский.

Язык из профиля (`ApplyFromProfile`) **не перетирает** явный локальный выбор:
пользователь мог переключить язык на этом устройстве до синхронизации.

## Проверено на устройстве

Redmi Note 8, Android 11, отладочная сборка. Переключение ru → uk → en
применяется мгновенно, переживает перезапуск приложения и доезжает до БД
(`UserProfiles.PreferredLanguage`). Сателлитные сборки `uk`/`en` реально
попадают в APK и грузятся.

Проверка делалась через CDP-подключение к WebView приложения
(`adb forward tcp:9333 localabstract:webview_devtools_remote_<pid>`),
потому что MIUI блокирует `adb shell input tap` без разрешения
INJECT_EVENTS. Отладочный сокет открыт благодаря
`AddBlazorWebViewDeveloperTools()` в Debug-сборке.

Мобильное приложение в Debug ходит на `http://127.0.0.1:5121`, то есть
требует `adb reverse tcp:5121 tcp:5121` и поднятого `WorkoutPlanner.Web`.

## Что осталось

- [ ] Перевести экраны. Сейчас переведены только «Предложить идею» и «Язык».
      Правило: трогаешь файл — выносишь из него строки в ресурсы; новый код
      хардкода не содержит.
- [ ] ~419 русских литералов в `WorkoutPlanner.Web` перевести на `ApiProblems`
      и коды. Мигрировать по эндпоинту за раз.
- [ ] `CultureInfo.GetCultureInfo("ru-RU")` захардкожен в `Calendar.razor`,
      `History.razor`, `ProgressExercises.razor`, `ProgressWorkouts.razor` —
      заменить на текущую культуру. Три из четырёх файлов сейчас в работе
      у другого агента, поэтому не тронуты.
- [ ] Wear OS: `strings.xml` содержит только `app_name`, ~145 русских
      литералов зашиты прямо в Compose. Нужны `values-uk/` и `values-en/`.
      Kotlin не использует .resx — там свой набор ресурсов, синхронизировать
      руками по этому же списку ключей.
- [ ] Заголовки разделов профиля (`Settings` в `ProfileSetting.razor`),
      шапка и нижняя навигация пока захардкожены по-русски — вынести в ресурсы.
- [ ] Переводы на украинский и английский сделаны разработчиком, носителем
      языка не проверялись.
