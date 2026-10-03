using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public sealed class ScoreCounterService(AppDbContext db)
{
    private async Task<User> User(long chatId) => await db.Users.SingleOrDefaultAsync(u => u.ChatId == chatId && u.IsAccepted)
        ?? throw new ScoreCounterException("Registered user not found.");

    public async Task<ScoreGroup> Group(long chatId, Guid groupId)
    {
        var user = await User(chatId);
        return await db.ScoreGroups.SingleOrDefaultAsync(g => g.Id == groupId &&
            db.ScoreMembers.Any(m => m.GroupId == g.Id && m.UserId == user.Id))
            ?? throw new ScoreCounterException("Score group not found or you are not a member.");
    }

    public async Task<List<ScoreGroup>> Groups(long chatId)
    {
        var user = await User(chatId);
        return await db.ScoreGroups.Where(g => db.ScoreMembers.Any(m => m.GroupId == g.Id && m.UserId == user.Id))
            .OrderBy(g => g.Name).ThenBy(g => g.Id).ToListAsync();
    }

    public async Task<ScoreGroup> Create(long chatId, string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 200) throw new ScoreCounterException("Enter a group name between 1 and 200 characters.");
        var user = await User(chatId);
        var group = new ScoreGroup { Id = Guid.NewGuid(), Name = name, CreatorId = user.Id };
        db.ScoreGroups.Add(group);
        db.ScoreMembers.Add(new ScoreMember { Id = Guid.NewGuid(), GroupId = group.Id, UserId = user.Id });
        await Save();
        return group;
    }

    public async Task<ScoreGroup> Join(long chatId, Guid invitationCode)
    {
        var user = await User(chatId);
        var group = await db.ScoreGroups.SingleOrDefaultAsync(g => g.InvitationCode == invitationCode)
            ?? throw new ScoreCounterException("Invalid score group invitation.");
        if (!await db.ScoreMembers.AnyAsync(m => m.GroupId == group.Id && m.UserId == user.Id))
        {
            group.Revision = Guid.NewGuid();
            db.ScoreMembers.Add(new ScoreMember { Id = Guid.NewGuid(), GroupId = group.Id, UserId = user.Id });
            await Save();
        }
        return group;
    }

    public async Task<List<User>> Members(long chatId, Guid groupId)
    {
        await Group(chatId, groupId);
        return await db.Users.Where(u => db.ScoreMembers.Any(m => m.GroupId == groupId && m.UserId == u.Id))
            .OrderBy(u => u.Name).ThenBy(u => u.Id).ToListAsync();
    }

    public async Task<List<ScoreRequest>> CreateRequests(long chatId, Guid groupId, ScoreRequestKind kind, int amount,
        Guid? personId = null, bool demand = false)
    {
        if (amount <= 0) throw new ScoreCounterException("Points must be a positive integer.");
        var group = await Group(chatId, groupId);
        var user = await User(chatId);
        var others = (await Members(chatId, groupId)).Where(u => u.Id != user.Id).Select(u => u.Id).ToList();
        if (others.Count == 0) throw new ScoreCounterException("Invite another person to this group first.");
        if (!demand && kind != ScoreRequestKind.Bank && (personId == null || !others.Contains(personId.Value)))
            throw new ScoreCounterException("Select another member of this group.");
        if (demand && kind != ScoreRequestKind.Ask) throw new ScoreCounterException("Invalid demand.");
        if (kind == ScoreRequestKind.Give && await Balance(groupId, user.Id) < amount)
            throw new ScoreCounterException("You do not have enough points to give away that amount.");
        var targets = demand ? others : new List<Guid> { kind == ScoreRequestKind.Bank ? user.Id : personId!.Value };
        var requests = new List<ScoreRequest>();
        foreach (var target in targets)
        {
            var request = new ScoreRequest
            {
                Id = Guid.NewGuid(), GroupId = groupId, InitiatorId = user.Id, Amount = amount, Kind = kind,
                FromUserId = kind == ScoreRequestKind.Bank ? null : kind == ScoreRequestKind.Give ? user.Id : target,
                ToUserId = kind == ScoreRequestKind.Give ? target : user.Id, CreatedAtUtc = DateTime.UtcNow
            };
            requests.Add(request);
            db.ScoreRequests.Add(request);
            foreach (var voter in kind == ScoreRequestKind.Bank ? others : new List<Guid> { target })
                db.ScoreApprovals.Add(new ScoreApproval { Id = Guid.NewGuid(), RequestId = request.Id, UserId = voter });
        }
        group.Revision = Guid.NewGuid();
        await Save();
        return requests;
    }

    public async Task<(ScoreRequest Request, bool Changed)> Respond(long chatId, Guid requestId, bool accept)
    {
        var user = await User(chatId);
        var groupId = await db.ScoreRequests.Where(r => r.Id == requestId).Select(r => (Guid?)r.GroupId).SingleOrDefaultAsync()
            ?? throw new ScoreCounterException("Request not found.");
        // Read the concurrency guard before any request, vote, or balance state.
        var group = await Group(chatId, groupId);
        var request = await db.ScoreRequests.SingleAsync(r => r.Id == requestId);
        var approvals = await db.ScoreApprovals.Where(a => a.RequestId == requestId).ToListAsync();
        var vote = approvals.SingleOrDefault(a => a.UserId == user.Id)
            ?? throw new ScoreCounterException("Only the requested person can approve or reject this request.");
        if (request.Status != ScoreRequestStatus.Pending || vote.Accepted != null) return (request, false);
        if (accept && request.FromUserId is { } source && await Balance(request.GroupId, source) < request.Amount)
            throw new ScoreCounterException("The sender does not have enough points. This request is still pending; it can be rejected or accepted after their balance increases.");
        vote.Accepted = accept;
        if (!accept) request.Status = ScoreRequestStatus.Rejected;
        else if (approvals.All(a => a.Accepted == true)) request.Status = ScoreRequestStatus.Accepted;
        if (request.Status != ScoreRequestStatus.Pending) request.ResolvedAtUtc = DateTime.UtcNow;
        group.Revision = Guid.NewGuid();
        await Save();
        return (request, true);
    }

    public async Task<List<ScoreApproval>> Approvals(long chatId, Guid requestId)
    {
        var request = await db.ScoreRequests.SingleOrDefaultAsync(r => r.Id == requestId)
            ?? throw new ScoreCounterException("Request not found.");
        await Group(chatId, request.GroupId);
        return await db.ScoreApprovals.Where(a => a.RequestId == requestId).ToListAsync();
    }

    public async Task<List<ScoreRequest>> History(long chatId, Guid groupId, int page = 0)
    {
        await Group(chatId, groupId);
        return await db.ScoreRequests.Where(r => r.GroupId == groupId).OrderByDescending(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id).Skip(Math.Max(0, page) * 8).Take(8).ToListAsync();
    }

    public async Task<List<(User User, long Points)>> Status(long chatId, Guid groupId)
    {
        var members = await Members(chatId, groupId);
        var accepted = db.ScoreRequests.Where(r => r.GroupId == groupId && r.Status == ScoreRequestStatus.Accepted);
        var incoming = await accepted.GroupBy(r => r.ToUserId).Select(g => new { UserId = g.Key, Points = g.Sum(r => (long)r.Amount) }).ToListAsync();
        var outgoing = await accepted.Where(r => r.FromUserId != null).GroupBy(r => r.FromUserId!.Value)
            .Select(g => new { UserId = g.Key, Points = g.Sum(r => (long)r.Amount) }).ToListAsync();
        return members.Select(u => (u, incoming.FirstOrDefault(x => x.UserId == u.Id)?.Points ?? 0L))
            .Select(x => (x.u, x.Item2 - (outgoing.FirstOrDefault(o => o.UserId == x.u.Id)?.Points ?? 0L))).ToList();
    }

    private async Task Save()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new ScoreCounterException("Another group update happened at the same time. Please try again.");
        }
    }

    private async Task<long> Balance(Guid groupId, Guid userId)
    {
        var ledger = db.ScoreRequests.Where(r => r.GroupId == groupId && r.Status == ScoreRequestStatus.Accepted);
        return (await ledger.Where(r => r.ToUserId == userId).SumAsync(r => (long?)r.Amount) ?? 0L)
            - (await ledger.Where(r => r.FromUserId == userId).SumAsync(r => (long?)r.Amount) ?? 0L);
    }
}
