using System.Collections.Generic;

namespace ScheduleBot.Models;

public static class Pattern
{
    public const string BluPattern = @"14\d{12}\.xlsx";
} 

public static class Messages
{
    #region Command
    
    public const string Start = "/start";
    
    #endregion
    
    #region MainKeyboard
    
    public const string About = "ℹ️ About";
    public const string PeriodTracker = "🌸 Period Tracker";
    public const string Cart = "🛒 Cart";
    public const string Transaction = "🏦 Wallet";
    public const string Notification = "⏰ Notification Cneter";
    public const string Metro = "🚇 Tehran Metro";
    public const string Mapify = "📍 Mapify";
    
    public const string Spotify = "🎵 Spotify";
    
    #endregion

    #region Mapify

    public const string MapifyWelcome = "Welcome to Mapify. Create shared maps for your favorite places, then add categories and locations.";
    public const string MapifyMyMaps = "My Maps";
    public const string MapifyCreateMap = "Create Map";
    public const string MapifyAskMapName = "Enter a name for your new map.";
    public const string MapifyCreated = "Map \"{0}\" has been created. You can share it using its map menu.\n\nMap code: `{1}`";
    public const string MapifyNoMaps = "You do not have access to any maps yet. Create one or join a shared map.";
    public const string MapifySelectMap = "Select a map.";
    public const string MapifySelected = "Map \"{0}\" is selected. What would you like to do?";
    public const string MapifyNotFound = "That map does not exist or you do not have access to it.";
    public const string MapifyShare = "Share Map";
    public const string MapifyInvite = "Share this button to invite someone to \"{0}\". Members can add categories and locations.";
    public const string MapifyJoinButton = "Join this map";
    public const string MapifyJoined = "You joined the shared map.";
    public const string MapifyJoinFailed = "This invitation is invalid, expired, or you already have access to the map.";
    public const string MapifyManageCategories = "Manage Categories";
    public const string MapifyCategoryManagement = "Type a category to add it. Tap a category to mark it for removal, then press Done to save.";
    public const string MapifyCategoryChangesSaved = "Map categories have been saved.";
    public const string MapifyCategoryAddFailed = "I could not add that category. Make sure it is not blank and you still have access to the map.";
    public const string MapifyNoCategories = "This map has no categories yet. Add at least one from Manage Categories first.";
    public const string MapifyAddLocation = "Add Location";
    public const string MapifyEditLocation = "Edit Location";
    public const string MapifyAskLocationToEditPin = "Send a pin near the location you want to edit. I will show the closest saved places first.";
    public const string MapifySelectLocationToEdit = "Select a location to edit. Places are ordered nearest first.";
    public const string MapifyNoLocations = "This map has no locations yet.";
    public const string MapifyLocationEditor = "<b>{0}</b>\n\nCategories: {1}\nVisited: {2}\nScore: {3}\nDescription: {4}";
    public const string MapifyEditName = "Edit name";
    public const string MapifyEditCategories = "Edit categories";
    public const string MapifyEditPin = "Edit pin";
    public const string MapifyEditDescription = "Edit description";
    public const string MapifyEditVisited = "Edit visited status";
    public const string MapifyEditScore = "Edit score";
    public const string MapifyEditClose = "Close";
    public const string MapifyAskEditedLocationName = "Send the new name for this place.";
    public const string MapifyAskEditedLocationPin = "Send the new exact pin for this place.";
    public const string MapifyAskEditedLocationDescription = "Send a new Instagram link or description. Send ‘skip’ to remove the current description.";
    public const string MapifyLocationUpdated = "Location updated.";
    public const string MapifyLocationUpdateFailed = "I could not update that location. It may have been removed or its categories changed; please try again.";
    public const string MapifySelectLocationCategories = "Select every category for this location.\n\nSelected: {0}/{1}";
    public const string MapifyAskLocationName = "What is this place called?";
    public const string MapifyAskLocationPin = "Send the exact pin for this place.";
    public const string MapifyAskLocationDescription = "Send an Instagram link or any description for this place. Send ‘skip’ if you do not want to add one.";
    public const string MapifyAskVisited = "Have you visited this place?";
    public const string MapifyAskScore = "What score would you give it out of 10? Decimals are fine.";
    public const string MapifyInvalidScore = "Enter a score from 0 to 10.";
    public const string MapifyLocationSaved = "Location saved to your map.";
    public const string MapifyLocationSaveFailed = "I could not save that location. Its categories may have changed; please try again.";
    public const string MapifySuggest = "Suggest a Location";
    public const string MapifySelectSuggestionCategories = "Select the kinds of places you want. A location can match any selected category.\n\nSelected: {0}/{1}";
    public const string MapifyAskSuggestionPin = "Send your current pin and I will suggest nearby matching places.";
    public const string MapifyNoSuggestions = "There are no saved locations matching those categories.";
    public const string MapifyNoNearbySuggestions = "There are no matching saved locations within 10 km of your pin.";
    public const string MapifySuggestions = "<b>Nearby suggestions</b>\n\n{0}";
    public const string MapifySeeMoreSuggestions = "Would you like to see more nearby suggestions?";
    public const string MapifyAllSuggestionsShown = "Those are all the nearby matching places.";
    public const string MapifySuggestionsStopped = "Okay — I stopped showing suggestions.";
    public const string MapifyChooseAtLeastOneCategory = "Choose at least one category first.";
    public const string MapifySendPin = "📍 Send pin";
    public const string MapifyCancelled = "Mapify action cancelled.";

