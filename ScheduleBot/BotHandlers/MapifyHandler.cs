using System.Globalization;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

/// <summary>Telegram interaction flow for collaborative place maps.</summary>
public sealed class MapifyHandler(
    ITelegramBotClient bot,
    IHttpClientFactory httpClientFactory,
    UserSessionService sessionService,
    MainService services,
    MapifyService mapifyService)
{
    private readonly ConcurrentDictionary<string, Task<byte[]?>> _mapTiles = new();
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
            case CallBacks.MapifyEditLocation when Guid.TryParse(value, out var editLocationMapId):
                await BeginLocationEditing(data.ChatId, editLocationMapId);
                break;
            case CallBacks.MapifySelectEditLocation when Guid.TryParse(value, out var editLocationId):
                await BeginLocationEdit(data.ChatId, editLocationId);
                break;
            case CallBacks.PreviousPage when value == CallBacks.MapifySelectEditLocation && int.TryParse(data.DataSeparated.ElementAtOrDefault(3), out var editPreviousPage):
                await LoadLocationsForEditing(data.ChatId, editPreviousPage - 1);
                break;
            case CallBacks.NextPage when value == CallBacks.MapifySelectEditLocation && int.TryParse(data.DataSeparated.ElementAtOrDefault(3), out var editNextPage):
                await LoadLocationsForEditing(data.ChatId, editNextPage + 1);
                break;
            case CallBacks.MapifyEditField:
                await HandleEditLocationField(data, value);
                break;
            case CallBacks.MapifyEditCategories:
                await HandleEditLocationCategorySelection(data);
                break;
            case CallBacks.MapifyEditVisited:
                await HandleEditVisitedSelection(data, value);
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
            case Actions.MapifyEditingLocation:
                await HandleEditingLocation(data, session);
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
            [new(Messages.MapifyAddLocation, $"{CallBacks.MapifyAddLocation}|{mapId}"), new(Messages.MapifyEditLocation, $"{CallBacks.MapifyEditLocation}|{mapId}")],
            [new(Messages.MapifySuggest, $"{CallBacks.MapifySuggest}|{mapId}")]
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

    private async Task BeginLocationEditing(long chatId, Guid mapId)
    {
        if (await mapifyService.GetMapForUserAsync(mapId, chatId) == null)
        {
            await services.SendMessage(chatId, Messages.MapifyNotFound);
            return;
        }

        var session = sessionService.SetData(chatId, Actions.MapifyEditingLocation, SessionCallBacks.MapifyAskLocationToEditPin);
        session.SetContext(Context.MapifyMapId, mapId);
        await AskForLocation(chatId, Messages.MapifyAskLocationToEditPin);
    }

    private async Task LoadLocationsForEditing(long chatId, int pageNumber = 0)
    {
        var session = sessionService.GetData(chatId);
        if (!session.Context.TryGetValue(Context.MapifyMapId, out var mapIdValue) || mapIdValue is not Guid mapId ||
            !session.Context.TryGetValue(Context.MapifyEditReferenceLatitude, out var latitudeValue) || latitudeValue is not double latitude ||
            !session.Context.TryGetValue(Context.MapifyEditReferenceLongitude, out var longitudeValue) || longitudeValue is not double longitude)
        {
            sessionService.ClearSession(chatId);
            await services.SendMessage(chatId, Messages.MapifyNotFound);
            return;
        }

        var locations = await mapifyService.GetLocationsAsync(chatId, mapId);
        if (locations.Count == 0)
        {
            await services.SendMessage(chatId, Messages.MapifyNoLocations);
            return;
        }

        var orderedLocations = locations.OrderBy(x => DistanceInKilometers(latitude, longitude, x.Latitude, x.Longitude)).ToList();
        pageNumber = Math.Clamp(pageNumber, 0, (orderedLocations.Count - 1) / 6);
        var collection = services.LoadCollectionInPages(orderedLocations, CallBacks.MapifySelectEditLocation, pageNumber, x => x.Id, x => x.Name, width: 2, height: 3);
        await services.SendMessage(chatId, Messages.MapifySelectLocationToEdit,
            services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Mapify}|"));
    }

    private async Task BeginLocationEdit(long chatId, Guid locationId)
    {
        var details = await mapifyService.GetLocationDetailsAsync(chatId, locationId);
        if (details == null)
        {
            await services.SendMessage(chatId, Messages.MapifyLocationUpdateFailed);
            return;
        }

        var session = sessionService.SetData(chatId, Actions.MapifyEditingLocation);
        SetLocationEditContext(session, details);
        await ShowLocationEditor(chatId, session);
    }

    private async Task HandleEditLocationField(UpdateData data, string? field)
    {
        var session = sessionService.GetData(data.ChatId);
        if (session.Action != Actions.MapifyEditingLocation) return;

        switch (field)
        {
            case "Name":
                session.SetCallBack(SessionCallBacks.MapifyAskEditedLocationName);
                await services.SendMessage(data.ChatId, Messages.MapifyAskEditedLocationName, new ForceReplyMarkup());
                break;
            case "Categories":
                session.SetContext(Context.MapifySelectorMessageId, 0);
                session.SetContext(Context.MapifyAllCategoriesSelected, false);
                await ShowEditLocationCategorySelection(data.ChatId);
                break;
            case "Pin":
                session.SetCallBack(SessionCallBacks.MapifyAskEditedLocationPin);
                await AskForLocation(data.ChatId, Messages.MapifyAskEditedLocationPin);
                break;
            case "Description":
                session.SetCallBack(SessionCallBacks.MapifyAskEditedLocationDescription);
                await services.SendMessage(data.ChatId, Messages.MapifyAskEditedLocationDescription, new ForceReplyMarkup());
                break;
            case "Visited":
                await AskEditVisited(data.ChatId);
                break;
            case "Score":
                session.SetCallBack(SessionCallBacks.MapifyAskEditedLocationScore);
                await services.SendMessage(data.ChatId, Messages.MapifyAskScore, new ForceReplyMarkup());
                break;
            case "Close":
                sessionService.ClearSession(data.ChatId);
                break;
        }
    }

    private async Task ShowEditLocationCategorySelection(long chatId)
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
        var keyboard = services.CreateKeyboard(
            inlineCollection: services.LoadCollectionMultiSelect(categories, selectedIds, (bool)session.Context[Context.MapifyAllCategoriesSelected], x => x.Id, x => x.Name),
            callBackStart: $"*{CallBacks.Mapify}|{CallBacks.MapifyEditCategories}|");
        var messageId = (int)session.Context[Context.MapifySelectorMessageId];
        var message = string.Format(Messages.MapifySelectLocationCategories, selectedIds.Count, categories.Count);
        if (messageId == 0) session.SetContext(Context.MapifySelectorMessageId, await services.SendMessage(chatId, message, keyboard));
        else await services.EditMessage(chatId, messageId, message, keyboard);
    }

    private async Task HandleEditLocationCategorySelection(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);
        if (session.Action != Actions.MapifyEditingLocation) return;
        var action = data.DataSeparated.ElementAtOrDefault(2);
        var selectedIds = (List<Guid>)session.Context[Context.MapifySelectedCategoryIds];
        switch (action)
        {
            case CallBacks.MultipleSelectToggle when Guid.TryParse(data.DataSeparated.ElementAtOrDefault(3), out var categoryId):
                if (!selectedIds.Remove(categoryId)) selectedIds.Add(categoryId);
                session.SetContext(Context.MapifySelectedCategoryIds, selectedIds);
                await ShowEditLocationCategorySelection(data.ChatId);
                break;
            case CallBacks.MultipleSelectAll:
                var mapId = (Guid)session.Context[Context.MapifyMapId];
                session.SetContext(Context.MapifySelectedCategoryIds, (await mapifyService.GetCategoriesAsync(mapId, data.ChatId)).Select(x => x.Id).ToList());
                session.SetContext(Context.MapifyAllCategoriesSelected, true);
                await ShowEditLocationCategorySelection(data.ChatId);
                break;
            case CallBacks.MultipleDeselectAll:
                selectedIds.Clear();
                session.SetContext(Context.MapifySelectedCategoryIds, selectedIds);
                session.SetContext(Context.MapifyAllCategoriesSelected, false);
                await ShowEditLocationCategorySelection(data.ChatId);
                break;
            case CallBacks.Done when selectedIds.Count > 0:
                await services.DeleteMessage(data.ChatId, (int)session.Context[Context.MapifySelectorMessageId]);
                await SaveEditedLocationAndShowEditor(data.ChatId, session);
                break;
            case CallBacks.Done:
                await services.SendMessage(data.ChatId, Messages.MapifyChooseAtLeastOneCategory);
                break;
            case CallBacks.Cancel:
                await services.DeleteMessage(data.ChatId, (int)session.Context[Context.MapifySelectorMessageId]);
                await ShowLocationEditor(data.ChatId, session);
                break;
        }
    }

    private async Task HandleEditingLocation(UpdateData data, UserSession session)
    {
        switch (session.CallbackData)
        {
            case SessionCallBacks.MapifyAskLocationToEditPin:
                if (data.Latitude == null || data.Longitude == null) { await AskForLocation(data.ChatId, Messages.MapifyAskLocationToEditPin); return; }
                session.SetContext(Context.MapifyEditReferenceLatitude, data.Latitude.Value);
                session.SetContext(Context.MapifyEditReferenceLongitude, data.Longitude.Value);
                await LoadLocationsForEditing(data.ChatId);
                break;
            case SessionCallBacks.MapifyAskEditedLocationName:
                if (string.IsNullOrWhiteSpace(data.MessageText)) { await services.SendMessage(data.ChatId, Messages.MapifyAskEditedLocationName); return; }
                session.SetContext(Context.MapifyLocationName, data.MessageText.Trim());
                await SaveEditedLocationAndShowEditor(data.ChatId, session);
                break;
            case SessionCallBacks.MapifyAskEditedLocationPin:
                if (data.Latitude == null || data.Longitude == null) { await AskForLocation(data.ChatId, Messages.MapifyAskEditedLocationPin); return; }
                session.SetContext(Context.MapifyLatitude, data.Latitude.Value);
                session.SetContext(Context.MapifyLongitude, data.Longitude.Value);
                await SaveEditedLocationAndShowEditor(data.ChatId, session);
                break;
            case SessionCallBacks.MapifyAskEditedLocationDescription:
                session.SetContext(Context.MapifyDescription, data.MessageText == Messages.Skip ? string.Empty : data.MessageText?.Trim() ?? string.Empty);
                await SaveEditedLocationAndShowEditor(data.ChatId, session);
                break;
            case SessionCallBacks.MapifyAskEditedLocationScore:
                if (!TryParseScore(data.MessageText, out var score)) { await services.SendMessage(data.ChatId, Messages.MapifyInvalidScore); return; }
                session.SetContext(Context.MapifyVisited, true);
                session.SetContext(Context.MapifyScore, score);
                await SaveEditedLocationAndShowEditor(data.ChatId, session);
                break;
        }
    }

    private async Task AskEditVisited(long chatId)
    {
        List<List<Tuple<string, string>>> collection = [[new(Messages.Yes, CallBacks.Yes), new(Messages.No, CallBacks.No)]];
        await services.SendMessage(chatId, Messages.MapifyAskVisited,
            services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Mapify}|{CallBacks.MapifyEditVisited}|"));
    }

    private async Task HandleEditVisitedSelection(UpdateData data, string? value)
    {
        var session = sessionService.GetData(data.ChatId);
        if (session.Action != Actions.MapifyEditingLocation) return;
        if (value == CallBacks.No)
        {
            session.SetContext(Context.MapifyVisited, false);
            session.Context.Remove(Context.MapifyScore);
            await SaveEditedLocationAndShowEditor(data.ChatId, session);
            return;
        }
        if (value == CallBacks.Yes)
        {
            session.SetContext(Context.MapifyVisited, true);
            if (session.Context.ContainsKey(Context.MapifyScore))
                await SaveEditedLocationAndShowEditor(data.ChatId, session);
            else
            {
                session.SetCallBack(SessionCallBacks.MapifyAskEditedLocationScore);
                await services.SendMessage(data.ChatId, Messages.MapifyAskScore, new ForceReplyMarkup());
            }
        }
    }

    private async Task SaveEditedLocationAndShowEditor(long chatId, UserSession session)
    {
        var saved = await mapifyService.UpdateLocationAsync(chatId, (Guid)session.Context[Context.MapifyLocationId], new MapifyLocationDraft
        {
            MapId = (Guid)session.Context[Context.MapifyMapId],
            Name = (string)session.Context[Context.MapifyLocationName],
            Latitude = (double)session.Context[Context.MapifyLatitude],
            Longitude = (double)session.Context[Context.MapifyLongitude],
            Description = (string)session.Context[Context.MapifyDescription],
            IsVisited = (bool)session.Context[Context.MapifyVisited],
            Score = session.Context.TryGetValue(Context.MapifyScore, out var scoreValue) && scoreValue is decimal score ? score : null,
            CategoryIds = (List<Guid>)session.Context[Context.MapifySelectedCategoryIds]
        });
        if (!saved)
        {
            sessionService.ClearSession(chatId);
            await services.SendMessage(chatId, Messages.MapifyLocationUpdateFailed);
            return;
        }
        await services.SendMessage(chatId, Messages.MapifyLocationUpdated);
        await ShowLocationEditor(chatId, session);
    }

    private async Task ShowLocationEditor(long chatId, UserSession session)
    {
        var details = await mapifyService.GetLocationDetailsAsync(chatId, (Guid)session.Context[Context.MapifyLocationId]);
        if (details == null)
        {
            sessionService.ClearSession(chatId);
            await services.SendMessage(chatId, Messages.MapifyLocationUpdateFailed);
            return;
        }

        SetLocationEditContext(session, details);
        var location = details.Location;
        var categoryNames = details.CategoryNames.Count == 0 ? "—" : string.Join(", ", details.CategoryNames.Select(WebUtility.HtmlEncode));
        var visited = location.IsVisited ? Messages.Yes : Messages.No;
        var score = location.Score?.ToString("0.#", CultureInfo.InvariantCulture) ?? "—";
        var description = string.IsNullOrWhiteSpace(location.Description) ? "—" : WebUtility.HtmlEncode(location.Description);
        var message = string.Format(Messages.MapifyLocationEditor, WebUtility.HtmlEncode(location.Name), categoryNames, visited, score, description);
        List<List<Tuple<string, string>>> collection =
        [
            [new(Messages.MapifyEditName, $"{CallBacks.MapifyEditField}|Name"), new(Messages.MapifyEditCategories, $"{CallBacks.MapifyEditField}|Categories")],
            [new(Messages.MapifyEditPin, $"{CallBacks.MapifyEditField}|Pin"), new(Messages.MapifyEditDescription, $"{CallBacks.MapifyEditField}|Description")],
            [new(Messages.MapifyEditVisited, $"{CallBacks.MapifyEditField}|Visited"), new(Messages.MapifyEditScore, $"{CallBacks.MapifyEditField}|Score")],
            [new(Messages.MapifyEditClose, $"{CallBacks.MapifyEditField}|Close")]
        ];
        await services.SendMessage(chatId, message,
            services.CreateKeyboard(inlineCollection: collection, callBackStart: $"{CallBacks.Mapify}|"), parseMode: ParseMode.Html);
    }

    private static void SetLocationEditContext(UserSession session, MapifyLocationDetails details)
    {
        session.SetContext(Context.MapifyMapId, details.Location.MapId);
        session.SetContext(Context.MapifyLocationId, details.Location.Id);
        session.SetContext(Context.MapifyLocationName, details.Location.Name);
        session.SetContext(Context.MapifyLatitude, details.Location.Latitude);
        session.SetContext(Context.MapifyLongitude, details.Location.Longitude);
        session.SetContext(Context.MapifyDescription, details.Location.Description ?? string.Empty);
        session.SetContext(Context.MapifyVisited, details.Location.IsVisited);
        session.SetContext(Context.MapifySelectedCategoryIds, details.CategoryIds.ToList());
        if (details.Location.Score is decimal score) session.SetContext(Context.MapifyScore, score);
        else session.Context.Remove(Context.MapifyScore);
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
            .Where(x => x.Distance <= 10d)
            .OrderBy(x => x.Distance)
            .Take(5)
            .ToList();
        if (ordered.Count == 0) { await services.SendMessage(data.ChatId, Messages.MapifyNoNearbySuggestions); return; }

        for (var index = 0; index < ordered.Count; index++)
        {
            var item = ordered[index];
            await SendSuggestionCard(data.ChatId, index + 1, data.Latitude.Value, data.Longitude.Value, item.Suggestion, item.Distance);
        }
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

    private async Task SendSuggestionCard(long chatId, int rank, double originLatitude, double originLongitude, MapifyLocationSuggestion suggestion, double distance)
    {
        var location = suggestion.Location;
        var mapImageTask = DownloadMapImageAsync(location.Latitude, location.Longitude);
        var linkPreviewTask = LoadLinkPreviewAsync(location.Description);
        await Task.WhenAll(mapImageTask, linkPreviewTask);

        var linkPreview = await linkPreviewTask;
        var image = MapifySuggestionCardRenderer.Render(rank, suggestion, distance, await mapImageTask, linkPreview);
        await using var stream = new MemoryStream(image);
        var walkingUrl = WalkingUrl(originLatitude, originLongitude, location.Latitude, location.Longitude);
        var caption = BuildSuggestionCaption(location.Name, distance, linkPreview?.Summary);
        var buttons = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithUrl("🚶 Walk here", walkingUrl) }
        };
        var sharedUrl = ExtractSharedUrl(location.Description);
        if (sharedUrl != null)
            buttons.Add(new[] { InlineKeyboardButton.WithUrl("🎬 Open shared post", sharedUrl) });
        var keyboard = new InlineKeyboardMarkup(buttons);
        await bot.SendPhoto(chatId, new InputFileStream(stream, $"mapify-{rank}.png"), caption: caption, parseMode: ParseMode.Html, replyMarkup: keyboard);
    }

    private static string BuildSuggestionCaption(string locationName, double distance, string? sharedPostCaption)
    {
        var caption = $"📍 <b>{WebUtility.HtmlEncode(locationName)}</b> · {FormatDistance(distance)} away";
        if (string.IsNullOrWhiteSpace(sharedPostCaption)) return caption;

        // Telegram photo captions are limited to 1024 characters. Keep room for the
        // heading and HTML entity expansion while preserving as much post text as possible.
        var postCaption = sharedPostCaption.Trim();
        if (postCaption.Length > 700) postCaption = $"{postCaption[..699]}…";
        return $"{caption}\n\n🎬 <b>Shared post</b>\n{WebUtility.HtmlEncode(postCaption)}";
    }

    private static string? ExtractSharedUrl(string? description)
    {
        var match = UrlRegex.Match(description ?? string.Empty);
        if (!match.Success) return null;
        var candidate = match.Value.TrimEnd('.', ',', ';', ':', ')', ']', '}');
        return Uri.TryCreate(candidate, UriKind.Absolute, out var url) && url.Scheme is "http" or "https"
            ? url.AbsoluteUri
            : null;
    }

    private async Task<MapifyMapImage?> DownloadMapImageAsync(double latitude, double longitude)
    {
        // One zoom level closer doubles the linear map scale and makes nearby streets useful.
        const int zoom = 16;
        const int tileSize = 256;
        const int imageWidth = 480;
        const int imageHeight = 330;
        var worldSize = tileSize * (1 << zoom);
        var centerX = (longitude + 180d) / 360d * worldSize;
        var latitudeRadians = DegreesToRadians(Math.Clamp(latitude, -85.05112878d, 85.05112878d));
        var centerY = (1d - Math.Asinh(Math.Tan(latitudeRadians)) / Math.PI) / 2d * worldSize;
        var left = centerX - imageWidth / 2d;
        var top = centerY - imageHeight / 2d;
        var minTileX = (int)Math.Floor(left / tileSize);
        var maxTileX = (int)Math.Floor((left + imageWidth - 1) / tileSize);
        var minTileY = Math.Max(0, (int)Math.Floor(top / tileSize));
        var maxTileY = Math.Min((1 << zoom) - 1, (int)Math.Floor((top + imageHeight - 1) / tileSize));
        var tileCount = 1 << zoom;
        var svg = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{imageWidth}\" height=\"{imageHeight}\" viewBox=\"0 0 {imageWidth} {imageHeight}\"><rect width=\"100%\" height=\"100%\" fill=\"#E6EEF6\"/>");
        var addedTiles = 0;

        for (var tileY = minTileY; tileY <= maxTileY; tileY++)
        for (var tileX = minTileX; tileX <= maxTileX; tileX++)
        {
            var wrappedTileX = ((tileX % tileCount) + tileCount) % tileCount;
            var tileUrl = $"https://tile.openstreetmap.org/{zoom}/{wrappedTileX}/{tileY}.png";
            var tile = await _mapTiles.GetOrAdd(tileUrl, DownloadKnownOsmTileAsync);
            if (tile == null) continue;
            var x = tileX * tileSize - left;
            var y = tileY * tileSize - top;
            var xText = x.ToString("0.##", CultureInfo.InvariantCulture);
            var yText = y.ToString("0.##", CultureInfo.InvariantCulture);
            svg.Append($"<image x=\"{xText}\" y=\"{yText}\" width=\"256\" height=\"256\" href=\"data:image/png;base64,{Convert.ToBase64String(tile)}\"/>");
            addedTiles++;
        }

        if (addedTiles == 0) return null;
        var markerX = imageWidth / 2d;
        var markerY = imageHeight / 2d;
        var markerXText = markerX.ToString("0.##", CultureInfo.InvariantCulture);
        var markerYText = markerY.ToString("0.##", CultureInfo.InvariantCulture);
        svg.Append($"<g transform=\"translate({markerXText} {markerYText})\">" +
                   "<ellipse cx=\"0\" cy=\"4\" rx=\"12\" ry=\"5\" fill=\"#102A43\" opacity=\"0.32\"/>" +
                   "<path d=\"M0 2 C-5 -6 -21 -20 -21 -36 A21 21 0 1 1 21 -36 C21 -20 5 -6 0 2Z\" fill=\"#E11D48\" stroke=\"#FFFFFF\" stroke-width=\"6\" stroke-linejoin=\"round\"/>" +
                   "<circle cx=\"0\" cy=\"-36\" r=\"8\" fill=\"#FFFFFF\"/>" +
                   "<circle cx=\"0\" cy=\"-36\" r=\"3\" fill=\"#102A43\"/>" +
                   "</g></svg>");
        return new MapifyMapImage(svg.ToString());
    }

    private async Task<byte[]?> DownloadKnownOsmTileAsync(string url)
    {
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ScheduleBot-Mapify/1.0");
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is long size && size > 700_000) return null;
            return await response.Content.ReadAsByteArrayAsync(cancellation.Token);
        }
        catch
        {
            return null;
        }
    }

    private async Task<MapifyLinkPreview?> LoadLinkPreviewAsync(string? description)
    {
        var urlMatch = UrlRegex.Match(description ?? string.Empty);
        if (!urlMatch.Success || !Uri.TryCreate(urlMatch.Value, UriKind.Absolute, out var url) || !await IsSafeExternalUrlAsync(url)) return null;

        string? title = null;
        string? summary = null;
        byte[]? image = null;
        try
        {
            var isInstagram = url.Host.EndsWith("instagram.com", StringComparison.OrdinalIgnoreCase);
            var previewUris = isInstagram
                ? new[] { url, GetPreviewUrl(url) }.Distinct().ToArray()
                : new[] { url };
            var htmlResults = await Task.WhenAll(previewUris.Select(LoadPreviewHtmlAsync));
            var documents = previewUris.Zip(htmlResults)
                .Where(item => !string.IsNullOrWhiteSpace(item.Second))
                .Select(item => (BaseUri: item.First, Html: item.Second!))
                .ToList();

            title = documents.Select(document => FindMetaContent(document.Html, "og:title", "twitter:title") ?? FindTitle(document.Html))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            if (isInstagram)
            {
                title ??= "Instagram post";
                summary = documents.Select(document => FindInstagramCaption(document.Html))
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            }
            else
            {
                summary = documents.Select(document => FindMetaContent(document.Html, "og:description", "twitter:description", "description"))
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            }

            foreach (var document in documents)
            {
                var imageUrl = FindMetaContent(document.Html, "og:image", "twitter:image");
                if (!Uri.TryCreate(document.BaseUri, imageUrl, out var thumbnailUrl) || !await IsSafeExternalUrlAsync(thumbnailUrl)) continue;
                image = await DownloadBytesAsync(thumbnailUrl, 2_500_000, imageOnly: true);
                if (image != null) break;
            }
        }
        catch
        {
            // Platform pages change frequently. Dedicated thumbnail fallbacks below
            // can still produce a useful card when their HTML cannot be parsed.
        }

        image ??= await LoadKnownVideoThumbnailAsync(url);
        image ??= await LoadInstagramThumbnailAsync(url);

        return string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(summary) && image == null
            ? null
            : new MapifyLinkPreview(TrimPreviewText(title, 180), TrimPreviewText(summary, 700), image);
    }

    private async Task<string?> LoadPreviewHtmlAsync(Uri url)
    {
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var client = CreatePreviewClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentType?.MediaType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) != true ||
                response.Content.Headers.ContentLength is > 350_000) return null;
            return await ReadHtmlAsync(response, cancellation.Token);
        }
        catch
        {
            return null;
        }
    }

    private async Task<byte[]?> DownloadBytesAsync(Uri url, int maxBytes, bool imageOnly)
    {
        if (!await IsSafeExternalUrlAsync(url)) return null;
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var client = CreatePreviewClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is long contentLength && contentLength > maxBytes ||
                (imageOnly && !string.IsNullOrWhiteSpace(mediaType) && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))) return null;
            await using var input = await response.Content.ReadAsStreamAsync(cancellation.Token);
            await using var output = new MemoryStream();
            var buffer = new byte[16_384];
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(), cancellation.Token)) > 0)
            {
                if (output.Length + read > maxBytes) return null;
                await output.WriteAsync(buffer.AsMemory(0, read), cancellation.Token);
            }
            return output.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private HttpClient CreatePreviewClient()
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; ScheduleBot/1.0; +https://t.me)");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9,fa;q=0.8");
        return client;
    }

    private static Uri GetPreviewUrl(Uri original)
    {
        if (!original.Host.EndsWith("instagram.com", StringComparison.OrdinalIgnoreCase)) return original;
        var parts = original.AbsolutePath.Trim('/').Split('/');
        return parts.Length >= 2 && parts[0] is "p" or "reel" or "tv"
            ? new Uri($"https://www.instagram.com/{parts[0]}/{parts[1]}/embed/captioned/")
            : original;
    }

    private async Task<byte[]?> LoadKnownVideoThumbnailAsync(Uri url)
    {
        var videoId = GetYoutubeVideoId(url);
        return videoId == null ? null : await DownloadBytesAsync(new Uri($"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg"), 2_500_000, imageOnly: true);
    }

    private async Task<byte[]?> LoadInstagramThumbnailAsync(Uri url)
    {
        if (!url.Host.EndsWith("instagram.com", StringComparison.OrdinalIgnoreCase)) return null;
        var parts = url.AbsolutePath.Trim('/').Split('/');
        if (parts.Length < 2 || parts[0] is not ("p" or "reel" or "tv")) return null;
        return await DownloadBytesAsync(new Uri($"https://www.instagram.com/{parts[0]}/{parts[1]}/media/?size=l"), 2_500_000, imageOnly: true);
    }

    private static string? GetYoutubeVideoId(Uri url)
    {
        if (url.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase)) return url.AbsolutePath.Trim('/').Split('/').FirstOrDefault();
        if (!url.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)) return null;
        if (url.AbsolutePath.StartsWith("/shorts/", StringComparison.OrdinalIgnoreCase)) return url.AbsolutePath.Split('/').ElementAtOrDefault(2);
        return url.Query.TrimStart('?').Split('&')
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2 && part[0] == "v")
            .Select(part => Uri.UnescapeDataString(part[1]))
            .FirstOrDefault();
    }

    private static async Task<string?> ReadHtmlAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var buffer = new char[8_192];
        var builder = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            if (builder.Length + read > 350_000) return null;
            builder.Append(buffer, 0, read);
        }
        return builder.ToString();
    }

    private static string? FindMetaContent(string html, params string[] names)
    {
        foreach (var name in names)
        {
            var escapedName = Regex.Escape(name);
            var propertyFirst = Regex.Match(html, $"<meta\\b(?=[^>]*(?:property|name)\\s*=\\s*['\\\"]{escapedName}['\\\"])[^>]*\\bcontent\\s*=\\s*['\\\"]([^'\\\"]+)['\\\"]", RegexOptions.IgnoreCase);
            if (propertyFirst.Success) return WebUtility.HtmlDecode(propertyFirst.Groups[1].Value);
            var contentFirst = Regex.Match(html, $"<meta\\b(?=[^>]*\\bcontent\\s*=\\s*['\\\"]([^'\\\"]+)['\\\"])[^>]*(?:property|name)\\s*=\\s*['\\\"]{escapedName}['\\\"]", RegexOptions.IgnoreCase);
            if (contentFirst.Success) return WebUtility.HtmlDecode(contentFirst.Groups[1].Value);
        }
        return null;
    }

    private static string? FindTitle(string html)
    {
        var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, "\\s+", " ").Trim()) : null;
    }

    private static string? FindInstagramCaption(string html)
    {
        string[] jsonPatterns =
        [
            "\\\"caption_text\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\"])*)\\\"",
            "\\\"caption\\\"\\s*:\\s*\\{[^{}]{0,4000}?\\\"text\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\"])*)\\\"",
            "\\\"edge_media_to_caption\\\"[^\\[]*\\[[^\\]]{0,8000}?\\\"text\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\"])*)\\\"",
            "\\\"articleBody\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\"])*)\\\"",
            "\\\"caption\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\"])*)\\\""
        ];

        foreach (var pattern in jsonPatterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success) continue;
            var decoded = DecodeJsonString(match.Groups[1].Value);
            if (IsUsefulInstagramCaption(decoded)) return decoded;
        }

        var captionElement = Regex.Match(html,
            "<(?:div|span)[^>]*class=['\\\"][^'\\\"]*(?:Caption|captionText)[^'\\\"]*['\\\"][^>]*>(.*?)</(?:div|span)>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (captionElement.Success)
        {
            var decoded = WebUtility.HtmlDecode(Regex.Replace(captionElement.Groups[1].Value, "<[^>]+>", " "));
            decoded = Regex.Replace(decoded, "\\s+", " ").Trim();
            if (IsUsefulInstagramCaption(decoded)) return decoded;
        }

        var metaDescription = FindMetaContent(html, "og:description", "twitter:description", "description");
        if (string.IsNullOrWhiteSpace(metaDescription)) return null;
        var quotedCaption = Regex.Match(metaDescription, "(?:on Instagram\\s*:\\s*|:\\s*)[\\\"“](.+?)[\\\"”]\\.?$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var candidate = quotedCaption.Success ? quotedCaption.Groups[1].Value.Trim() : metaDescription.Trim();
        return IsUsefulInstagramCaption(candidate) ? candidate : null;
    }

    private static string? DecodeJsonString(string escapedValue)
    {
        try { return JsonSerializer.Deserialize<string>($"\\\"{escapedValue}\\\"")?.Trim(); }
        catch { return null; }
    }

    private static bool IsUsefulInstagramCaption(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 2) return false;
        var normalized = value.Trim();
        return !normalized.Equals("Instagram", StringComparison.OrdinalIgnoreCase) &&
               !normalized.StartsWith("Log in", StringComparison.OrdinalIgnoreCase) &&
               !normalized.StartsWith("Sign up", StringComparison.OrdinalIgnoreCase) &&
               !normalized.StartsWith("Create an account", StringComparison.OrdinalIgnoreCase) &&
               !normalized.StartsWith("See Instagram photos and videos", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TrimPreviewText(string? text, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();
        return trimmed.Length <= maximumLength ? trimmed : $"{trimmed[..(maximumLength - 1)]}…";
    }

    private static async Task<bool> IsSafeExternalUrlAsync(Uri url)
    {
        if (url.Scheme is not ("http" or "https") || url.IsLoopback || string.Equals(url.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return false;
        if (IPAddress.TryParse(url.Host, out var address)) return !IsPrivateAddress(address);
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(url.DnsSafeHost);
            return addresses.Length > 0 && addresses.All(address => !IsPrivateAddress(address));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.AddressFamily == AddressFamily.InterNetworkV6 && (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)) return true;
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0 ||
               bytes[0] == 169 && bytes[1] == 254 ||
               bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
               bytes[0] == 192 && bytes[1] == 168;
    }

    private static string WalkingUrl(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude) =>
        string.Create(CultureInfo.InvariantCulture,
            $"https://www.google.com/maps/dir/?api=1&origin={fromLatitude},{fromLongitude}&destination={toLatitude},{toLongitude}&travelmode=walking");

    private static string FormatDistance(double kilometers) => kilometers < 1
        ? $"{Math.Round(kilometers * 1000 / 10) * 10:N0} m"
        : $"{kilometers:0.0} km";

    private static readonly Regex UrlRegex = new("https?://[^\\s<>\\\"']+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
