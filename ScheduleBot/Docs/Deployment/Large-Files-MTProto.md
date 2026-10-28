# Large Telegram downloads on the existing Ubuntu bot

## What changes

The file download service can now authenticate as your **bot** through MTProto using WTelegramClient 4.4.8. Documents, videos, audio, voice, animations, stickers and video notes share Telegram's document representation and use this backend. The original Telegram.Bot client still handles commands, normal features and polling. No second bot process, local Bot API server, user-account login or file cache is required. Default behavior stays on the existing hosted API until `FileLinks:UseMtProto` is enabled.

Link creation still uses metadata/database operations only, regardless of file size. Each HTTP request refreshes the source message's document reference, checks its original document identity/chat/size, and pulls aligned 256 KB chunks directly from Telegram. The application streams those chunks to the browser using the existing endpoint, backpressure, expiration/revocation and concurrency limits. Ranges use the requested byte offset without downloading earlier file contents. A range starting between aligned boundaries can fetch up to one additional chunk at its boundary. Single bounded, suffix and open-ended ranges are supported; If-Range still receives a full response because stable HTTP validators are not exposed.

The configured default maximum is **4 GiB**, represented with 64-bit lengths and offsets. This is an application safety limit, not a guarantee that Telegram permits every file/account combination or unlimited traffic. Existing Telegram access restrictions, rate/flood limits and network throughput still apply. Photos and unsupported/legacy unique-ID formats use the existing hosted API path and its 20 MB limit; re-send large media as a document if needed. No source-message deletion event subscription is added. MTProto re-reads the source message: if it is unavailable or now points to a different document, new download requests fail. This behavior is covered with simulated message data, not yet verified against live deletion events.

## 1. Obtain application credentials

