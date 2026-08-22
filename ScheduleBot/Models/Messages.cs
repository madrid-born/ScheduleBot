using System.Collections.Generic;

namespace ScheduleBot.Models;

public static class Messages
{
    #region Command
    
    public const string Start = "/start";
    
    #endregion
    
    #region MainKeyboard
    
    public const string AboutSymbol = "ℹ️ ";
    public const string About = "About";
    public const string PeriodTrackerSymbol = "🌸 ";
    public const string PeriodTracker = "Period Tracker";
    public const string CartSymbol = "🛒 ";
    public const string Cart = "Cart";
    public const string TransactionSymbol = "🏦 ";
    public const string Transaction = "Transaction";
    public const string SpotifySymbol = "🎵 ";
    public const string Spotify = "Spotify";
    
    #endregion

    #region Universal
    
    public const string Yes = "Yes";
    public const string No = "No";
    public const string PreviousPage = "<<";
    public const string NextPage = ">>";
    public const string All = "All";
    public const string Done = "✅ Done";
    public const string Cancel = "❌ Cancel";
    public const string SelectAll = "☑ Select All";
    public const string DeselectAll = "☐ Deselect All";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Add = "Add";
    public const string Skip = "skip";
    public const string Ignore = "Ignore";
    public const string Split = "Split";
    public const string Welcome = "Welcome to the bot\nyou can choose your action by the keyboard below.";
    public const string NotFound = "Command not found.";
    public const string SomethingWentWrong = "something went wrong.";
    public const string InvalidDate = "Invalid date format. Please use YYYY-MM-DD";
    public const string InvalidInteger = "Invalid Integer format. Please use a Natural Number";
    
    public const string ScrollerAction = "You can type new items to add 🆕\nAnd click on item to remove 🗑\nAnd make sure to press done to submit or cancel to decline your changes ✅ ❌";
    public const string ScrollerActionChanges = "\nAdded:\n\n{0}\n\n\nDeleted:\n{1}\n\n\nAdded then deleted:\n{2}\n\n";
    public const string ScrollerActionSubmitted = "The following changes had been submitted to '{0}' {3} by {1}\n{2}\n";
    public const string ScrollerActionAborted = "The following changes had been declined for '{0}' {2} \n{1}\n";

    
    #endregion
    
    #region Register
    
    public const string AdminApprovalPending = "Your registration is pending for admin approval.";
    public const string NotDefinedUser = "You are not defined as a user here\ndo you wish to register?";
    public const string EnterYourName = "Please enter your Name.";
    public const string EnterYourEmail = "Please enter your Email";
    public const string RegistrationSuccessful = "Registration was successful\nWait for admin approval.";
    public const string AdminMessageTemplate = "User \n\nGuid: `{0}`\nChatId: `{1}`\nName: `{2}`\nEmail: `{3}`\nUsername: {4}\n\nWish To register\nDo you accept?";
    public const string AdminAcceptanceTemplate = "User `{0}` has been {1}";
    public const string UserAcceptanceTemplate = "Admin {0} you";
    
    #endregion
    
    #region CycleTracker
    
    public const string Setup = "Setup period";
    public const string Edit = "Edit period details";
    public const string CurrentStatus = "Current Statuses";
    public const string AddToCycle = "Add Someone To My Cycle Notification";
    public const string JoinToCycle = "Join To Someone Cycle Notification";
    
    public const string EditPeriodLength = "Edit Period Length";
    public const string EditCycleLength = "Edit Cycle Length";
    public const string EditFollowers = "Remove Followers";
    public const string EditFollowing = "Remove Followings";
    public const string EditLastPeriod = "Remove Last Period";
    public const string EditNotify = "Edit Notification method";

