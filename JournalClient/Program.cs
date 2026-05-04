using System.Text.Json;
using JournalClient;
using JournalClient.Events;

// ============================================================
// КЛИЕНТСКОЕ ПРИЛОЖЕНИЕ — Система управления редакцией журнала
// Демонстрирует: делегаты, события, Action<T>, Func<T,TResult>,
//                динамическую подписку/отписку (+= / -=),
//                EventHandler<TEventArgs>, custom события
// ============================================================

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║   Клиент: Система управления редакцией журнала           ║");
Console.WriteLine("║        (Демонстрация делегатов и событий)                ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝\n");

// Путь к API (через Nginx на порту 80)
var baseUrl = args.Length > 0 ? args[0] : "http://localhost:80";
using var api = new ApiService(baseUrl);

// ==============================================================
// ЧАСТЬ 1: СТАРЫЙ СТИЛЬ ДЕЛЕГАТОВ (для демонстрации)
// Простые делегаты OnRequestCompleted и OnRequestError
// ==============================================================

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ЧАСТЬ 1: Стиль делегатов (Action<T> и OnRequestCompleted) ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

// --- Обработчик 1: Вывод в консоль ---
OnRequestCompleted consoleHandler = (endpoint, method, statusCode, elapsedMs) =>
{
    var color = statusCode >= 200 && statusCode < 300 ? ConsoleColor.Green
              : statusCode >= 400 ? ConsoleColor.Red
              : ConsoleColor.Yellow;

    Console.ForegroundColor = color;
    Console.WriteLine($"  [CONSOLE-LOG] [{method}] {endpoint} → {statusCode} ({elapsedMs}мс)");
    Console.ResetColor();
};

// --- Обработчик 2: Логирование в файл ---
var logFile = "api-requests.log";
OnRequestCompleted fileLogHandler = (endpoint, method, statusCode, elapsedMs) =>
{
    var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {method} {endpoint} → {statusCode} ({elapsedMs}мс)" + Environment.NewLine;
    File.AppendAllText(logFile, logEntry);
};

// --- Обработчик ошибок ---
OnRequestError errorHandler = (message, ex) =>
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  [ERROR-HANDLER] X {message}");
    if (ex != null) Console.WriteLine($"    {ex.GetType().Name}: {ex.Message}");
    Console.ResetColor();
};

// Подписываемся на события старого стиля
api.RequestCompleted += consoleHandler;
api.RequestCompleted += fileLogHandler;  // Многоадресный делегат!
api.RequestError += errorHandler;

Console.WriteLine("Подписаны: консоль-лог + файл-лог + обработчик ошибок\n");

// ==============================================================
// ЧАСТЬ 2: НОВЫЙ СТИЛЬ СОБЫТИЙ (EventHandler<TEventArgs>)
// Более современный и рекомендуемый подход в .NET
// ==============================================================

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ЧАСТЬ 2: Новый стиль событий (EventHandler<TEventArgs>)   ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

// Создаём слушателей событий
var consoleListener = new ApiEventListener("CONSOLE");
var fileListener = new ApiEventListener("FILE");

// Подписываем их на события новой системы
api.OnRequestCompleted += consoleListener.OnRequestCompleted;
api.OnRequestError += consoleListener.OnRequestError;

api.OnEntityCreated += consoleListener.OnEntityCreated;
api.OnEntityDeleted += consoleListener.OnEntityDeleted;
api.OnAuthenticationAttempt += consoleListener.OnAuthenticationAttempt;

// Второй слушатель (для демонстрации многоадресности)
api.OnRequestCompleted += fileListener.OnRequestCompleted;
api.OnEntityCreated += fileListener.OnEntityCreated;
api.OnEntityDeleted += fileListener.OnEntityDeleted;

Console.WriteLine("Подписаны два слушателя на события новой системы\n");

// ==============================================================
// OPERATION 1: Получить список авторов
// ==============================================================
Console.WriteLine("--- ОПЕРАЦИЯ 1: Получить список авторов ---");

Action<List<dynamic>?> onAuthorsReceived = authors =>
{
    if (authors == null) { Console.WriteLine("  Авторы не получены\n"); return; }
    Console.WriteLine($"  Получено авторов: {authors.Count}");
    foreach (var a in authors.Take(3))
    {
        var elem = (JsonElement)a;
        var firstName = elem.GetProperty("firstName").GetString();
        var lastName = elem.GetProperty("lastName").GetString();
        var email = elem.GetProperty("email").GetString();
        Console.WriteLine($"    * {firstName} {lastName} ({email})");
    }
    Console.WriteLine();
};

await api.GetAuthorsAsync(onAuthorsReceived);

// ==============================================================
// OPERATION 2: Создать нового автора
// Демонстрирует раннее срабатывание события OnEntityCreated
// ==============================================================
Console.WriteLine("--- ОПЕРАЦИЯ 2: Создать нового автора ---");

var newAuthor = new
{
    firstName = "Test",
    lastName = "Client",
    email = $"client_{DateTime.Now.Ticks}@test.com",
    bio = "Created by console client"
};

var createdAuthor = await api.CreateAuthorAsync(newAuthor);
int newAuthorId = 0;

if (createdAuthor is JsonElement authorElem)
{
    newAuthorId = authorElem.GetProperty("id").GetInt32();
    Console.WriteLine($"  Author created with Id={newAuthorId}\n");
}

