namespace ScheduleBot.Services;

public class UserSessionService
{
    private readonly Dictionary<long, UserSession> _sessions = new();
    
    public void SetData(long chatId, string action, string callbackData)
    {
        _sessions[chatId] = new UserSession
        {
            Action = action,
            CallbackData = callbackData,
            Timestamp = DateTime.UtcNow
        };
    }
    
    public UserSession? GetPendingAction(long chatId)
    {
        _sessions.TryGetValue(chatId, out var session);
        return session;
    }

    public string? GetData(long chatId, string action)
    {
        _sessions.TryGetValue(chatId, out var session);
        if (session == null || session.Action != action) return null;
        return session.CallbackData;
    }
    
    public void ClearSession(long chatId)
    {
        _sessions.Remove(chatId);
    }
}

public class UserSession
{
    public string Action { get; set; }
    public string CallbackData { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
}