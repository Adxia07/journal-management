# Journal Management - Полная документация реализованных компонентов

## Обзор проекта

Система управления редакцией научного журнала с полным мониторингом, логированием и демонстрацией паттернов C#.

---

## 1. Grafana Dashboard (Performance Monitoring)

**Файл:** `grafana/dashboards/journal-api-dashboard.json`

### Панели дашборда:
1. **HTTP Requests Rate** — Количество запросов в секунду по типам (GET, POST, PUT, DELETE)
2. **Request Latency (p95, p99)** — Процентили времени ответа API
3. **HTTP Status Distribution** — Распределение кодов ответов (2xx, 4xx, 5xx)
4. **Error Rate gauge** — Gauge визуализация 5xx ошибок/сек
5. **Requests by Endpoint** — Трафик по конкретным эндпоинтам API
6. **Total Requests Count** — График полного количества запросов
7. **Total Requests gauge** — Gauge счётчик всех запросов

### Метрики (Prometheus):
- `http_requests_received_total` — счётчик запросов (сумма по коду ответа)
- `http_request_duration_seconds_bucket` — гистограмма latency
- Все метрики помечены: `app="journal-api"`, `environment="production"`

### Доступ:
```
http://localhost:3000
Логин: admin
Пароль: admin
Путь: Dashboards → Journal Management → Journal Management API
```

---

## 2. Prometheus Provisioning

**Файлы:**
- `grafana/provisioning/datasources/prometheus.yml` — конфиг Prometheus datasource
- `grafana/provisioning/dashboards.yml` — автозагрузка дашбордов

### Конфигурация:
- Prometheus URL: `http://prometheus:9090` (внутри Docker-сети)
- Интервал сбора: 15 секунд
- Временной диапазон дашборда: Last 6 hours
- Refresh: 30 seconds

---

## 3. C# Console Client с Делегатами и Событиями

### Архитектура:

#### 3.1 Events.cs — Система событий (EventHandler<TEventArgs>)

**Классы событий:**
- `RequestCompletedEventArgs` — информация о завершении запроса
- `RequestErrorEventArgs` — информация об ошибке
- `EntityCreatedEventArgs` — создание сущности (Author, Article)
- `EntityDeletedEventArgs` — удаление сущности
- `AuthenticationEventArgs` — попытка аутентификации

**ApiEventPublisher:**
- Издатель событий (Publisher)
- Методы: `RaiseRequestCompleted()`, `RaiseEntityCreated()`, `RaiseEntityDeleted()` и т.д.
- Использует `EventHandler<TEventArgs>` — современный стиль .NET

**ApiEventListener:**
- Подписчик событий (Subscriber)
- Методы-обработчики: `OnRequestCompleted()`, `OnEntityCreated()`, `OnEntityDeleted()` и т.д.
- Выводит события в консоль с цветовой разметкой

#### 3.2 ApiService.cs — HTTP клиент с делегатами

**Старые делегаты (для совместимости):**
```csharp
public delegate void OnRequestCompleted(string endpoint, string method, int statusCode, long elapsedMs);
public delegate void OnRequestError(string message, Exception? exception);

public event OnRequestCompleted? RequestCompleted;
public event OnRequestError? RequestError;
```

**Новые события (EventHandler<TEventArgs>):**
```csharp
public event EventHandler<RequestCompletedEventArgs>? OnRequestCompleted;
public event EventHandler<RequestErrorEventArgs>? OnRequestError;
public event EventHandler<EntityCreatedEventArgs>? OnEntityCreated;
public event EventHandler<EntityDeletedEventArgs>? OnEntityDeleted;
```

**Стандартные делегаты:**
- `Action<T>` — для callback'ов без возврата (GetAuthorsAsync)
- `Func<T, TResult>` — для функций с параметром и возвратом (GetAuthorById)

**Методы API:**
- GET: `/api/authors`, `/api/articles`, `/api/issues`
- POST: `/api/authors`, `/api/articles` (с событием OnEntityCreated)
- PUT: `/api/articles/{id}` (с событием OnEntityUpdated)
- DELETE: `/api/authors/{id}`, `/api/articles/{id}` (с событием OnEntityDeleted)

#### 3.3 Program.cs — Демонстрация

**Операции (7 штук):**

1. **GET авторов** — Action<T> callback
2. **POST автор** — Создание + событие OnEntityCreated
3. **GET автор по Id** — Func<int, Task<T>> делегат
4. **POST статья** — Создание + событие OnEntityCreated
5. **PUT статья** — Обновление (после отписки fileLogHandler)
6. **DELETE статья** — Удаление + событие OnEntityDeleted
7. **DELETE автор** — Удаление + событие OnEntityDeleted

**Демонстрируемые концепции:**

