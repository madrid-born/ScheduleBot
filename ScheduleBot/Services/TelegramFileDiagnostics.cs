using System.Net.Sockets;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public static class TelegramFileDiagnostics
{
    public static async Task<T> RunAsync<T>(string stage, Func<Task<T>> action)
    {
        try { return await action(); }
        catch (TelegramFileTransferException) { throw; }
        catch (Exception ex) when (ex is WTelegram.WTException or TL.RpcException or SocketException or IOException)
        { throw new TelegramFileTransferException(stage, Reason(ex)); }
    }

    // Emit only fixed labels or numeric statuses. Never log library messages, paths or payloads.
    public static string Reason(Exception ex) => ex switch
    {
        SocketException socket => "socket-" + socket.SocketErrorCode,
        TL.RpcException rpc => "rpc-" + rpc.Code,
        WTelegram.WTException when ex.Message.StartsWith("Exception while reading session file:", StringComparison.Ordinal)
            || ex.Message.Contains("Integrity check failed in session loading", StringComparison.Ordinal) => "session-unreadable",
        WTelegram.WTException when ex.Message.Contains("connect to Telegram first", StringComparison.Ordinal) => "not-connected",
        WTelegram.WTException when ex.Message.Contains("Connection shut down", StringComparison.Ordinal) => "connection-closed",
        WTelegram.WTException when ex.Message.StartsWith("Could not find adequate dc_option", StringComparison.Ordinal) => "data-center-unavailable",
        WTelegram.WTException when ex.Message.StartsWith("Failed to get Authorization:", StringComparison.Ordinal) => "authorization-response-invalid",
        WTelegram.WTException when ex.Message.Contains("call got a result of type", StringComparison.Ordinal) => "protocol-response-invalid",
        WTelegram.WTException => "protocol-error",
        IOException io when (io.HResult & 0xffff) == 32 => "session-file-in-use",
        IOException => "io-error",
        _ => "unexpected-error"
    };
}
