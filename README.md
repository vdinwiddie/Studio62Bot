# Studio 62 Discord Bot

This version implements:
- `/join-group`
- `/reserve`
- `/cancel`
- `/list`
- SQLite persistence for reservations

## Important security note
The bot token was included in the uploaded project. That means it should be treated as exposed.
Rotate the bot token in the Discord Developer Portal before running this version.

## What to configure

### 1) `BotToken.txt`
Put your bot token in `BotToken.txt`.
The app will also accept `DISCORD_BOT_TOKEN` from an environment variable.

### 2) `bands.json`
Replace the placeholder `roleId` values with your real Discord role IDs.
You can keep up to 4 bands, or add/remove entries as needed.

Example:
```json
[
  { "roleId": 123456789012345678, "name": "Band A", "enabled": true, "sortOrder": 1 },
  { "roleId": 234567890123456789, "name": "Band B", "enabled": true, "sortOrder": 2 }
]
```

### 3) `botsettings.json`
- `RegisterCommandsToGuildId`: your test server ID for fast slash-command updates.
- `DatabasePath`: where the SQLite database file will live.

## Required bot permissions / intents
Make sure the bot has:
- `Manage Roles`
- `Use Application Commands`
- `Send Messages`
- `Embed Links` (optional but useful later)

In the Discord Developer Portal, enable the **Server Members Intent**.

Also make sure the bot's highest role is above the band roles it needs to assign.

## How the commands work

### `/join-group`
Shows a select menu for the configured bands and adds the chosen role to the user.

### `/reserve`
Flow:
1. choose band from the roles the user already has
2. enter date as `M-D`
3. enter time as `H:mm` or `HH:mm` in 24-hour time
4. choose duration `1`, `2`, `3`, or `4`

Rules:
- auto-rolls into next year when the entered month/day has already passed this year
- blocks past start times
- blocks reservations more than one year ahead
- blocks overlaps with any existing reservation
- blocks reservations that cross midnight

### `/cancel`
Prompts for a reservation ID, verifies the user has the related band role or a Discord role named `Admin`, then asks for yes/no confirmation. Admin users can cancel any reservation.

### `/list`
Shows all upcoming reservations grouped by band.

## Suggested next steps in Visual Studio
1. Open the solution.
2. Restore NuGet packages.
3. Update `bands.json` with real role IDs.
4. Rotate the bot token and update `BotToken.txt`.
5. Run the bot.
6. Test `/join-group`, `/reserve`, `/cancel`, and `/list` in your server.

## Notes
- `bands.json` is read from disk, so you can update it without changing code.
- Reservations are stored in SQLite and survive restarts.
- Reservation IDs are auto-incrementing integers.
