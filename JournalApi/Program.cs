using Microsoft.EntityFrameworkCore;
using JournalApi.Data;
using JournalApi.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ===== 1. Чтение конфигурации из переменных окружения =====
// Строки подключения берутся из docker-compose.yml через environment:
var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=journal_db;Username=journal_user;Password=journal_pass";

var redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION")
    ?? "localhost:6379";

// ===== 2. Регистрация сервисов в DI-контейнере =====

// Entity Framework Core + PostgreSQL
builder.Services.AddDbContext<JournalDbContext>(options =>
    options.UseNpgsql(connectionString));

// Redis распределённый кэш
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = "journal:";   // Префикс для всех ключей в Redis
});

// Наш сервис кэширования (обёртка над IDistributedCache)
builder.Services.AddScoped<CacheService>();

// Контроллеры API
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Сериализуем Enum как строки (Draft, Published и т.д.)
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Swagger / OpenAPI документация
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Система управления редакцией журнала",
        Version = "v1",
        Description = "REST API для управления статьями, авторами, редакторами и выпусками научного журнала"
    });
    
    // Включаем XML-комментарии в документацию
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// CORS — разрешаем запросы с любых источников (для разработки)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// ===== 3. Настройка HTTP pipeline =====

// Swagger UI доступен по адресу /swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Journal API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();

// Prometheus метрики доступны по /metrics
// Nginx не проксирует /metrics наружу — это внутренний эндпоинт
app.UseMetricServer();         // Регистрирует /metrics эндпоинт
app.UseHttpMetrics();          // Автоматически собирает метрики HTTP-запросов

app.UseRouting();
app.MapControllers();

// Проверка работоспособности (healthcheck для docker-compose depends_on)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// ===== 4. Применение миграций и заполнение тестовыми данными =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JournalDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Применение миграций базы данных...");

        // Принудительно применяем миграции
        await db.Database.MigrateAsync();
        logger.LogInformation("Миграции успешно применены.");

        // Заполняем тестовыми данными
        await DbSeeder.SeedAsync(db);
        logger.LogInformation("База данных готова к работе.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ошибка при инициализации базы данных. Приложение запустится без данных.");
    }
}

app.Run();

// Делаем Program доступным для тестов
public partial class Program { }
