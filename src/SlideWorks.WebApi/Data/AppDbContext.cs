using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Features.OAuth;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Deck> Decks => Set<Deck>();

    public DbSet<Slide> Slides => Set<Slide>();

    public DbSet<ActivityEntryRow> ActivityEntries => Set<ActivityEntryRow>();

    public DbSet<DeckViewer> DeckViewers => Set<DeckViewer>();

    public DbSet<DeckImage> DeckImages => Set<DeckImage>();

    public DbSet<OAuthClientRow> OAuthClients => Set<OAuthClientRow>();

    public DbSet<OAuthRequestRow> OAuthRequests => Set<OAuthRequestRow>();

    public DbSet<OAuthCodeRow> OAuthCodes => Set<OAuthCodeRow>();

    public DbSet<AgentTokenRow> AgentTokens => Set<AgentTokenRow>();

    public DbSet<AudienceSession> AudienceSessions => Set<AudienceSession>();

    public DbSet<AudienceJoin> AudienceJoins => Set<AudienceJoin>();

    // SQLite can't compare or ORDER BY a DateTimeOffset stored as TEXT, so every one is stored as a sortable integer.
    // Expiry checks and newest-first lists then run in SQL.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(u => u.Email).IsUnique();
            user.HasIndex(u => u.GoogleSubject).IsUnique();
            user.Property(u => u.Access).HasConversion<string>();
        });

        modelBuilder.Entity<Deck>(deck =>
        {
            deck.Property(d => d.Id).HasMaxLength(12);
            deck.Property(d => d.Sharing).HasConversion<string>();
            deck.HasOne<User>().WithMany().HasForeignKey(d => d.OwnerId).OnDelete(DeleteBehavior.Restrict);
            deck.HasIndex(d => d.OwnerId);
            deck.Property(d => d.JoinCode).HasMaxLength(6);
            deck.HasIndex(d => d.JoinCode).IsUnique();
        });

        modelBuilder.Entity<Slide>(slide =>
        {
            slide.HasOne<Deck>().WithMany().HasForeignKey(s => s.DeckId);
            slide.HasIndex(s => new { s.DeckId, s.Position });
        });

        modelBuilder.Entity<ActivityEntryRow>(entry =>
        {
            entry.HasOne<Deck>().WithMany().HasForeignKey(e => e.DeckId);
            entry.HasIndex(e => new { e.DeckId, e.At });
        });

        modelBuilder.Entity<DeckViewer>(viewer =>
        {
            viewer.HasKey(v => new { v.DeckId, v.Email });
            viewer.HasOne<Deck>().WithMany().HasForeignKey(v => v.DeckId);
        });

        modelBuilder.Entity<DeckImage>(image =>
        {
            image.Property(i => i.Id).HasMaxLength(14);
            image.HasOne<Deck>().WithMany().HasForeignKey(i => i.DeckId);
            image.HasIndex(i => i.DeckId);
            image.HasIndex(i => i.UploadTokenHash).IsUnique();
        });

        // Deleting a deck cascades to its sessions, and a session to its joins, as it does for slides and activity.
        modelBuilder.Entity<AudienceSession>(session =>
        {
            session.HasOne<Deck>().WithMany().HasForeignKey(s => s.DeckId);
            session.HasIndex(s => new { s.DeckId, s.StartedAt });
        });

        modelBuilder.Entity<AudienceJoin>(join =>
        {
            join.HasKey(j => new { j.SessionId, j.GuestId });
            join.HasOne<AudienceSession>().WithMany().HasForeignKey(j => j.SessionId);
        });

        modelBuilder.Entity<OAuthClientRow>(client => client.HasKey(c => c.ClientId));

        modelBuilder.Entity<OAuthRequestRow>(request => request.HasKey(r => r.Id));

        modelBuilder.Entity<OAuthCodeRow>(code => code.HasKey(c => c.CodeHash));

        modelBuilder.Entity<AgentTokenRow>(token =>
        {
            token.Property(t => t.Kind).HasConversion<string>();
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasIndex(t => t.FamilyId);
        });
    }
}
