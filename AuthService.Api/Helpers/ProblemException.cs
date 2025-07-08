namespace AuthService.Api.Helpers;

[Serializable]
public class ProblemException(string error, int status, string? message = null, object? errors = null) : Exception(message)
{
    public string Error { get; } = error;
    public int Status { get; } = status;
    public object? Errors { get; } = errors;
}