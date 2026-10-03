using System.Globalization;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

public sealed class ScoreCounterHandler(UserSessionService sessions, MainService main, ScoreCounterService scores)
{
    public const string SessionAction = "ScoreCounterFlow";
    private static InlineKeyboardButton Button(string text, string action) => InlineKeyboardButton.WithCallbackData(text, $"SC|{action}");
    private static InlineKeyboardMarkup Keyboard(params InlineKeyboardButton[][] rows) => new(rows);
    private Task<int> Send(long chatId, string text, InlineKeyboardMarkup? keyboard = null) => main.SendMessage(chatId, text, keyboard, parseMode: ParseMode.None);
    private static Guid Id(string? value) => Guid.TryParse(value, out var id) ? id : throw new ScoreCounterException("Invalid selection.");
    private static int Page(string? value) => int.TryParse(value, out var page) && page is >= 0 and <= 100000 ? page : 0;
    private static string Name(User user)
    {
        var name = user.Name ?? user.Username ?? "Member";
        return name.Length > 80 ? name[..80] : name;
    }

    public async Task HandleSection(UpdateData data)
    {
        sessions.ClearSession(data.ChatId);
        await Send(data.ChatId, "Score Counter\nCreate a group, invite people, request or give points, and view scores and history.\n/demand 10 requests 10 points from each other member.",
            Keyboard([Button("Create group", "create")], [Button("My groups", "groups|0")], [Button("Join group", "join")]));
    }

    public async Task HandleCallBack(UpdateData data)
    {
        try
        {
            var p = data.DataSeparated;
            var action = p.ElementAtOrDefault(1);
            var value = p.ElementAtOrDefault(2);
            switch (action)
            {
                case "home": await HandleSection(data); break;
                case "create":
                case "join":
                    sessions.SetData(data.ChatId, SessionAction, action);
                    await Send(data.ChatId, action == "create" ? "Enter the group name. This is also its currency name (for example Stars)." : "Enter the invitation code.");
                    break;
                case "groups": await Groups(data.ChatId, "group", Page(value)); break;
                case "dpage":
                    var paging = sessions.GetOrSetData(data.ChatId);
                    if (paging.Action != SessionAction || paging.CallbackData != "demand")
                        throw new ScoreCounterException("Send /demand again to select a group.");
                    await Groups(data.ChatId, "demand", Page(value));
                    break;
                case "group": await Menu(data.ChatId, Id(value)); break;
                case "invite":
                    var group = await scores.Group(data.ChatId, Id(value));
                    var link = $"{main.Url}?start=SC_join_{group.InvitationCode:N}";
                    await Send(data.ChatId, $"Invite people to {group.Name}\nInvitation code: {group.InvitationCode}",
                        Keyboard([InlineKeyboardButton.WithUrl("Join score group", link)]));
                    break;
                case "ask": await People(data.ChatId, Id(value), false, Page(p.ElementAtOrDefault(3))); break;
                case "give": await People(data.ChatId, Id(value), true, Page(p.ElementAtOrDefault(3))); break;
                case "person":
                    var session = sessions.GetOrSetData(data.ChatId);
                    if (session.Action != SessionAction || session.CallbackData != "person" ||
                        !session.Context.TryGetValue("token", out var token) || (string)token != p.ElementAtOrDefault(3))
                        throw new ScoreCounterException("This selection has expired. Choose Ask for points or Give away points again.");
                    var person = value == "bank" ? (Guid?)null : Id(value);
                    session.SetContext("person", person ?? Guid.Empty);
                    session.SetContext("kind", person == null ? ScoreRequestKind.Bank : (ScoreRequestKind)session.Context["kind"]);
                    session.SetCallBack("amount");
                    var currency = await scores.Group(data.ChatId, (Guid)session.Context["group"]);
                    await Send(data.ChatId, $"How many {currency.Name}? Enter a positive integer.");
                    break;
                case "demand":
                    var demandSession = sessions.GetOrSetData(data.ChatId);
                    if (demandSession.Action != SessionAction || demandSession.CallbackData != "demand" ||
                        !demandSession.Context.TryGetValue("token", out var demandToken) || (string)demandToken != p.ElementAtOrDefault(3))
                        throw new ScoreCounterException("This demand selection has expired. Send /demand again.");
                    await CreateRequests(data.ChatId, Id(value), ScoreRequestKind.Ask, (int)demandSession.Context["amount"], demand: true);
                    break;
                case "yes":
                case "no":
                    var result = await scores.Respond(data.ChatId, Id(value), action == "yes");
                    await Send(data.ChatId, result.Changed ? $"Response recorded. Request: {result.Request.Status}." : "You already responded, or this request has been resolved.");
                    if (result.Changed)
                    {
                        await main.EditMessage(data.ChatId, data.MessageId, $"Your response: {(action == "yes" ? "Accepted" : "Rejected")}\nRequest: {result.Request.Status}", Keyboard(), ParseMode.None);
                        if (result.Request.Status != ScoreRequestStatus.Pending)
                        {
                            var members = await scores.Members(data.ChatId, result.Request.GroupId);
                            foreach (var user in members.Where(u => u.Id == result.Request.InitiatorId || u.Id == result.Request.FromUserId || u.Id == result.Request.ToUserId))
                                if (user.ChatId != data.ChatId) await Send(user.ChatId, $"Request for {result.Request.Amount} {(await scores.Group(data.ChatId, result.Request.GroupId)).Name}: {result.Request.Status}.");
                        }
                    }
                    break;
                case "status":
                    var statusGroup = await scores.Group(data.ChatId, Id(value));
                    var status = await scores.Status(data.ChatId, statusGroup.Id);
                    // Chunk large groups to stay within Telegram's message limit.
                    foreach (var chunk in status.Chunk(8))
                        await Send(data.ChatId, $"{statusGroup.Name} — current scores\n" + string.Join("\n", chunk.Select(x => $"{Name(x.User)}: {x.Points} {statusGroup.Name}")));
                    break;
                case "history": await History(data.ChatId, Id(value), Page(p.ElementAtOrDefault(3))); break;
                default: throw new ScoreCounterException("Invalid Score Counter action.");
            }
        }
        catch (ScoreCounterException ex) { await Send(data.ChatId, ex.Message); }
    }

