using System.Diagnostics;
using System.Text;
using System.Text.Json;
using JournalClient.Events;

namespace JournalClient;

// ============================================================
// ДЕЛЕГАТЫ — объявляем собственные типы делегатов
// ============================================================

/// <summary>
/// Делегат для обработки завершения HTTP-запроса.
/// Вызывается после каждого запроса к API с информацией о результате.
/// </summary>
/// <param name="endpoint">URL эндпоинта, к которому обращались</param>
/// <param name="method">HTTP-метод (GET, POST, PUT, DELETE)</param>
/// <param name="statusCode">HTTP-код ответа (200, 201, 404, 500 и т.д.)</param>
/// <param name="elapsedMs">Время выполнения запроса в миллисекундах</param>
public delegate void OnRequestCompleted(string endpoint, string method, int statusCode, long elapsedMs);

/// <summary>
/// Делегат для обработки ошибок API.
/// Вызывается при сетевых ошибках или HTTP-ошибках (4xx, 5xx).
/// </summary>
/// <param name="message">Сообщение об ошибке</param>
/// <param name="exception">Исключение (null если HTTP-ошибка, а не исключение)</param>
public delegate void OnRequestError(string message, Exception? exception);

/// <summary>
/// Сервис для взаимодействия с Journal Management API.
/// Инкапсулирует всю логику HTTP-запросов и демонстрирует делегаты и события.
/// </summary>
public class ApiService : ApiEventPublisher, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    // ============================================================
    // СТАРЫЕ ДЕЛЕГАТЫ (для обратной совместимости)
    // ============================================================

    public event OnRequestCompleted? RequestCompleted;
    public event OnRequestError? RequestError;

    public ApiService(string baseUrl = "http://localhost:80")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    // ============================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ — обёртки над HttpClient
    // ============================================================

    /// <summary>
    /// Выполняет GET-запрос и десериализует JSON-ответ.
    /// Использует Func[T, TResult] — стандартный делегат .NET.
    /// </summary>
    public async Task<T?> GetAsync<T>(string path)
    {
        return await ExecuteAsync<T>(path, "GET", async () =>
            await _httpClient.GetAsync(path));
    }

    /// <summary>
    /// Выполняет POST-запрос с телом в формате JSON.
    /// </summary>
    public async Task<T?> PostAsync<T>(string path, object body)
    {
        return await ExecuteAsync<T>(path, "POST", async () =>
        {
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _httpClient.PostAsync(path, content);
        });
    }

    /// <summary>
    /// Выполняет PUT-запрос для обновления записи.
    /// </summary>
    public async Task<T?> PutAsync<T>(string path, object body)
    {
        return await ExecuteAsync<T>(path, "PUT", async () =>
        {
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _httpClient.PutAsync(path, content);
        });
    }

    /// <summary>
    /// Выполняет DELETE-запрос.
    /// Возвращает true если удаление прошло успешно (204 No Content).
    /// </summary>
    public async Task<bool> DeleteAsync(string path)
    {
        var result = await ExecuteAsync<object>(path, "DELETE", async () =>
            await _httpClient.DeleteAsync(path));
        return result != null || true; // DELETE возвращает 204 без тела
    }

    /// <summary>
    /// Универсальный метод выполнения HTTP-запроса.
    /// Измеряет время, вызывает событие RequestCompleted, обрабатывает ошибки.
    /// 
    /// Принимает Func[Task[HttpResponseMessage]] — делегат-фабрика запроса.
    /// Это позволяет передавать разные HTTP-методы как параметры.
    /// </summary>
    private async Task<T?> ExecuteAsync<T>(
        string path,
        string method,
        Func<Task<HttpResponseMessage>> requestFactory)  // Func<TResult> — стандартный делегат
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await requestFactory();
            stopwatch.Stop();

            // Вызываем события (новые и старые делегаты)
            RequestCompleted?.Invoke(path, method, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);
            RaiseRequestCompleted(path, method, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                var errorMessage = $"HTTP {(int)response.StatusCode}: {errorBody}";
                RequestError?.Invoke(errorMessage, null);
                RaiseRequestError(errorMessage);
                return default;
            }

            // 204 No Content — нет тела для десериализации
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return default;

            var responseJson = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<T>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true // "id" и "Id" — одно и то же
            });
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var errorMessage = $"Сетевая ошибка: {ex.Message}";
            RequestError?.Invoke(errorMessage, ex);
            RaiseRequestError(errorMessage, ex);
            RequestCompleted?.Invoke(path, method, 0, stopwatch.ElapsedMilliseconds);
            RaiseRequestCompleted(path, method, 0, stopwatch.ElapsedMilliseconds);
            return default;
        }
    }

    // ============================================================
    // API МЕТОДЫ — конкретные операции с предметной областью
    // Используют Action<T> — стандартный делегат .NET для callback'ов
    // ============================================================

    /// <summary>
    /// Получить список всех авторов.
    /// callback вызывается с результатом — демонстрация Action[T].
    /// </summary>
    public async Task GetAuthorsAsync(Action<List<dynamic>?>? callback = null)
    {
        var result = await GetAsync<List<dynamic>>("/api/authors");
        callback?.Invoke(result); // Action<T> — void-делегат, не возвращает значение
    }

    public async Task<dynamic?> GetAuthorByIdAsync(int id)
        => await GetAsync<dynamic>($"/api/authors/{id}");

    public async Task<dynamic?> CreateAuthorAsync(object dto)
    {
        var result = await PostAsync<dynamic>("/api/authors", dto);
        if (result is JsonElement elem && elem.TryGetProperty("id", out var idProp))
        {
            var id = idProp.GetInt32();
            var nameProp = elem.TryGetProperty("firstName", out var fn) ? fn.GetString() : "Unknown";
            RaiseEntityCreated("Author", id, nameProp ?? "Unknown");
        }
        return result;
    }

    public async Task<dynamic?> UpdateAuthorAsync(int id, object dto)
        => await PutAsync<dynamic>($"/api/authors/{id}", dto);

    public async Task<bool> DeleteAuthorAsync(int id)
    {
        var result = await DeleteAsync($"/api/authors/{id}");
        if (result) RaiseEntityDeleted("Author", id);
        return result;
    }

    public async Task<dynamic?> GetArticlesAsync()
        => await GetAsync<dynamic>("/api/articles");

    public async Task<dynamic?> GetArticleByIdAsync(int id)
        => await GetAsync<dynamic>($"/api/articles/{id}");

    public async Task<dynamic?> CreateArticleAsync(object dto)
    {
        var result = await PostAsync<dynamic>("/api/articles", dto);
        if (result is JsonElement elem && elem.TryGetProperty("id", out var idProp))
        {
            var id = idProp.GetInt32();
            var titleProp = elem.TryGetProperty("title", out var tp) ? tp.GetString() : "Unknown";
            RaiseEntityCreated("Article", id, titleProp ?? "Unknown");
        }
        return result;
    }

    public async Task<dynamic?> UpdateArticleAsync(int id, object dto)
        => await PutAsync<dynamic>($"/api/articles/{id}", dto);

    public async Task<bool> DeleteArticleAsync(int id)
    {
        var result = await DeleteAsync($"/api/articles/{id}");
        if (result) RaiseEntityDeleted("Article", id);
        return result;
    }

    public void Dispose() => _httpClient.Dispose();
}