Open [Telegram API development tools](https://my.telegram.org/apps) yourself. Register an application if you do not have one (for example an application named ScheduleBot with platform Other), and obtain its `api_id` and `api_hash`. These identify your API application; they are distinct from the bot token. The website may require you to sign in to your Telegram account to register the application, but **the running bot does not use that personal account**. It authorizes with the existing bot token through `LoginBotIfNeeded`. Do not supply your phone number, verification codes, two-step password or a user session to the bot or this chat. Keep the API hash private and configure it on your server.

## 2. Deploy the updated application

Restore/publish using your existing deployment process. One new runtime package is required: WTelegramClient 4.4.8. If the development machine's configured `D:\nuget` source does not exist, restore explicitly from the public feed:

```bash
dotnet restore --source https://api.nuget.org/v3/index.json
dotnet publish --no-restore -c Release
```

Deploy that publish output to the existing service's deployment directory using your usual process. There are no additional schema changes for MTProto; the previously added FileLinks table is still required. Nginx continues to proxy `telegramdownload.kofi.rest` to port **5002**. No new public listener is introduced.

## 3. Configure the existing systemd service

On Ubuntu create a private environment file:

```bash
sudo install -m 600 -o root -g root /dev/null /etc/schedulebot-filelinks.env
sudo nano /etc/schedulebot-filelinks.env
```

Run the install command only when the environment file does not already exist; it creates an empty file. Copy the settings from `schedulebot-filelinks.env.example` in this directory and replace both application credential placeholders with your actual values. Do not paste credentials into chat or commit the resulting file. The example keeps your production domain and allows downloads up to 4 GiB with a 120-minute application deadline. Existing bot credentials remain in their current configuration. Use the Production environment so Development settings do not select localhost, the development token or proxy.

Then edit the drop-in for the existing unit:

```bash
sudo systemctl edit schedulebot.service
```

Paste the contents of `schedulebot-filelinks.systemd.conf`:

```ini
[Service]
StateDirectory=schedulebot-telegram
StateDirectoryMode=0700
EnvironmentFile=/etc/schedulebot-filelinks.env
```

Systemd creates a persistent private state directory owned by the service's configured user. The session path in the example points into it. The session contains **bot authorization keys, not downloaded files**. Keep it outside web roots and deployed publish output; never share it, serve it through Nginx, or use it from two application instances. The repository ignores the default session files. On Windows local development, set the same `FileLinks` options using environment variables/user secrets and a writable private session path; use your local base URL. In Development, MTProto reuses the existing `Proxy:Url` setting through a SOCKS5 TCP tunnel. Keep your proxy application running (currently port 10808). An explicit `FileLinks:MtProtoProxyUrl` takes precedence in any environment and supports `socks5://host:port` or SOCKS5 username/password authentication. In Production the Development proxy is not inherited; connections remain direct unless this explicit option is set. Proxy handshakes have a 15-second deadline, propagate shutdown cancellation and fail without bypassing the configured proxy. Unsupported proxy schemes are rejected.

## 4. Restart and test

```bash
sudo systemctl daemon-reload
sudo systemctl restart schedulebot.service
sudo systemctl status schedulebot.service --no-pager
curl -i https://telegramdownload.kofi.rest/api/bot/status
```

Send a file above 20 MB (initially perhaps 50–100 MB) using **File Links → Create download link**. A URL should appear immediately; bot authorization/metadata retrieval and streaming occur when opening it. Open the new URL in your browser. Test interruption/resume with a download client if desired. The existing Nginx streaming settings remain applicable. For very long downloads increase the Nginx timeout if connections sit idle; `proxy_read_timeout` is an idle timeout, while the application deadline is total elapsed time.

Useful diagnostics: `sudo journalctl -u schedulebot.service -n 80 --no-pager`. A setup message means credentials are missing/malformed. A 404 can mean the original source message or document is unavailable/mismatched. An upstream failure can mean invalid credentials, Telegram connectivity, a stale reference or a flood restriction. Native protocol logs are suppressed to avoid recording credentials. Log messages intentionally contain error types, not protocol payloads or secrets. Repeated failures require checking credentials/network and a live controlled test; do not disable the identity checks or file limit as a workaround.

## Roll back this backend

Set `FileLinks__UseMtProto=false` in the environment file and restart the **existing** service. That restores the previous hosted-API download path (20 MB limit) without changing commands or other features. Removing the drop-in also removes its environment settings; keep any shared service configuration intact.

## Tested versus pending

Automated checks use simulated protocol messages and chunk readers: multi-gigabyte metadata-only links, document/chat/size identity matching, unavailable/edited sources, 64-bit range offsets above 2 GB, bounded reads, truncation, empty files, cancellation and a native-RPC concurrency bound that remains held until in-flight requests finish. A canceled HTTP wait does not cancel the library's native RPC; its permit stays reserved until completion so canceled clients cannot create unlimited background transfers. No full file bytes are stored.

The code builds and 110 checks pass, including simulated SOCKS5 tunnels, credential negotiation, IPv4/IPv6/domain destinations, rejection, cancellation and safe stage-specific errors. Live **development-bot authorization and first-byte file retrieval through SOCKS5 succeeded**, including resuming the saved session. The diagnostic read only the exact link reference supplied in the user's log and requested a first-byte range (one aligned Telegram chunk at most), with no file content saved and no second poller started. This verifies private-message lookup, original document matching, data-center access and a live range for that file. Full large-file transfers, real transfer cancellation and production download/resume remain unverified. No server deployment was performed. Complete the large-file test above before treating full download behavior as live-verified.

Native failures now log a safe operation (`SessionOpen`, `BotAuthorization`, `MessageLookup`, `DataCenterConnect`, or `ChunkRead`) and a fixed reason such as `session-unreadable`, `connection-closed`, or a numeric RPC status. Raw exception messages and credentials are not logged. A verified independent session was selected for local Development using user secrets; the original session file was preserved. Restart local runs to load that setting. Never delete or replace a session while an application instance is using it.

References: [Telegram bot authorization](https://core.telegram.org/api/bots), [application credentials](https://core.telegram.org/api/obtaining_api_id), [bot message lookup](https://core.telegram.org/method/messages.getMessages), [file retrieval](https://core.telegram.org/method/upload.getFile), [WTelegramClient](https://github.com/wiz0u/WTelegramClient), [TDLib file identity serialization](https://github.com/tdlib/td/blob/master/td/telegram/files/FileLocation.hpp).
