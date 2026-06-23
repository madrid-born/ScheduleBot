using System.Collections.Generic;

namespace ScheduleBot.Models;

public static class Messages
{
    #region Command
    
    public const string Start = "/start";
    public const string SetupPeriod = "/SetupPeriod";
    public const string EditPeriod = "/EditPeriod";
    
    #endregion
    
    #region MainKeyboard
    
    public const string PeriodTrackerSymbol = "🌸 ";
    public const string PeriodTracker = "Period Tracker";
    public const string About = "ℹ️ About";
    
    #endregion

    #region Universal
    
    public const string Yes = "Yes";
    public const string No = "No";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Welcome = "Welcome to the bot\nyou can choose your action by the keyboard below.";
    public const string NotFound = "Command not found or something went wrong.";
    public const string InvalidDate = "Invalid date format. Please use YYYY-MM-DD";
    public const string InvalidInteger = "Invalid Integer format. Please use a Natural Number";
    
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
    
    public const string KeyboardReportEnd = "Report end";
    public const string KeyboardReportStart = "Report start";
    public const string KeyboardSetup = "Setup";
    public const string KeyboardEdit = "Edit";
    public const string KeyboardCurrentStatus = "Current Status";
    public const string KeyboardAddToCycle = "Add To My Cycle Notification";
    public const string KeyboardJoinToCycle = "Join To Someone Cycle Notification";
    
    public const string SavedData = "Your cycle got restarted successfully.";
    public const string NotifyStart = "{0}'s period just got started.";
    public const string NotifyEnd = "{0}'s period just got ended.";
    public const string LoadPeriodTracker = "Welcome to the period tracker section.\nWhat do you want to do?";
    public const string AvailableCycle = "You already have a cycle tracker set up. Use /EditPeriod to make changes.";

    public const string SetupTracker = "Please enter the start date of your last period (YYYY-MM-DD):\nBoth Jalali and Gregorian dates would work\nBut remember to enter like 1405-03-07 or 2026-06-05";
    public const string AskForCycleLength = "What is your average cycle length (days between periods)?";
    // public const string AskForCycleLength = "What is your average cycle length (days between end of period and starting the next)?";
    public const string AskForPeriodLength = "How many days does your period typically last?";
    public const string AskForNotifyMode = "How often would you like to receive notifications?";
    public static readonly List<string> NotifyModes = ["Never", "Every day", "Weekly", "Start & End only", "3 days before + during period"];
    public const string SetupComplete = "Period tracker setup complete!";
    public const string EditPeriodReminder = " You can use /EditPeriod to update your cycle.";
    public const string SetNotifyComplete = "You'll receive notifications\n{0}.";
    public const string SetNotifyCompleteGuest = "You have been successfully added to {0}'s Cycle Notification\n";
    public const string ShareCycleId = "Your cycle id is mentioned below\nYou can share it with anyone who you want them to get notified.\n\n`{0}`";
    public const string AskForCycleId = "Please enter the id you received from the person you want to join on their notification.";
    public const string CycleIdIsWrong = "Looks like there is something wrong with your input cycle Id.";
    public const string EditCheck = "What do you want to change\n\n";
    public const string ChangesCommited = "Changes commited succesfully\n\n";
    public const string CurrentData = "Your current setted data is as below:\nLast Start: {0} \nCycle length: {1} days\nPeriod length: {2} days\nAverage cycle length: {3} days\nAverage period length: {4} days\n\n";
    public const string Followers = "And those who follow you are:\n{0}";
    public const string SelectCycle = "Select the sycle you want";
    public const string SelectUser = "Select the user you want";
    public const string DidItStart = "Did your period start today?";
    public const string DidItEnd = "Did your period start today?";
    
    public const string RemoveFollowerForOwner = "Succesfuly Removed {0} from cycle";
    public const string RemoveFollowerForReceiver = "{0} removed you from her(their) cycle";
    public const string RemoveFollowingForOwner = "{0} exited from your cycle notification";
    public const string RemoveFollowingForReceiver = "Succesfuly exited {0}'s cycle notification";
    
    public const string EditPeriodLength = "Edit Period Length";
    public const string EditCycleLength = "Edit Cycle Length";
    public const string EditFollowers = "Remove Followers";
    public const string EditLastPeriod = "Remove Last Period";
    public const string EditNotify = "Edit Notification method";
    
    #endregion
    
    #region CycleStatus
    
    public const string StatusForReceiver = "today is {0}\nAnd {1} is now in this situation\n\n{2}";
    
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
}

public static class CallBacks
{
    #region Universal

    public const string Yes = "Yes";
    public const string No = "No";

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
    public const string CurrentStatus = "CurrentStatus";
    public const string ReportStart = "ReportStart";
    public const string ReportEnd = "ReportEnd";
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
    public const string EditPeriod = "EditPeriod";
    public const string NewPeriod = "NewPeriod";
    public const string ViewStatus = "ViewStatus";

    #endregion
}