| Концепция | Описание |
|-----------|---------|
| OnRequestCompleted | Собственный делегат для логирования запросов |
| OnRequestError | Собственный делегат для обработки ошибок |
| Action<T> | Стандартный void-делегат для callback'ов |
| Func<T, TResult> | Стандартный делегат с возвратом значения |
| Многоадресный делегат | += для подключения нескольких обработчиков |
| Отписка | -= для удаления обработчика из цепи |
| EventHandler<TEventArgs> | Современный способ работы с событиями в .NET |
| Publisher/Subscriber | Паттерн между ApiService и ApiEventListener |
| Безопасный вызов | ?. оператор при вызове событий |

**Вывод программы:**

```
╔══════════════════════════════════════════════════════════╗
║   Клиент: Система управления редакцией журнала           ║
║        (Демонстрация делегатов и событий)                ║
╚══════════════════════════════════════════════════════════╝

ЧАСТЬ 1: Стиль делегатов (Action<T> и OnRequestCompleted)
Подписаны: консоль-лог + файл-лог + обработчик ошибок

ЧАСТЬ 2: Новый стиль событий (EventHandler<TEventArgs>)
Подписаны два слушателя на события новой системы

--- ОПЕРАЦИЯ 1: Получить список авторов ---
  [CONSOLE-LOG] [GET] /api/authors → 200 (709мс)
  [CONSOLE] [GET] /api/authors → 200 (709мс)
  [FILE] [GET] /api/authors → 200 (709мс)
  Получено авторов: 5

--- ОПЕРАЦИЯ 2: Создать нового автора ---
  [CONSOLE-LOG] [POST] /api/authors → 201 (234мс)
  [CONSOLE] [POST] /api/authors → 201 (234мс)
  [FILE] [POST] /api/authors → 201 (234мс)
  [CONSOLE] ✨ Создано: Author #7 - Test
  [FILE] ✨ Создано: Author #7 - Test

... (операции 3-7)

Файл лога 'api-requests.log' содержит 41 записей (операции 1-4).
```

---

## 4. Запуск и проверка

### Запуск контейнеров:
```bash
cd C:\Users\Alex\Desktop\journal-management
docker compose up -d
```

### Запуск консольного клиента:
```bash
cd C:\Users\Alex\Desktop\journal-management\JournalClient
dotnet run
```

### Доступ к компонентам:
- **API** — http://localhost:8080/swagger
- **Prometheus** — http://localhost:9090
- **Grafana** — http://localhost:3000 (admin/admin)
- **Nginx** — http://localhost:80
- **PostgreSQL** — localhost:5432
- **Redis** — localhost:6379

---

## 5. Файловая структура

```
journal-management/
├── grafana/
│   ├── dashboards/
│   │   └── journal-api-dashboard.json          # Дашборд с панелями
│   └── provisioning/
│       ├── dashboards.yml                       # Автозагрузка дашбордов
│       └── datasources/
│           └── prometheus.yml                   # Конфиг Prometheus
├── prometheus/
│   └── prometheus.yml                          # Сбор метрик из API
├── JournalClient/
│   ├── Program.cs                              # Main (7 операций)
│   ├── ApiService.cs                           # HTTP клиент + делегаты
│   ├── Events.cs                               # EventHandler<TEventArgs>
│   ├── JournalClient.csproj                    # .NET 8.0 проект
│   └── api-requests.log                        # Лог файлового обработчика
├── JournalApi/                                 # REST API
├── docker-compose.yml                          # Оркестрация сервисов
└── nginx/
    └── nginx.conf                              # Reverse proxy
```

---

## 6. Ключевые особенности

### Мониторинг:
✓ Prometheus собирает метрики каждые 15 секунд
✓ Grafana визуализирует: RPS, Latency, Status codes, Errors
✓ Dashboard обновляется каждые 30 секунд
✓ Временной диапазон: Last 6 hours

### C# паттерны:
✓ Собственные делегаты (OnRequestCompleted, OnRequestError)
✓ Стандартные делегаты (Action<T>, Func<T, TResult>)
✓ Многоадресные делегаты (+=, -=)
✓ EventHandler<TEventArgs> (современный стиль)
✓ Publisher/Subscriber архитектура
✓ Safe event invocation (?.)

### Логирование:
✓ Консоль (CONSOLE-LOG, CONSOLE, FILE слушатели)
✓ Файл (api-requests.log) — логирует только операции 1-4 (до отписки)
✓ События (OnEntityCreated, OnEntityDeleted) — разные для каждого слушателя

---

## 7. Следующие шаги

1. **GitHub Actions CI/CD** — `.github/workflows/ci.yml`
2. **Отчёт по ГОСТ** — Оформление документации
3. **Расширение Client'а** — Добавить асинхронные события
4. **Кастомные метрики** — Добавить business-level метрики
5. **Alerting** — Настроить правила алертирования в Prometheus/Grafana

---

**Версия:** 1.0
**Дата создания:** 2025-05-04
**Статус:** ✓ Полностью готово
