using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JournalApi.Data;
using JournalApi.Models;
using JournalApi.Services;

namespace JournalApi.Controllers;

/// <summary>
/// Контроллер для управления авторами журнала.
/// Реализует полный CRUD с кэшированием GET-запросов через Redis.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthorsController : ControllerBase
{
    private readonly JournalDbContext _db;
    private readonly CacheService _cache;

    // Ключи кэша — вынесены в константы для удобства инвалидации
    private const string AllAuthorsKey = "authors:all";
    private string AuthorKey(int id) => $"authors:{id}";

    public AuthorsController(JournalDbContext db, CacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>GET /api/authors — список всех авторов (кэшируется)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AuthorDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        // Пробуем получить из Redis
        var cached = await _cache.GetAsync<List<AuthorDto>>(AllAuthorsKey);
        if (cached != null) return Ok(cached);

        // Если в кэше нет — идём в PostgreSQL
        var authors = await _db.Authors
            .Include(a => a.Articles)
            .Select(a => new AuthorDto(
                a.Id, a.FirstName, a.LastName, a.Email, a.Bio,
                a.CreatedAt, a.Articles.Count))
            .ToListAsync();

        // Сохраняем в Redis на 5 минут
        await _cache.SetAsync(AllAuthorsKey, authors, TimeSpan.FromMinutes(5));
        return Ok(authors);
    }

    /// <summary>GET /api/authors/{id} — один автор по Id (кэшируется)</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AuthorDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var cached = await _cache.GetAsync<AuthorDto>(AuthorKey(id));
        if (cached != null) return Ok(cached);

        var author = await _db.Authors
            .Include(a => a.Articles)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (author == null) return NotFound(new { message = $"Автор с Id={id} не найден" });

        var dto = new AuthorDto(
            author.Id, author.FirstName, author.LastName, author.Email,
            author.Bio, author.CreatedAt, author.Articles.Count);

        await _cache.SetAsync(AuthorKey(id), dto, TimeSpan.FromMinutes(5));
        return Ok(dto);
    }

    /// <summary>POST /api/authors — создать нового автора</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AuthorDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateAuthorDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Проверяем уникальность Email
        if (await _db.Authors.AnyAsync(a => a.Email == dto.Email))
            return BadRequest(new { message = "Автор с таким Email уже существует" });

        var author = new Author
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Bio = dto.Bio,
            CreatedAt = DateTime.UtcNow
        };

        _db.Authors.Add(author);
        await _db.SaveChangesAsync();

        // Инвалидируем кэш списка (добавилась новая запись)
        await _cache.RemoveAsync(AllAuthorsKey);

        var result = new AuthorDto(
            author.Id, author.FirstName, author.LastName, author.Email,
            author.Bio, author.CreatedAt, 0);

        return CreatedAtAction(nameof(GetById), new { id = author.Id }, result);
    }

    /// <summary>PUT /api/authors/{id} — обновить автора</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AuthorDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAuthorDto dto)
    {
        var author = await _db.Authors.FindAsync(id);
        if (author == null) return NotFound(new { message = $"Автор с Id={id} не найден" });

        author.FirstName = dto.FirstName;
        author.LastName = dto.LastName;
        author.Email = dto.Email;
        author.Bio = dto.Bio;

        await _db.SaveChangesAsync();

        // Инвалидируем кэш для этого автора И для списка всех авторов
        await _cache.RemoveManyAsync(AuthorKey(id), AllAuthorsKey);

        var articleCount = await _db.Articles.CountAsync(a => a.AuthorId == id);
        return Ok(new AuthorDto(
            author.Id, author.FirstName, author.LastName, author.Email,
            author.Bio, author.CreatedAt, articleCount));
    }

    /// <summary>DELETE /api/authors/{id} — удалить автора</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Delete(int id)
    {
        var author = await _db.Authors.Include(a => a.Articles).FirstOrDefaultAsync(a => a.Id == id);
        if (author == null) return NotFound(new { message = $"Автор с Id={id} не найден" });

        if (author.Articles.Any())
            return BadRequest(new { message = "Нельзя удалить автора, у которого есть статьи" });

        _db.Authors.Remove(author);
        await _db.SaveChangesAsync();

        await _cache.RemoveManyAsync(AuthorKey(id), AllAuthorsKey);
        return NoContent();
    }
}
