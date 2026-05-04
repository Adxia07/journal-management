using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JournalApi.Data;
using JournalApi.Models;
using JournalApi.Services;

namespace JournalApi.Controllers;

/// <summary>
/// Контроллер для управления выпусками журнала.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class IssuesController : ControllerBase
{
    private readonly JournalDbContext _db;
    private readonly CacheService _cache;

    private const string AllIssuesKey = "issues:all";
    private string IssueKey(int id) => $"issues:{id}";

    public IssuesController(JournalDbContext db, CacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>GET /api/issues — список всех выпусков (кэшируется)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<IssueDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var cached = await _cache.GetAsync<List<IssueDto>>(AllIssuesKey);
        if (cached != null) return Ok(cached);

        var issues = await _db.Issues
            .Include(i => i.Editor)
            .Include(i => i.Articles)
            .Select(i => new IssueDto(
                i.Id, i.Title, i.Number, i.Year, i.PublishedAt, i.Description,
                i.EditorId,
                i.Editor.FirstName + " " + i.Editor.LastName,
                i.Articles.Count))
            .ToListAsync();

        await _cache.SetAsync(AllIssuesKey, issues, TimeSpan.FromMinutes(5));
        return Ok(issues);
    }

    /// <summary>GET /api/issues/{id} — один выпуск по Id</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(IssueDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var cached = await _cache.GetAsync<IssueDto>(IssueKey(id));
        if (cached != null) return Ok(cached);

        var issue = await _db.Issues
            .Include(i => i.Editor)
            .Include(i => i.Articles)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (issue == null) return NotFound(new { message = $"Выпуск с Id={id} не найден" });

        var dto = new IssueDto(
            issue.Id, issue.Title, issue.Number, issue.Year,
            issue.PublishedAt, issue.Description,
            issue.EditorId,
            issue.Editor.FirstName + " " + issue.Editor.LastName,
            issue.Articles.Count);

        await _cache.SetAsync(IssueKey(id), dto, TimeSpan.FromMinutes(5));
        return Ok(dto);
    }

    /// <summary>POST /api/issues — создать новый выпуск</summary>
    [HttpPost]
    [ProducesResponseType(typeof(IssueDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateIssueDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!await _db.Editors.AnyAsync(e => e.Id == dto.EditorId))
            return BadRequest(new { message = $"Редактор с Id={dto.EditorId} не существует" });

        var issue = new Issue
        {
            Title = dto.Title,
            Number = dto.Number,
            Year = dto.Year,
            PublishedAt = dto.PublishedAt,
            Description = dto.Description,
            EditorId = dto.EditorId
        };

        _db.Issues.Add(issue);
        await _db.SaveChangesAsync();
        await _cache.RemoveAsync(AllIssuesKey);

        // Загружаем редактора для ответа
        await _db.Entry(issue).Reference(i => i.Editor).LoadAsync();
        var result = new IssueDto(
            issue.Id, issue.Title, issue.Number, issue.Year,
            issue.PublishedAt, issue.Description, issue.EditorId,
            issue.Editor.FirstName + " " + issue.Editor.LastName, 0);

        return CreatedAtAction(nameof(GetById), new { id = issue.Id }, result);
    }

    /// <summary>PUT /api/issues/{id} — обновить выпуск</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(IssueDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateIssueDto dto)
    {
        var issue = await _db.Issues.Include(i => i.Editor).Include(i => i.Articles)
                                    .FirstOrDefaultAsync(i => i.Id == id);
        if (issue == null) return NotFound(new { message = $"Выпуск с Id={id} не найден" });

        if (!await _db.Editors.AnyAsync(e => e.Id == dto.EditorId))
            return BadRequest(new { message = $"Редактор с Id={dto.EditorId} не существует" });

        issue.Title = dto.Title;
        issue.Number = dto.Number;
        issue.Year = dto.Year;
        issue.PublishedAt = dto.PublishedAt;
        issue.Description = dto.Description;
        issue.EditorId = dto.EditorId;

        await _db.SaveChangesAsync();
        await _db.Entry(issue).Reference(i => i.Editor).LoadAsync();
        await _cache.RemoveManyAsync(IssueKey(id), AllIssuesKey);

        return Ok(new IssueDto(
            issue.Id, issue.Title, issue.Number, issue.Year,
            issue.PublishedAt, issue.Description, issue.EditorId,
            issue.Editor.FirstName + " " + issue.Editor.LastName,
            issue.Articles.Count));
    }

    /// <summary>DELETE /api/issues/{id} — удалить выпуск</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var issue = await _db.Issues.FindAsync(id);
        if (issue == null) return NotFound(new { message = $"Выпуск с Id={id} не найден" });

        _db.Issues.Remove(issue);
        await _db.SaveChangesAsync();
        await _cache.RemoveManyAsync(IssueKey(id), AllIssuesKey);
        return NoContent();
    }
}
