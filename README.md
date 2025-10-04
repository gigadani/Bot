# Telegram RSVP Bot (C#/.NET 9)

[![CI](https://github.com/gigadani/Bot/actions/workflows/ci.yml/badge.svg)](https://github.com/gigadani/Bot/actions/workflows/ci.yml)

A simple, bilingual (fi/en) Telegram bot for collecting event RSVPs. Guests can sign up with their name and optional +1 (avec). Admins can export RSVPs to CSV, broadcast messages, and manage group-specific admins.

## Features
- Guided RSVP flow with language choice (fi/en)
- Optional +1 (avec) with name and optional @handle
- Stores data as JSON Lines (`data/rsvps.jsonl`)
- CSV export via in-chat command or CLI
- Broadcast messages (admin only)
- Superadmin can manage group-specific admins
- Party info endpoint to show text + optional image

## Requirements
- .NET SDK 9.0+
- Telegram Bot token from @BotFather

## Quick Start
1) Restore and build
   - `dotnet restore`
   - `dotnet build -c Debug`

2) Configure the bot token (recommended: user-secrets)
   - `dotnet user-secrets set "Telegram:Token" "123456:abc..."`
   - Or set it in `appsettings.json` under `Telegram:Token`

3) Optional admin settings
- Set `Admin:UserId` in `appsettings.json` to a numeric Telegram user ID or an `@username` to designate the superadmin.
- Superadmin can designate additional group-specific admins at runtime (see Admin Commands).

4) Optional single-group mode
- Set `Group:Id` to your group chat ID to enable the bot in that group. Without this, the bot operates only in private chats. When set, non-private messages from other groups are ignored, and group-admin commands operate on this configured group.

5) Optional party info
- Configure `Party:InfoTextPath` and/or `Party:InfoImagePath` in `appsettings.json`.
- If neither exists or has content, the “Party info” option is hidden.

6) Run locally
- `dotnet run`
- In Telegram, DM your bot and send `/start`.

## Usage (Guest)
- `/start` – begin the flow; choose language, enter full name, choose +1 (avec) yes/no, provide +1 details if applicable.
- Party info – choose the “Party info / Tapahtuman tiedot” option or send `/info`.
- Change +1 – from the completed menu tap “Change +1 name / Vaihda avecin nimi”, or send `/avec`.
- Remove signup – tap “Remove signup / Peru ilmoittautuminen”, or send `/removeme` or `/signout`.

## Admin Commands
- Export CSV
  - Button: “Export CSV / Vie CSV” (visible to admins), or send `/export`.
  - The bot replies with a CSV document of current (non-deleted) RSVPs.
- Broadcast
  - Reply to a message and send `/broadcast`, or use the “Broadcast / Lähetä kaikille” button.
  - Sends that message to all chats that interacted with the bot.
- Who am I
  - `/whoami` (admin only) returns your `UserId`, `ChatId`, and `@username`.
- Group admins (superadmin only, in private chat)
  - If `Group:Id` is set:
    - `/groupadmins list` – lists admins for the configured group
    - `/groupadmins add <userId>` – add admin to the configured group
    - `/groupadmins remove <userId>` – remove admin from the configured group
  - If `Group:Id` is not set: `/groupadmins` is disabled (no group configured).

## Data & Storage
- RSVP data: `data/rsvps.jsonl` (JSON Lines), append-only with the latest record considered authoritative per user.
- CSV exports: `data/rsvps.csv` (by default).
- Group admins: `data/group_admins.json`.
- Party info: paths resolved relative to the app base directory unless absolute.

## CLI Export Mode
Run without connecting to Telegram to generate a CSV from a JSONL file:
- `dotnet run -- export [out.csv] [in.jsonl]`
- Defaults: `out.csv = data/rsvps.csv`, `in.jsonl = data/rsvps.jsonl`.

## Build, Test, Format
- Build: `dotnet build -c Debug` | `dotnet build -c Release`
- Run tests: `dotnet test`
- Format code: `dotnet format`

## Deployment (systemd on Debian/Ubuntu)
An installer script is provided: `scripts/install-service.sh`.

Examples:
- From published output:
  - `sudo ./scripts/install-service.sh --from /path/to/publish --name rsvp-bot`
- Publish and install from project:
  - `sudo ./scripts/install-service.sh --project /path/to/Bot.csproj --name rsvp-bot`

Notes:
- The service installs to `/opt/<name>` (default `/opt/rsvp-bot`), runs as a dedicated user, and creates a writable `data/` directory.
- Provide configuration in `<install_dir>/appsettings.json` (e.g., `Telegram:Token`) or set user-secrets for the run user if preferred.
- Manage with `systemctl` (e.g., `sudo systemctl status rsvp-bot`).

## Configuration Reference (appsettings.json)
```
{
  "Telegram": {
    "Token": ""
  },
  "Admin": {
    "UserId": "@adminperson"  // or numeric ID
  },
  "Group": {
    "Id": "-1001234567890"   // optional: restrict to this group
  },
  "Party": {
    "InfoTextPath": "party-info.txt",
    "InfoImagePath": "party.jpg"
  }
}
```

## Security
- Do not commit secrets (tokens) to version control.
- Prefer `dotnet user-secrets` for local development or secure configuration on the server.

## Project Structure
- Entry point: `Program.cs`
- Core handlers: `Bot/BotHandlers.*.cs`
- Services: `Services/*` (RSVP storage, group admins, session state)
- Models: `Models/*`
- Utilities: `Util/CsvExporter.cs`
- Tests: `tests/Bot.Tests`

## License
This project is provided as-is. Add a license file if you intend to publish publicly.
