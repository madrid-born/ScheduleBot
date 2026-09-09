using System.Globalization;
using System.Text;
using ScheduleBot.Models;
using ScheduleBot.Services;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ScheduleBot.BotHandlers;

/// <summary>
/// Telegram-only Metro interaction flow. Database work stays in MetroService.
/// </summary>
public sealed class MetroHandler(
    MainService services,
    UserSessionService sessionService,
    MetroService metroService)
{
    public async Task HandleSection(UpdateData data)
    {
        List<List<Tuple<string, string>>> keyboard =
        [
            [new(Messages.MetroNavigation, CallBacks.MetroNavigation)],
            [new(Messages.MetroStationDetails, CallBacks.MetroStationDetails)]
        ];
        await services.SendMessage(
            data.ChatId,
            Messages.MetroWelcome,
            services.CreateKeyboard(
                inlineCollection: keyboard,
                callBackStart: $"{CallBacks.Metro}|{CallBacks.MainSection}|"));
    }

    public async Task HandleCallBack(UpdateData data)
    {
        switch (data.DataSeparated.ElementAtOrDefault(1))
        {
            case CallBacks.MainSection:
                switch (data.DataSeparated.ElementAtOrDefault(2))
                {
                    case CallBacks.MetroNavigation:
                        await BeginNavigation(data.ChatId);
                        break;
                    case CallBacks.MetroStationDetails:
                        sessionService.SetData(
                            data.ChatId,
                            Actions.MetroStationDetails,
                            SessionCallBacks.AskMetroStationName);
                        await services.SendMessage(data.ChatId, Messages.MetroAskStationName);
                        break;
                }
                break;
            case CallBacks.MetroStationSelected:
                var stationId = data.DataSeparated.ElementAtOrDefault(2);
                if (!string.IsNullOrWhiteSpace(stationId))
                    await ShowStation(data.ChatId, stationId);
                break;
        }
    }

    public async Task HandleSession(UpdateData data)
    {
        if (data.MessageText == Messages.Cancel)
        {
            sessionService.ClearSession(data.ChatId);
            await services.SendMessage(data.ChatId, Messages.MetroCancelled);
            return;
        }

        var session = sessionService.GetData(data.ChatId);
        switch (session.Action)
        {
            case Actions.MetroNavigation:
                await HandleNavigationLocation(data, session);
                break;
            case Actions.MetroStationDetails:
                await SearchStation(data);
                break;
        }
    }

    private async Task BeginNavigation(long chatId)
    {
        sessionService.SetData(
            chatId,
            Actions.MetroNavigation,
            SessionCallBacks.AskMetroOriginLocation);
        await AskForLocation(chatId, Messages.MetroAskOriginLocation);
    }

    private async Task HandleNavigationLocation(UpdateData data, UserSession session)
    {
        if (data.Latitude == null || data.Longitude == null)
        {
            var prompt = session.CallbackData == SessionCallBacks.AskMetroOriginLocation
                ? Messages.MetroAskOriginLocation
                : Messages.MetroAskDestinationLocation;
            await AskForLocation(data.ChatId, prompt);
            return;
        }

        if (session.CallbackData == SessionCallBacks.AskMetroOriginLocation)
        {
            session.SetContext(Context.MetroOriginLatitude, data.Latitude.Value);
            session.SetContext(Context.MetroOriginLongitude, data.Longitude.Value);
            session.SetCallBack(SessionCallBacks.AskMetroDestinationLocation);
            await AskForLocation(data.ChatId, Messages.MetroAskDestinationLocation);
            return;
        }

        if (session.CallbackData != SessionCallBacks.AskMetroDestinationLocation) return;
        var origin = new MetroGeoPoint(
            (double)session.Context[Context.MetroOriginLatitude],
            (double)session.Context[Context.MetroOriginLongitude]);
        var destination = new MetroGeoPoint(data.Latitude.Value, data.Longitude.Value);

        await services.SendMessage(data.ChatId, Messages.MetroCalculating);
        var result = await metroService.NavigateAsync(origin, destination);
        sessionService.ClearSession(data.ChatId);
        if (result == null)
        {
            await services.SendMessage(data.ChatId, Messages.MetroRouteNotFound);
            return;
        }
        await services.SendMessage(
            data.ChatId,
            FormatNavigation(result, origin, destination),
            parseMode: ParseMode.Html);
    }

    private async Task SearchStation(UpdateData data)
    {
        if (string.IsNullOrWhiteSpace(data.MessageText))
        {
            await services.SendMessage(data.ChatId, Messages.MetroAskStationName);
            return;
        }

        var matches = await metroService.SearchStationsAsync(data.MessageText);
        if (matches.Count == 0)
        {
            await services.SendMessage(data.ChatId, Messages.MetroStationNotFound);
            return;
        }
        if (matches.Count == 1)
        {
            sessionService.ClearSession(data.ChatId);
            await ShowStation(data.ChatId, matches[0].StationId);
            return;
        }

        List<List<Tuple<string, string>>> keyboard = matches
            .Select(x => new List<Tuple<string, string>>
            {
                new($"{x.NameEn} — {x.NameFa}", x.StationId)
            })
            .ToList();
        await services.SendMessage(
            data.ChatId,
            Messages.MetroSelectStation,
            services.CreateKeyboard(
                inlineCollection: keyboard,
                callBackStart: $"{CallBacks.Metro}|{CallBacks.MetroStationSelected}|"));
    }

    private async Task ShowStation(long chatId, string stationId)
    {
        var station = await metroService.GetStationDetailsAsync(stationId);
        if (station == null)
        {
            await services.SendMessage(chatId, Messages.MetroStationNotFound);
            return;
        }

        sessionService.ClearSession(chatId);
        var amenities = station.Amenities.Count == 0
            ? "None recorded"
            : string.Join(", ", station.Amenities);
        var mapsUrl = FormattableString.Invariant(
            $"https://www.google.com/maps/search/?api=1&query={station.Latitude},{station.Longitude}");
        var message = $"""
            🚇 <b>{station.NameEn}</b>
            {station.NameFa}

            Lines: {string.Join(", ", station.Lines.Select(x => $"Line {x}"))}
            Status: {station.Status.Replace('_', ' ')}
            Amenities: {amenities}

            <a href="{mapsUrl}">Open station on map</a>
            """;
        await services.SendMessage(chatId, message, parseMode: ParseMode.Html);
    }

    private async Task AskForLocation(long chatId, string message)
    {
        var keyboard = new ReplyKeyboardMarkup(
        [
            [KeyboardButton.WithRequestLocation(Messages.MetroSendLocation)],
            [new KeyboardButton(Messages.Cancel)]
        ])
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };
        await services.SendMessage(chatId, message, keyboard);
    }

    private static string FormatNavigation(
        MetroNavigationResult result,
        MetroGeoPoint origin,
        MetroGeoPoint destination)
    {
        var text = new StringBuilder();
        text.AppendLine("🚇 <b>Tehran Metro route</b>");
        text.AppendLine();
        text.AppendLine($"1. Walk about <b>{FormatDistance(result.OriginWalkMeters)}</b> ({FormatDuration(result.OriginWalkTime)}) to <b>{result.OriginStation.NameEn}</b>.");
        text.AppendLine($"   <a href=\"{WalkingUrl(origin, result.OriginStation)}\">Walking directions to the station</a>");

        if (result.Legs.Count == 0)
        {
            text.AppendLine();
            text.AppendLine("Both locations use the same nearest Metro station; a Metro ride is not useful for this trip.");
        }

        for (var index = 0; index < result.Legs.Count; index++)
        {
            var leg = result.Legs[index];
            text.AppendLine();
            if (leg.RequiresTransfer)
            {
                text.AppendLine($"{index + 2}. At <b>{leg.FromStationName}</b>, switch to <b>Line {leg.LineId}</b> toward <b>{leg.DirectionName}</b>" +
                                (leg.TransferWalkSeconds > 0
                                    ? $"; allow {FormatDuration(TimeSpan.FromSeconds(leg.TransferWalkSeconds))} to walk between platforms."
                                    : "."));
            }
            else
            {
                text.AppendLine($"{index + 2}. Take <b>Line {leg.LineId}</b> toward <b>{leg.DirectionName}</b>.");
            }

            if (leg.ScheduleFound)
            {
                text.AppendLine($"   Board at <b>{leg.FromStationName}</b> around <b>{leg.Departure:HH:mm}</b>" +
                                (leg.IsExpress ? " (express)" : "") +
                                $" and arrive at <b>{leg.ToStationName}</b> around <b>{leg.Arrival:HH:mm}</b>.");
                text.AppendLine($"   Platform wait: {FormatDuration(leg.WaitTime)}. Train time: {FormatDuration(leg.EstimatedRideTime)}.");
            }
            else
            {
                text.AppendLine($"   Ride from <b>{leg.FromStationName}</b> to <b>{leg.ToStationName}</b>: approximately {FormatDuration(leg.EstimatedRideTime)}. No matching timetable departure was found.");
            }
        }

        text.AppendLine();
        text.AppendLine($"{result.Legs.Count + 2}. From <b>{result.DestinationStation.NameEn}</b>, walk about <b>{FormatDistance(result.DestinationWalkMeters)}</b> ({FormatDuration(result.DestinationWalkTime)}) to the destination.");
        text.AppendLine($"   <a href=\"{WalkingUrl(result.DestinationStation, destination)}\">Walking directions to the destination</a>");
        text.AppendLine();
        text.AppendLine($"Estimated arrival: <b>{result.EstimatedArrival:HH:mm}</b>");
        text.AppendLine($"Estimated total time: <b>{FormatDuration(result.TotalTime)}</b>");
        text.AppendLine();
        text.Append("Walking distances are estimates; use the linked walking route for street-level directions.");
        return text.ToString();
    }

    private static string WalkingUrl(MetroGeoPoint from, MetroNearestStation to) =>
        WalkingUrl(from.Latitude, from.Longitude, to.Latitude, to.Longitude);

    private static string WalkingUrl(MetroNearestStation from, MetroGeoPoint to) =>
        WalkingUrl(from.Latitude, from.Longitude, to.Latitude, to.Longitude);

    private static string WalkingUrl(double fromLat, double fromLng, double toLat, double toLng) =>
        string.Create(CultureInfo.InvariantCulture,
            $"https://www.google.com/maps/dir/?api=1&origin={fromLat},{fromLng}&destination={toLat},{toLng}&travelmode=walking");

    private static string FormatDistance(double meters) =>
        meters < 1000 ? $"{Math.Round(meters / 10) * 10:N0} m" : $"{meters / 1000:0.0} km";

    private static string FormatDuration(TimeSpan duration)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes));
        return minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h {minutes % 60} min";
    }
}
