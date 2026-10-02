The compressed road snapshot contains public OpenStreetMap ways downloaded on
2026-10-01 from Overpass for the Tehran bounding box (35.65,51.32,35.83,51.49).
Query: `[out:json][timeout:90];way[highway](35.65,51.32,35.83,51.49);out geom;`

Map data © OpenStreetMap contributors, licensed under ODbL:
https://www.openstreetmap.org/copyright

This is an offline starter snapshot, not a Geofabrik import. A future Geofabrik
pipeline should provide equivalent road geometry and names. Starter point
coordinates are not used to invent street shapes. Only roads with matching
OSM names and supported road classes are eligible for lessons and quizzes.
No personal anchor coordinates are included in this snapshot.

The catalog importer adds 121 distinct named OSM streets with deterministic
IDs to SQL Server without replacing the original places. It runs idempotently
at startup and uses a SQL application lock to coordinate app instances.
Together with the original nine renderable streets this provides 130 learnable
places (65 two-place nights), in addition to the original point-only records.

Learning is per user: `UserPlaceLearningProgress.IsLearnt` defaults to false.
Quiz scores or old progress do not automatically reveal places. A nightly
session (Tehran local calendar date, UTC+03:30) assigns two unseen places.
Viewing a lesson is only a preview; confirmation unlocks it. Both confirmations
unlock a single quiz of up to 15 unique questions (name recognition, orientation,
relative position). Two known places supply five distinct questions; early
nights are shorter instead of repeating questions. Recognition questions use
unlabeled map photos. Orientation and relative-position questions are text-only
and name their streets in the prompt. Relative position uses eight compass
sectors, with four diagonal choices for diagonal answers and four cardinal
choices for cardinal answers. A relationship and its reverse are counted as
the same question. Completed nights cannot be repeated to unlock more places. An
interrupted night resumes even after midnight. No reminders are scheduled.

Maps contain only learned street geometry, with the current teaching preview
as the sole exception. The Map button labels learned streets; quiz images never
label streets. No background roads, neighborhoods or points are drawn.

Apply the additive migration before starting this version on another database:

    dotnet ef database update --project ScheduleBot --context AppDbContext

Run the integration harness from the repository root:

    dotnet run --project ScheduleBot/Tests/TehranFlowTests -c Release -- ScheduleBot

The harness uses the configured SQL Server with retries enabled, creates two
synthetic users, exercises real transactions, concurrency, ownership checks,
stale buttons, photo failures, completion, lesson gating and date rollover, and
removes only those users' test records in finally. It does not apply migrations.
It imports and retains the requested expanded catalog. Telegram
HTTP responses are simulated, and rendered PNGs are saved in Tests/artifacts
for visual inspection.

For offline-only map validation, append `--offline` after the root argument.
