# Telegram file links

The feature follows the existing Notification/Metro organization and interaction style:

- `BotHandlers/FileLinkHandler.cs`: Telegram actions through `HandleSection`, `HandleCallBack` and `HandleSession`; uses `MainService`, `UpdateData`, `UserSessionService`, and `Messages` constants.
- `Services/FileLinkService.cs`: database operations for creation, lookup, expiration and owner-only revocation. It receives file references, not Telegram messages.
- `Models/FileLink.cs` and `Models/FileLinkOptions.cs`: persistence model, exception and configuration.
- `Controllers/FileDownloadController.cs`: the HTTP download action.
- `Services/FileDownloadService.cs` and `Services/TelegramFileService.cs`: bounded streaming, upstream access and resource limits.

There is no separate feature folder, registration extension or parallel Telegram router. Dependencies are registered in `Program.cs` alongside existing handlers and services. The optional large-file backend adds WTelegramClient 4.4.8; the original Telegram.Bot client remains in use for polling and existing features.

## Use

When enabled, the normal main keyboard includes **🔗 File Links**. Its inline menu offers:

1. **Create download link**: starts an existing-style session; send a document, photo, video, animation, audio, voice, video note or sticker. Only this explicit file-link upload session bypasses the existing document importer. The bot captures metadata and returns a URL without downloading the file. Replying to a previous file with `/getlink` also works.
2. **Revoke download link**: starts a session asking for the URL or token. Only the creating user can revoke it. `/revokelink <URL or token>` also works.

Cancel exits either session, and switching main sections clears the file-link session. Existing user registration checks still run. File Links operates in private chats. With the flag disabled, its actions are inactive, its commands fall through existing routing, and downloads return 404. Other document workflows retain their existing behavior.

## Configure and deploy

### Test on your PC

`appsettings.Development.json` enables File Links with `PublicBaseUrl=http://localhost:5208`, matching the existing HTTP launch profile. Run `dotnet run --launch-profile http`. Use the Telegram bot's File Links > Create action, then copy the generated URL into a browser on this same PC. No public server, DNS or tunnel is needed: the existing Telegram long poller receives messages through outbound connections. The existing SQL Server database still needs the FileLinks migration, and Telegram connectivity is still required. Do not run another poller with the same bot token simultaneously.

Localhost means the device opening the link. A phone cannot use your PC's localhost URL. Use Telegram Desktop or copy the link to your PC. If you select the HTTPS profile instead, override the base URL to `https://localhost:7140` and use your trusted ASP.NET development certificate. Keep the base URL port consistent with the selected launch profile.

HTTP is accepted only for loopback addresses (`localhost`, `127.0.0.1`, `[::1]`); non-local HTTP URLs remain rejected. The documented HTTP profile exposes only an HTTP listener, so the existing HTTPS redirection has no HTTPS destination; if your environment explicitly configures an HTTPS redirect port, use the HTTPS profile/base URL instead.

### Deploy later

For this domain on Ubuntu 24 with Nginx, use the [server setup guide](Deployment/Ubuntu-Nginx.md) and [Nginx site configuration](Deployment/telegramdownload.nginx.conf). The application processes the forwarded HTTPS scheme only from the default trusted loopback proxies, before HTTPS redirection.

The feature is **disabled by default** in `appsettings.json`. To enable:

1. Review and apply the additive migration with `dotnet ef database update --context AppDbContext`, using the intended deployment environment/database. It creates only the `FileLinks` table and an index. No migration runs automatically; the disabled feature does not need this table.
2. Set `FileLinks__Enabled=true` and `FileLinks__PublicBaseUrl=https://files.example.com` through normal configuration. Restart the application.
3. Route `GET /downloads/*` on that HTTPS origin to the existing ASP.NET Core host. No second bot instance/listener is required. Existing Kestrel configuration controls its binding. For a base path such as `/bot`, strip it at the proxy before forwarding.

All configuration values (shown with defaults except the required public URL):

```json
{
  "FileLinks": {
    "Enabled": false,
    "PublicBaseUrl": "https://files.example.com",
    "ExpirationMinutes": 60,
    "MaxConcurrentDownloads": 8,
    "RequestsPerMinute": 60,
    "MaxActiveLinksPerUser": 100,
    "DownloadTimeoutMinutes": 30
  }
}
```

Link creation requires an HTTPS base URL, or HTTP on loopback for local testing, without embedded credentials, query or fragment. If it is unset or invalid, the bot still starts, logs a setup warning, and Create Link explains the missing configuration without accessing the database or creating an unusable link. Existing links and revocation continue to work. Configure the public address and restart when hosting is ready. Positive bounded resource limits are still validated at startup. Disable response buffering, disk spooling and caching at the reverse proxy. Use appropriate stream timeouts and propagate disconnects. Keep existing HTTPS redirection working with the proxy, or use HTTPS to the application. No forwarded-header trust configuration was changed.

Download URLs are bearer secrets. Redact `/downloads/*` paths from access logs/APM and retain Warning-level ASP.NET request/routing logs. Do not enable URI logging on the dedicated Telegram HTTP client: upstream URLs contain the bot credential. Existing Development proxy settings are reused. Link replies disable Telegram previews to avoid triggering an unnecessary download.