    public const string LoadPeriodTracker = "Welcome to the period tracker section.\nWhat do you want to do?";
    public const string SetupTracker = "Please enter the start date of your last period (YYYY-MM-DD):\nBoth Jalali and Gregorian dates would work\nBut remember to enter like 1405-03-07 or 2026-06-05";
    public const string AvailableCycle = "You already have a cycle tracker set up. Use Edit Period to make changes.";
    public const string LastStartChanged = "Last start date was changed successfuly.";
    public const string AskForCycleLength = "What is your average cycle length (days between end of one period till start of the new one)?";
    public const string CycleLengthChanged = "Cycle Length was changed successfuly.";
    public const string AskForPeriodLength = "How many days does your period typically last?";
    public const string PeriodLengthChanged = "Period Length was changed successfuly.";
    public const string AskForNotifyMode = "How often would you like to receive notifications?";
    public static readonly List<string> NotifyModes = ["Never", "Every day", "Weekly", "Start & End only", "3 days before + during period"];
    public const string SetNotifyComplete = "You'll receive notifications\n{0}.";
    public const string SetNotifyCompleteGuest = "You have been successfully added to {0}'s Cycle Notification\n";
    public const string SetupComplete = "Period tracker setup complete!";
    
    public const string CurrentData = "Your current setted data is as below:\nLast Start: {0} \nCycle length: {1} days\nPeriod length: {2} days\nAverage cycle length: {3} days\nAverage period length: {4} days\n\n";
    public const string Followers = "And those who follow you are:\n{0}";
    public const string EditCheck = "What do you want to change\n\n";
    public const string SelectUser = "Select the user you want";
    public const string SelectCycle = "Select the cycle you want";
    public const string RemoveFollowerForOwner = "Succesfuly Removed {0} from cycle";
    public const string RemoveFollowerForReceiver = "{0} removed you from her(their) cycle";
    public const string RemoveFollowingForOwner = "{0} exited from your cycle notification";
    public const string RemoveFollowingForReceiver = "Succesfuly exited {0}'s cycle notification";

    public const string StatusForReceiver = "today is {0}\nAnd {1} is now in this situation\n\n{2}";
    public const string StatusForOwner = "toddway is {0}\nAnd you are now in this situation\n\n{1}";
    public const string DidItStart = "Did your period start?";
    public const string DidItEnd = "Did your period end?";
    public const string HopeTomorrow = "Got it. We'll keep tracking your cycle based on this.";
    public const string SavedData = "Your cycle got restarted successfully.";
    public const string NotifyStart = "{0}'s period just got started.";
    public const string NotifyEnd = "{0}'s period just got ended.";

    public const string ShareCycleId = "Your cycle id is mentioned below\n\n`{0}`\n\nYou can share it with anyone who you want them to get notified.\n\nOr they can simply click on this direct link down here.";
    public const string AskForCycleId = "Please enter the id you received from the person you want to join on their notification.";
    public const string CycleIdIsWrong = "Looks like there is something wrong with your input cycle Id.";
    #endregion
    
    #region CycleStatus
    
    public const string NoCycleData = "No cycle data available (missing last start date).";
    public const string InvalidFutureCycle = "Cycle start date is in the future. Data is invalid.";
    
    public const string EarlyPeriod = "Early period";
    public const string MidPeriod = "Mid period";
    public const string LatePeriod = "Late period";
    public const string FinalPeriod = "Final stage";
    public const string ExtendedPeriod = "Extended bleeding window";
    public const string EarlyPeriodDescription = "Flow typically heavy, cramps more likely. Hormone levels are dropping quickly.";
    public const string MidPeriodDescription = "Flow usually stabilizes. Symptoms vary widely between individuals.";
    public const string LatePeriodDescription = "Flow generally lighter. Body starts transitioning out of menstruation.";
    public const string FinalPeriodDescription = "Light spotting possible. Uterus lining mostly shed.";
    public const string ExtendedPeriodDescription = "This may be spotting or prolonged menstruation.";
    
