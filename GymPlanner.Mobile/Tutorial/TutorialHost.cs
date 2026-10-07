using GymPlanner.Mobile.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WorkoutPlanner.Localization;

namespace GymPlanner.Mobile.Tutorial;

public enum TutorialScenario { Free, Template, Workouts }

/// <summary>
/// Обучение, запущенное кнопкой «?» на «Сегодня» или «Тренировках»: выбор
/// проходки, надписи эпизодов, рука и подставные данные.
/// </summary>
/// <remarks>
/// <para>
/// Слой обучения нарисован в макете (<c>TutorialLayer</c>), а не на странице:
/// ролик по шаблону ходит по «Главной», «Календарю» и «Сегодня», и слой с рукой
/// не должен пропадать при переходах. Пока обучение идёт, страницы берут данные
/// из <see cref="Backend"/>, а не из API: ни сервер, ни офлайн-очередь, ни
/// напоминания ничего не получают.
/// </para>
/// <para>
/// Свободную проходку ведёт сама страница «Сегодня» — она вызывает свои
/// обработчики. Проходка по шаблону нажимает настоящие элементы страниц и
/// поэтому описана здесь, без доступа к их внутренностям. Ролик «Тренировок»
/// начинает список шаблонов, а продолжает редактор шаблона: страница,
/// открытая роликом, отдаёт свою часть через <see cref="ContinueOnPage"/>.
/// </para>
/// </remarks>
public sealed class TutorialHost(
    IJSRuntime js,
    IAppText text,
    NavigationManager navigation,
    MobileBackNavigationService backNavigation) : IAsyncDisposable
{
    // Сколько надпись держится на экране, и сколько она появляется и уходит
    // вместе с размытием (совпадает с переходом .tutorial-curtain в CSS).
    private const int CardHoldMilliseconds = 2500;
    private const int CardFadeMilliseconds = 450;
    // Сколько ждать, пока после перехода загрузится нужная страница.
    private const int PageWaitMilliseconds = 8000;

    private readonly TutorialSpotlight _spotlight = new(js);
    private TaskCompletionSource _attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<TutorialScenario> _choice = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<Func<CancellationToken, Task>> _pagePart = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<CancellationToken, Task>? _playPage;
    private CancellationTokenSource? _cancellation;
    private SynchronizationContext? _context;
    private TutorialScenario? _scenario;
    private bool _layerAttached;

    private readonly System.Diagnostics.Stopwatch _clock = new();
    // Момент, к которому сценарий должен был дойти по своим паузам.
    private long _schedule;

    /// <summary>Подставной сервер; есть, только пока идёт обучение.</summary>
    public TutorialWorkoutBackend? Backend { get; private set; }

    public bool IsActive => Backend is not null;

    // Надпись по центру: ключ текста, номер эпизода (у вступления и
    // «Готово!» его нет) и видна ли она сейчас.
    public string CardKey { get; private set; } = "Tutorial_Today_Intro";
    public int? CardNumber { get; private set; }
    public bool CardVisible { get; private set; }

    // Пока на экране выбор проходки и описание выбранного вида. На
    // «Тренировках» выбора нет — сразу описание.
    public bool Choosing { get; private set; }
    public bool HasMenu { get; private set; }
    public TutorialScenario? Described { get; private set; }
    public bool Leaving { get; private set; }

    /// <summary>Проходка, для которой нажали «Начать».</summary>
    public TutorialScenario? Scenario => _scenario;

    public TutorialSpotlight Spotlight => _spotlight;

    /// <summary>Слой должен перерисоваться.</summary>
    public event Action? Changed;

    /// <summary>Обучение начало закрываться: страница прерывает свои удержания.</summary>
    public event Action? Stopping;

    /// <summary>
    /// Обучение закрыто, подставные данные убраны: страница возвращает
    /// настоящее состояние.
    /// </summary>
    public event Action? Stopped;

    /// <summary>
    /// «Сегодня»: открывает выбор проходки и ведёт обучение до конца или до
    /// «Пропустить».
    /// </summary>
    /// <param name="playFree">Свободная проходка — её ведёт страница «Сегодня».</param>
    public bool Start(Func<CancellationToken, Task> playFree) =>
        Start(playFree, preset: null, new TutorialWorkoutBackend(text, withDemoTemplate: true));

    /// <summary>
    /// «Тренировки»: сразу описание шаблона тренировки и «Начать». Список
    /// шаблонов во время ролика пустой, демо-шаблон живёт только в памяти.
    /// </summary>
    /// <param name="playList">Начало ролика — его ведёт список шаблонов.</param>
    public bool StartWorkouts(Func<CancellationToken, Task> playList) =>
        Start(playList, TutorialScenario.Workouts, new TutorialWorkoutBackend(text, withDemoTemplate: false));

    private bool Start(Func<CancellationToken, Task> playPage, TutorialScenario? preset, TutorialWorkoutBackend backend)
    {
        if (IsActive)
            return false;

        Backend = backend;
        _playPage = playPage;
        _cancellation = new CancellationTokenSource();
        _context = SynchronizationContext.Current;
        _attached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _choice = new TaskCompletionSource<TutorialScenario>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pagePart = new TaskCompletionSource<Func<CancellationToken, Task>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _layerAttached = false;
        _scenario = null;
        Choosing = true;
        HasMenu = preset is null;
        Described = preset;
        CardVisible = false;
        CardKey = "Tutorial_Today_Intro";
        CardNumber = null;
        Leaving = false;
        _clock.Restart();
        _schedule = 0;
        backNavigation.SetBackInterceptor(RequestBack);
        Changed?.Invoke();

        _ = RunAsync(playPage, _cancellation.Token);
        return true;
    }

    private async Task RunAsync(Func<CancellationToken, Task> playPage, CancellationToken token)
    {
        try
        {
            // Первая подсветка до загрузки модуля прожектора просто потерялась бы.
            await _attached.Task.WaitAsync(token);
            var scenario = await _choice.Task.WaitAsync(token);
            _scenario = scenario;

            // Описание сменяется вступлением без просвета: слой перерисуется
            // уже с надписью, и размытие не гаснет.
            Choosing = false;
            Resync();

            switch (scenario)
            {
                case TutorialScenario.Template:
                    await PlayTemplateAsync(token);
                    break;
                case TutorialScenario.Workouts:
                    await PlayWorkoutsAsync(playPage, token);
                    break;
                default:
                    await playPage(token);
                    break;
            }

            await StopAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Обучение остановили «Пропустить» или «Назад»: возврат уже сделан.
        }
    }

    // Кнопка выбора сначала показывает описание вида тренировки, ролик
    // начинается по «Начать».
    public void Choose(TutorialScenario scenario)
    {
        if (!IsActive || !Choosing || !HasMenu)
            return;

        Described = scenario;
        Changed?.Invoke();
    }

    public void Begin()
    {
        if (Described is { } scenario)
            _choice.TrySetResult(scenario);
    }

    /// <summary>
    /// «Назад» на телефоне: с описания вида тренировки — к выбору (если он
    /// был), в остальное время закрывает обучение.
    /// </summary>
    public void RequestBack()
    {
        if (_context is { } context)
            context.Post(_ => _ = BackAsync(), null);
        else
            _ = BackAsync();
    }

    private Task BackAsync()
    {
        if (Choosing && HasMenu && Described is not null)
        {
            Described = null;
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        return StopAsync();
    }

    /// <summary>
    /// Пока идёт обучение, «Назад» принадлежит ему: страницы ставят свой
    /// перехват только вне обучения.
    /// </summary>
    public void SetBackInterceptor(Action? interceptor) =>
        backNavigation.SetBackInterceptor(IsActive ? RequestBack : interceptor);

    public async Task StopAsync()
    {
        if (!IsActive || Leaving)
            return;

        Leaving = true;
        _cancellation?.Cancel();
        Stopping?.Invoke();
        Changed?.Invoke();

        await _spotlight.ClearAsync();
        await _spotlight.HideFingerAsync();

        // Слой гаснет, и только потом под ним возвращается настоящая страница.
        await Task.Delay(260);

        var scenario = _scenario;
        Backend = null;
        _playPage = null;
        Choosing = false;
        Described = null;
        CardVisible = false;
        Leaving = false;
        _layerAttached = false;
        backNavigation.SetBackInterceptor(null);
        await _spotlight.DetachAsync();
        _cancellation?.Dispose();
        _cancellation = null;
        Changed?.Invoke();
        Stopped?.Invoke();

        // Ролик мог увести на другую страницу: возвращаемся туда, откуда его
        // запускали, — к настоящим данным.
        if (scenario == TutorialScenario.Template && !IsOnPage("today"))
            navigation.NavigateTo("today");
        else if (scenario == TutorialScenario.Workouts && !IsOnPage("workouts"))
            navigation.NavigateTo("workouts");
    }

    /// <summary>
    /// Страница, которую открыл ролик, продолжает его своими действиями —
    /// как редактор шаблона на «Тренировках».
    /// </summary>
    public void ContinueOnPage(TutorialScenario scenario, Func<CancellationToken, Task> part)
    {
        if (IsActive && _scenario == scenario)
            _pagePart.TrySetResult(part);
    }

    /// <summary>Слой отрисован: можно подключать прожектор.</summary>
    public async Task AttachAsync(ElementReference layer)
    {
        if (!IsActive || _layerAttached)
            return;

        _layerAttached = true;
        try
        {
            await _spotlight.AttachAsync(layer);
        }
        finally
        {
            // Без прожектора обучение всё равно идёт, просто без подсветки.
            _attached.TrySetResult();
        }
    }

    // ===== Шаблон на «Тренировках» ========================================

    /// <summary>
    /// Как составить шаблон: список шаблонов создаёт его и открывает, редактор
    /// добавляет упражнения и суперсет.
    /// </summary>
    private async Task PlayWorkoutsAsync(Func<CancellationToken, Task> playList, CancellationToken token)
    {
        await CardAsync(token, "Tutorial_Workouts_Intro", null);
        await playList(token);

        // Редактор открывается переходом и отдаёт свою часть, когда загрузится.
        var part = await _pagePart.Task.WaitAsync(TimeSpan.FromMilliseconds(PageWaitMilliseconds), token);
        Resync();
        await part(token);

        await CardAsync(token, "Tutorial_Today_Done", null, final: true);
    }

    // ===== Проходка по шаблону ============================================

    /// <summary>
    /// Как назначить тренировку по шаблону: с «Главной» в календарь, на
    /// сегодняшний день — шаблон «Грудь и спина», затем на «Сегодня», где он
    /// уже ждёт; саму тренировку не проходим, только проматываем.
    /// </summary>
    private async Task PlayTemplateAsync(CancellationToken token)
    {
        // Пока на экране вступление, под размытием открывается «Главная»:
        // ролик начинается оттуда.
        CardKey = "Tutorial_Today_TemplateIntro";
        CardNumber = null;
        CardVisible = true;
        Changed?.Invoke();
        navigation.NavigateTo("");
        await PauseAsync(CardFadeMilliseconds + CardHoldMilliseconds, token);
        await HideCardAsync(token);

        // 1. С «Главной» — в календарь: сегодняшний день, шаблон, «Назначить».
        await CardAsync(token, "Tutorial_Today_TemplateEpisode1", 1);
        await WaitForAsync(token, "[data-tour=\"home-calendar\"]");
        await ClickAsync(token, "[data-tour=\"home-calendar\"]", after: 900);
        await WaitForAsync(token, "[data-tour=\"calendar-today\"]");
        await ClickAsync(token, "[data-tour=\"calendar-today\"]", after: 900);
        var plan = $"[data-tour=\"calendar-plan-{TutorialWorkoutBackend.TemplatePlanId}\"]";
        await WaitForAsync(token, plan);
        await ClickAsync(token, plan, after: 700, aim: 1100);
        await ClickAsync(token, "[data-tour=\"calendar-assign\"]", after: 900, aim: 1100);
        // Шаблон появился на сегодняшнем дне; рука убрана, чтобы не закрывать метку.
        await FocusAsync(token, "[data-tour=\"calendar-today\"]", null);
        await PauseAsync(2000, token);

        // 2. На «Сегодня» тренировка уже ждёт: только проматываем её.
        await CardAsync(token, "Tutorial_Today_TemplateEpisode2", 2);
        await ClickAsync(token, "[data-tour=\"nav-today\"]", after: 600);
        await WaitForAsync(token, "[data-tour=\"plan-name\"]");
        await ShowAsync(token, "[data-tour=\"plan-name\"]", 1600, "[data-tour=\"plan-name\"]");
        await _spotlight.HideFingerAsync();
        // В окне — заголовки, а не целые блоки: окно над высоким блоком
        // заняло бы весь экран, и края перестали бы размываться.
        await FocusAsync(token, "[data-superset-id] .superset-label", null);
        await PauseAsync(1200, token);
        await FocusAsync(token, "[data-tour=\"finish\"]", null);
        await PauseAsync(1600, token);

        await CardAsync(token, "Tutorial_Today_Done", null, final: true);
    }

    private bool IsOnPage(string path) =>
        string.Equals(navigation.ToBaseRelativePath(navigation.Uri).Split('?', '#')[0], path, StringComparison.OrdinalIgnoreCase);

    /// <summary>Ждёт, пока после перехода на экране появится элемент.</summary>
    private async Task WaitForAsync(CancellationToken token, string selector)
    {
        token.ThrowIfCancellationRequested();
        await _spotlight.WaitForAsync(selector, PageWaitMilliseconds);
        token.ThrowIfCancellationRequested();
        Resync();
    }

    /// <summary>
    /// Нажатие на настоящий элемент страницы: рука доезжает и «нажимает»,
    /// а элемент получает обычный клик — срабатывает его собственный обработчик.
    /// </summary>
    private Task ClickAsync(CancellationToken token, string selector, int after = 1200, int? aim = null) =>
        TapAsync(token, selector, () => _spotlight.ClickAsync(selector), after, aim: aim);

    // ===== Общие шаги сценария ============================================

    /// <summary>
    /// Надпись по центру на полностью размытом фоне: вступление и «Готово!» —
    /// крупным текстом, эпизоды — в стеклянной капсуле с номером. Пока она на
    /// экране, рука спрятана; потом надпись и размытие уходят и эпизод идёт.
    /// Последняя надпись не уходит сама: ролик закрывается, и она гаснет
    /// вместе со всем слоем обучения.
    /// </summary>
    public async Task CardAsync(CancellationToken token, string key, int? number, bool final = false)
    {
        token.ThrowIfCancellationRequested();
        await _spotlight.HideFingerAsync();
        CardKey = key;
        CardNumber = number;
        CardVisible = true;
        Changed?.Invoke();
        await PauseAsync(CardFadeMilliseconds + CardHoldMilliseconds, token);
        if (final)
            return;

        await HideCardAsync(token);
    }

    private async Task HideCardAsync(CancellationToken token)
    {
        CardVisible = false;
        Changed?.Invoke();
        await PauseAsync(CardFadeMilliseconds + 250, token);
        // Надпись шла своё время: отставание до неё не нагоняется.
        Resync();
    }

    public async Task FocusAsync(CancellationToken token, string selector, string? finger)
    {
        token.ThrowIfCancellationRequested();
        await _spotlight.FocusAsync(selector, finger);
        // Сценарий ждал плавную прокрутку страницы: это не отставание, и
        // следующие паузы не должны укорачиваться, чтобы его нагнать.
        Resync();
    }

    /// <summary>Подсветить элемент, «нажать» на него и выполнить то, что делает нажатие.</summary>
    public async Task TapAsync(
        CancellationToken token,
        string selector,
        Func<Task> action,
        int after = 1200,
        string? finger = null,
        bool fast = false,
        int? aim = null)
    {
        // Рука доезжает до цели (страница к этому времени докручена) и
        // задерживается, чтобы было видно, куда нажмут.
        await FocusAsync(token, selector, finger ?? selector);
        await PauseAsync(aim ?? (fast ? 1100 : 1500), token);
        await _spotlight.TapAsync();
        await PauseAsync(250, token);

        token.ThrowIfCancellationRequested();
        await action();
        await PauseAsync(after, token);
    }

    /// <summary>
    /// Набор значения по одному символу, как с клавиатуры. В фокусе всё поле
    /// вместе с подписью (или <paramref name="area"/>); рука нажимает на само
    /// поле ввода. Набор идёт бойко: поля рядом, рука почти не едет.
    /// </summary>
    /// <param name="apply">Записывает набранное в черновик страницы.</param>
    /// <param name="render">Перерисовывает страницу.</param>
    public async Task TypeAsync(
        CancellationToken token,
        string selector,
        string value,
        Action<string> apply,
        Action render,
        bool fast = false,
        string? area = null)
    {
        await TapAsync(token, area ?? selector, () => Task.CompletedTask, after: 150, finger: $"{selector} input", aim: fast ? 450 : 700);
        for (var length = 1; length <= value.Length; length++)
        {
            token.ThrowIfCancellationRequested();
            apply(value[..length]);
            render();
            await PauseAsync(fast ? 140 : 180, token);
        }

        await PauseAsync(fast ? 300 : 450, token);
    }

    /// <summary>
    /// Показ результата: на него не распространяется нагон отставания,
    /// результат должен быть виден полностью. Рука не исчезает, а показывает
    /// на <paramref name="finger"/>.
    /// </summary>
    public async Task ShowAsync(CancellationToken token, string selector, int duration, string finger)
    {
        await FocusAsync(token, selector, finger);
        Resync();
        await PauseAsync(duration, token);
    }

    /// <summary>
    /// Пауза по общей шкале сценария. Обращения к WebView на медленном
    /// телефоне занимают заметное время; оно немного вычитается из следующих
    /// пауз, чтобы демонстрация не затягивалась, но пауза не ужимается сильнее,
    /// чем на четверть, — иначе происходящее не успеть разглядеть.
    /// </summary>
    public async Task PauseAsync(int milliseconds, CancellationToken token)
    {
        _schedule += milliseconds;
        var elapsed = _clock.ElapsedMilliseconds;
        var wait = (int)Math.Clamp(_schedule - elapsed, milliseconds * 3 / 4, milliseconds);
        // Большое отставание не копится: иначе все дальнейшие паузы шли бы по минимуму.
        if (elapsed + wait - _schedule > 1500)
            _schedule = elapsed + wait - 1500;
        await Task.Delay(wait, token);
    }

    public void Resync() => _schedule = _clock.ElapsedMilliseconds;

    public async ValueTask DisposeAsync()
    {
        _cancellation?.Cancel();
        await _spotlight.DisposeAsync();
    }
}
