using System.Globalization;
using System.Net;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

/// <summary>Telegram interaction flow for collaborative place maps.</summary>
public sealed class MapifyHandler(UserSessionService sessionService, MainService services, MapifyService mapifyService)
{
    public async Task HandleSection(UpdateData data)
    {
        List<List<Tuple<string, string>>> collection =
        [
            [new(Messages.MapifyMyMaps, CallBacks.MapifyMyMaps), new(Messages.MapifyCreateMap, CallBacks.MapifyCreateMap)]
        ];
        await services.SendMessage(data.ChatId, Messages.MapifyWelcome,
            services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Mapify}|{CallBacks.MainSection}|"));
    }

    public async Task HandleCallBack(UpdateData data)
    {
        var action = data.DataSeparated.ElementAtOrDefault(1);
        var value = data.DataSeparated.ElementAtOrDefault(2);
        switch (action)
        {
            case CallBacks.MainSection when value == CallBacks.MapifyCreateMap:
                sessionService.SetData(data.ChatId, Actions.MapifyCreateMap, SessionCallBacks.MapifyAskMapName);
                await services.SendMessage(data.ChatId, Messages.MapifyAskMapName, new ForceReplyMarkup());
                break;
            case CallBacks.MainSection when value == CallBacks.MapifyMyMaps:
                await LoadMaps(data.ChatId);
                break;
            case CallBacks.MapifySelectMap when Guid.TryParse(value, out var selectedMapId):
                await ShowMapMenu(data, selectedMapId);
                break;
            case CallBacks.PreviousPage when value == CallBacks.MapifySelectMap && int.TryParse(data.DataSeparated.ElementAtOrDefault(3), out var previousPage):
                await LoadMaps(data.ChatId, previousPage - 1);
                break;
            case CallBacks.NextPage when value == CallBacks.MapifySelectMap && int.TryParse(data.DataSeparated.ElementAtOrDefault(3), out var nextPage):
                await LoadMaps(data.ChatId, nextPage + 1);
                break;
            case CallBacks.MapifyInvite when Guid.TryParse(value, out var inviteMapId):
                await InviteToMap(data.ChatId, inviteMapId);
                break;
            case CallBacks.MapifyManageCategories when Guid.TryParse(value, out var categoryMapId):
                await BeginCategoryManagement(data.ChatId, categoryMapId);
                break;
            case CallBacks.MapifyCategoryAction:
                await HandleCategoryAction(data, value);
                break;
            case CallBacks.MapifyAddLocation when Guid.TryParse(value, out var addLocationMapId):
                await BeginAddLocation(data.ChatId, addLocationMapId);
                break;
            case CallBacks.MapifyAddLocationCategories:
                await HandleLocationCategorySelection(data);
                break;
            case CallBacks.MapifyVisited:
                await HandleVisitedSelection(data, value);
                break;
            case CallBacks.MapifySuggest when Guid.TryParse(value, out var suggestionMapId):
                await BeginSuggestion(data.ChatId, suggestionMapId);
                break;
            case CallBacks.MapifySuggestCategories:
                await HandleSuggestionCategorySelection(data);
                break;
        }
    }

    public async Task HandleSession(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);
        if (data.MessageText == Messages.Cancel)
        {
            sessionService.ClearSession(data.ChatId);
            await services.SendMessage(data.ChatId, Messages.MapifyCancelled);
            return;
        }