    public const string SlightlyLate = "slightly late";
    public const string ModeratelyLate = "moderately late";
    public const string SignificantlyLate = "significantly late";
    public const string HighlyIrregular = "highly irregular";
    public const string SlightlyLateReason = "Normal biological variation, stress, sleep changes, or minor hormonal fluctuation.";
    public const string ModeratelyLateReason = "Common causes include stress, illness, hormonal imbalance, or cycle irregularity.";
    public const string SignificantlyLateReason = "This level of delay often indicates cycle irregularity or strong hormonal disruption.";
    public const string HighlyIrregularReason = "Extended delay. Could be data inconsistency or major physiological change.";

    public const string InPeriodTemplate = "{0} (Day {1}/{2}).\n{3}\nEstimated remaining: {4} day(s).";
    public const string LateCycleTemplate = "Cycle is {0} by {1} day(s).\n{2}\nEstimated stability confidence: {3}%.";
    public const string MenstrualPhaseTemplate = "Menstrual phase (Day {0}/{1}).\nExpected window: day 1–{2}.\nUncertainty: ±1 day variation in real cycles.\nRemaining in phase: ~{3} day(s).";
    public const string FollicularPhaseTemplate = "Follicular phase (Day {0}/{1}).\nEstimated ovulation window: day {2}–{3}.\nUncertainty: ±{4} days.\nTime to fertile window: ~{5} day(s).";
    public const string OvulationPhaseTemplate = "Ovulation window (Day {0}/{1}).\nPeak fertility likely around day {2} (±{3}).\nConfidence decreases as you move {4} day(s) away from peak.\nShort fertile window (~3–5 days total).";
    public const string LutealPhaseTemplate = "Luteal phase (Day {0}/{1}).\nMore stable phase biologically (~12–14 days after ovulation).\nExpected variation: ±2–3 days depending on cycle length.\nNext cycle in ~{2} day(s).";
    public const string PremenstrualPhaseTemplate = "Premenstrual phase (Day {0}/{1}).\nHormone drop phase leading into menstruation.\nHigh variability: symptoms may start 3–7 days before cycle.\nNext cycle in ~{2} day(s).";

    #endregion

    #region Cart

    public const string LoadCart = "Welcome to the period tracker section.\nWhat do you want to do?";
    public const string KeyboardProduct = "Product service";
    public const string KeyboardCart = "Cart service";
    public const string KeyboardShow = "Show cart";
    public const string KeyboardAddProduct = "Add Product";
    public const string KeyboardRemoveProduct = "Remove Product";
    public const string KeyboardCreateCart = "Create Cart";
    public const string KeyboardDeleteCart = "Delete Cart";
    public const string KeyboardInviteToCart = "Invite To Cart";
    public const string KeyboardJoinToCart = "Join To Cart";
    
    public const string SelectCart = "Select the cart you want.";
    public const string CartNotFound = "Cart not found.";
    public const string ShowCart = "Items inside Cart \"{0}\":\n\n{1}";
    public const string CartEmpty = "Cart \"{0}\" is empty";
    
    public const string AskCartName = "Enter the Cart name.";
    public const string CartCreated = "Cart \"{0}\" has been created.\nYou can invite others to this cart using this code\n\n`{1}`";
    public const string CartDeleteFail = "Unable to delete cart.";
    public const string CartDeleted = "Cart \"{0}\" deleted by {1}.\nlast instance of it, is as follows.";
    
    public const string AskCartId = "Enter the Cart code that had been shared with you.";
    public const string InviteToCart = "The Invitation code for cart \"{0}\" is as follows.\n\n`{1}`\n\nyou can share it with anyone you want to invite to this cart.\n\nOr they can simply click on this direct link down here.";
    public const string InviteAccepted = "You have been successfuly added to cart \"{0}\"";
    public const string InviteAcceptedOwner = "User {0} had been joined to cart \"{1}\"";
    public const string CartIdFormatFail = "Cart code format is not valid.";
    public const string CartNotExist = "No cart is available with that code.";
    public const string RedundantAccess = "You are already in the mentioned cart.";

