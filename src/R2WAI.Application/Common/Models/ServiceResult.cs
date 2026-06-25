using MediatR;

namespace R2WAI.Application.Common.Models;

public class ServiceResult<T>
{
    public bool Succeeded { get; init; }
    public T? Data { get; init; }
    public string[] Errors { get; init; } = [];

    public static ServiceResult<T> Success(T data) => new() { Succeeded = true, Data = data };
    public static ServiceResult<T> Failure(params string[] errors) => new() { Succeeded = false, Errors = errors };
}

public class ServiceResult : ServiceResult<Unit> { }
