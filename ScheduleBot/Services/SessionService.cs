using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class UserSessionService
{
    private readonly Dictionary<long, UserSession> _sessions = new();
    
    public UserSession SetData(long chatId, string action = "", string callbackData = "")
    {
        var session = new UserSession();
        session.SetAction(action);
        session.SetCallBack(callbackData);
        _sessions[chatId] = session;
        return session;
    }
    
    public UserSession? GetData(long chatId)
    {
        _sessions.TryGetValue(chatId, out var session);
        return session;
    }
    
    public void ClearSession(long chatId)
    {
        _sessions.Remove(chatId);
    }
}

public class UserSession
{
    public string Action { get; private set; }
    public string CallbackData { get; private set; }
    public DateTime Timestamp { get; private set; }
    public DatePicker? DatePickerSetup { get; private set; }
    public Dictionary<string, object> Context { get; private set; } = new();

    public void SetAction(string action)
    {
        Action = action;
        Timestamp = DateTime.Now;
    }

    public void SetCallBack(string callbackData)
    {
        CallbackData = callbackData;
        Timestamp = DateTime.Now;
    }

    public void SetContext(string key, object value)
    {
        Context[key] = value;
        Timestamp = DateTime.Now;
    }

    public void SetDatePicker(DatePicker value)
    {
        DatePickerSetup = new DatePicker
        {
            ChatId = value.ChatId,
            IsJalali = value.IsJalali,
            Message = value.Message,
            FixedDate = value.FixedDate
        };
        Timestamp = DateTime.Now;
    }

    public void ClearDatePicker()
    {
        DatePickerSetup = null;
        Timestamp = DateTime.Now;
    }
}