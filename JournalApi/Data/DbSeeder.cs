using JournalApi.Models;
using Microsoft.EntityFrameworkCore;

namespace JournalApi.Data;

/// <summary>
/// Заполняет базу данных тестовыми данными при первом запуске.
/// Проверяет наличие данных перед вставкой, чтобы не дублировать.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(JournalDbContext context)
    {
        try
        {
            Console.WriteLine("[DbSeeder] Начинаем инициализацию БД");
            
            // Применяем миграции автоматически при старте приложения
            await context.Database.MigrateAsync();
            Console.WriteLine("[DbSeeder] Миграции применены");

            // Если статьи уже есть — не вставляем повторно
            var articlesCount = await context.Articles.CountAsync();
            Console.WriteLine($"[DbSeeder] Статей в БД: {articlesCount}");
            if (articlesCount > 0) 
            {
                Console.WriteLine("[DbSeeder] Статьи уже есть, пропускаем заполнение");
                return;
            }
            
            var authorsCount = await context.Authors.CountAsync();
            Console.WriteLine($"[DbSeeder] Авторов в БД: {authorsCount}");

            // ===== 1. Создаём авторов =====
            var authors = new List<Author>
            {
                new() { FirstName = "Иван", LastName = "Петров", Email = "petrov@example.com",
                        Bio = "Специалист в области искусственного интеллекта" },
                new() { FirstName = "Мария", LastName = "Сидорова", Email = "sidorova@example.com",
                        Bio = "Исследователь в области квантовых вычислений" },
                new() { FirstName = "Алексей", LastName = "Козлов", Email = "kozlov@example.com",
                        Bio = "Эксперт по кибербезопасности" },
                new() { FirstName = "Елена", LastName = "Новикова", Email = "novikova@example.com",
                        Bio = "Специалист в области биоинформатики" },
            };
            context.Authors.AddRange(authors);
            Console.WriteLine($"[DbSeeder] Добавлено {authors.Count} авторов в очередь");

            // ===== 2. Создаём редакторов =====
            var editors = new List<Editor>
            {
                new() { FirstName = "Дмитрий", LastName = "Орлов", Email = "orlov@journal.com",
                        Specialization = "Информационные технологии", HiredAt = new DateTime(2020, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
                new() { FirstName = "Светлана", LastName = "Морозова", Email = "morozova@journal.com",
                        Specialization = "Физика и математика", HiredAt = new DateTime(2019, 6, 1, 0, 0, 0, DateTimeKind.Utc) },
            };
            context.Editors.AddRange(editors);
            await context.SaveChangesAsync(); // Сохраняем, чтобы получить Id
            Console.WriteLine($"[DbSeeder] Сохранено {authors.Count} авторов и {editors.Count} редакторов");

            // ===== 3. Создаём выпуски =====
            var issues = new List<Issue>
            {
                new() { Title = "Технологии будущего", Number = 1, Year = 2024,
                        PublishedAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                        Description = "Специальный выпуск об инновационных технологиях",
                        EditorId = editors[0].Id },
                new() { Title = "Наука и общество", Number = 2, Year = 2024,
                        PublishedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                        Description = "Выпуск о влиянии науки на общество",
                        EditorId = editors[1].Id },
            };
            context.Issues.AddRange(issues);
            await context.SaveChangesAsync();
            Console.WriteLine($"[DbSeeder] Добавлено {issues.Count} выпусков");

            // ===== 4. Создаём статьи =====
            var articles = new List<Article>
            {
                new() { Title = "Машинное обучение в медицине",
                        Content = "В данной статье рассматриваются применения МО в диагностике заболеваний...",
                        Keywords = "машинное обучение, медицина, нейронные сети",
                        Status = ArticleStatus.Published,
                        SubmittedAt = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                        PublishedAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                        AuthorId = authors[0].Id, EditorId = editors[0].Id, IssueId = issues[0].Id },
                new() { Title = "Квантовые алгоритмы шифрования",
                        Content = "Квантовые компьютеры открывают новые возможности для криптографии...",
                        Keywords = "квантовые вычисления, криптография, безопасность",
                        Status = ArticleStatus.Published,
                        SubmittedAt = new DateTime(2024, 1, 20, 0, 0, 0, DateTimeKind.Utc),
                        PublishedAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                        AuthorId = authors[1].Id, EditorId = editors[0].Id, IssueId = issues[0].Id },
                new() { Title = "Угрозы кибербезопасности в 2024 году",
                        Content = "Анализ актуальных угроз и методов противодействия...",
                        Keywords = "кибербезопасность, угрозы, защита данных",
                        Status = ArticleStatus.Accepted,
                        SubmittedAt = new DateTime(2024, 2, 5, 0, 0, 0, DateTimeKind.Utc),
                        AuthorId = authors[2].Id, EditorId = editors[1].Id, IssueId = issues[1].Id },
                new() { Title = "Геномика и персонализированная медицина",
                        Content = "Современные методы анализа генома позволяют разрабатывать...",
                        Keywords = "геномика, персонализированная медицина, биоинформатика",
                        Status = ArticleStatus.UnderReview,
                        SubmittedAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                        AuthorId = authors[3].Id, EditorId = editors[1].Id },
                new() { Title = "Этика искусственного интеллекта",
                        Content = "По мере развития ИИ возникают вопросы этического использования...",
                        Keywords = "искусственный интеллект, этика, общество",
                        Status = ArticleStatus.Submitted,
                        SubmittedAt = new DateTime(2024, 4, 15, 0, 0, 0, DateTimeKind.Utc),
                        AuthorId = authors[0].Id },
            };
            context.Articles.AddRange(articles);
            Console.WriteLine($"[DbSeeder] Добавлено {articles.Count} статей в очередь");
            
            await context.SaveChangesAsync();
            Console.WriteLine($"[DbSeeder] Успешно сохранено {articles.Count} статей");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbSeeder] ОШИБКА: {ex.Message}");
            Console.WriteLine($"[DbSeeder] Stack: {ex.StackTrace}");
        }
        finally
        {
            Console.WriteLine("[DbSeeder] Завершение инициализации БД");
        }
    }
}
