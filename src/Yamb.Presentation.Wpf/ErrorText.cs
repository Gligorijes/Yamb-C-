using Yamb.Application.Exceptions;

namespace Yamb.Presentation.Wpf;

public static class ErrorText
{
    public static string From(Exception exception)
    {
        if (Localization.Language == AppLanguage.English) return exception.Message;

        var message = exception.Message;
        if (exception is GameNotFoundException) return Localization.Text("L.Error.GameNotFound");

        return message switch
        {
            "At least one player name is required." => Localization.Text("L.Error.NameRequired"),
            "Player names cannot be empty." => Localization.Text("L.Error.NameEmpty"),
            "At least one player is required." => Localization.Text("L.Error.PlayerRequired"),
            "Yamb supports at most 2 players." => Localization.Text("L.Error.TooManyPlayers"),
            "VersusComputer mode is not implemented yet." => Localization.Text("L.Error.ComputerNotReady"),
            "Single player mode requires exactly one player." => Localization.Text("L.Error.SinglePlayerCount"),
            "Two player mode requires exactly two players." => Localization.Text("L.Error.TwoPlayerCount"),
            "Roll the dice first." => Localization.Text("L.Error.RollFirst"),
            "Cannot change holds after the final roll." => Localization.Text("L.Error.HoldsAfterFinal"),
            "Maximum 3 rolls per turn. Write a score first." => Localization.Text("L.Error.MaxRolls"),
            "Announcement can only be made after the first roll." => Localization.Text("L.Error.AnnouncePhase"),
            "A row has already been announced this turn." => Localization.Text("L.Error.AlreadyAnnounced"),
            "You must roll at least once before writing a score." => Localization.Text("L.Error.WriteBeforeRoll"),
            _ => TranslateDynamic(message)
        };
    }

    private static string TranslateDynamic(string message)
    {
        if (message.StartsWith("It is ", StringComparison.Ordinal) &&
            message.EndsWith("'s turn.", StringComparison.Ordinal))
        {
            int start = "It is ".Length;
            int length = message.Length - start - "'s turn.".Length;
            return Localization.Format("L.Error.OtherTurn", message.Substring(start, length));
        }

        if (message.StartsWith("Invalid player index ", StringComparison.Ordinal))
            return Localization.Text("L.Error.InvalidPlayer");

        if (message.StartsWith("No saved game found with id ", StringComparison.Ordinal))
            return Localization.Text("L.Error.GameNotFound");

        if (message.StartsWith("You announced ", StringComparison.Ordinal))
            return Localization.Text("L.Error.WriteAnnouncement");

        if (message.StartsWith("Cannot write to [", StringComparison.Ordinal))
            return Localization.Text("L.Error.NotAvailable");

        if (message.StartsWith("Maximum column requires ", StringComparison.Ordinal))
            return Localization.Text("L.Error.MaximumColumn");

        if (message.StartsWith("Row ", StringComparison.Ordinal) &&
            message.Contains("Announcement column", StringComparison.Ordinal))
        {
            return Localization.Text("L.Error.AnnouncementFilled");
        }

        if (message.StartsWith("Column ", StringComparison.Ordinal) &&
            message.EndsWith(" is not part of this sheet.", StringComparison.Ordinal))
        {
            return Localization.Text("L.Error.ColumnMissing");
        }

        if (message.StartsWith("Cannot write to any cell", StringComparison.Ordinal))
            return Localization.Text("L.Error.NotAvailable");

        return message;
    }
}
