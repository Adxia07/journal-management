# Journal Management System

Production-ready система управления редакцией журнала на базе современного стека технологий.

## 📋 Описание

Полнофункциональная система для управления статьями, авторами, редакторами и выпусками научного журнала. Реализована на ASP.NET Core с использованием PostgreSQL, Redis, Nginx, Prometheus и Grafana.

## 🏗️ Архитектура

### Компоненты системы:

- **ASP.NET Core Web API** — REST API с CRUD операциями
- **PostgreSQL** — реляционная база данных с миграциями EF Core
- **Redis** — кэширование часто запрашиваемых данных
- **Nginx** — обратный прокси и балансировка нагрузки
- **Prometheus** — сбор метрик приложения
- **Grafana** — визуализация метрик в дашбордах
- **C# Console Client** — клиент с демонстрацией делегатов и событий

### Сущности:

1. **Author** — авторы статей
   - FirstName, LastName, Email, Bio
   
2. **Article** — статьи журнала
   - Title, Content, Keywords, Status, AuthorId, EditorId, IssueId
   - Статусы: Draft, Submitted, UnderReview, Accepted, Rejected, Published

3. **Editor** — редакторы и редакционный совет
   - FirstName, LastName, Email, Specialization

4. **Issue** — выпуски журнала
   - Title, Number, Year, PublishedAt, EditorId

## 🚀 Быстрый старт

### Требования:
- Docker & Docker Compose
- .NET SDK 8.0+
- Git

### Установка и запуск:

```bash
# Клонирование репозитория
git clone https://github.com/Adxia07/journal-management.git
cd journal-management

# Запуск всех компонентов в Docker
docker compose up -d

# Проверка статуса
docker compose ps
```

### Доступ к компонентам:

- **API Swagger** — http://localhost/swagger
- **Prometheus** — http://localhost:9090
- **Grafana** — http://localhost:3000 (admin/admin)
- **PostgreSQL** — localhost:5432
- **Redis** — localhost:6379

## 📡 REST API Эндпоинты

### Authors (Авторы)
```
GET    /api/authors              # Список всех авторов
GET    /api/authors/{id}         # Автор по ID
POST   /api/authors              # Создать автора
PUT    /api/authors/{id}         # Обновить автора
DELETE /api/authors/{id}         # Удалить автора
```

### Articles (Статьи)
```
GET    /api/articles             # Список всех статей (кэшируется)
GET    /api/articles/{id}        # Статья по ID (кэшируется)
GET    /api/articles/by-author/{authorId}
GET    /api/articles/by-status/{status}
POST   /api/articles             # Создать статью
PUT    /api/articles/{id}        # Обновить статью
DELETE /api/articles/{id}        # Удалить статью
```

### Editors (Редакторы)
```
GET    /api/editors              # Список редакторов
GET    /api/editors/{id}         # Редактор по ID
POST   /api/editors              # Создать редактора
PUT    /api/editors/{id}         # Обновить редактора
DELETE /api/editors/{id}         # Удалить редактора
```

### Issues (Выпуски)
```
GET    /api/issues               # Список выпусков
GET    /api/issues/{id}          # Выпуск по ID
POST   /api/issues               # Создать выпуск
PUT    /api/issues/{id}          # Обновить выпуск
DELETE /api/issues/{id}          # Удалить выпуск
```

## 🔧 Кэширование

Redis используется для кэширования:
- `GET /api/articles` — все статьи (TTL: 5 минут)
- `GET /api/articles/{id}` — одна статья (TTL: 5 минут)

Кэш инвалидируется при создании, обновлении или удалении статьи.

## 📊 Мониторинг

### Grafana Dashboard

Дашборд включает следующие панели:
- HTTP Requests Rate (запросы/сек по типам)
- Request Latency (p95, p99 percentile)
- HTTP Status Distribution (2xx/4xx/5xx)
- Error Rate gauge (5xx ошибки)
- Requests by Endpoint
- Total Requests Count
- Total Requests gauge
- Memory Usage

### Prometheus Метрики

Приложение предоставляет метрики через `/metrics`:
- `http_requests_received_total` — счётчик запросов
- `http_request_duration_seconds_bucket` — latency гистограмма

## 💻 C# Console Client

Консольное приложение демонстрирует применение делегатов в C#:

```bash
cd JournalClient
dotnet run
```