    #endregion

    #region Metro

    public const string MetroNavigation = "Navigation";
    public const string MetroStationDetails = "Show station detail";
    public const string MetroWelcome = "Welcome to Tehran Metro. What would you like to do?";
    public const string MetroSelectLine = "Choose a Metro line, then I will show its stations.";
    public const string MetroSelectLineStation = "Choose a station on Line {0}.";
    public const string MetroAskOriginLocation = "Send your starting point with Telegram's location button.";
    public const string MetroAskDestinationLocation = "Now send your destination with Telegram's location button.";
    public const string MetroSendLocation = "📍 Send location";
    public const string MetroCalculating = "Calculating the best Metro route and next available trains...";
    public const string MetroRouteNotFound = "I could not find a usable Metro route for those locations.";
    public const string MetroStationNotFound = "No matching Metro station was found.";
    public const string MetroCancelled = "Metro request cancelled.";

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
    public const string EnterDate = "Please enter the ";
    public const string EnterDateDetail = " :\nBoth Jalali and Gregorian dates would work\nBut remember to enter in this format (YYYY-MM-DD) like 1405-03-07 or 2026-06-05";

    public const string ScrollerAction = "You can type new items to add 🆕\nAnd click on item to remove 🗑\nAnd make sure to press done to submit or cancel to decline your changes ✅ ❌";
    public const string ScrollerActionChanges = "\nAdded:\n\n{0}\n\n\nDeleted:\n{1}\n\n\nAdded then deleted:\n{2}\n\n";
    public const string ScrollerActionSubmitted = "The following changes had been submitted to '{0}' {3} by {1}\n{2}\n";
    public const string ScrollerActionAborted = "The following changes had been declined for '{0}' {2} \n{1}\n";

    public const string SelectDatePicker = "Select Date";
    public const string SelectGregorianCalender = "Load Gregorian calender";
    public const string SelectJalaliCalender = "Load Jalali calender";
    public const string LevelUp = "Level Up";
    public const string SelectYear = "Select The year you want to set for the wanted date";
    public const string SelectMonth = "Select The month you want to set for the wanted date";
    public const string SelectDay = "Select The day you want to set for the wanted date";
    public const string SelectHour = "Select The hour you want to set for the wanted date";
    public const string SelectMinute = "Select The minute you want to set for the wanted date";
    public const string DateNotValid = "This date is nt valid";

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
    public const string SetupTracker = EnterDate + "date of start of your last period" + EnterDateDetail;
    public const string AvailableCycle = "You already have a cycle tracker set up. Use Edit Period to make changes.";
    public const string LastStartChanged = "Last start date changed successfuly.";
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
    
    public const string ThisMonth = "This Month";
    public const string LastMonth = "Last Month";
    public const string AllTime = "All Time";
    public const string CustomPeriod = "Custom Period";
    public const string SendCustomDate = EnterDate + "{0}" + EnterDateDetail;
    public const string CustomStart = "start of custom period";
    public const string CustomEnd = "end of custom period";
    public const string SelectDate = "Select the date you want to take report of";
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
    
