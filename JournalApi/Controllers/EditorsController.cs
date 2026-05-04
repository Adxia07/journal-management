using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JournalApi.Data;
using JournalApi.Models;
using JournalApi.Services;

namespace JournalApi.Controllers;

/// <summary>
/// Контроллер для управления редакторами журнала.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EditorsController : ControllerBase
{
    private readonly JournalDbContext _db;
    private readonly CacheService _cache;

    private const string AllEditorsKey = "editors:all";
    private string EditorKey(int id) => $"editors:{id}";

    public EditorsController(JournalDbContext db, CacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>GET /api/editors — список всех редакторов (кэшируется)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EditorDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var cached = await _cache.GetAsync<List<EditorDto>>(AllEditorsKey);
        if (cached != null) return Ok(cached);

        var editors = await _db.Editors
            .Select(e => new EditorDto(
                e.Id, e.FirstName, e.LastName, e.Email,
                e.Specialization, e.HiredAt))
            .ToListAsync();

        await _cache.SetAsync(AllEditorsKey, editors, TimeSpan.FromMinutes(5));
        return Ok(editors);
    }

    /// <summary>GET /api/editors/{id} — один редактор по Id</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EditorDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var cached = await _cache.GetAsync<EditorDto>(EditorKey(id));
        if (cached != null) return Ok(cached);

        var editor = await _db.Editors.FindAsync(id);
        if (editor == null) return NotFound(new { message = $"Редактор с Id={id} не найден" });

        var dto = new EditorDto(
            editor.Id, editor.FirstName, editor.LastName,
            editor.Email, editor.Specialization, editor.HiredAt);

        await _cache.SetAsync(EditorKey(id), dto, TimeSpan.FromMinutes(5));
        return Ok(dto);
    }

    /// <summary>POST /api/editors — создать редактора</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EditorDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateEditorDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _db.Editors.AnyAsync(e => e.Email == dto.Email))
            return BadRequest(new { message = "Редактор с таким Email уже существует" });

        var editor = new Editor
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Specialization = dto.Specialization,
            HiredAt = DateTime.UtcNow
        };

        _db.Editors.Add(editor);
        await _db.SaveChangesAsync();

        await _cache.RemoveAsync(AllEditorsKey);

        var result = new EditorDto(
            editor.Id, editor.FirstName, editor.LastName,
            editor.Email, editor.Specialization, editor.HiredAt);

        return CreatedAtAction(nameof(GetById), new { id = editor.Id }, result);
    }

    /// <summary>PUT /api/editors/{id} — обновить редактора</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EditorDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEditorDto dto)
    {
        var editor = await _db.Editors.FindAsync(id);
        if (editor == null) return NotFound(new { message = $"Редактор с Id={id} не найден" });

        editor.FirstName = dto.FirstName;
        editor.LastName = dto.LastName;
        editor.Email = dto.Email;
        editor.Specialization = dto.Specialization;

        await _db.SaveChangesAsync();
        await _cache.RemoveManyAsync(EditorKey(id), AllEditorsKey);

        return Ok(new EditorDto(
            editor.Id, editor.FirstName, editor.LastName,
            editor.Email, editor.Specialization, editor.HiredAt));
    }

    /// <summary>DELETE /api/editors/{id} — удалить редактора</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Delete(int id)
    {
        var editor = await _db.Editors
            .Include(e => e.Articles)
            .Include(e => e.Issues)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (editor == null) return NotFound(new { message = $"Редактор с Id={id} не найден" });

        if (editor.Articles.Any() || editor.Issues.Any())
            return BadRequest(new { message = "Нельзя удалить редактора, у которого есть статьи или выпуски" });

        _db.Editors.Remove(editor);
        await _db.SaveChangesAsync();

        await _cache.RemoveManyAsync(EditorKey(id), AllEditorsKey);
        return NoContent();
    }
}