        switch (session.Action)
        {
            case Actions.MapifyCreateMap:
                await CreateMap(data);
                break;
            case Actions.MapifyManagingCategories:
                await AddCategory(data);
                break;
            case Actions.MapifyAddingLocation:
                await HandleAddingLocation(data, session);
                break;
            case Actions.MapifySuggestingLocation:
                await HandleSuggestionLocation(data, session);
                break;
        }
    }

    public async Task JoinMapById(UpdateData data)
    {
        if (!Guid.TryParse(data.MessageText, out var mapId) || !await mapifyService.AcceptInvitationAsync(data.ChatId, mapId))
        {
            await services.SendMessage(data.ChatId, Messages.MapifyJoinFailed);
            return;
        }
        await services.SendMessage(data.ChatId, Messages.MapifyJoined);
    }

    private async Task LoadMaps(long chatId, int pageNumber = 0)
    {
        var maps = await mapifyService.GetMapsForUserAsync(chatId);
        if (maps.Count == 0)
        {
            await services.SendMessage(chatId, Messages.MapifyNoMaps);
            return;
        }
        var collection = services.LoadCollectionInPages(maps, CallBacks.MapifySelectMap, pageNumber, x => x.Id, x => x.Name, width: 2, height: 2);
        await services.SendMessage(chatId, Messages.MapifySelectMap,
            services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Mapify}|"));
    }

    private async Task ShowMapMenu(UpdateData data, Guid mapId)
    {
        var map = await mapifyService.GetMapForUserAsync(mapId, data.ChatId);
        if (map == null)
        {
            await services.SendMessage(data.ChatId, Messages.MapifyNotFound);
            return;
        }
        List<List<Tuple<string, string>>> collection =
        [
            [new(Messages.MapifyShare, $"{CallBacks.MapifyInvite}|{mapId}"), new(Messages.MapifyManageCategories, $"{CallBacks.MapifyManageCategories}|{mapId}")],
            [new(Messages.MapifyAddLocation, $"{CallBacks.MapifyAddLocation}|{mapId}"), new(Messages.MapifySuggest, $"{CallBacks.MapifySuggest}|{mapId}")]
        ];
        await services.SendMessage(data.ChatId, string.Format(Messages.MapifySelected, map.Name),
            services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Mapify}|"));
    }

    private async Task CreateMap(UpdateData data)
    {
        if (string.IsNullOrWhiteSpace(data.MessageText))
        {
            await services.SendMessage(data.ChatId, Messages.MapifyAskMapName, new ForceReplyMarkup());
            return;
        }
        var mapId = await mapifyService.CreateMapAsync(data.ChatId, data.MessageText);
        sessionService.ClearSession(data.ChatId);
        await services.SendMessage(data.ChatId, string.Format(Messages.MapifyCreated, data.MessageText.Trim(), mapId));
    }

    private async Task InviteToMap(long chatId, Guid mapId)
    {
        var map = await mapifyService.GetMapForUserAsync(mapId, chatId);
        if (map == null)
        {
            await services.SendMessage(chatId, Messages.MapifyNotFound);
            return;
        }
        var link = $"{services.Url}?start={CallBacks.Mapify}_{CallBacks.MapifyJoin}_{mapId}";
        await services.SendMessage(chatId, string.Format(Messages.MapifyInvite, map.Name),
            InlineKeyboardButton.WithUrl(Messages.MapifyJoinButton, link));
    }

    private async Task BeginCategoryManagement(long chatId, Guid mapId)
    {
        if (await mapifyService.GetMapForUserAsync(mapId, chatId) == null)
        {
            await services.SendMessage(chatId, Messages.MapifyNotFound);
            return;
        }
        var categories = await mapifyService.GetCategoriesAsync(mapId, chatId, includePending: true);
        var keyboard = CreateCategoryManagementKeyboard(categories);
        var messageId = await services.SendMessage(chatId, Messages.MapifyCategoryManagement, keyboard);
        sessionService.SetData(chatId, Actions.MapifyManagingCategories, $"{messageId}|{mapId}");
    }

    private ReplyMarkup CreateCategoryManagementKeyboard(List<MapifyCategory> categories)
    {
        var collection = services.LoadCollectionInScroller(categories, x => x.Id, x => x.Name, x => x.TempAdded, x => x.TempDeleted);
        return services.CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.Mapify}|{CallBacks.MapifyCategoryAction}|")!;
    }

    private async Task AddCategory(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);
        var parts = session.CallbackData.Split('|');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var messageId) || !Guid.TryParse(parts[1], out var mapId)) return;
        if (!await mapifyService.AddCategoryAsync(data.ChatId, mapId, data.MessageText ?? string.Empty))
        {
            await services.SendMessage(data.ChatId, Messages.MapifyCategoryAddFailed);
            return;
        }
        await RefreshCategoryManagement(data.ChatId, messageId, mapId);
    }

    private async Task HandleCategoryAction(UpdateData data, string? action)
    {
        var session = sessionService.GetData(data.ChatId);
        var parts = session.CallbackData.Split('|');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var messageId) || !Guid.TryParse(parts[1], out var mapId)) return;
        switch (action)
        {
            case CallBacks.Done:
                await mapifyService.SubmitCategoryChangesAsync(data.ChatId, mapId);
                sessionService.ClearSession(data.ChatId);
                await services.DeleteMessage(data.ChatId, messageId);
                await services.SendMessage(data.ChatId, Messages.MapifyCategoryChangesSaved);
                break;
            case CallBacks.Cancel:
                await mapifyService.CancelCategoryChangesAsync(data.ChatId, mapId);
                sessionService.ClearSession(data.ChatId);
                await services.DeleteMessage(data.ChatId, messageId);
                await services.SendMessage(data.ChatId, Messages.MapifyCancelled);
                break;
            default:
                if (Guid.TryParse(action, out var categoryId) && await mapifyService.ToggleCategoryDeletionAsync(data.ChatId, categoryId))
                    await RefreshCategoryManagement(data.ChatId, messageId, mapId);
                break;
        }
    }

    private async Task RefreshCategoryManagement(long chatId, int messageId, Guid mapId)
    {
        var categories = await mapifyService.GetCategoriesAsync(mapId, chatId, includePending: true);
        await services.EditMessage(chatId, messageId, replyMarkup: CreateCategoryManagementKeyboard(categories));
    }

    private async Task BeginAddLocation(long chatId, Guid mapId)
    {
        if (await mapifyService.GetMapForUserAsync(mapId, chatId) == null)
        {
            await services.SendMessage(chatId, Messages.MapifyNotFound);
            return;
        }
        sessionService.SetData(chatId, Actions.MapifyAddingLocation);
        var session = sessionService.GetData(chatId);
        session.SetContext(Context.MapifyMapId, mapId);
        session.SetContext(Context.MapifySelectedCategoryIds, new List<Guid>());
        session.SetContext(Context.MapifyAllCategoriesSelected, false);
        session.SetContext(Context.MapifySelectorMessageId, 0);
        await ShowLocationCategorySelection(chatId);
    }

    private async Task ShowLocationCategorySelection(long chatId)
    {
        var session = sessionService.GetData(chatId);
        var mapId = (Guid)session.Context[Context.MapifyMapId];
        var categories = await mapifyService.GetCategoriesAsync(mapId, chatId);
        if (categories.Count == 0)
        {
            sessionService.ClearSession(chatId);
            await services.SendMessage(chatId, Messages.MapifyNoCategories);
            return;
        }
        var selectedIds = (List<Guid>)session.Context[Context.MapifySelectedCategoryIds];
        var allSelected = (bool)session.Context[Context.MapifyAllCategoriesSelected];
        var keyboard = services.CreateKeyboard(
            inlineCollection: services.LoadCollectionMultiSelect(categories, selectedIds, allSelected, x => x.Id, x => x.Name),
            callBackStart: $"*{CallBacks.Mapify}|{CallBacks.MapifyAddLocationCategories}|");
        var messageId = (int)session.Context[Context.MapifySelectorMessageId];
        var message = string.Format(Messages.MapifySelectLocationCategories, selectedIds.Count, categories.Count);
        if (messageId == 0) session.SetContext(Context.MapifySelectorMessageId, await services.SendMessage(chatId, message, keyboard));
        else await services.EditMessage(chatId, messageId, message, keyboard);
    }

    private async Task HandleLocationCategorySelection(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);
        var action = data.DataSeparated.ElementAtOrDefault(2);
        var selectedIds = (List<Guid>)session.Context[Context.MapifySelectedCategoryIds];
        switch (action)
        {
            case CallBacks.MultipleSelectToggle when Guid.TryParse(data.DataSeparated.ElementAtOrDefault(3), out var categoryId):
                if (!selectedIds.Remove(categoryId)) selectedIds.Add(categoryId);
                session.SetContext(Context.MapifySelectedCategoryIds, selectedIds);
                await ShowLocationCategorySelection(data.ChatId);
                break;
            case CallBacks.MultipleSelectAll:
                var mapId = (Guid)session.Context[Context.MapifyMapId];
                session.SetContext(Context.MapifySelectedCategoryIds, (await mapifyService.GetCategoriesAsync(mapId, data.ChatId)).Select(x => x.Id).ToList());
                session.SetContext(Context.MapifyAllCategoriesSelected, true);
                await ShowLocationCategorySelection(data.ChatId);
                break;
            case CallBacks.MultipleDeselectAll:
                selectedIds.Clear();
                session.SetContext(Context.MapifySelectedCategoryIds, selectedIds);
                session.SetContext(Context.MapifyAllCategoriesSelected, false);
                await ShowLocationCategorySelection(data.ChatId);
                break;
            case CallBacks.Done when selectedIds.Count > 0:
                await services.DeleteMessage(data.ChatId, (int)session.Context[Context.MapifySelectorMessageId]);
                session.SetCallBack(SessionCallBacks.MapifyAskLocationName);
                await services.SendMessage(data.ChatId, Messages.MapifyAskLocationName, new ForceReplyMarkup());
                break;
            case CallBacks.Done:
                await services.SendMessage(data.ChatId, Messages.MapifyChooseAtLeastOneCategory);
                break;
            case CallBacks.Cancel:
                await services.DeleteMessage(data.ChatId, (int)session.Context[Context.MapifySelectorMessageId]);
                sessionService.ClearSession(data.ChatId);
                break;
        }
    }

    private async Task HandleAddingLocation(UpdateData data, UserSession session)
    {
        switch (session.CallbackData)
        {
            case SessionCallBacks.MapifyAskLocationName:
                if (string.IsNullOrWhiteSpace(data.MessageText)) { await services.SendMessage(data.ChatId, Messages.MapifyAskLocationName); return; }
                session.SetContext(Context.MapifyLocationName, data.MessageText.Trim());
                session.SetCallBack(SessionCallBacks.MapifyAskLocationPin);
                await AskForLocation(data.ChatId, Messages.MapifyAskLocationPin);
                break;
            case SessionCallBacks.MapifyAskLocationPin:
                if (data.Latitude == null || data.Longitude == null) { await AskForLocation(data.ChatId, Messages.MapifyAskLocationPin); return; }
                session.SetContext(Context.MapifyLatitude, data.Latitude.Value);
                session.SetContext(Context.MapifyLongitude, data.Longitude.Value);
                session.SetCallBack(SessionCallBacks.MapifyAskLocationDescription);
                await services.SendMessage(data.ChatId, Messages.MapifyAskLocationDescription, new ForceReplyMarkup());
                break;
            case SessionCallBacks.MapifyAskLocationDescription:
                if (data.MessageText != Messages.Skip && !string.IsNullOrWhiteSpace(data.MessageText))
                    session.SetContext(Context.MapifyDescription, data.MessageText.Trim());
                session.SetCallBack(SessionCallBacks.MapifyAskVisited);
                await AskVisited(data.ChatId);
                break;
            case SessionCallBacks.MapifyAskScore:
                if (!TryParseScore(data.MessageText, out var score)) { await services.SendMessage(data.ChatId, Messages.MapifyInvalidScore); return; }
                session.SetContext(Context.MapifyScore, score);
                await SaveLocation(data.ChatId, session);
                break;
        }
    }

    private async Task AskVisited(long chatId)
    {
        List<List<Tuple<string, string>>> collection = [[new(Messages.Yes, CallBacks.Yes), new(Messages.No, CallBacks.No)]];
        await services.SendMessage(chatId, Messages.MapifyAskVisited,
            services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Mapify}|{CallBacks.MapifyVisited}|"));
    }

    private async Task HandleVisitedSelection(UpdateData data, string? value)
    {
        var session = sessionService.GetData(data.ChatId);
        if (session.Action != Actions.MapifyAddingLocation) return;
        if (value == CallBacks.No)
        {
            session.SetContext(Context.MapifyVisited, false);
            await SaveLocation(data.ChatId, session);
            return;
        }
        if (value == CallBacks.Yes)
        {
            session.SetContext(Context.MapifyVisited, true);
            session.SetCallBack(SessionCallBacks.MapifyAskScore);
            await services.SendMessage(data.ChatId, Messages.MapifyAskScore, new ForceReplyMarkup());
        }
    }

    private async Task SaveLocation(long chatId, UserSession session)
    {
        var saved = await mapifyService.AddLocationAsync(chatId, new MapifyLocationDraft
        {
            MapId = (Guid)session.Context[Context.MapifyMapId],
            Name = (string)session.Context[Context.MapifyLocationName],
            Latitude = (double)session.Context[Context.MapifyLatitude],
            Longitude = (double)session.Context[Context.MapifyLongitude],
            Description = session.Context.TryGetValue(Context.MapifyDescription, out var descriptionValue) && descriptionValue is string description ? description : null,
            IsVisited = (bool)session.Context[Context.MapifyVisited],
            Score = session.Context.TryGetValue(Context.MapifyScore, out var scoreValue) && scoreValue is decimal score ? score : null,
            CategoryIds = (List<Guid>)session.Context[Context.MapifySelectedCategoryIds]
        });
        sessionService.ClearSession(chatId);
        await services.SendMessage(chatId, saved ? Messages.MapifyLocationSaved : Messages.MapifyLocationSaveFailed);
    }

    private async Task BeginSuggestion(long chatId, Guid mapId)
    {
        if (await mapifyService.GetMapForUserAsync(mapId, chatId) == null) { await services.SendMessage(chatId, Messages.MapifyNotFound); return; }
        sessionService.SetData(chatId, Actions.MapifySuggestingLocation);
        var session = sessionService.GetData(chatId);
        session.SetContext(Context.MapifyMapId, mapId);
        session.SetContext(Context.MapifySelectedCategoryIds, new List<Guid>());
        session.SetContext(Context.MapifyAllCategoriesSelected, false);
        session.SetContext(Context.MapifySelectorMessageId, 0);
        await ShowSuggestionCategorySelection(chatId);
    }

    private async Task ShowSuggestionCategorySelection(long chatId)
    {
        var session = sessionService.GetData(chatId);
        var mapId = (Guid)session.Context[Context.MapifyMapId];
        var categories = await mapifyService.GetCategoriesAsync(mapId, chatId);
        if (categories.Count == 0) { sessionService.ClearSession(chatId); await services.SendMessage(chatId, Messages.MapifyNoCategories); return; }
        var selectedIds = (List<Guid>)session.Context[Context.MapifySelectedCategoryIds];
        var keyboard = services.CreateKeyboard(
            inlineCollection: services.LoadCollectionMultiSelect(categories, selectedIds, (bool)session.Context[Context.MapifyAllCategoriesSelected], x => x.Id, x => x.Name),
            callBackStart: $"*{CallBacks.Mapify}|{CallBacks.MapifySuggestCategories}|");
        var messageId = (int)session.Context[Context.MapifySelectorMessageId];
        var message = string.Format(Messages.MapifySelectSuggestionCategories, selectedIds.Count, categories.Count);
        if (messageId == 0) session.SetContext(Context.MapifySelectorMessageId, await services.SendMessage(chatId, message, keyboard));
        else await services.EditMessage(chatId, messageId, message, keyboard);
    }

    private async Task HandleSuggestionCategorySelection(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);
        var action = data.DataSeparated.ElementAtOrDefault(2);
        var selectedIds = (List<Guid>)session.Context[Context.MapifySelectedCategoryIds];
        switch (action)
        {
            case CallBacks.MultipleSelectToggle when Guid.TryParse(data.DataSeparated.ElementAtOrDefault(3), out var categoryId):
                if (!selectedIds.Remove(categoryId)) selectedIds.Add(categoryId);
                session.SetContext(Context.MapifySelectedCategoryIds, selectedIds);
                await ShowSuggestionCategorySelection(data.ChatId);
                break;
            case CallBacks.MultipleSelectAll:
                var mapId = (Guid)session.Context[Context.MapifyMapId];
                session.SetContext(Context.MapifySelectedCategoryIds, (await mapifyService.GetCategoriesAsync(mapId, data.ChatId)).Select(x => x.Id).ToList());
                session.SetContext(Context.MapifyAllCategoriesSelected, true);
                await ShowSuggestionCategorySelection(data.ChatId);
                break;
            case CallBacks.MultipleDeselectAll:
                selectedIds.Clear(); session.SetContext(Context.MapifySelectedCategoryIds, selectedIds); session.SetContext(Context.MapifyAllCategoriesSelected, false);
                await ShowSuggestionCategorySelection(data.ChatId);
                break;
            case CallBacks.Done when selectedIds.Count > 0:
                await services.DeleteMessage(data.ChatId, (int)session.Context[Context.MapifySelectorMessageId]);
                session.SetCallBack(SessionCallBacks.MapifyAskSuggestionPin);
                await AskForLocation(data.ChatId, Messages.MapifyAskSuggestionPin);
                break;
            case CallBacks.Done:
                await services.SendMessage(data.ChatId, Messages.MapifyChooseAtLeastOneCategory);
                break;
            case CallBacks.Cancel:
                await services.DeleteMessage(data.ChatId, (int)session.Context[Context.MapifySelectorMessageId]); sessionService.ClearSession(data.ChatId);
                break;
        }
    }

    private async Task HandleSuggestionLocation(UpdateData data, UserSession session)
    {
        if (session.CallbackData != SessionCallBacks.MapifyAskSuggestionPin) return;
        if (data.Latitude == null || data.Longitude == null) { await AskForLocation(data.ChatId, Messages.MapifyAskSuggestionPin); return; }
        var results = await mapifyService.GetSuggestionLocationsAsync(data.ChatId, (Guid)session.Context[Context.MapifyMapId],
            (List<Guid>)session.Context[Context.MapifySelectedCategoryIds]);
        sessionService.ClearSession(data.ChatId);
        if (results.Count == 0) { await services.SendMessage(data.ChatId, Messages.MapifyNoSuggestions); return; }

        var ordered = results.Select(x => new { Suggestion = x, Distance = DistanceInKilometers(data.Latitude.Value, data.Longitude.Value, x.Location.Latitude, x.Location.Longitude) })
            .OrderBy(x => x.Distance).Take(5).ToList();
        var cards = ordered.Select((x, index) => FormatSuggestion(index + 1, x.Suggestion, x.Distance));
        await services.SendMessage(data.ChatId, string.Format(Messages.MapifySuggestions, string.Join("\n\n", cards)), parseMode: ParseMode.Html);
    }

    private async Task AskForLocation(long chatId, string message)
    {
        var keyboard = new ReplyKeyboardMarkup([[KeyboardButton.WithRequestLocation(Messages.MapifySendPin)], [new KeyboardButton(Messages.Cancel)]])
        {
            ResizeKeyboard = true, OneTimeKeyboard = true
        };
        await services.SendMessage(chatId, message, keyboard);
    }

    private static bool TryParseScore(string? text, out decimal score) =>
        (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out score) || decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out score)) && score is >= 0 and <= 10;

    private static double DistanceInKilometers(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double radius = 6371d;
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var a = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2) + Math.Cos(DegreesToRadians(latitude1)) * Math.Cos(DegreesToRadians(latitude2)) * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);
        return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private static string FormatSuggestion(int rank, MapifyLocationSuggestion suggestion, double distance)
    {
        var location = suggestion.Location;
        var mapUrl = FormattableString.Invariant($"https://www.google.com/maps/search/?api=1&query={location.Latitude},{location.Longitude}");
        var visited = location.IsVisited ? $"Visited · score {location.Score:0.#}/10" : "Not visited yet";
        var description = string.IsNullOrWhiteSpace(location.Description) ? string.Empty : $"\n{WebUtility.HtmlEncode(location.Description)}";
        return $"<b>{rank}. {WebUtility.HtmlEncode(location.Name)}</b> · {distance:0.0} km\n{WebUtility.HtmlEncode(string.Join(", ", suggestion.CategoryNames))}\n{visited}{description}\n<a href=\"{mapUrl}\">Open in Google Maps</a>";
    }
}