    public async Task Join(UpdateData data, string code)
    {
        try
        {
            var group = await scores.Join(data.ChatId, Id(code));
            sessions.ClearSession(data.ChatId);
            await Menu(data.ChatId, group.Id);
        }
        catch (ScoreCounterException ex) { await Send(data.ChatId, ex.Message); }
    }

    public async Task HandleSession(UpdateData data)
    {
        try
        {
            var session = sessions.GetOrSetData(data.ChatId);
            switch (session.CallbackData)
            {
                case "create":
                    var group = await scores.Create(data.ChatId, data.MessageText ?? "");
                    sessions.ClearSession(data.ChatId);
                    await Menu(data.ChatId, group.Id);
                    break;
                case "join": await Join(data, data.MessageText ?? ""); break;
                case "amount":
                    if (!TryAmount(data.MessageText, out var amount)) throw new ScoreCounterException("Enter a positive integer, for example 10.");
                    var person = (Guid)session.Context["person"];
                    await CreateRequests(data.ChatId, (Guid)session.Context["group"], (ScoreRequestKind)session.Context["kind"], amount, person == Guid.Empty ? null : person);
                    break;
                default: await Send(data.ChatId, "Choose a person or group using the buttons, or send /cancel."); break;
            }
        }
        catch (ScoreCounterException ex) { await Send(data.ChatId, ex.Message); }
    }

