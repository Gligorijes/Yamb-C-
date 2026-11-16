namespace Yamb.Application.Exceptions;

public class GameException : Exception
{
    public GameException(string message) : base(message)
    {
    }
}

public class GameNotFoundException : GameException
{
    public GameNotFoundException(Guid gameId) : base($"No saved game found with id {gameId}.")
    {
    }
}
