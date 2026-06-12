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
    public const string PeriodTrackerMessage = "Period Tracker";
    public const string PeriodTracker = PeriodTrackerSymbol + PeriodTrackerMessage;
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
    
    public const string KeyboardSetup = "Setup Period";
    public const string KeyboardCheckNotifications = "Check My Current Notifications";
    
    public const string LoadPeriodTracker = "Welcome to the period tracker section.\nWhat do you want to do?";
    public const string AvailableCycle = "You already have a cycle tracker set up. Use /EditPeriod to make changes.";
    public const string SetupTracker = "Please enter the start date of your last period (YYYY-MM-DD):\nBoth Jalali and Gregorian dates would work\nBut remember to enter like 1405-03-07 or 2026-06-05";
    public const string AskForCycleLength = "What is your average cycle length (days between periods)?";
    public const string AskForPeriodLength = "How many days does your period typically last?";
    public const string AskForNotifyMode = "How often would you like to receive notifications?";
    public static readonly List<string> NotifyModes = ["Every day", "Weekly", "Start & End only", "3 days before + during period"];
    public const string SetupComplete = "Period tracker setup complete!";
    public const string EditPeriodReminder = " You can use /EditPeriod to update your cycle.";
    public const string SetNotifyComplete = "You'll receive notifications\n{0}.";
    
    //public const string Daily = "Every day";
    //public const string Weekly = "Weekly";
    //public const string StartAndEnd = "Start & End only";
    //public const string DuringAndBefore = "3 days before + during period";
    
    
    
    public const string CycleTrackerWelcome = "🌸 Period Tracker\n\nTrack your menstrual cycle, get daily insights, and never miss a period!";
    public const string CycleSetupComplete = "✅ Period tracker setup complete!";
    public const string NoActiveTracker = "❌ You don't have an active period tracker. Click the Period Tracker button to set one up.";

    #endregion
}

public static class CallBacks
{
    #region Register

    public const string Register = "Register";
    public const string AskToRegister = "AskToRegister";
    public const string AcceptRegister = "AcceptRegister";
    public const string RejectRegister = "RejectRegister";
    
    #endregion
    
    #region CycleTracker

    public const string Cycle = "Cycle";
    public const string SetNotifyMode = "SetNotifyMode";
    
    

    
    public const string Setup = "Setup";
    public const string EditPeriod = "EditPeriod";
    public const string NewPeriod = "NewPeriod";
    public const string ViewStatus = "ViewStatus";

    #endregion
}