    public static bool TryAmount(string? text, out int amount) => int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out amount) && amount > 0;

    public async Task Demand(UpdateData data)
    {
        var parts = (data.MessageText ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !TryAmount(parts[1], out var amount))
        { await Send(data.ChatId, "Usage: /demand 10 — enter a positive integer."); return; }
        try
        {
            sessions.ClearSession(data.ChatId);
            var groups = await scores.Groups(data.ChatId);
            if (groups.Count == 0) { await Send(data.ChatId, "Create or join a Score Counter group first."); return; }
            if (groups.Count == 1) { await CreateRequests(data.ChatId, groups[0].Id, ScoreRequestKind.Ask, amount, demand: true); return; }
            var session = sessions.SetData(data.ChatId, SessionAction, "demand");
            session.SetContext("amount", amount);
            session.SetContext("token", Guid.NewGuid().ToString("N")[..8]);
            await Groups(data.ChatId, "demand", 0);
        }
        catch (ScoreCounterException ex) { await Send(data.ChatId, ex.Message); }
    }

    private async Task Groups(long chatId, string action, int page)
    {
        var groups = await scores.Groups(chatId);
        if (groups.Count == 0) { await Send(chatId, "You have no score groups. Create or join one first."); return; }
        var suffix = action == "demand" ? $"|{sessions.GetData(chatId).Context["token"]}" : "";
        var rows = groups.Skip(page * 8).Take(8).Select(g => new[] { Button(g.Name, $"{action}|{g.Id:N}{suffix}") }).ToList();
        if (page > 0) rows.Add([Button("Previous", $"{(action == "demand" ? "dpage" : "groups")}|{page - 1}")]);
        if ((page + 1) * 8 < groups.Count) rows.Add([Button("Next", $"{(action == "demand" ? "dpage" : "groups")}|{page + 1}")]);
        await Send(chatId, "Select a score group.", new InlineKeyboardMarkup(rows));
    }

    private async Task Menu(long chatId, Guid groupId)
    {
        var group = await scores.Group(chatId, groupId);
        sessions.ClearSession(chatId);
        await Send(chatId, group.Name, Keyboard([Button("Invite people", $"invite|{groupId:N}")],
            [Button("Ask for points", $"ask|{groupId:N}|0")], [Button("Give away points", $"give|{groupId:N}|0")],
            [Button("Current status", $"status|{groupId:N}")], [Button("History / pending requests", $"history|{groupId:N}|0")], [Button("Score Counter", "home")]));
    }

    private async Task People(long chatId, Guid groupId, bool give, int page)
    {
        var members = (await scores.Members(chatId, groupId)).Where(u => u.ChatId != chatId).ToList();
        var session = sessions.SetData(chatId, SessionAction, "person");
        session.SetContext("group", groupId);
        session.SetContext("kind", give ? ScoreRequestKind.Give : ScoreRequestKind.Ask);
        var token = Guid.NewGuid().ToString("N")[..8];
        session.SetContext("token", token);
        var rows = members.Skip(page * 8).Take(8).Select(u => new[] { Button(Name(u), $"person|{u.Id:N}|{token}") }).ToList();
        if (!give) rows.Add([Button("Bank (everyone must approve)", $"person|bank|{token}")]);
        if (page > 0) rows.Add([Button("Previous", $"{(give ? "give" : "ask")}|{groupId:N}|{page - 1}")]);
        if ((page + 1) * 8 < members.Count) rows.Add([Button("Next", $"{(give ? "give" : "ask")}|{groupId:N}|{page + 1}")]);
        await Send(chatId, "Select a person.", new InlineKeyboardMarkup(rows));
    }

    private async Task CreateRequests(long chatId, Guid groupId, ScoreRequestKind kind, int amount, Guid? person = null, bool demand = false)
    {
        var requests = await scores.CreateRequests(chatId, groupId, kind, amount, person, demand);
        sessions.ClearSession(chatId);
        var group = await scores.Group(chatId, groupId);
        await Send(chatId, $"Created {requests.Count} request(s) for {amount} {group.Name} each. Scores change only after acceptance.");
        var members = await scores.Members(chatId, groupId);
        var initiator = members.Single(u => u.ChatId == chatId);
        var failed = 0;
        foreach (var request in requests)
            foreach (var approval in await scores.Approvals(chatId, request.Id))
            {
                try
                {
                    await Send(members.Single(u => u.Id == approval.UserId).ChatId,
                        $"{group.Name}\n{Name(initiator)} {(kind == ScoreRequestKind.Give ? "wants to give you" : kind == ScoreRequestKind.Bank ? "requests from the bank" : "requests from you")} {amount} {group.Name}." +
                        (kind == ScoreRequestKind.Bank ? "\nAll other members must approve. Any rejection cancels the request." : ""),
                        ApprovalKeyboard(request.Id));
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException) { failed++; }
            }
        if (failed > 0) await Send(chatId, $"Could not deliver {failed} notification(s). The requests are saved and available in group history.");
    }

    private static InlineKeyboardMarkup ApprovalKeyboard(Guid id) => Keyboard([Button("Accept", $"yes|{id:N}"), Button("Reject", $"no|{id:N}")]);

    private async Task History(long chatId, Guid groupId, int page)
    {
        var requests = await scores.History(chatId, groupId, page);
        var group = await scores.Group(chatId, groupId);
        var members = await scores.Members(chatId, groupId);
        string MemberName(Guid? id) => id == null ? "Bank" : members.FirstOrDefault(u => u.Id == id) is { } member ? Name(member) : "Member";
        if (requests.Count == 0) await Send(chatId, "No requests on this page.");
        foreach (var request in requests)
        {
            var approvals = await scores.Approvals(chatId, request.Id);
            var canRespond = request.Status == ScoreRequestStatus.Pending && approvals.Any(a => a.Accepted == null && members.Any(u => u.Id == a.UserId && u.ChatId == chatId));
            await Send(chatId, $"{main.GetIranDateTime(request.CreatedAtUtc):yyyy-MM-dd HH:mm}\n{request.Kind}: {MemberName(request.FromUserId)} → {MemberName(request.ToUserId)}: {request.Amount} {group.Name}\n{request.Status}\nApprovals: {approvals.Count(a => a.Accepted == true)}/{approvals.Count}",
                canRespond ? ApprovalKeyboard(request.Id) : null);
        }
        var rows = new List<InlineKeyboardButton[]>();
        if (page > 0) rows.Add([Button("Previous", $"history|{groupId:N}|{page - 1}")]);
        if (requests.Count == 8) rows.Add([Button("Next", $"history|{groupId:N}|{page + 1}")]);
        rows.Add([Button("Group menu", $"group|{groupId:N}")]);
        await Send(chatId, $"History page {page + 1}", new InlineKeyboardMarkup(rows));
    }
}
