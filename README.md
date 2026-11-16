# Yamb

A modern Windows implementation of the Balkan dice game **Yamb**, built with .NET 8 and WPF.

## Features

- Single-player and local two-player games.
- 4 to 10 score columns, with 5 or 6 dice.
- Automatic and manual scoring modes.
- Cream dark theme and blue light theme.
- Serbian and English interface.
- Saved games, local leaderboard, detailed rules and version history.
- Pips, numbers and Roman-numeral dice faces.

## Architecture

The solution follows a Clean Architecture layout:

- `Yamb.Domain` - game rules, scoring and domain model.
- `Yamb.Application` - use cases, DTOs and service interfaces.
- `Yamb.Infrastructure` - SQLite persistence with EF Core.
- `Yamb.Presentation` - presentation contracts.
- `Yamb.Presentation.Wpf` - WPF desktop application.
- `tests` - domain, application and infrastructure tests.

## Requirements

- Windows 10 or newer.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

## Build and run

```powershell
dotnet build YambGame.sln -c Release
dotnet run --project src/Yamb.Presentation.Wpf/Yamb.Presentation.Wpf.csproj -c Release
```

## Tests

```powershell
dotnet test YambGame.sln -c Release
```

## Local data

Saved games, settings and the leaderboard are stored locally in:

```text
%LOCALAPPDATA%\Yamb
```

The database and local settings are intentionally excluded from the repository.

## Versioning

The current alpha version is shown in the app under **Help > About**. Build history is documented in [CHANGELOG.md](CHANGELOG.md).
