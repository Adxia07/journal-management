using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JournalApi.Data;
using JournalApi.Models;
using JournalApi.Services;

namespace JournalApi.Controllers;

/// <summary>
/// Контроллер для управления статьями журнала.
/// Это центральная сущность системы.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ArticlesController : ControllerBase
{
    private readonly JournalDbContext _db;
    private readonly CacheService _cache;

    private const string AllArticlesKey = "articles:all";
    private string ArticleKey(int id) => $"articles:{id}";

    public ArticlesController(JournalDbContext db, CacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>GET /api/articles — список всех статей (кэшируется в Redis)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ArticleDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var cached = await _cache.GetAsync<List<ArticleDto>>(AllArticlesKey);
        if (cached != null) return Ok(cached);

        var articles = await _db.Articles
            .Include(a => a.Author)
            .Include(a => a.Editor)
            .Include(a => a.Issue)
            .Select(a => new ArticleDto(
                a.Id, a.Title, a.Content, a.Keywords,
                a.Status.ToString(),
                a.SubmittedAt, a.PublishedAt,
                a.AuthorId,
                a.Author.FirstName + " " + a.Author.LastName,
                a.EditorId,
                a.Editor != null ? a.Editor.FirstName + " " + a.Editor.LastName : null,
                a.IssueId,
                a.Issue != null ? a.Issue.Title : null))
            .ToListAsync();

        await _cache.SetAsync(AllArticlesKey, articles, TimeSpan.FromMinutes(5));
        return Ok(articles);
    }

    /// <summary>GET /api/articles/{id} — одна статья по Id (кэшируется)</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ArticleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var cached = await _cache.GetAsync<ArticleDto>(ArticleKey(id));
        if (cached != null) return Ok(cached);

        var article = await _db.Articles
            .Include(a => a.Author)
            .Include(a => a.Editor)
            .Include(a => a.Issue)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (article == null) return NotFound(new { message = $"Статья с Id={id} не найдена" });

        var dto = MapToDto(article);
        await _cache.SetAsync(ArticleKey(id), dto, TimeSpan.FromMinutes(5));
        return Ok(dto);
    }

    /// <summary>GET /api/articles/by-author/{authorId} — статьи конкретного автора</summary>
    [HttpGet("by-author/{authorId:int}")]
    [ProducesResponseType(typeof(IEnumerable<ArticleDto>), 200)]
    public async Task<IActionResult> GetByAuthor(int authorId)
    {
        var articles = await _db.Articles
            .Include(a => a.Author)
            .Include(a => a.Editor)
            .Include(a => a.Issue)
            .Where(a => a.AuthorId == authorId)
            .Select(a => new ArticleDto(
                a.Id, a.Title, a.Content, a.Keywords,
                a.Status.ToString(),
                a.SubmittedAt, a.PublishedAt,
                a.AuthorId,
                a.Author.FirstName + " " + a.Author.LastName,
                a.EditorId,
                a.Editor != null ? a.Editor.FirstName + " " + a.Editor.LastName : null,
                a.IssueId,
                a.Issue != null ? a.Issue.Title : null))
            .ToListAsync();

        return Ok(articles);
    }

    /// <summary>GET /api/articles/by-status/{status} — статьи по статусу</summary>
    [HttpGet("by-status/{status}")]
    [ProducesResponseType(typeof(IEnumerable<ArticleDto>), 200)]
    public async Task<IActionResult> GetByStatus(string status)
    {
        if (!Enum.TryParse<ArticleStatus>(status, true, out var articleStatus))
            return BadRequest(new { message = $"Неверный статус: {status}. Допустимые: Draft, Submitted, UnderReview, Accepted, Rejected, Published" });

        var articles = await _db.Articles
            .Include(a => a.Author)
            .Include(a => a.Editor)
            .Include(a => a.Issue)
            .Where(a => a.Status == articleStatus)
            .Select(a => new ArticleDto(
                a.Id, a.Title, a.Content, a.Keywords,
                a.Status.ToString(),
                a.SubmittedAt, a.PublishedAt,
                a.AuthorId,
                a.Author.FirstName + " " + a.Author.LastName,
                a.EditorId,
                a.Editor != null ? a.Editor.FirstName + " " + a.Editor.LastName : null,
                a.IssueId,
                a.Issue != null ? a.Issue.Title : null))
            .ToListAsync();

        return Ok(articles);
    }

    /// <summary>POST /api/articles — создать статью</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ArticleDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateArticleDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!await _db.Authors.AnyAsync(a => a.Id == dto.AuthorId))
            return BadRequest(new { message = $"Автор с Id={dto.AuthorId} не существует" });

        if (dto.EditorId.HasValue && !await _db.Editors.AnyAsync(e => e.Id == dto.EditorId))
            return BadRequest(new { message = $"Редактор с Id={dto.EditorId} не существует" });

        if (dto.IssueId.HasValue && !await _db.Issues.AnyAsync(i => i.Id == dto.IssueId))
            return BadRequest(new { message = $"Выпуск с Id={dto.IssueId} не существует" });

        var article = new Article
        {
            Title = dto.Title,
            Content = dto.Content,
            Keywords = dto.Keywords,
            Status = ArticleStatus.Draft,
            SubmittedAt = DateTime.UtcNow,
            AuthorId = dto.AuthorId,
            EditorId = dto.EditorId,
            IssueId = dto.IssueId
        };

        _db.Articles.Add(article);
        await _db.SaveChangesAsync();

        // Перезагружаем статью с загруженными связями
        var createdArticle = await _db.Articles
            .Include(a => a.Author)
            .Include(a => a.Editor)
            .Include(a => a.Issue)
            .FirstOrDefaultAsync(a => a.Id == article.Id);

        if (createdArticle == null)
            return BadRequest(new { message = "Не удалось загрузить созданную статью" });

        await _cache.RemoveAsync(AllArticlesKey);

        return CreatedAtAction(nameof(GetById), new { id = createdArticle.Id }, MapToDto(createdArticle));
    }

    /// <summary>PUT /api/articles/{id} — обновить статью</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ArticleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateArticleDto dto)
    {
        var article = await _db.Articles
            .Include(a => a.Author).Include(a => a.Editor).Include(a => a.Issue)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (article == null) return NotFound(new { message = $"Статья с Id={id} не найдена" });

        article.Title = dto.Title;
        article.Content = dto.Content;
        article.Keywords = dto.Keywords;
        article.Status = dto.Status;
        article.AuthorId = dto.AuthorId;
        article.EditorId = dto.EditorId;
        article.IssueId = dto.IssueId;

        // Если статья опубликована — ставим дату публикации
        if (dto.Status == ArticleStatus.Published && article.PublishedAt == null)
            article.PublishedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Перезагружаем связи после обновления
        await _db.Entry(article).Reference(a => a.Author).LoadAsync();
        if (article.EditorId.HasValue)
            await _db.Entry(article).Reference(a => a.Editor).LoadAsync();
        if (article.IssueId.HasValue)
            await _db.Entry(article).Reference(a => a.Issue).LoadAsync();

        await _cache.RemoveManyAsync(ArticleKey(id), AllArticlesKey);

        return Ok(MapToDto(article));
    }

    /// <summary>DELETE /api/articles/{id} — удалить статью</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return NotFound(new { message = $"Статья с Id={id} не найдена" });

        _db.Articles.Remove(article);
        await _db.SaveChangesAsync();
        await _cache.RemoveManyAsync(ArticleKey(id), AllArticlesKey);
        return NoContent();
    }

    // Вспомогательный метод маппинга сущности -> DTO
    private static ArticleDto MapToDto(Article a) => new(
        a.Id, a.Title, a.Content, a.Keywords,
        a.Status.ToString(),
        a.SubmittedAt, a.PublishedAt,
        a.AuthorId,
        a.Author != null ? a.Author.FirstName + " " + a.Author.LastName : "—",
        a.EditorId,
        a.Editor != null ? a.Editor.FirstName + " " + a.Editor.LastName : null,
        a.IssueId,
        a.Issue?.Title);
}
