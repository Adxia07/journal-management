using Microsoft.EntityFrameworkCore;
using JournalApi.Models;

namespace JournalApi.Data;

/// <summary>
/// Контекст базы данных для системы управления редакцией журнала.
/// Описывает все таблицы и их связи через Fluent API.
/// </summary>
public class JournalDbContext : DbContext
{
    public JournalDbContext(DbContextOptions<JournalDbContext> options) : base(options) { }

    // Таблицы базы данных
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Editor> Editors => Set<Editor>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Article> Articles => Set<Article>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===== Настройка сущности Author =====
        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(a => a.LastName).IsRequired().HasMaxLength(100);
            entity.Property(a => a.Email).IsRequired().HasMaxLength(200);
            entity.HasIndex(a => a.Email).IsUnique(); // Email уникален
        });

        // ===== Настройка сущности Editor =====
        modelBuilder.Entity<Editor>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Specialization).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // ===== Настройка сущности Issue =====
        modelBuilder.Entity<Issue>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Title).IsRequired().HasMaxLength(300);

            // Связь Issue -> Editor (главный редактор выпуска)
            entity.HasOne(i => i.Editor)
                  .WithMany(e => e.Issues)
                  .HasForeignKey(i => i.EditorId)
                  .OnDelete(DeleteBehavior.Restrict); // Нельзя удалить редактора, если у него есть выпуски
        });

        // ===== Настройка сущности Article =====
        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Title).IsRequired().HasMaxLength(500);
            entity.Property(a => a.Content).IsRequired();
            entity.Property(a => a.Status).HasConversion<int>(); // Enum -> int в БД

            // Связь Article -> Author (автор обязателен)
            entity.HasOne(a => a.Author)
                  .WithMany(au => au.Articles)
                  .HasForeignKey(a => a.AuthorId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Связь Article -> Editor (редактор необязателен)
            entity.HasOne(a => a.Editor)
                  .WithMany(e => e.Articles)
                  .HasForeignKey(a => a.EditorId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);

            // Связь Article -> Issue (выпуск необязателен)
            entity.HasOne(a => a.Issue)
                  .WithMany(i => i.Articles)
                  .HasForeignKey(a => a.IssueId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });
    }
}