### Демонстрируемые концепции:

- **Собственные делегаты**: `OnRequestCompleted`, `OnRequestError`
- **EventHandler<TEventArgs>**: современный способ работы с событиями
- **Action<T>**: void-делегаты для callback'ов
- **Func<T, TResult>**: делегаты с возвратом значения
- **Многоадресные делегаты**: несколько обработчиков на одно событие
- **Динамическая подписка**: операторы `+=` и `-=`

Клиент выполняет 7 операций:
1. GET авторов
2. POST создание автора (событие OnEntityCreated)
3. GET автора по ID (Func-делегат)
4. POST создание статьи (событие OnEntityCreated)
5. PUT обновление статьи (после отписки fileLogHandler)
6. DELETE удаление статьи (событие OnEntityDeleted)
7. DELETE удаление автора (событие OnEntityDeleted)

## 🧪 Тестирование

```bash
# Запуск всех тестов
dotnet test

# Или конкретно тесты API
dotnet test JournalApi.Tests
```

Включены 6 тестов для проверки:
- CRUD операций с Author
- Связей между сущностями
- Статусов статей
- Подсчёта записей

## 🔄 CI/CD Pipeline

GitHub Actions автоматически:
1. Восстанавливает зависимости (`dotnet restore`)
2. Собирает проект (`dotnet build`)
3. Запускает тесты (`dotnet test`)
4. Собирает Docker образы

Статус pipeline видно в разделе Actions на GitHub.

## 📁 Структура проекта

```
journal-management/
├── .github/workflows/
│   └── ci.yml                          # GitHub Actions pipeline
├── JournalApi/
│   ├── Controllers/
│   │   ├── ArticlesController.cs       # CRUD для статей
│   │   ├── AuthorsController.cs        # CRUD для авторов
│   │   ├── EditorsController.cs        # CRUD для редакторов
│   │   └── IssuesController.cs         # CRUD для выпусков
│   ├── Models/
│   │   ├── Entities.cs                 # Классы сущностей
│   │   └── Dtos.cs                     # DTO для API
│   ├── Data/
│   │   ├── JournalDbContext.cs         # DbContext
│   │   └── DbSeeder.cs                 # Инициализация данных
│   ├── Services/
│   │   └── CacheService.cs             # Обёртка Redis
│   ├── Dockerfile                      # Многоступенчатая сборка
│   ├── Program.cs                      # Конфигурация приложения
│   └── JournalApi.csproj
├── JournalApi.Tests/
│   ├── DbContextTests.cs               # Тесты
│   └── JournalApi.Tests.csproj
├── JournalClient/
│   ├── Program.cs                      # Main клиента
│   ├── ApiService.cs                   # HTTP клиент
│   ├── Events.cs                       # EventHandler система
│   └── JournalClient.csproj
├── JournalManagement.Web/              # Web интерфейс (отдельно)
├── nginx/
│   └── nginx.conf                      # Конфиг обратного прокси
├── prometheus/
│   └── prometheus.yml                  # Конфиг сбора метрик
├── grafana/
│   ├── dashboards/
│   │   └── journal-api-dashboard.json  # Дашборд JSON
│   └── provisioning/
│       ├── dashboards.yml
│       └── datasources/
│           └── prometheus.yml
├── docker-compose.yml                  # Оркестрация контейнеров
├── DOCUMENTATION.md                    # Подробная документация
└── README.md                           # Этот файл
```

## 🛠️ Используемые технологии

- **Backend**: C#, ASP.NET Core 8.0
- **Database**: PostgreSQL 16
- **Cache**: Redis 7
- **Proxy**: Nginx
- **Monitoring**: Prometheus, Grafana
- **Testing**: xUnit
- **Containerization**: Docker, Docker Compose
- **CI/CD**: GitHub Actions

## 📝 Требования к коду

- .NET SDK 8.0 или выше
- Docker & Docker Compose
- Git для версионирования

## 🤝 Авторы

- Разработано как лабораторная работа по дисциплине "Современные серверные стеки"

## 📄 Лицензия

MIT

## 📞 Поддержка

В случае возникновения проблем проверьте:
1. Установлен ли Docker Desktop
2. Свободны ли порты 80, 3000, 9090, 5432, 6379
3. Достаточно ли места на диске для образов
4. Содержит ли `.env` корректные значения переменных окружения