// ==============================================================
// OPERATION 3: Демонстрация Func<int, Task<dynamic>>
// ==============================================================
Console.WriteLine("--- ОПЕРАЦИЯ 3: Получить автора по Id (через Func) ---");

Func<int, Task<dynamic?>> getAuthorById = async (id) => await api.GetAuthorByIdAsync(id);

if (newAuthorId > 0)
{
    var foundAuthor = await getAuthorById(newAuthorId);
    if (foundAuthor is JsonElement foundElem)
    {
        var firstName = foundElem.GetProperty("firstName").GetString();
        var email = foundElem.GetProperty("email").GetString();
        Console.WriteLine($"  Found: {firstName} - {email}\n");
    }
}

// ==============================================================
// OPERATION 4: Создать статью
// Демонстрирует раннее срабатывание события OnEntityCreated
// ==============================================================
Console.WriteLine("--- ОПЕРАЦИЯ 4: Создать статью ---");

var newArticle = new
{
    title = "Article from client with delegates",
    content = "Demonstrates Event, EventHandler usage in .NET",
    keywords = "delegates, events, EventHandler",
    authorId = newAuthorId > 0 ? newAuthorId : 1,
    editorId = (int?)null,
    issueId = (int?)null
};

var createdArticle = await api.CreateArticleAsync(newArticle);
int newArticleId = 0;

if (createdArticle is JsonElement articleElem)
{
    newArticleId = articleElem.GetProperty("id").GetInt32();
    var title = articleElem.GetProperty("title").GetString();
    Console.WriteLine($"  Article created with Id={newArticleId}\n");
}

// ==============================================================
// ДЕМОНСТРАЦИЯ ОТПИСКИ
// ==============================================================
Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  ДЕМОНСТРАЦИЯ ОТПИСКИ (-= оператор)                        ║");
Console.WriteLine("║  Отключим fileLogHandler - файлового логгера              ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

api.RequestCompleted -= fileLogHandler;

// ==============================================================
// OPERATION 5: Обновить статью (после отписки)
// Логирование в файл теперь отключено!
// ==============================================================
Console.WriteLine("--- ОПЕРАЦИЯ 5: Обновить статью (файл-лог отключен) ---");

if (newArticleId > 0)
{
    var updateDto = new
    {
        title = "Updated article",
        content = "Content updated by console client",
        keywords = "update, demo",
        status = 2,
        authorId = newAuthorId > 0 ? newAuthorId : 1,
        editorId = (int?)null,
        issueId = (int?)null
    };

    var updated = await api.UpdateArticleAsync(newArticleId, updateDto);
    if (updated is JsonElement updElem)
    {
        var status = updElem.GetProperty("status").GetString();
        Console.WriteLine($"  Article updated. New status: {status}\n");
    }
}

// ==============================================================
// OPERATION 6: Удалить статью
// Демонстрирует срабатывание события OnEntityDeleted
// ==============================================================
Console.WriteLine("--- ОПЕРАЦИЯ 6: Удалить статью ---");

if (newArticleId > 0)
{
    var deleted = await api.DeleteArticleAsync(newArticleId);
    Console.WriteLine();
}

// ==============================================================
// OPERATION 7: Удалить автора
// Демонстрирует срабатывание события OnEntityDeleted
// ==============================================================
Console.WriteLine("--- ОПЕРАЦИЯ 7: Удалить автора ---");

if (newAuthorId > 0)
{
    await api.DeleteAuthorAsync(newAuthorId);
    Console.WriteLine();
}

// ==============================================================
// ИТОГОВЫЙ ОТЧЕТ
// ==============================================================
Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                      ИТОГОВЫЙ ОТЧЕТ                       ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Выполнено 7 операций с API (GET, POST, PUT, DELETE)     ║");
Console.WriteLine("║                                                            ║");
Console.WriteLine("║  ДЕМОНСТРИРОВАННЫЕ КОНЦЕПЦИИ:                             ║");
Console.WriteLine("║  * OnRequestCompleted - собственный делегат              ║");
Console.WriteLine("║  * OnRequestError - собственный делегат                  ║");
Console.WriteLine("║  * Action<T> - стандартный void-делегат                 ║");
Console.WriteLine("║  * Func<T, TResult> - стандартный делегат с возвратом   ║");
Console.WriteLine("║  * Многоадресные делегаты (+=)                           ║");
Console.WriteLine("║  * Отписка от делегатов (-=)                             ║");
Console.WriteLine("║  * Собственные события (EventHandler<TEventArgs>)        ║");
Console.WriteLine("║  * Publisher/Subscriber паттерн                          ║");
Console.WriteLine("║  * Несколько слушателей на одно событие                  ║");
Console.WriteLine("║  * Безопасный вызов событий (?.)                         ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  ЦИКЛ ЖИЗНИ ПОДПИСКИ:                                     ║");
Console.WriteLine("║  1. Операции 1-4: консоль + файл (оба логгера активны)   ║");
Console.WriteLine("║  2. Операции 5-7: только консоль (файл отключен)         ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

if (File.Exists(logFile))
{
    var logLines = File.ReadAllLines(logFile).Length;
    Console.WriteLine($"\nФайл лога '{logFile}' содержит {logLines} записей (операции 1-4).");
}
