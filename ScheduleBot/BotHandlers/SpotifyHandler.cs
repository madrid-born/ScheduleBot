using ScheduleBot.Models;
using ScheduleBot.Services;

namespace ScheduleBot.BotHandlers;

public class SpotifyHandler(
    SpotifyService spotifyService,
    UserSessionService sessionService,
    MainService services)
{
    public async Task HandleSection(UpdateData data)
    {
        if (data.ChatId != services.AdminChatId) return;
        var collection = new List<List<Tuple<string, string>>>
        {
            new()
            {
                new (Messages.KeyboardCategorizePlaylist, $"{CallBacks.Spotify}|{CallBacks.CategorizePlaylist}"),
            }
        };
        var keyboard = services.CreateKeyboard(inlineCollection: collection);

        var accessToken = await spotifyService.GetAccessToken();
        sessionService.SetData(data.ChatId, Actions.LoadSpotify, accessToken);
        await services.SendMessage(data.ChatId, Messages.LoadSpotify, replyMarkup: keyboard);
    }

    public async Task HandleCallBack(UpdateData data)
    {
        if (data.ChatId != services.AdminChatId) return;

        if (data.DataSeparated.Count < 2) return;

        switch (data.DataSeparated[1])
        {
            case CallBacks.CategorizePlaylist:
                await AskForPlaylistId(data);
                break;
            case CallBacks.NotCategorizePlaylist:
                await CategorizePlaylist(data);
                break;
            case CallBacks.TrackAction:
                await TrackCategorizingAction(data);
                break;
        }
    }
    
    private async Task AskForPlaylistId(UpdateData data)
    {
        var collection = new List<List<Tuple<string, string>>>
        {
            new()
            {
                new (Messages.KeyboardNotCategorizePlaylist, $"{CallBacks.Spotify}|{CallBacks.NotCategorizePlaylist}"),
            }
        };
        var keyboard = services.CreateKeyboard(inlineCollection: collection);

        var session = sessionService.GetData(data.ChatId);
        session.SetAction(Actions.AwaitingPlaylistId);
        await services.SendMessage(data.ChatId, Messages.AskForPlaylistId, replyMarkup: keyboard);
    }
    
    public async Task CategorizePlaylist(UpdateData data, string? playlistId = null)
    {
        playlistId ??= SpotifyApi.NotCategorizedPlaylistId;
        var session = sessionService.GetData(data.ChatId);
        var playlistDetail = await spotifyService.GetPlayListFromSpotify(session!.CallbackData, playlistId);
        if (playlistDetail.PlaylistTracks.Count < 1) await services.SendMessage(data.ChatId, Messages.PlaylistEmpty, imageUrl: playlistDetail.ImageUrl);
        var message = string.Format(Messages.StartCategorizing, playlistDetail.PlaylistName, playlistDetail.OwnerName, playlistDetail.PlaylistTracks.Count);
        await services.SendMessage(data.ChatId, message, imageUrl: playlistDetail.ImageUrl);
        
        session.SetAction(Actions.AwaitingTrackReview);
        session.SetContext(Context.TracksIds, playlistDetail.PlaylistTracks);
        var playlists = await spotifyService.GetPlayListsFromDatabase(session.CallbackData);
        session.SetContext(Context.OtherPlaylists, playlists.Where(x => x.PlaylistTypeId == 4).ToList());
        session.SetContext(Context.MoodsPlaylists, playlists.Where(x => x.PlaylistTypeId == 1).ToList());
        session.SetContext(Context.GenresPlaylists, playlists.Where(x => x.PlaylistTypeId == 2).ToList());
        session.SetContext(Context.Section, CallBacks.WaitForTrackReview);
        session.SetContext(Context.Index, 0);
        session.SetContext(Context.MessageId, 0);
        EmptyContextForTrack(session);
        await CategorizeTrack(data.ChatId);
    }

    private async Task CategorizeTrack(long chatId)
    {
        var session = sessionService.GetData(chatId);
        var trackIds = (List<string>)session!.Context[Context.TracksIds];
        var section = (string)session.Context[Context.Section];
        var index = (int)session.Context[Context.Index];
        var loadedMessageId = (int)session.Context[Context.MessageId];
        var response = (string)session.Context[Context.Response];
        if (index >= trackIds.Count)
        {
            sessionService.ClearSession(chatId);
            await services.SendMessage(chatId, Messages.PlaylistFinished);
            return;
        }

        var trackId = trackIds[index];
        var fullTrack = await spotifyService.GetTrackFromSpotify(session!.CallbackData, trackId);
        var track = fullTrack.Track;
        var album = fullTrack.TrackAlbum;
        var artists = fullTrack.TrackArtists;
        
        var artistsText = "";
        var noArtistAvailable = true;
        foreach (var artist in artists)
        {
            artistsText += artist.ArtistName;
            if (artist.SpotifyPlayListId is > 0)
            {
                artistsText += "✅\n";
                noArtistAvailable = false;
            }
            else artistsText += "❌\n";
        }

        var moodsText = ((List<string>)session.Context[Context.MoodsSelectedIds]).Aggregate("",
            (current, moodId) =>
                current + ((List<Playlist>)session.Context[Context.MoodsPlaylists]).FirstOrDefault(x =>
                    x.SpotifyId == moodId)!.PlaylistName + "\n");
        
        var genresText = ((List<string>)session.Context[Context.GenresSelectedIds]).Aggregate("",
            (current, moodId) =>
                current + ((List<Playlist>)session.Context[Context.GenresPlaylists]).FirstOrDefault(x =>
                    x.SpotifyId == moodId)!.PlaylistName + "\n");

        var dateAndTime = track.ReleaseDate == null
            ? "Not specified"
            : MainService.ConvertGregorianToJalaliAndGregorian((DateTime)track.ReleaseDate);
        var indexString = $"{index + 1}/{trackIds.Count}";

        var messageP1 = string.Format(Messages.TrackReviewP1, indexString, track.TrackName, dateAndTime, album != null ? album.AlbumName : "None");
        var messageP2 = string.Format(Messages.TrackReviewP2, artistsText);
        var messageP3 = string.Format(Messages.TrackReviewP3, moodsText);
        var messageP4 = string.Format(Messages.TrackReviewP4, genresText);
        
        var message = messageP1 + messageP2;
        List<List<Tuple<string, string>>>? collection = [];
        
        if (session.Action != Actions.AwaitingTrackReview) return;
        switch (section)
        {
            case CallBacks.WaitForTrackReview:
                session.SetContext(Context.TrackId, trackId);
                message += Messages.TrackAsk12;
                collection =
                [
                    [new(Messages.Add, CallBacks.Add), new(Messages.Ignore, CallBacks.Ignore)],
                ];
                break;
            case CallBacks.AcceptToSaveTrack:
                message += Messages.TrackAsk3;
                collection = LoadCollectionForMoodOrGenres(chatId, true);
                break;
            case CallBacks.MoodsSelected:
                message += messageP3 + Messages.TrackAsk4;
                collection = LoadCollectionForMoodOrGenres(chatId, false);
                break;
            case CallBacks.GenresSelected:
                if (!noArtistAvailable)
                {
                    session.SetContext(Context.Section, CallBacks.ArtistsSelected);
                    goto case CallBacks.ArtistsSelected;
                }
                message += messageP3 + messageP4 + Messages.TrackAsk5;
                collection = [
                    [new(Messages.AcceptPersianPlaylists, CallBacks.PersianArtist), new(Messages.AcceptNonPersianPlaylists, CallBacks.NonPersianArtist),],
                    [new(Messages.AcceptNoArtistPlaylists, CallBacks.NoArtist),]
                ];
                break;
            case CallBacks.ArtistsSelected:
                message += messageP3 + messageP4 + Messages.TrackAsk6;
                collection = [[new(Messages.Done, CallBacks.Done), new(Messages.Cancel, CallBacks.Cancel),]];
                break;
            case CallBacks.Saved:
                message += messageP3 + messageP4 + response;
                collection = null;
                session.SetContext(Context.Index, index + 1);
                session.SetContext(Context.MessageId, 0);
                session.SetContext(Context.Section, CallBacks.WaitForTrackReview);
                EmptyContextForTrack(session);
                await CategorizeTrack(chatId);
                break;
        }
        
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.Spotify}|{CallBacks.TrackAction}|");
        if (loadedMessageId != 0)
        {
            await services.EditMessage(chatId, loadedMessageId, message, keyboard);
        }
        else
        {
            var messageId = await services.SendMessage(chatId, message, replyMarkup: keyboard);
            session.SetContext(Context.MessageId, messageId);
        }
    }

    private List<List<Tuple<string, string>>> LoadCollectionForMoodOrGenres(long chatId, bool isMood)
    {
        var session = sessionService.GetData(chatId);
        var (playlistString, selectedIdsString, allSelectedString, callbackPrefix) = isMood
            ? (Context.MoodsPlaylists, Context.MoodsSelectedIds, Context.MoodsAllSelected, CallBacks.MoodsSelection)
            : (Context.GenresPlaylists, Context.GenresSelectedIds, Context.GenresAllSelected, CallBacks.GenreSelection);
        
        var items = (List<Playlist>)session!.Context[playlistString];
        var selectedIds = (List<string>)session.Context[selectedIdsString];
        var allSelected = (bool)session.Context[allSelectedString];

        return services.LoadCollectionMultiSelect(items, selectedIds, allSelected, playlist => playlist.SpotifyId, playlist => playlist.PlaylistName/*, prefixCallbackData: callbackPrefix*/);
    }


    private async Task TrackCategorizingAction(UpdateData data)
    {
        var session = sessionService.GetData(data.ChatId);
        var trackId = (string)session.Context[Context.TrackId];
        var section = (string)session.Context[Context.Section];
        var index = (int)session.Context[Context.Index];

        var value = data.DataSeparated.ElementAtOrDefault(2);
        var (playlistString, selectedIdsString, allSelectedString, nextSection) = ("", "", "", "");

        switch (section)
        {
            case CallBacks.WaitForTrackReview:
            {
                switch (value)
                {
                    case CallBacks.Add:
                        session.SetContext(Context.Section, CallBacks.AcceptToSaveTrack);
                        break;
                    case CallBacks.Ignore:
                        session.SetContext(Context.Section, CallBacks.WaitForTrackReview);
                        session.SetContext(Context.Index, index + 1);
                        EmptyContextForTrack(session);
                        break;
                }
                break;
            }
            case CallBacks.AcceptToSaveTrack:
                (playlistString, selectedIdsString, allSelectedString, nextSection) = 
                    (Context.MoodsPlaylists, Context.MoodsSelectedIds, Context.MoodsAllSelected, CallBacks.MoodsSelected);
                session.SetContext(Context.Section, CallBacks.AcceptToSaveTrack);
                goto case CallBacks.WaitForMoodOrGenre;
            case CallBacks.MoodsSelected:
                (playlistString, selectedIdsString, allSelectedString, nextSection) =
                    (Context.GenresPlaylists, Context.GenresSelectedIds, Context.GenresAllSelected, CallBacks.GenresSelected);
                goto case CallBacks.WaitForMoodOrGenre;
            case CallBacks.WaitForMoodOrGenre:
            {
                var allPlaylist = (List<Playlist>)session.Context[playlistString];
                var selectedIds = (List<string>)session.Context[selectedIdsString];
                switch (value)
                {
                    case CallBacks.MultipleSelectToggle:
                        var playlistId = data.DataSeparated.ElementAtOrDefault(3)!;
                        if (!selectedIds.Remove(playlistId)) selectedIds.Add(playlistId);
                        session.SetContext(selectedIdsString, selectedIds);
                        break;
                    case CallBacks.MultipleSelectAll:
                        selectedIds = new List<string>(allPlaylist.Select(c => c.SpotifyId));
                        session.SetContext(selectedIdsString, selectedIds);
                        session.SetContext(allSelectedString, true);
                        break;
                    case CallBacks.MultipleDeselectAll:
                        selectedIds.Clear();
                        session.SetContext(selectedIdsString, selectedIds);
                        session.SetContext(allSelectedString, false);
                        break;
                    case CallBacks.Done:
                        session.SetContext(Context.Section, nextSection);
                        break;
                    case CallBacks.Cancel:
                        goto case CallBacks.MultipleDeselectAll;
                }
                break;
            }
            case CallBacks.GenresSelected:
            {
                var allPlaylist = (List<Playlist>)session.Context[Context.OtherPlaylists];
                var selectedIds = (List<string>)session.Context[Context.AdditionalPlaylistIds];
                var playlistId = "";
                switch (value)
                {
                    case CallBacks.PersianArtist:
                        playlistId = allPlaylist.FirstOrDefault(x => x.PlaylistName == "Other Persian Artists")!.SpotifyId;
                        break;
                    case CallBacks.NonPersianArtist:
                        playlistId = allPlaylist.FirstOrDefault(x => x.PlaylistName == "Other NonPersian Artist")?.SpotifyId;
                        break;
                    case CallBacks.NoArtist:
                        break;
                }
                if(!string.IsNullOrEmpty(playlistId)) if (!selectedIds.Remove(playlistId)) selectedIds.Add(playlistId);
                session.SetContext(Context.AdditionalPlaylistIds, selectedIds);
                session.SetContext(Context.Section, CallBacks.ArtistsSelected);
                break;
            }
            case CallBacks.ArtistsSelected:
            {
                switch (value)
                {
                    case CallBacks.Done:
                        var additionals = (List<string>)session.Context[Context.AdditionalPlaylistIds];
                        var moods = (List<string>)session.Context[Context.MoodsSelectedIds];
                        var genres = (List<string>)session.Context[Context.GenresSelectedIds];

                        var allPlaylistIds = additionals.Union(moods).Union(genres).ToList();
                        var response = await spotifyService.AddTrackToCollections(session.CallbackData, trackId, allPlaylistIds);
                        session.SetContext(Context.Response, response);
                        session.SetContext(Context.Section, CallBacks.Saved);
                        break;
                    case CallBacks.Cancel:
                        session.SetContext(Context.Section, CallBacks.WaitForTrackReview);
                        EmptyContextForTrack(session);
                        break;
                }
                break;
            }
        }
        await CategorizeTrack(data.ChatId);
    }

    private void EmptyContextForTrack(UserSession session)
    {
        session.SetContext(Context.Response, "");
        session.SetContext(Context.MoodsAllSelected, false);
        session.SetContext(Context.GenresAllSelected, false);
        session.SetContext(Context.MoodsSelectedIds, new List<string>());
        session.SetContext(Context.GenresSelectedIds, new List<string>());
        session.SetContext(Context.AdditionalPlaylistIds, new List<string>());
    }
}
