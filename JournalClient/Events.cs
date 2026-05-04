namespace JournalClient.Events;

// ============================================================
// ПОЛЬЗОВАТЕЛЬСКИЕ СОБЫТИЯ И ДЕЛЕГАТЫ
// Демонстрируют полный цикл работы с event-driven архитектурой
// ============================================================

/// <summary>
/// Информация о событии завершения HTTP-запроса.
/// Используется вместо простых параметров для лучшей расширяемости.
/// </summary>
public class RequestCompletedEventArgs : EventArgs
{
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long ElapsedMs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Информация о событии ошибки API.
/// </summary>
public class RequestErrorEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Информация о событии аутентификации (демонстрация custom-событий).
/// </summary>
public class AuthenticationEventArgs : EventArgs
{
    public string Username { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Информация о создании новой записи в системе.
/// </summary>
public class EntityCreatedEventArgs : EventArgs
{
    public string EntityType { get; set; } = string.Empty; // "Author", "Article", etc.
    public int EntityId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Информация об удалении записи.
/// </summary>
public class EntityDeletedEventArgs : EventArgs
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Издатель (Publisher) событий API.
/// Инкапсулирует логику управления подписчиками (subscribers).
/// </summary>
public class ApiEventPublisher
{
    // ============================================================
    // СОБЫТИЯ (используют EventHandler<TEventArgs>)
    // ============================================================

    /// <summary>
    /// Событие завершения HTTP-запроса.
    /// Подписчики получают информацию о запросе, методе, статусе и времени.
    /// </summary>
    public event EventHandler<RequestCompletedEventArgs>? OnRequestCompleted;

    public event EventHandler<RequestErrorEventArgs>? OnRequestError;
    public event EventHandler<EntityCreatedEventArgs>? OnEntityCreated;
    public event EventHandler<EntityDeletedEventArgs>? OnEntityDeleted;
    public event EventHandler<AuthenticationEventArgs>? OnAuthenticationAttempt;

    // ============================================================
    // МЕТОДЫ ДЛЯ ИЗДАТЕЛЯ (вызываются из ApiService)
    // ============================================================

    public virtual void RaiseRequestCompleted(string endpoint, string method, int statusCode, long elapsedMs)
    {
        OnRequestCompleted?.Invoke(this, new RequestCompletedEventArgs
        {
            Endpoint = endpoint,
            Method = method,
            StatusCode = statusCode,
            ElapsedMs = elapsedMs
        });
    }

    public virtual void RaiseRequestError(string message, Exception? ex = null)
    {
        OnRequestError?.Invoke(this, new RequestErrorEventArgs
        {
            Message = message,
            Exception = ex
        });
    }

    public virtual void RaiseEntityCreated(string entityType, int entityId, string description)
    {
        OnEntityCreated?.Invoke(this, new EntityCreatedEventArgs
        {
            EntityType = entityType,
            EntityId = entityId,
            Description = description
        });
    }

    public virtual void RaiseEntityDeleted(string entityType, int entityId)
    {
        OnEntityDeleted?.Invoke(this, new EntityDeletedEventArgs
        {
            EntityType = entityType,
            EntityId = entityId
        });
    }

    public virtual void RaiseAuthenticationAttempt(string username, bool isSuccess)
    {
        OnAuthenticationAttempt?.Invoke(this, new AuthenticationEventArgs
        {
            Username = username,
            IsSuccess = isSuccess
        });
    }
}

/// <summary>
/// Подписчик (Subscriber) событий.
/// Обрабатывает события и выполняет необходимые действия.
/// </summary>
public class ApiEventListener
{
    private readonly string _listenerName;

    public ApiEventListener(string name)
    {
        _listenerName = name;
    }

    // ============================================================
    // ОБРАБОТЧИКИ СОБЫТИЙ (EventHandler<TEventArgs>)
    // ============================================================

    public void OnRequestCompleted(object? sender, RequestCompletedEventArgs e)
    {
        var color = e.StatusCode >= 200 && e.StatusCode < 300 ? ConsoleColor.Green
                  : e.StatusCode >= 400 ? ConsoleColor.Red
                  : ConsoleColor.Yellow;

        Console.ForegroundColor = color;
        Console.WriteLine($"  [{_listenerName}] [{e.Method}] {e.Endpoint} → {e.StatusCode} ({e.ElapsedMs}мс)");
        Console.ResetColor();
    }

    public void OnRequestError(object? sender, RequestErrorEventArgs e)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [{_listenerName}] ✗ ОШИБКА: {e.Message}");
        if (e.Exception != null)
            Console.WriteLine($"    {e.Exception.GetType().Name}: {e.Exception.Message}");
        Console.ResetColor();
    }

    public void OnEntityCreated(object? sender, EntityCreatedEventArgs e)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  [{_listenerName}] ✨ Создано: {e.EntityType} #{e.EntityId} - {e.Description}");
        Console.ResetColor();
    }

    public void OnEntityDeleted(object? sender, EntityDeletedEventArgs e)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"  [{_listenerName}] 🗑️  Удалено: {e.EntityType} #{e.EntityId}");
        Console.ResetColor();
    }

    public void OnAuthenticationAttempt(object? sender, AuthenticationEventArgs e)
    {
        var color = e.IsSuccess ? ConsoleColor.Green : ConsoleColor.Red;
        Console.ForegroundColor = color;
        Console.WriteLine($"  [{_listenerName}] 🔐 Аутентификация: {e.Username} - {(e.IsSuccess ? "OK" : "FAILED")}");
        Console.ResetColor();
    }
}
