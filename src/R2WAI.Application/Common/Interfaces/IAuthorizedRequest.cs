namespace R2WAI.Application.Common.Interfaces;

public interface IAuthorizedRequest
{
    string[] RequiredRoles { get; }
}
