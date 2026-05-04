namespace JournalApi.Models;

/// <summary>
/// Автор статьи
/// </summary>
public class Author
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Article> Articles { get; set; } = new List<Article>();
}

/// <summary>
/// Редактор журнала
/// </summary>
public class Editor
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public DateTime HiredAt { get; set; } = DateTime.UtcNow;

    public ICollection<Article> Articles { get; set; } = new List<Article>();
    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
}

/// <summary>
/// Выпуск (номер) журнала
/// </summary>
public class Issue
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Number { get; set; }
    public int Year { get; set; }
    public DateTime PublishedAt { get; set; }
    public string? Description { get; set; }
    
    public int EditorId { get; set; }
    public Editor Editor { get; set; } = null!;

    public ICollection<Article> Articles { get; set; } = new List<Article>();
}

/// <summary>
/// Статья журнала (центральная сущность)
/// </summary>
public class Article
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Keywords { get; set; }
    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }

    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;

    public int? EditorId { get; set; }
    public Editor? Editor { get; set; }

    public int? IssueId { get; set; }
    public Issue? Issue { get; set; }
}

/// <summary>
/// Статус статьи в редакционном процессе
/// </summary>
public enum ArticleStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Accepted = 3,
    Rejected = 4,
    Published = 5,
    Withdrawn = 6
}