    #region Notification

    public const string LoadNotification = "Welcome to the Notification Center.\nWhat do you want to do?";
    
    public const string KeyboardCreateNotification = "Create New Notification";
    public const string KeyboardNotificationManagement = "Notification Management";
    
    public const string AskNotificationName = "Enter the Notification name.";
    public const string FirstOccurrence = "Enter the First Occurrence.";
    public const string ReminderHowOften = "How often would you like to receive notifications?";
    public const string HowOftenUnit = "How many {0} should the bot wait between reminders?";
    public const string ReminderMessage = "Send the messge that you want to be reminded with.";
    public const string ReminderSetSuccessful = "Your notification got successfuly added.";
    
    public const string NotificationHour = "Hour(s)";
    public const string NotificationDay = "Day(s)";
    public const string NotificationMonth = "Month(s)";
    public const string NotificationOneTime = "One Time";

    public const string SelectNotification = "Select the notification you want.";
    public const string NoNotifications = "You do not have any notifications to manage yet.";
    public const string NotificationEditor = "<b>{0}</b>\n\nStatus: {1}\nDefault message: {2}\nNext occurrence: {3}\nNext message: {4}\nRepeat: {5}";
    public const string NotificationActive = "Active";
    public const string NotificationInactive = "Inactive";
    public const string NotificationNoNextOccurrence = "Not scheduled";
    public const string NotificationUsesDefaultMessage = "Uses the default message";
    public const string NotificationEditName = "Edit name";
    public const string NotificationEditMessage = "Edit default message";
    public const string NotificationEditNextTime = "Edit next occurrence";
    public const string NotificationEditNextMessage = "Edit next message";
    public const string NotificationEditSeparationType = "Edit repeat type";
    public const string NotificationEditSeparationValue = "Edit repeat value";
    public const string NotificationDeactivate = "Deactivate";
    public const string NotificationActivate = "Activate";
    public const string NotificationBackToList = "Back to notifications";
    public const string AskEditedNotificationName = "Send the new notification name.";
    public const string AskEditedNotificationMessage = "Send the new default notification message.";
    public const string AskEditedNextMessage = "Send the message for the next occurrence only. Send 'skip' to use the default message.";
    public const string AskEditedSeparationValue = "Send the new positive repeat value.";
    public const string SelectNotificationSeparationType = "Select the new repeat type.";
    public const string NotificationUpdated = "Notification updated.";
    public const string NotificationUpdateFailed = "I could not update that notification. It may no longer exist or the value may be invalid.";
    public const string NotificationActivationFailed = "Set a future next occurrence before activating this notification.";

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
    public const string DatePicker = "DP";
    public const string DatePickerMianMenu = "DPMM";
    public const string SelectGregorianCalender = "SGC";
    public const string SelectJalaliCalender = "SJC";
    public const string SelectYear = "DPSY";
    public const string SelectMonth = "DPSM";
    public const string SelectDay = "DPSD";
    public const string SelectHour = "DPSH";
    public const string SelectMinute = "DPSMi";
    public const string Jalali = "J";
    public const string Gregorian = "G";
    public const string LevelUp = "LU";

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
    public const string SelectWalletToProcess = "SWTP";
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
    public const string CustomPeriod = "CP";
    public const string SelectDate = "SD";
        
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

    #region Notification

    public const string Notification = "Notification";

    public const string CreateNotification = "CN";
    public const string NotificationManagement = "NM";
    
    public const string NotificationsHowOften = "NHO";
    public const string NotificationSelect = "NS";
    public const string NotificationEditField = "NEF";
    public const string NotificationEditType = "NET";
    public const string NotificationToggleActive = "NTA";
    public const string NotificationBackToList = "NBL";
    public const int NotificationOneTime = 0;
    public const int NotificationHour = 1;
    public const int NotificationDay = 2;
    public const int NotificationMonthGregorian = 3;
    public const int NotificationMonthJalali = 4;
    
    public const int SpecialAdminCheckSpotify = 1;
    public const int SpecialPeriodTracker = 2;

    #endregion

    #region Metro

