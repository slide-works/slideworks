using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts.Avatars;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Sharing.Email;
using SlideWorks.WebApi.Features.Sharing.Limits;
using SlideWorks.WebApi.Features.Sharing.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Sharing;

// Who can open a deck (K4 sharing.json, D6): GET and PUT /api/decks/{id}/sharing for the owner in the browser, and the
// set_sharing tool for the owner's agent, under the same rules. Anyone but the owner gets 404 "Deck not found". A change
// runs in one transaction that first stamps Deck.UpdatedAt from TimeProvider (which also takes SQLite's write lock),
// replaces the viewer list, records exactly one activity entry ("changed sharing: Private → Anyone with the link") and
// then publishes one DeckChanged. A request that changes nothing records nothing.
//
// The viewer list is kept whatever the level, so switching from named people to the link and back keeps the names; it
// grants access only while the level is Named (DeckAccess).
[RegisterPerRequest]
public class SharingService(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IActivityLog activity,
    IDeckEvents events,
    IPublicUrls urls,
    TimeProvider clock,
    IDisplayDates dates,
    IAvatars avatars,
    IShareGuard guard,
    IShareRecipients recipients,
    IShareNotifier notifier)
{
    public const string TOOL = "set_sharing";

    public const string LEVEL_INVALID = "level: must be \"private\", \"link\" or \"named\"";

    public const string VIEWERS_INVALID = "viewers: must be a list of email addresses";

    public const string PEOPLE_INVALID = "people: must be a list of { email, access } with access \"read\" or \"write\"";

    public const string PERSON_REQUIRED = "person: required with access";

    public const string ACCESS_INVALID = "access: must be \"read\", \"write\" or \"remove\"";

    private static readonly string[] ACCESS_NAMES = ["read", "write", "remove"];

    // The summary names at most this many added or removed emails, each cut to EMAIL_SHOWN characters, and counts the
    // rest, so it stays under about 230 characters however long the list (the library repeats it as lastChange.summary).
    private const int CHANGES_SHOWN = 3;

    private const int EMAIL_SHOWN = 40;

    // The wire names (camelCase, as every enum goes on the wire), matched case-sensitively.
    private static readonly IReadOnlyDictionary<string, DeckSharing> LEVELS = new Dictionary<string, DeckSharing>(StringComparer.Ordinal)
    {
        ["private"] = DeckSharing.Private,
        ["link"] = DeckSharing.Link,
        ["named"] = DeckSharing.Named,
    };

    public async Task<SharingView> GetAsync(string deckId)
    {
        await RequireOwnerAsync(deckId);
        return await ViewAsync(deckId);
    }

    // The dialog's PUT: the body carries the level and the whole list, as people with access or as plain viewers (read).
    public Task<SharingView> UpdateAsync(string deckId, SharingUpdate update)
    {
        return SetAsync(deckId, new SharingChange(update.Level, null, update.Viewers?.Select(v => (string?)v).ToList(), update.People, null, ListRequired: true), tool: null);
    }

    // set_sharing: the level by its wire name; leaving it out keeps the level, and leaving viewers out (or null) keeps the
    // list as it is. McpArgumentCheck has already refused a viewers that isn't a list of strings. A list longer than
    // ViewerEmails.MAX_ENTRIES is refused before any entry is read, and a null entry goes on to ViewerEmails, which names
    // it as not an email, the same as in the dialog's PUT.
    public Task<SharingView> SetFromAgentAsync(string deckId, string? level, IReadOnlyList<string?>? viewers)
        => SetAsync(deckId, new SharingChange(null, level, viewers, null, null, ListRequired: false), TOOL);

    // set_sharing with person: one full email and "read", "write" or "remove" (D20). The rest of the list and, when level is
    // left out, the level stay as they are. It runs the same path as the PUT, so the guard, the share log and the notifier see it.
    public Task<SharingView> SetPersonFromAgentAsync(string deckId, string? level, string? person, string? access)
    {
        if (person == null)
        {
            throw new BadRequestException(PERSON_REQUIRED);
        }
        if (access == null || !ACCESS_NAMES.Contains(access))
        {
            throw new BadRequestException(ACCESS_INVALID);
        }
        return SetAsync(deckId, new SharingChange(null, level, null, null, new PersonChange(person, access), ListRequired: false), TOOL);
    }

    // The whole change in C6's order, inside one write transaction (EF begins SQLite's BEGIN IMMEDIATE, so the lock is held
    // before anything is read): owner check, normalise, added = emails not already on the deck, guard (throws before any
    // write), write viewers and access, record the new recipients, activity entry, commit; then the notifier and DeckChanged.
    private async Task<SharingView> SetAsync(string deckId, SharingChange change, string? tool)
    {
        IReadOnlyList<string> added = [];
        var changed = false;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await RequireOwnerAsync(deckId);
            if (change.ListRequired && change.People == null && change.Emails == null)
            {
                throw new BadRequestException(VIEWERS_INVALID);
            }
            var level = ParseLevel(change);
            var replaces = change.People != null || change.Emails != null || change.Person != null;
            await db.Decks.Where(d => d.Id == deckId).ExecuteUpdateAsync(set => set.SetProperty(d => d.UpdatedAt, clock.GetUtcNow()));
            var from = await db.Decks.Where(d => d.Id == deckId).Select(d => d.Sharing).SingleAsync();
            var rows = await db.DeckViewers.Where(v => v.DeckId == deckId).ToListAsync();
            var current = rows.Select(v => v.Email).ToList();
            var wanted = change.Person == null ? Normalize(change) : ApplyPerson(rows, change.Person);
            var wantedAccess = wanted.ToDictionary(w => w.Email, w => w.Access, StringComparer.Ordinal);
            var next = replaces ? wanted.Select(w => w.Email).ToList() : current;
            added = next.Except(current, StringComparer.Ordinal).ToList();
            var to = level ?? from;
            var summary = Summary(from, to, rows, replaces ? wanted : null);
            if (summary != null)
            {
                if (added.Count > 0)
                {
                    await guard.AdmitAsync(actor.PresenterId!.Value, added);
                }
                await db.Decks.Where(d => d.Id == deckId).ExecuteUpdateAsync(set => set.SetProperty(d => d.Sharing, to));
                if (replaces)
                {
                    db.DeckViewers.RemoveRange(rows.Where(row => !next.Contains(row.Email)));
                    foreach (var row in rows)
                    {
                        row.Access = wantedAccess.GetValueOrDefault(row.Email, row.Access);
                    }
                    db.DeckViewers.AddRange(wanted.Where(w => added.Contains(w.Email)).Select(w => new DeckViewer { DeckId = deckId, Email = w.Email, Access = w.Access }));
                }
                await db.SaveChangesAsync();
                await recipients.RecordAsync(actor.PresenterId!.Value, deckId, added);
                await activity.RecordAsync(deckId, ActivityEntry.By(actor, tool, summary));
                await transaction.CommitAsync();
                changed = true;
            }
        }

        if (changed)
        {
            if (added.Count > 0)
            {
                await notifier.QueueAsync(new ShareNotice(deckId, actor.PresenterId!.Value, added));
            }
            await events.PublishAsync(new DeckChanged(deckId));
        }
        return await ViewAsync(deckId);
    }

    private static DeckSharing? ParseLevel(SharingChange change)
    {
        if (change.LevelName == null)
        {
            return change.Level;
        }
        return LEVELS.TryGetValue(change.LevelName, out var known) ? known : throw new BadRequestException(LEVEL_INVALID);
    }

    // The new list as stored (each email normalised and checked, duplicates and the owner's own email dropped, the first
    // access given for an email kept); empty when the change leaves the list as it is.
    private IReadOnlyList<(string Email, DeckViewerAccess Access)> Normalize(SharingChange change)
    {
        if (change.People != null)
        {
            var emails = ViewerEmails.NormalizeAll(change.People.Select(p => (string?)p.Email).ToList(), actor.Email, "people");
            var accessGiven = new Dictionary<string, DeckViewerAccess>(StringComparer.Ordinal);
            foreach (var person in change.People)
            {
                accessGiven.TryAdd(ViewerEmails.Normalize(person.Email), person.Access);
            }
            return emails.Select(email => (email, accessGiven[email])).ToList();
        }
        return change.Emails == null ? [] : ViewerEmails.NormalizeAll(change.Emails, actor.Email).Select(email => (email, DeckViewerAccess.Read)).ToList();
    }

    // The stored list with one person added, changed or taken off. The email is checked as in the PUT (full address, the owner's
    // own dropped), and the list may not pass the same cap.
    private IReadOnlyList<(string Email, DeckViewerAccess Access)> ApplyPerson(IReadOnlyList<DeckViewer> rows, PersonChange person)
    {
        var list = rows.Select(r => (r.Email, r.Access)).ToList();
        var email = ViewerEmails.NormalizeAll([person.Email], actor.Email, "person").SingleOrDefault();
        if (email == null)
        {
            return list;
        }
        list.RemoveAll(entry => entry.Email == email);
        if (person.Access != "remove")
        {
            if (list.Count >= ViewerEmails.MAX_VIEWERS)
            {
                throw new BadRequestException(ViewerEmails.PERSON_TOO_MANY);
            }
            list.Add((email, person.Access == "write" ? DeckViewerAccess.Write : DeckViewerAccess.Read));
        }
        return list;
    }

    private sealed record PersonChange(string Email, string Access);

    private sealed record SharingChange(
        DeckSharing? Level,
        string? LevelName,
        IReadOnlyList<string?>? Emails,
        IReadOnlyList<SharingPersonUpdate>? People,
        PersonChange? Person,
        bool ListRequired);

    // What the change log says, or null when nothing changed. after is null when the list is left as it is. Added people read
    // "+email" (with " write" for write access), a changed level of access "email read → write", removed people "-email".
    private static string? Summary(DeckSharing from, DeckSharing to, IReadOnlyList<DeckViewer> before, IReadOnlyList<(string Email, DeckViewerAccess Access)>? after)
    {
        var changes = new List<string>();
        if (after != null)
        {
            var was = before.ToDictionary(v => v.Email, v => v.Access, StringComparer.Ordinal);
            changes.AddRange(after.Where(a => !was.ContainsKey(a.Email)).Select(a => Shorten("+" + a.Email) + (a.Access == DeckViewerAccess.Write ? " write" : "")));
            changes.AddRange(after.Where(a => was.TryGetValue(a.Email, out var old) && old != a.Access).Select(a => $"{Shorten(a.Email)} {AccessName(was[a.Email])} → {AccessName(a.Access)}"));
            changes.AddRange(before.Where(v => after.All(a => a.Email != v.Email)).Select(v => Shorten("-" + v.Email)));
        }
        var viewers = ViewerChanges(changes);
        if (from != to)
        {
            return $"{SharingActivity.PREFIX}{Label(from)} → {Label(to)}{viewers}";
        }
        return viewers.Length == 0 ? null : $"{SharingActivity.PREFIX}{Label(to)}{viewers}";
    }

    private static string AccessName(DeckViewerAccess access) => access.ToString().ToLowerInvariant();

    // " (+alex@team.example, -mira@team.example and 12 more)", or "" when the list didn't change.
    private static string ViewerChanges(IReadOnlyList<string> changes)
    {
        if (changes.Count == 0)
        {
            return "";
        }
        var shown = string.Join(", ", changes.Take(CHANGES_SHOWN));
        var more = changes.Count > CHANGES_SHOWN ? $" and {changes.Count - CHANGES_SHOWN} more" : "";
        return $" ({shown}{more})";
    }

    private static string Shorten(string email) => email.Length <= EMAIL_SHOWN ? email : email[..(EMAIL_SHOWN - 1)] + "…";

    // The dialog's words for each level.
    private static string Label(DeckSharing level) => level switch
    {
        DeckSharing.Private => "Private",
        DeckSharing.Link => "Anyone with the link",
        DeckSharing.Named => "Named people",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    private async Task<SharingView> ViewAsync(string deckId)
    {
        var deck = await db.Decks.AsNoTracking().Where(d => d.Id == deckId).Select(d => new { d.Id, d.Slug, d.Sharing, d.OwnerId }).SingleAsync();
        var rows = await db.DeckViewers.AsNoTracking().Where(v => v.DeckId == deckId).OrderBy(v => v.Email).ToListAsync();
        var viewers = rows.Select(v => v.Email).ToList();
        var owner = await db.Users.AsNoTracking().SingleAsync(u => u.Id == deck.OwnerId);
        var accounts = (await db.Users.AsNoTracking().Where(u => viewers.Contains(u.Email.ToLower())).ToListAsync())
            .GroupBy(u => ViewerEmails.Normalize(u.Email)).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var people = rows.Select(v => accounts.TryGetValue(v.Email, out var account)
            ? new SharingPersonView(v.Email, account.Name, avatars.For(account), AccessName(v.Access))
            : new SharingPersonView(v.Email, null, null, AccessName(v.Access))).ToList();
        var last = await db.ActivityEntries.AsNoTracking()
            .Where(e => e.DeckId == deckId && e.Summary.StartsWith(SharingActivity.PREFIX))
            .OrderByDescending(e => e.At)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync();
        var lastChange = last == null ? null : SharingLastChange.From(last, dates.Ago(last.At));
        return new SharingView(deck.Sharing, viewers, urls.Deck(deck.Id, deck.Slug), lastChange, new SharingOwnerView(owner.Email, owner.Name, avatars.For(owner)), people);
    }

    private async Task RequireOwnerAsync(string deckId)
    {
        if (!await access.IsOwnerAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
    }
}
