# Ubuntu 24 / Nginx setup for telegramdownload.kofi.rest

Nginx routes the domain to the running ASP.NET Core bot. Do not point its document root at the source folder or launch a second poller. These are server-side steps; they have not been executed remotely by Codex.

## 1. Check the existing bot listener

On the Ubuntu server:

```bash
sudo ss -ltnp
```

The supplied Nginx configuration uses the confirmed `schedulebot.service` listener at `http://127.0.0.1:5002`. Keep the existing service binding. If configuring the service from scratch, the corresponding settings are:

```ini
Environment=ASPNETCORE_URLS=http://127.0.0.1:5002
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=FileLinks__Enabled=true
Environment=FileLinks__PublicBaseUrl=https://telegramdownload.kofi.rest
```

If using systemd, put these entries under `[Service]` in a drop-in for your existing unit (`sudo systemctl edit YOUR_BOT_SERVICE`), then `sudo systemctl daemon-reload` and restart that existing unit. Do not create another bot instance. Publish/deploy the updated application first: it now processes `X-Forwarded-Proto` from loopback proxies before HTTPS redirection, retaining loopback-only trust. This configuration assumes Nginx and the bot run directly on the same machine, without a Docker bridge.

Production environment matters: Development overrides the public base URL with localhost and also selects the development bot token/proxy. Keep your existing production credentials. The existing SQL Server database must have the FileLinks migration applied.

## 2. Configure the domain in Nginx

The supplied server output already shows `telegramdownload.kofi.rest` in `/etc/nginx/sites-enabled/default`, with Certbot-managed HTTPS listeners, but no proxy destination. Edit only that domain's HTTPS `server` block: replace its existing `location /` with the location settings from `telegramdownload.nginx.conf`, targeting port 5002. Retain its existing certificate directives and HTTP redirect block. Do not create a duplicate site or replace the whole default file. Certificate reissuance is unnecessary if the existing certificate is valid.

DNS must resolve this hostname to this Ubuntu server. Ports 80 and 443 must be reachable through your firewall/provider. Keep SSH access allowed if changing firewall settings.

Copy `telegramdownload.nginx.conf` from this directory onto the Ubuntu server. If this domain already has an Nginx site, back up and edit that site's configuration instead of adding a duplicate `server_name`. Otherwise install the file as `/etc/nginx/sites-available/telegramdownload.kofi.rest` and enable it:

```bash
sudo ln -s /etc/nginx/sites-available/telegramdownload.kofi.rest /etc/nginx/sites-enabled/telegramdownload.kofi.rest
sudo nginx -t
sudo systemctl reload nginx
```

Run the link command only if the enabled-site link does not already exist. Reload only after `nginx -t` succeeds. The supplied file is initially an HTTP site so certificate validation can work without a pre-existing certificate. It disables response buffering, disk spooling, caching, upstream retries and routine token-path logging.

## 3. Issue and install HTTPS

If Certbot is already installed, use it. Otherwise the official Certbot snap can be installed as follows (avoid installing a second Certbot alongside another managed installation):

```bash
sudo snap install --classic certbot
```

Use the installed command, or the snap's explicit path:

```bash
sudo /snap/bin/certbot --nginx -d telegramdownload.kofi.rest --redirect
sudo nginx -t
sudo systemctl reload nginx
sudo /snap/bin/certbot renew --dry-run
```

For an existing non-snap installation replace `/snap/bin/certbot` with `certbot`. Certbot requests a certificate, installs it in Nginx, and configures HTTP-to-HTTPS redirection. Do not manually reference certificate paths before issuance. Existing installations should retain their renewal scheduler; the snap installs scheduled renewal.

## 4. Verify routing before testing files

```bash
curl -i https://telegramdownload.kofi.rest/api/bot/status
```

Expect HTTP 200 with JSON containing `Status: Online` (property casing may be camelCase). A 502 usually means the bot is stopped or the proxy port is wrong. Nginx's welcome page or an HTML 404 means the hostname is reaching another site. Certificate errors indicate TLS must be fixed first. Repeated redirects indicate stale application code, incorrect forwarded headers, or a proxy that is not on loopback.

Once status works, generate a **new** file link in the production bot and open it. Existing localhost links still refer to the PC and are not rewritten by configuration changes. Application responses 404/410/429/502 mean the request reached download handling and require checking the specific link, limits, database or Telegram connectivity. Do not post download tokens or bot credentials in diagnostic logs.

References: [Nginx proxy module](https://nginx.org/en/docs/http/ngx_http_proxy_module.html), [ASP.NET Core forwarded headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer), [Certbot Nginx instructions](https://certbot.eff.org/instructions?ws=nginx&os=snap).