    public const string Metro = "Metro";
    public const string MetroNavigation = "MN";
    public const string MetroStationDetails = "MSD";
    public const string MetroLineSelected = "MLS";
    public const string MetroStationSelected = "MSS";

    #endregion

    #region Mapify

    public const string Mapify = "Mapify";
    public const string MapifyMyMaps = "MM";
    public const string MapifyCreateMap = "CM";
    public const string MapifySelectMap = "SM";
    public const string MapifyInvite = "IM";
    public const string MapifyJoin = "JM";
    public const string MapifyManageCategories = "MMC";
    public const string MapifyCategoryAction = "MCA";
    public const string MapifyAddLocation = "AML";
    public const string MapifyAddLocationCategories = "ALC";
    public const string MapifyVisited = "LV";
    public const string MapifyEditLocation = "EML";
    public const string MapifySelectEditLocation = "ESL";
    public const string MapifyEditField = "ELF";
    public const string MapifyEditCategories = "ELC";
    public const string MapifyEditVisited = "ELV";
    public const string MapifySuggest = "SML";
    public const string MapifySuggestCategories = "SLC";
    public const string MapifyMoreSuggestions = "SMS";

    #endregion
}

public static class Errors
{
    public const string FirstOccurrencePassed = "The time registered as the first occurrence has passed.";
}

public static class Actions
{
    public const string Register = "Register";
    
    public const string SetUpPeriod = "SetUpPeriod";

    public const string SetUpCart = "SetUpCart";
    
    public const string AwaitingProductActions = "AwaitingProductActions";
    public const string AwaitingCategoryName = "AwaitingCategoryName";
    public const string AwaitingBluFile = "AwaitingBluFile";
    public const string AwaitingBluReview = "AwaitingBluReview";
    public const string BuildingReport = "BuildingReport";

    public const string LoadSpotify = "AwaitingPlaylistId";
    public const string AwaitingPlaylistId = "AwaitingPlaylistId";
    public const string AwaitingTrackReview = "CategorizePlaylist";
    
    public const string SetUpNotification = "SetUpNotification";
    public const string EditNotification = "EditNotification";

    public const string MetroNavigation = "MetroNavigation";
    public const string MapifyCreateMap = "MapifyCreateMap";
    public const string MapifyManagingCategories = "MapifyManagingCategories";
    public const string MapifyAddingLocation = "MapifyAddingLocation";
    public const string MapifyEditingLocation = "MapifyEditingLocation";
    public const string MapifySuggestingLocation = "MapifySuggestingLocation";
}

public static class SessionCallBacks
{
    public const string AskForName = "AskForName";
    public const string AskForEmail = "AskForEmail";
    
    public const string AskForCycleLength = "AskForCycleLength";
    public const string AskForPeriodLength = "AskForPeriodLength";
    
    public const string AskCartName = "AskCartName";
    
    public const string AskNotificationName = "AskNotificationName";
    public const string AskNotificationOftenUnit = "AskNotificationOftenUnit";
    public const string AskReminderMessage = "AskReminderMessage";
    public const string AskEditedNotificationName = "AskEditedNotificationName";
    public const string AskEditedNotificationMessage = "AskEditedNotificationMessage";
    public const string AskEditedNextMessage = "AskEditedNextMessage";
    public const string AskEditedSeparationValue = "AskEditedSeparationValue";

    public const string AskMetroOriginLocation = "AskMetroOriginLocation";
    public const string AskMetroDestinationLocation = "AskMetroDestinationLocation";
    public const string MapifyAskMapName = "MapifyAskMapName";
    public const string MapifyAskLocationName = "MapifyAskLocationName";
    public const string MapifyAskLocationPin = "MapifyAskLocationPin";
    public const string MapifyAskLocationDescription = "MapifyAskLocationDescription";
    public const string MapifyAskVisited = "MapifyAskVisited";
    public const string MapifyAskScore = "MapifyAskScore";
    public const string MapifyAskEditedLocationName = "MapifyAskEditedLocationName";
    public const string MapifyAskEditedLocationPin = "MapifyAskEditedLocationPin";
    public const string MapifyAskEditedLocationDescription = "MapifyAskEditedLocationDescription";
    public const string MapifyAskEditedLocationScore = "MapifyAskEditedLocationScore";
    public const string MapifyAskLocationToEditPin = "MapifyAskLocationToEditPin";
    public const string MapifyAskSuggestionPin = "MapifyAskSuggestionPin";
    public const string MapifyAwaitMoreSuggestions = "MapifyAwaitMoreSuggestions";
    public const string MapifyLoadingSuggestions = "MapifyLoadingSuggestions";
}

