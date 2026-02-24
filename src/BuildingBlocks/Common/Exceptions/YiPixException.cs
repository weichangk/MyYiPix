namespace YiPix.BuildingBlocks.Common.Exceptions;

public class YiPixException : Exception
{
    public int StatusCode { get; }

    public YiPixException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class NotFoundException : YiPixException
{
    public NotFoundException(string entity, Guid id)
        : base($"{entity} with id '{id}' was not found.", 404) { }
}

public class UnauthorizedException : YiPixException
{
    public UnauthorizedException(string message = "Unauthorized")
        : base(message, 401) { }
}

public class ForbiddenException : YiPixException
{
    public ForbiddenException(string message = "Forbidden")
        : base(message, 403) { }
}

public class ConflictException : YiPixException
{
    public ConflictException(string message)
        : base(message, 409) { }
}