    public const string CartLoadFail = "Loading cart failed.";

    #endregion

    #region Transaction

    public const string LoadTransaction = "Welcome to the Transaction section.\nWhat do you want to do?";

    public const string KeyboardWalletManagement = "Wallet Management";
    public const string KeyboardCreateWallet = "Create Wallet";
    public const string KeyboardManageCategories = "Manage Categories";
    public const string KeyboardInviteToWallet = "Invite To Wallet";
    public const string KeyboardAddTransaction = "Add Transaction to Wallet";
    public const string KeyboardGenerateReport = "Generate Report";
    public const string KeyboardManualTransaction = "Manual";
    public const string KeyboardBluTransaction = "Auto from Blu";

    public const string AskWalletName = "Enter the Wallet name.";
    public const string WalletCreated = "Wallet \"{0}\" has been created.\nYou can invite others using this code\n\n`{1}`";
    public const string WalletLoadFail = "Loading wallet failed.";
    public const string WalletIdFormatFail = "Wallet code format is not valid.";
    public const string SelectWallet = "Select the wallet you want.";
    public const string WalletNotFound = "Wallet not found or you do not have access to it.";
    public const string WalletSelected = "Wallet {0} is selected\nYou can choose the action you want from the list below.";
    public const string InviteToWallet = "Invite to wallet \"{0}\" with the below button";
    public const string WalletJoined = "You joined wallet \"{0}\".";
    
    public const string BluFilePrompt = "Send the Blu .xlsx export file now.";
    public const string BluFinished = "All Blu transactions in this file processed.";
    public const string BluReviewP1 = "👉 Index: {0}\n🏧 Type: {1}\n📅 Date and time: {2}\n\n";
    public const string BluReviewP2D = "🟢 Deposit: {0:N0}\n";
    public const string BluReviewP2W = "🔴 Withdraw: {0:N0}\n";
    public const string BluReviewP2 = "💲 Balance after: {0:N0}\n\n";
    public const string BluReviewP3 = "🛂 Description: {0}\n\n";
    public const string BluReviewP4 = "Category: {0}\n";
    public const string BluReviewP5 = "Title: {0}\n";
    public const string BluAsk123 = "\nDo you want to add this transaction?";
    public const string BluAsk1234 = "\nIn how many section you want to split it?";
    public const string BluAsk4 = "\nSelect the category this transaction belonged to";
    public const string BluAsk5 = "\nWrite down the transaction title or skip";
    public const string BluAsk6 = "\nClick to save";
    public const string BluView = "\nTransaction had been saved ✅";
    
    public const string NoCategoryInWallet = "No categories found in this wallet. Please add categories first.";
    public const string ReportCancelled = "Report generation cancelled.";
    public const string ReportCategorySelection = "Select Categories for Report\n\nSelect which categories to include in the report. Click a category to toggle it on/off.\n\nSelected: {0}/{1} categories";
    public const string ReportGenerating = "Generating your report... Please wait.";
    public const string ExcelCaption = "Transaction details in Excel format!";
    public const string ReportReady = "Your wallet report is ready!\n\nWallet: {0}\nGenerated: {1}\nTransactions: {2}";
    
    #endregion
    
    #region Spotify

    public const string LoadSpotify = "Welcome to the Spotify section.\nWhat do you want to do?";
    
    public const string KeyboardCategorizePlaylist = "Categorize Playlist";
    public const string KeyboardNotCategorizePlaylist = "Use Not categorized playlist";
    
    public const string AskForPlaylistId = "Please Enter the playlist ID.";
    public const string StartCategorizing = "Lets start categorizing playlist '{0}' by '{1}' with {2} items inside.";
    public const string PlaylistEmpty = "Playlist '{0}' by '{1}' has no items to categorize.";
    public const string PlaylistFinished = "Categorizing playlist finished.";

