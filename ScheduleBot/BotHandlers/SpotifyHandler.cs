using ScheduleBot.Models;
using ScheduleBot.Services;

namespace ScheduleBot.BotHandlers;

public class SpotifyHandler(
    SpotifyService spotifyService,
    UserSessionService sessionService,
    MainService services)
{
    private static readonly string[][] Moods =
    [
        ["Taryak", "Sat By The Fire", "Directly To IR"],
        ["I Wanna Cry", "Depression Is My Passion", "Sit And Chill"],
        ["Just A Chemical Reaction", "Fuck The System", "Hits"],
        ["Sweat Dreams", "Life Sucks", "Serious Party"],
        ["Tell Me A Story", "Beautiful Day", "Beef Is Not Just Meat"]
    ];

    private static readonly string[][] Genres =
    [
        ["Persian HipHop", "Persian Classic", "Persian Ultra Classic"],
        ["Rock", "Soft Rock", "Rock N Roll"],
        ["Indie Rock", "Metal", "Trash Metal"],
        ["Persian Rock", "American HipHop", "Pop"],
        ["Empty Notes SoundTrack", "Movies And Games SoundTrack", "Female Vocalist"]
    ];

    private static readonly IReadOnlyDictionary<string, string> PlaylistIds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Taryak"] = "0AecVqw0GwwM5PXZD6Jhkp", ["Sat By The Fire"] = "5qSlEpa6dEurWAIR3Iwpy7", ["Directly To IR"] = "4a77RaEYNILmT9OJTYBIAC",
        ["I Wanna Cry"] = "4PWSHhEzlzvrBXQrkzZOpO", ["Depression Is My Passion"] = "5tIuZLcWEdfbDO2vJD0JAP", ["Sit And Chill"] = "4gALtkA6vAyVBVZ8LVlYT7",
        ["Just A Chemical Reaction"] = "4YNTnobR4uHuAaeJftFffy", ["Fuck The System"] = "1i74rvqryggEZgzQ9RJ1oO", ["Hits"] = "2ltSuxm3SflLBYDmXiudDi",
        ["Sweat Dreams"] = "25yB4arw2tpDwxF3fv5xCa", ["Life Sucks"] = "66pf6tMhNw0uawI4kcUggX", ["Serious Party"] = "1YZySvWnCH760zgpt4cPT7",
        ["Tell Me A Story"] = "0kaY6jxq6VK4mk8c4rvPpz", ["Beautiful Day"] = "7a44WRB7Jef3BeKTFPPBAg", ["Beef Is Not Just Meat"] = "0YlfrCAKzLzWEN5635S2yJ",
        ["Persian HipHop"] = "2x3OSei2kDxK5xhvSGepmu", ["Persian Classic"] = "1VQFtYGNQmrjg550VuN9Os", ["Persian Ultra Classic"] = "4esx5g2oX2GbdIojpB4HzR",
        ["Rock"] = "29Kb9ufLj857WZZh4c8pOs", ["Soft Rock"] = "3UOdWux3HpM08EDe3sYIa4", ["Rock N Roll"] = "48zpBLPoDEsJwkvCsNiFfG",
        ["Indie Rock"] = "0FnOUaEIjzdLlq3qqT1lEY", ["Metal"] = "08amo8Z86PzmzpkdyVeSWo", ["Trash Metal"] = "7JQDv5L1u77L3jj9JzR3nG",
        ["Persian Rock"] = "0244kdsnXGPE1KiMah5grS", ["American HipHop"] = "1c90hjQKekJgtWka5xapKc", ["Pop"] = "3uWFnsg83jAcuyeHn9ePYz",
        ["Empty Notes SoundTrack"] = "1vGsehpqi1yA0jomB09FX5", ["Movies And Games SoundTrack"] = "5hqy47JU2Q8NzAMss2PlVo", ["Female Vocalist"] = "6SRqiXwf4bQlrMlb904pO1",
        ["Ali Sorena"] = "4ZLr2eoSXUB0l2yTzzZnwS", ["Farshad"] = "0amOkqAdK9DnmDEgSxH8Hf", ["Mc Tes"] = "79uRePssbNrU05kmM6Qfp1", ["Naghsh"] = "4tEW77hpLAe30i1aTSDuBV",
        ["Ben"] = "1MHo5Vi5xonFcUxXAnMmoQ", ["Bamdad"] = "2Fa0eWTLCtA6T23Mk1QAlj", ["Daygard"] = "1e9mQbHVSxVDqOgprODdhy", ["Soall"] = "2jiVLRaPOTOqoFizH5svpK",
        ["Kaboos"] = "6sqiOtVhP2zA9wdlr2UaEu", ["Safir"] = "01GPv6XMWf1XWz0D9FHTHc", ["Rokh"] = "1SW4vyoEPSuP1VnEV94IRY", ["Sezavar"] = "5JKJOkAjuBF8M1ySbMUPdW",
        ["Bahram"] = "0fCMWJriqifNiwaNt7sjio", ["Navid"] = "5Sx8NTPMp0CZZDMW84G9yK", ["Rez"] = "3bpYvtnwyufeQxo8p96MQB", ["Hichkas"] = "2rnznqJSuXjNmyih5NIeWa",
        ["Quf"] = "1Ym1opKGJfEzBZXfLVfOry", ["Fadaei"] = "4IVILUBPa2Jom9fp5LPxyc", ["Shapur"] = "3RYNAsJFKlILk726kc25Fv", ["Hamed Slash"] = "7jsmstzMOdiQJHyyou8RE0",
        ["Reza Pishro"] = "1dwX6q1S2XXhSGM6XPGLKa", ["Ho3ein"] = "2ty7WsOm5h1xpi8NaAqWmp", ["Sadegh"] = "5onnI39zx5OFq1gIIdtNjB", ["Shayea"] = "7z7OAhhLk6YAXGHE7oOCGe",
        ["Shahin Najafi"] = "5PaCXgb5D5JOM0ZnL9u4r5", ["Yas"] = "5zMSIMGyLfpm3xsh9wiOGs", ["SoelChigini"] = "1HCmwxw1mV4qwzzUmVb9zm", ["Naaji"] = "0nKvw0rmjOtzEQpxkXkWdq",
        ["Ahood"] = "3MpGFQO5VnuAphILc79s31", ["Hiphopologist"] = "02zbgydLkBempUaxUQd29r", ["Banan"] = "6o4gFki5mys2pdGc0C82Xt",
        ["Mohammad-Reza Shajarian"] = "4KFHa0lHMOj0dkPeJmZncn", ["Homayoun Shajarian"] = "4KFHa0lHMOj0dkPeJmZncn", ["Farhad Mehrad"] = "4OkcJXPSaf9u5nVwyQObSv",
        ["Hayedeh"] = "3lGocTcJzwH0AvfyebrWm1", ["Ebi"] = "5aKtj3JJgLd1AX94VJWUHY", ["Dariush"] = "2NBz69vz7grjalqMdO2J41", ["Fereidoon Foroughi"] = "6SsBT5eiPumt6f8yT2Aqf8",
        ["Fereydoon Foroughi"] = "6SsBT5eiPumt6f8yT2Aqf8", ["Parastoo Ahmadi"] = "5dFgDwfThzdbCIrXnAHhbx", ["Az Shanbe"] = "7rz7MtMZQbB2cciCGhZx88",
        ["Moody Moussavi"] = "0LK9xbwJCxMZuaQg4gtegL", ["B-Band"] = "1hXPzFCNozjASslwJMeCc3", ["Architects"] = "5uWHivxFLz9LmpqacIhyhG",
        ["Cassyette"] = "2lIA869v3xVWQjzSfClWQI", ["Bring Me The Horizon"] = "5i2zeH5o3KEo7kaoUmtkDX", ["System Of A Down"] = "7INjceX35qNPV1JOKOjtUf",
        ["Metallica"] = "6OscXI89zOOVsLHNdzc3Ng", ["Five Finger Death Punch"] = "70c8nLrI0pRIlZEZOxHBk4", ["Halestorm"] = "2vovqXgMy0dqwVq9IREqYQ",
        ["Kami Kehoe"] = "6otJdRvHL46J6SsSOa0t3f", ["Linkin Park"] = "3N8UuAPoTDlGI6BbLkHxSP", ["TOOL"] = "1LD15Ffadc4rJvQi1RVo47",
        ["Queen"] = "3elhh5SVn04V8ojeQU6yHD", ["The Score"] = "3gBf3ZidH0wKAzAfZaCaa9", ["Imagine Dragons"] = "5YYy4lPdaKgeT0cjjTBLYP",
        ["Måneskin"] = "7HitZa0NYuin5s2csRyQRa", ["Arctic Monkeys"] = "1CA28XILPxF0oMJeGYmYPI", ["Bec Lauder and The Noise"] = "0JwRG3Asb4qwRQIHBi1L4s",
        ["Dea Matrona"] = "25qlSbOk59zSYUINbVxDWQ", ["U2"] = "2YqwXmmTSwXg23aeorwtvw", ["Red Hot Chili Peppers"] = "2pqV7XSigQwTFLi2oX1cPg",
        ["Pink Floyd"] = "3VKEPtvrzFno8To791jnTd", ["Twenty One Pilots"] = "2vKV0DUYCQ3s6ssmu9iMxp", ["Eminem"] = "5fiEyDRixTbbrivcOsYuWO",
        ["NF"] = "4ONAxDnusJwhg0nYahRUGT", ["Jacob Lee"] = "6ARxHWqFz2comSziVAu5vs",
        ["Other Persian Artists"] = "3mdVjPbrU8Gz7XxNY5JeS0", ["Other NonPersian Artists"] = "4vL8aklaP4kkNhY4x6IMZ3", ["All Songs"] = "0R37XmZXLrE7zgAFYvIfdr"
    };

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
                // await LoadNextTrack(data);
                break;
            
            
            
            
            case CallBacks.AcceptTrack:
                await ShowMoods(data);
                break;
            case CallBacks.RejectTrack:
                if (TryGetSession(data, out _))
                {
                    sessionService.ClearSession(data.ChatId);
                    await services.EditMessage(data.ChatId, data.MessageId, Messages.TrackRejected);
                }
                break;
            case CallBacks.SpotifyMood:
                await HandleCategory(data, Moods, Context.SpotifyMoods, Messages.SelectSpotifyMoods, CallBacks.SpotifyMood, ShowGenres);
                break;
            case CallBacks.SpotifyGenre:
                await HandleCategory(data, Genres, Context.SpotifyGenres, Messages.SelectSpotifyGenres, CallBacks.SpotifyGenre, ShowPlaylistAcceptance);
                break;
            case CallBacks.SpotifyPlaylist:
                await FinishCategorization(data);
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
        if (session == null) return;
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
        session.SetContext(Context.MoodsPlaylists, playlists.Where(x => x.PlaylistTypeId == 1));
        session.SetContext(Context.GenresPlaylists, playlists.Where(x => x.PlaylistTypeId == 2));
        session.SetContext(Context.Index, 0);
        session.SetContext(Context.MessageId, 0);
        await CategorizeTrack(data.ChatId);
    }

    private async Task CategorizeTrack(long chatId)
    {
        var session = sessionService.GetData(chatId);
        var trackIds = (List<string>)session!.Context[Context.TracksIds];
        var section = (string)session.Context[Context.Section];
        var index = (int)session.Context[Context.Index];
        var loadedMessageId = (int)session.Context[Context.MessageId];
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
        
        var dateAndTime = MainService.ConvertGregorianToJalaliAndGregorian((DateTime)track.ReleaseDate!);
        var indexString = $"{index + 1}/{trackIds.Count}";

        var messageP1 = string.Format(Messages.TrackReviewP1, indexString, track.TrackName, dateAndTime, track.isPlayable ? "True" : "Not Now", album != null ? album.AlbumName : "None");
        var messageP2 = string.Format(Messages.TrackReviewP2, artistsText);
        var messageP3 = string.Format(Messages.TrackReviewP3, string.Join("\n", artists));
        var messageP4 = string.Format(Messages.TrackReviewP4, string.Join("\n", artists));
        
        var message = messageP1 + messageP2;
        List<List<Tuple<string, string>>>? collection = [];
        
        if (session.Action != Actions.AwaitingTrackReview) return;
        switch (section)
        {
            case CallBacks.WaitForTrackReview:
                session.SetContext(Context.MoodsSelectedIds, new List<string>());
                session.SetContext(Context.GenresSelectedIds, new List<string>());
                session.SetContext(Context.MoodsAllSelected, false);
                session.SetContext(Context.GenresAllSelected, false);
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
                message += messageP3 + messageP4 + Messages.TrackView;
                collection = null;
                session.SetContext(Context.Index, index + 1);
                session.SetContext(Context.MessageId, 0);
                session.SetContext(Context.Section, CallBacks.WaitForTrackReview);
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
        if (session == null) return;
        var section = (string)session.Context[Context.Section];
        var index = (int)session.Context[Context.Index];
        var messageId = (int)session.Context[Context.MessageId];

        var value = data.DataSeparated.ElementAtOrDefault(2);
        var (playlistString, selectedIdsString, allSelectedString, nextSection) = ("", "", "", "");

        switch (section)
        {
            case CallBacks.WaitForTrackReview:
                switch (value)
                {
                    case CallBacks.Add:
                        session.SetContext(Context.Section, CallBacks.AcceptToSaveTrack);
                        break;
                    case CallBacks.Ignore:
                        session.SetContext(Context.Section, CallBacks.WaitForTrackReview);
                        session.SetContext(Context.Index, index + 1);
                        break;
                }
                break;
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
                        await services.DeleteMessage(data.ChatId, messageId);
                        session.SetContext(Context.Section, nextSection);
                        break;
                    case CallBacks.Cancel:
                        goto case CallBacks.MultipleDeselectAll;
                }
                break;
            //todo : continue here
            case CallBacks.GenresSelected:
                // if (!noArtistAvailable) goto case CallBacks.ArtistsSelected;
                // message += messageP3 + messageP4 + Messages.TrackAsk5;
                // collection = [
                //     [new(Messages.AcceptPersianPlaylists, CallBacks.PersianArtist), new(Messages.AcceptNonPersianPlaylists, CallBacks.NonPersianArtist),],
                //     [new(Messages.AcceptNoArtistPlaylists, CallBacks.NoArtist),]
                // ];
                break;
            case CallBacks.ArtistsSelected:
                // switch (value)
                // {
                //     case CallBacks.Done:
                //         await tServices.AddTransaction(data.ChatId, walletId, transactionProcess);
                //         session.SetCallBack(CallBacks.Saved);
                //         break;
                //     case CallBacks.Cancel:
                //         transactionProcess.CategoryId = Guid.Empty;
                //         transactionProcess.CategoryName = "";
                //         transactionProcess.Title = "";
                //         session.SetCallBack(CallBacks.WaitForReview);
                //         break;
                // }
                break;
            case CallBacks.Saved:
                // message += messageP3 + messageP4 + Messages.TrackView;
                // collection = null;
                // session.SetContext(Context.Index, index + 1);
                // session.SetContext(Context.MessageId, 0);
                // session.SetContext(Context.Section, CallBacks.WaitForTrackReview);
                // await CategorizeTrack(chatId);
                break;
        }
        await CategorizeTrack(data.ChatId);
    }


    private async Task LoadNextTrack(UpdateData data)
    {
        var track = await spotifyService.GetNextTrackAsync();
        if (track == null)
        {
            await services.EditMessage(data.ChatId, data.MessageId, Messages.NoSpotifyTracks);
            return;
        }

        var collection = new List<List<Tuple<string, string>>>
        {
            new()
            {
                new(Messages.AcceptTrack, CallBacks.AcceptTrack),
                new(Messages.RejectTrack, CallBacks.RejectTrack)
            }
        };
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.Spotify}|");
        var messageId = await services.SendMessage(data.ChatId, BuildTrackMessage(track), replyMarkup: keyboard);

        sessionService.SetData(data.ChatId, Actions.CategorizingSpotifyTrack, CallBacks.Spotify);
        var session = sessionService.GetData(data.ChatId)!;
        session.SetContext(Context.SpotifyTrack, track);
        session.SetContext(Context.SpotifyMessageId, messageId);
        session.SetContext(Context.SpotifyMoods, new HashSet<string>(StringComparer.Ordinal));
        session.SetContext(Context.SpotifyGenres, new HashSet<string>(StringComparer.Ordinal));
    }

    private async Task ShowMoods(UpdateData data)
    {
        if (!TryGetSession(data, out var session)) return;
        await EditCategory(data, session, Moods, Context.SpotifyMoods, Messages.SelectSpotifyMoods, CallBacks.SpotifyMood);
    }

    private async Task HandleCategory(UpdateData data, string[][] options, string contextKey, string heading, string callback,
        Func<UpdateData, Task> onDone)
    {
        if (!TryGetSession(data, out var session) || data.DataSeparated.Count < 3) return;

        var selection = data.DataSeparated[2];
        if (selection == CallBacks.Done)
        {
            await onDone(data);
            return;
        }

        var selected = GetSelection(session, contextKey);
        if (selection == CallBacks.Reset) selected.Clear();
        else if (options.SelectMany(row => row).Contains(selection))
        {
            if (!selected.Add(selection)) selected.Remove(selection);
        }
        else return;

        await EditCategory(data, session, options, contextKey, heading, callback);
    }

    private async Task ShowGenres(UpdateData data)
    {
        if (!TryGetSession(data, out var session)) return;
        await EditCategory(data, session, Genres, Context.SpotifyGenres, Messages.SelectSpotifyGenres, CallBacks.SpotifyGenre);
    }

    private async Task EditCategory(UpdateData data, UserSession session, string[][] options, string contextKey, string heading, string callback)
    {
        var track = (SpotifyTrack)session.Context[Context.SpotifyTrack];
        var selected = GetSelection(session, contextKey);
        var collection = options
            .Select(row => row.Select(option => new Tuple<string, string>(selected.Contains(option) ? $"✅ {option}" : option, option)).ToList())
            .ToList();
        collection.Add([new(Messages.Done, CallBacks.Done), new(Messages.Reset, CallBacks.Reset)]);

        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.Spotify}|{callback}|");
        await services.EditMessage(data.ChatId, data.MessageId, $"{heading}\n\n{BuildTrackDetails(track)}", keyboard);
    }

    private async Task ShowPlaylistAcceptance(UpdateData data)
    {
        if (!TryGetSession(data, out var session)) return;

        var track = (SpotifyTrack)session.Context[Context.SpotifyTrack];
        var knownArtists = track.Artists.Where(PlaylistIds.ContainsKey).ToList();
        var allArtistsUnknown = knownArtists.Count == 0;
        session.SetContext(Context.SpotifyKnownArtists, knownArtists);

        List<List<Tuple<string, string>>> collection = allArtistsUnknown
            ?
            [
                [new(Messages.AcceptPersianPlaylists, CallBacks.PersianArtist), new(Messages.AcceptNonPersianPlaylists, CallBacks.NonPersianArtist)],
                [new(Messages.AcceptNoArtistPlaylists, CallBacks.NoArtist), new(Messages.RejectPlaylists, CallBacks.RejectTrack)]
            ]
            :
            [
                [new(Messages.AcceptPlaylists, CallBacks.NoArtist), new(Messages.RejectPlaylists, CallBacks.RejectTrack)]
            ];

        var artistLines = string.Join('\n', track.Artists.Select(artist => $"{EscapeMarkdown(artist)} {(PlaylistIds.ContainsKey(artist) ? "✅" : "❌")}"));
        var keyboard = services.CreateKeyboard(inlineCollection: collection, callBackStart: $"*{CallBacks.Spotify}|{CallBacks.SpotifyPlaylist}|");
        await services.EditMessage(data.ChatId, data.MessageId, $"{Messages.AcceptSpotifyPlaylists}\n\n{BuildTrackDetails(track)}\n\nArtists:\n{artistLines}", keyboard);
    }

    private async Task FinishCategorization(UpdateData data)
    {
        if (!TryGetSession(data, out var session) || data.DataSeparated.Count < 3) return;

        var choice = data.DataSeparated[2];
        if (choice == CallBacks.RejectTrack)
        {
            sessionService.ClearSession(data.ChatId);
            await services.EditMessage(data.ChatId, data.MessageId, Messages.TrackRejected);
            return;
        }

        var track = (SpotifyTrack)session.Context[Context.SpotifyTrack];
        var playlistIds = new List<string> { PlaylistIds["All Songs"] };
        playlistIds.AddRange(GetSelection(session, Context.SpotifyMoods).Select(selection => PlaylistIds[selection]));
        playlistIds.AddRange(GetSelection(session, Context.SpotifyGenres).Select(selection => PlaylistIds[selection]));

        if (choice == CallBacks.PersianArtist) playlistIds.Add(PlaylistIds["Other Persian Artists"]);
        else if (choice == CallBacks.NonPersianArtist) playlistIds.Add(PlaylistIds["Other NonPersian Artists"]);
        else playlistIds.AddRange(((List<string>)session.Context[Context.SpotifyKnownArtists]).Select(artist => PlaylistIds[artist]));

        await spotifyService.AddTrackToCollectionsAsync(track.Id, playlistIds);
        sessionService.ClearSession(data.ChatId);
        await services.EditMessage(data.ChatId, data.MessageId, $"{Messages.TrackCategorized}\n\n{BuildTrackDetails(track)}");
    }

    private bool TryGetSession(UpdateData data, out UserSession session)
    {
        session = sessionService.GetData(data.ChatId)!;
        return session is { Action: Actions.CategorizingSpotifyTrack } &&
               session.Context.TryGetValue(Context.SpotifyMessageId, out var messageId) &&
               messageId is int id && id == data.MessageId;
    }

    private static HashSet<string> GetSelection(UserSession session, string key)
        => (HashSet<string>)session.Context[key];

    private static string BuildTrackMessage(SpotifyTrack track)
        => $"{Messages.AcceptSpotifyTrack}\n\n{BuildTrackDetails(track)}";

    private static string BuildTrackDetails(SpotifyTrack track)
        => $"Track Id: {EscapeMarkdown(track.Id)}\nTrack Name: {EscapeMarkdown(track.Name)}\nAlbum Name: {EscapeMarkdown(track.AlbumName ?? "Unknown")}\nArtists:\n{string.Join('\n', track.Artists.Select(EscapeMarkdown))}";

    private static string EscapeMarkdown(string value)
        => value.Replace("\\", "\\\\")
            .Replace("_", "\\_")
            .Replace("*", "\\*")
            .Replace("[", "\\[")
            .Replace("`", "\\`");

}
