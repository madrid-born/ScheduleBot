namespace ScheduleBot.Models;

public static class Messages
{
    #region Universal
    
    public const string Yes = "Yes";
    public const string No = "No";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Welcome = "Welcome to the bot\nyou can choose your action by the keyboard below.";
    public const string NotFound = "Command not found or something went wrong.";
    
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

}

public static class CallBacks
{
    #region Register

    public const string Register = "Register";
    public const string AskToRegister = "AskToRegister";
    public const string AcceptRegister = "AcceptRegister";
    public const string RejectRegister = "RejectRegister";
    
    #endregion
}