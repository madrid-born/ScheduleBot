# Score Counter

Open **🏆 Score Counter** on the bot's main keyboard. Create a group and name its currency (for example `Stars`). The group name is used as the currency name in requests, balances, and history. Invite registered people with a join link or invitation code; everyone starts with zero.

- **Ask for points**: select another member, enter a positive integer, and wait for that person to accept or reject. Acceptance transfers their currency to you.
- **Give away points**: select another member and enter an amount. Acceptance transfers your currency to them. Sufficient funds are required when creating a gift and again when it is accepted.
- **Bank**: select Bank under Ask for points. All other members at the time of creation must approve. Any rejection cancels the request. Unanimous approval issues new currency to the requester. A group needs at least two members to request bank currency.
- **Current status** lists all member balances. **History / pending requests** shows requests, outcomes, and bank approval counts. Eligible recipients can respond from history if a notification could not be delivered.
- **`/demand 10`** creates an independent request for 10 units from each other group member. One group is selected automatically; multiple groups show a selector. Each recipient decides independently. Amounts must be positive integers up to 2,147,483,647.
- **`/cancel`** exits an unfinished Score Counter prompt.

Pending requests do not reserve funds. Acceptance checks the latest balance and never allows a negative balance. Requests that cannot currently be funded remain pending and may be rejected or retried later. Duplicate approvals do not transfer currency twice. Concurrent changes are guarded by a group revision; if another update wins, the bot asks the user to retry.

Requests, votes, and the accepted transfer ledger are persisted in the database. Interactive creation prompts use the existing in-memory session system. Score Counter operates in private chats with the bot, matching the registered users' chat IDs.

## Deployment

The `AddScoreCounter` migration adds four tables and their constraints and indexes. Apply migrations to the intended database before running the updated bot:

```powershell
dotnet ef database update --context AppDbContext
```

EF tooling reads `ASPNETCORE_ENVIRONMENT` / `DOTNET_ENVIRONMENT` (Development by default) and the corresponding appsettings and environment variables. Development uses `DefaultConnectionTest`; other environments use `DefaultConnection`. The design-time factory avoids running bot polling or startup catalog seeding.

## Validation

```powershell
dotnet test Tests/ScoreCounterTests/ScoreCounter.Tests.csproj
```