public static class Context
{
    public const string BotNotifications = "BotNotifications";
    
    public const string Tps = "TransactionProcesses";
    public const string Wallet = "Wallet";
    public const string Index = "Index";
    public const string Section = "Section";
    public const string MessageId = "MessageId";

    public const string FileAddress = "FileAddress";
    public const string ReportWalletId = "ReportWalletId";
    public const string ReportSelectedCategories = "ReportSelectedCategories";
    public const string ReportAllSelected = "ReportAllSelected";
    public const string ReportMessageId = "ReportMessageId";
    public const string StartDate = "StartDate";
    public const string EndDate = "EndDate";

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
    
    public const string NotificationName = "NotificationName";
    public const string FirstOccurrence = "FirstOccurrence";
    public const string ReminderUnit = "ReminderUnit";
    public const string UnitCount = "UnitCount";
    public const string ReminderMessage = "ReminderMessage";
    public const string NotificationId = "NotificationId";

    public const string MetroOriginLatitude = "MetroOriginLatitude";
    public const string MetroOriginLongitude = "MetroOriginLongitude";
    public const string MapifyMapId = "MapifyMapId";
    public const string MapifyLocationId = "MapifyLocationId";
    public const string MapifySelectedCategoryIds = "MapifySelectedCategoryIds";
    public const string MapifyAllCategoriesSelected = "MapifyAllCategoriesSelected";
    public const string MapifySelectorMessageId = "MapifySelectorMessageId";
    public const string MapifyLocationName = "MapifyLocationName";
    public const string MapifyLatitude = "MapifyLatitude";
    public const string MapifyLongitude = "MapifyLongitude";
    public const string MapifyEditReferenceLatitude = "MapifyEditReferenceLatitude";
    public const string MapifyEditReferenceLongitude = "MapifyEditReferenceLongitude";
    public const string MapifyDescription = "MapifyDescription";
    public const string MapifyVisited = "MapifyVisited";
    public const string MapifyScore = "MapifyScore";
    public const string MapifySuggestionOriginLatitude = "MapifySuggestionOriginLatitude";
    public const string MapifySuggestionOriginLongitude = "MapifySuggestionOriginLongitude";
    public const string MapifyShownLocationIds = "MapifyShownLocationIds";
}

public static class DatePickerMethods
{
    public const string PeriodDateCycleTracker = "PeriodDateCycleTracker";
    
    public const string CustomStartTransactionReport = "CustomStartTransactionReport";
    public const string CustomEndTransactionReport = "CustomEndTransactionReport";
    
    public const string NotificationFirstOccurrence = "NotificationFirstOccurrence";
    public const string NotificationNextOccurrence = "NotificationNextOccurrence";
}

public static class Files
{
    public const string PdfWalletReport = "WalletReport_{0}.pdf";
    public const string ExcelWalletReport = "WalletDetailedTransactions_{0}.xlsx";
}

public static class SpotifyApi
{
    public const string AllSongsPlaylistId = "21SgdiMHUdSgXrZpnuqc56";
    public const string NotCategorizedPlaylistId = "21SgdiMHUdSgXrZpnuqc56";
    public const string ApiPassword = "VeryStrongPasswordForAuthentication";
    public const string ApiCallSignIn = "/api/Authorization/SignIn";
    public const string ApiCallGetPlaylist = "api/PlayList/GetPlayListFromSpotify";
    public const string ApiCallGetPlaylists = "/api/PlayList/GetPlayListsFromDatabase";
    public const string ApiCallGetTrack = "api/PlayList/GetTrackFromSpotify";
    public const string ApiCallAddTrack = "api/PlayList/AddTrackToCollection";
    public const string ApiCheckForPlaylistTracksAvailability = "api/PlayList/CheckForPlaylistTracksAvailability";
}
