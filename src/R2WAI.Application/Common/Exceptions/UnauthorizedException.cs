namespace R2WAI.Application.Common.Exceptions;

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }

    public UnauthorizedException() : base("You are not authorized to perform this action.") { }
}