    public const string TrackReviewP1 = "👉 Index: {0}\n🎼 Name: {1}\n📅 ReleasedDate: {2}\n🎶 Album: {3}\n\n";
    public const string TrackReviewP2 = "👨‍🎤 Artists: \n{0}\n\n";
    public const string TrackReviewP3 = "Ⓜ️ Moods: \n{0}\n\n";
    public const string TrackReviewP4 = "🗂 Genres: \n{0}\n\n";
    public const string TrackAsk12 = "\nDo you want to add this track?";
    public const string TrackAsk3 = "\nSelect the moods this track belongs to";
    public const string TrackAsk4 = "\nSelect the genres this track belongs to";
    public const string TrackAsk5 = "\nSelect the Artsit(s) section";
    public const string TrackAsk6 = "\nClick to save";

    public const string AcceptPersianPlaylists = "(Persian) Accept Playlists";
    public const string AcceptNonPersianPlaylists = "(NonPersian) Accept Playlists";
    public const string AcceptNoArtistPlaylists = "None";

    #endregion

}

public static class CallBacks
{
    #region Universal

    public const string Yes = "Yes";
    public const string No = "No";
    public const string Add = "Add";
    public const string Skip = "Skip";
    public const string Ignore = "Ignore";
    public const string Split = "Split";
    public const string PreviousPage = "<<";
    public const string NextPage = ">>";
    public const string All = "All";
    public const string Done = "Done";
    public const string Cancel = "Cancel";
    public const string MainSection = "MainSection";
    public const string MultipleSelectToggle = "MST";
    public const string MultipleSelectAll = "MSA";
    public const string MultipleDeselectAll = "MDA";



    #endregion
    
    #region Register

    public const string Register = "Register";
    public const string AskToRegister = "AskToRegister";
    public const string AcceptRegister = "AcceptRegister";
    public const string RejectRegister = "RejectRegister";
    
    #endregion
    
    #region CycleTracker

    public const string Cycle = "Cycle";
    public const string SetNotifyMode = "SetNotifyMode";
    public const string EditSection = "EditSection";
    public const string EditPeriodLength = "EditPeriodLength";
    public const string EditCycleLength = "EditCycleLength";
    public const string EditFollowers = "EditFollowers";
    public const string EditFollowing = "EditFollowing";
    public const string EditLastPeriod = "EditLastPeriod";
    public const string EditNotify = "EditNotify";
    public const string RemoveFollowing = "RemoveFollowing";
    public const string RemoveFollower = "RemoveFollower";
    
    public const string Setup = "Setup";
    public const string Edit = "Edit";
    public const string CurrentStatus = "CurrentStatus";
    public const string ReportStart = "ReportStart";
    public const string ReportEnd = "ReportEnd";
    public const string AddToCycle = "AddToCycle";
    public const string JoinToCycle = "JoinToCycle";

    #endregion

    #region Cart

    public const string Cart = "Cart";
    public const string ProductService = "ProductService";
    public const string CartService = "CartService";
    public const string Show = "Show";
    public const string ProductAction = "ProductAction";
    public const string AddProduct = "AddProduct";
    public const string RemoveProduct = "RemoveProduct";
    public const string CreateCart = "CreateCart";
    public const string DeleteCart = "DeleteCart";
    public const string InviteToCart = "InviteToCart";
    public const string JoinToCart = "JoinToCart";
    
    #endregion

    #region Transaction

    public const string Transaction = "Transaction";
    public const string CreateWallet = "CW";
    public const string WalletManagement = "WM";
    public const string InviteToWallet = "ITW";
    public const string AddTransaction = "AT";
    // public const string GenerateReport = "GR";
    public const string DeleteWallet = "DW";
    public const string ManageCategories = "MC";
    public const string CategoryAction = "CA";
    public const string SelectCategory = "SC";
    public const string ManualTransaction = "MT";
    public const string BluTransaction = "BT";
    public const string BluAction = "BA";
    public const string JoinWallet = "JW";

