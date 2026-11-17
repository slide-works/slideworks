using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Features.OAuth;
using SlideWorks.WebApi.Features.Polls.Data;
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

    public DbSet<Poll> Polls => Set<Poll>();

    public DbSet<PollRound> PollRounds => Set<PollRound>();

    public DbSet<PollAnswer> PollAnswers => Set<PollAnswer>();

    public DbSet<PollOption> PollOptions => Set<PollOption>();

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
            // At most one open session per deck, so a double-clicked Go live cannot open two.
            session.HasIndex(s => s.DeckId).IsUnique().HasFilter("\"EndedAt\" IS NULL").HasDatabaseName("IX_AudienceSessions_DeckId_Open");
        });

        modelBuilder.Entity<AudienceJoin>(join =>
        {
            join.HasKey(j => new { j.SessionId, j.GuestId });
            join.HasOne<AudienceSession>().WithMany().HasForeignKey(j => j.SessionId);
        });

        ConfigurePolls(modelBuilder);

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

    // SW-6's poll tables (V1, V2), made general by SW-9 (Q2): a poll is a slide's poll or the deck's one Q&A poll, and a
    // session has at most one unclosed round of each kind. Kind is stored by name ('Slide', 'Qa'); a check constraint ties
    // it to SlideId on both the poll and the round. Nothing cascades from a deck, a slide or a session to a poll, a round,
    // an option or an answer: slides are soft-deleted and keep their poll, rounds and answers, and app code removes none
    // of them.
    private static void ConfigurePolls(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Poll>(poll =>
        {
            poll.HasKey(p => p.Id);
            poll.HasOne<Slide>().WithOne().HasForeignKey<Poll>(p => p.SlideId).OnDelete(DeleteBehavior.Restrict);
            poll.HasIndex(p => p.SlideId).IsUnique().HasFilter("\"SlideId\" IS NOT NULL");
            poll.HasOne<Deck>().WithMany().HasForeignKey(p => p.DeckId).OnDelete(DeleteBehavior.Restrict);
            poll.HasIndex(p => p.DeckId, "IX_Polls_DeckId");
            // At most one Q&A poll per deck.
            poll.HasIndex(p => p.DeckId).IsUnique().HasFilter("\"Kind\" = 'Qa'").HasDatabaseName("IX_Polls_DeckId_Qa");
            poll.Property(p => p.Kind).HasConversion<string>();
            poll.Property(p => p.Pick).HasConversion<string>();
            poll.Property(p => p.Results).HasConversion<string>();
            poll.Property(p => p.Opens).HasConversion<string>();
            poll.Property(p => p.Privacy).HasConversion<string>();
            poll.ToTable(table => table.HasCheckConstraint(
                "CK_Polls_Kind_SlideId", "(\"Kind\" = 'Slide' AND \"SlideId\" IS NOT NULL) OR (\"Kind\" = 'Qa' AND \"SlideId\" IS NULL)"));
        });

        modelBuilder.Entity<PollRound>(round =>
        {
            round.HasOne<Poll>().WithMany().HasForeignKey(r => r.PollId).OnDelete(DeleteBehavior.Restrict);
            round.HasOne<Slide>().WithMany().HasForeignKey(r => r.SlideId).OnDelete(DeleteBehavior.Restrict);
            round.HasOne<AudienceSession>().WithMany().HasForeignKey(r => r.SessionId).OnDelete(DeleteBehavior.Restrict);
            round.HasIndex(r => new { r.PollId, r.SessionId, r.Number }).IsUnique();
            // At most one round per session and kind without ClosedAt (draft or open, D12; SW-9 Q2), so two opens can never
            // both land, and a slide round and a Q&A round can be open side by side.
            round.HasIndex(r => new { r.SessionId, r.Kind }).IsUnique().HasFilter("\"ClosedAt\" IS NULL").HasDatabaseName("IX_PollRounds_SessionId_Kind_Open");
            round.Property(r => r.Kind).HasConversion<string>();
            round.Property(r => r.Final).HasConversion(
                final => final == null ? null : JsonSerializer.Serialize(final, JsonSerializerOptions.Web),
                json => json == null ? null : JsonSerializer.Deserialize<PollFinal>(json, JsonSerializerOptions.Web));
            round.ToTable(table => table.HasCheckConstraint(
                "CK_PollRounds_Kind_SlideId", "(\"Kind\" = 'Slide' AND \"SlideId\" IS NOT NULL) OR (\"Kind\" = 'Qa' AND \"SlideId\" IS NULL)"));
        });

        modelBuilder.Entity<PollOption>(option =>
        {
            option.HasKey(o => new { o.RoundId, o.Position });
            option.HasOne<PollRound>().WithMany().HasForeignKey(o => o.RoundId).OnDelete(DeleteBehavior.Restrict);
            option.Property(o => o.Text).HasMaxLength(PollOption.TEXT_MAX);
            // SQLite ignores a column's length, so the limit is a check.
            option.ToTable(table => table.HasCheckConstraint("CK_PollOptions_Text_Length", $"length(\"Text\") <= {PollOption.TEXT_MAX}"));
        });

        modelBuilder.Entity<PollAnswer>(answer =>
        {
            answer.HasKey(a => new { a.RoundId, a.GuestId });
            answer.HasOne<PollRound>().WithMany().HasForeignKey(a => a.RoundId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
