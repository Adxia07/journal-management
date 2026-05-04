namespace JournalApi.Models;

// ===================== AUTHOR DTOs =====================

public record AuthorDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string? Bio,
    DateTime CreatedAt,
    int ArticleCount
);

public record CreateAuthorDto(
    string FirstName,
    string LastName,
    string Email,
    string? Bio
);

public record UpdateAuthorDto(
    string FirstName,
    string LastName,
    string Email,
    string? Bio
);

// ===================== EDITOR DTOs =====================

public record EditorDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string Specialization,
    DateTime HiredAt
);

public record CreateEditorDto(
    string FirstName,
    string LastName,
    string Email,
    string Specialization
);

public record UpdateEditorDto(
    string FirstName,
    string LastName,
    string Email,
    string Specialization
);

// ===================== ISSUE DTOs =====================

public record IssueDto(
    int Id,
    string Title,
    int Number,
    int Year,
    DateTime PublishedAt,
    string? Description,
    int EditorId,
    string EditorName,
    int ArticleCount
);

public record CreateIssueDto(
    string Title,
    int Number,
    int Year,
    DateTime PublishedAt,
    string? Description,
    int EditorId
);

public record UpdateIssueDto(
    string Title,
    int Number,
    int Year,
    DateTime PublishedAt,
    string? Description,
    int EditorId
);

// ===================== ARTICLE DTOs =====================

public record ArticleDto(
    int Id,
    string Title,
    string Content,
    string? Keywords,
    string Status,
    DateTime SubmittedAt,
    DateTime? PublishedAt,
    int AuthorId,
    string AuthorName,
    int? EditorId,
    string? EditorName,
    int? IssueId,
    string? IssueTitle
);

public record CreateArticleDto(
    string Title,
    string Content,
    string? Keywords,
    int AuthorId,
    int? EditorId,
    int? IssueId
);

public record UpdateArticleDto(
    string Title,
    string Content,
    string? Keywords,
    ArticleStatus Status,
    int AuthorId,
    int? EditorId,
    int? IssueId
);