    public const string WaitForReview = "WaitForReview";
    public const string SelectSplitCount = "SelectSplitCount";
    public const string AcceptToSave = "AcceptToSave";
    public const string CategorySelected = "categorySelected";
    public const string TitleSelected = "TitleSelected";
    public const string Saved = "Saved";
    
    public const string GenerateReport = "GR";
    public const string ReportContinue = "RD";
    public const string ReportGenerate = "RG";

        
    #endregion
    
    #region Spotify

    public const string Spotify = "Spotify";
    public const string CategorizePlaylist = "CP";
    public const string NotCategorizePlaylist = "NCP";
    
    public const string TrackAction = "TA";
    public const string WaitForTrackReview = "WFR";
    public const string AcceptToSaveTrack = "AST";
    public const string MoodsSelected = "MS";
    public const string GenresSelected = "GS";
    public const string WaitForMoodOrGenre = "WFMG";
    public const string ArtistsSelected = "AS";
    public const string PersianArtist = "PA";
    public const string NonPersianArtist = "NPA";
    public const string NoArtist = "NA";
    public const string MoodsSelection = "MSN";
    public const string GenreSelection = "GSN";
    #endregion
}

public static class Actions
{
    public const string AwaitingProductActions = "AwaitingProductActions";
    public const string AwaitingCategoryName = "AwaitingCategoryName";
    public const string AwaitingBluFile = "AwaitingBluFile";
    public const string AwaitingBluReview = "AwaitingBluReview";
    public const string BuildingReport = "BuildingReport";
    
    public const string LoadSpotify = "AwaitingPlaylistId";
    public const string AwaitingPlaylistId = "AwaitingPlaylistId";
    public const string AwaitingTrackReview = "CategorizePlaylist";
}

public static class Context
{
    public const string Tps = "TransactionProcesses";
    public const string Wallet = "Wallet";
    public const string Index = "Index";
    public const string Section = "Section";
    public const string MessageId = "MessageId";

    public const string ReportWalletId = "ReportWalletId";
    public const string ReportSelectedCategories = "ReportSelectedCategories";
    public const string ReportAllSelected = "ReportAllSelected";
    public const string ReportMessageId = "ReportMessageId";

    public const string Response = "Response";
    public const string TrackId = "TrackId";
    public const string TracksIds = "TracksIds";
    public const string OtherPlaylists = "OtherPlaylists";
    public const string MoodsPlaylists = "MoodsPlaylists";
    public const string GenresPlaylists = "GenresPlaylists";
    public const string MoodsSelectedIds = "MoodsSelectedIds";
    public const string MoodsAllSelected = "MoodsAllSelected";
    public const string GenresSelectedIds = "GenresSelectedIds";
    public const string GenresAllSelected = "GenresAllSelected";
    public const string AdditionalPlaylistIds = "AdditionalPlaylistIds";
}

public static class Files
{
    public const string PdfWalletReport = "WalletReport_{0}.pdf";
    public const string ExcelWalletReport = "WalletDetailedTransactions_{0}.xlsx";
}

public static class SpotifyApi
{
    public const string NotCategorizedPlaylistId = "21SgdiMHUdSgXrZpnuqc56";
    public const string ApiPassword = "VeryStrongPasswordForAuthentication";
    public const string ApiCallSignIn = "/api/Authorization/SignIn";
    public const string ApiCallGetPlaylist = "api/PlayList/GetPlayListFromSpotify";
    public const string ApiCallGetPlaylists = "/api/PlayList/GetPlayListsFromDatabase";
    public const string ApiCallGetTrack = "api/PlayList/GetTrackFromSpotify";
    public const string ApiCallAddTrack = "api/PlayList/AddTrackToCollection";
}