## Download behavior and limits

For large files, the [MTProto setup guide](Deployment/Large-Files-MTProto.md) includes ready-to-use environment and systemd templates for the existing Ubuntu service. `UseMtProto=false` retains the original hosted API path; enabling it requires application API credentials. The bot session uses the existing bot token, never a personal-account login. No file bytes are cached to disk. No additional database migration is required for the new backend.

- `GET /downloads/{token}` returns an attachment with the sanitized filename and MIME type. Tokens contain 256 random bits; SQL stores only SHA-256 token hashes and file-reference metadata, never file bytes or private upstream URLs.
- Missing, malformed or inaccessible files return 404; expired/revoked links return 410; upstream failures 502; timeouts 504; metadata-service failures 503; overload 429 with Retry-After. Errors after headers abort the connection, without exposing upstream error text or credentials.
- Streaming uses ResponseHeadersRead, a 64 KB buffer, awaited writes, cancellation and disposal. There are no temporary or permanent file copies. Separate accepted downloads use separate upstream streams; no caching or shared stream fan-out is introduced.
- Concurrency and request limits apply per process, with no wait queue. A global token bucket refills once per minute. Active-link quotas constrain normal use but concurrent creation can briefly exceed the quota. For multiple replicas, coordinate the existing Telegram poller and apply deployment-wide limits at the proxy.
- Expiration and revocation reject new requests; already-started transfers may finish. Expired/revoked metadata can be periodically deleted by operations. Afterwards those links return 404.
- Single byte ranges, including suffix/open-ended intervals, are forwarded. A 206 is passed through only after validating Content-Range and Content-Length against the request. Valid upstream 416 responses are propagated. If Telegram ignores Range, the full 200 response uses `Accept-Ranges: none`. Malformed/multiple ranges and If-Range receive the full representation. No prefix is downloaded/discarded to simulate seeking. Actual hosted Telegram Range availability has not been verified.

The hosted Telegram Bot API path has a **20 MB download limit**. The optional MTProto backend supports document-like media beyond this limit, with a configurable `MaxDownloadBytes` default of **4 GiB**. It refreshes source metadata and validates the original document identity on each request. Native reads use aligned 256 KB chunks and support single-byte ranges with 64-bit offsets; they are copied to HTTP with the existing 64 KB buffer. Photos and legacy/unknown unique-ID formats retain the hosted path. Telegram media may already be compressed: send a document to retain the uploaded original bytes. Live MTProto authorization/large-file retrieval still needs verification after server credentials are supplied.

Deleting an ordinary Telegram message does **not** revoke a link. There is no ordinary-message deletion update; business deletion updates are specific to connected business accounts and are not used. Expiration and explicit revocation are the supported invalidation mechanisms.

Self-hosted Bot API local mode is not required. The optional MTProto file service authenticates as the bot and preserves the no-file-copy design. It stores only private authorization state, which must remain outside the public web root and deployed application output. No personal-account session is used. Source deletion/replacement may make an MTProto lookup unavailable; no deletion event subscription or live-verified deletion invalidation is claimed.

Official references: [getFile](https://core.telegram.org/bots/api#getfile), [local Bot API](https://core.telegram.org/bots/api#using-a-local-bot-api-server), [updates](https://core.telegram.org/bots/api#update), [persistent file IDs](https://core.telegram.org/bots/faq#can-i-count-on-file-ids-to-be-persistent).

## Verification and change inventory

Run `dotnet build` and `dotnet run --project Tests/FileLinkTests`. The console suite follows the existing test-project approach and uses SQLite only as a test dependency, with fake Telegram responses. Checks include keyboard routing, Create/Revoke/Cancel sessions, command fallthrough, group restrictions, metadata-only upload extraction, real HTTP controller downloads, expiration/revocation, ranges, resource disposal, concurrency/rate bounds, filename/MIME handling, disabled-host startup, and graceful handling of missing/invalid public addresses before database access.

Fixed-buffer/backpressure and concurrency checks establish structural resource bounds; this is not a production load benchmark. Live Telegram, production SQL Server migration application, proxy behavior and full original-bot startup/regression remain unverified. Startup normally accesses the existing database and starts polling. No live bot or production database was used. NuGet vulnerability auditing could not reach api.nuget.org (NU1900); dependencies restored from available packages.

New files: the handler, two models, three services and controller listed above; `Migrations/20261024111007_AddFileLinks.cs` and its Designer; `Tests/FileLinkTests/FileLinkTests.csproj` and `Program.cs`; this document.

Integration edits: `Program.cs` (normal registrations/options), `Models/AppDbContext.cs` and migration snapshot (additive model), `Models/Messages.cs` (text/callback/action constants), `Models/UpdateData.cs` (metadata fields), `BotHandlers/MessageHandler.cs` (existing command/keyboard/callback/session routing and opt-in import bypass), `Services/MainService.cs` (conditional keyboard button and optional preview/cancellation arguments), `appsettings.json` (disabled flag). Existing Spotify and Tehran changes were preserved.
