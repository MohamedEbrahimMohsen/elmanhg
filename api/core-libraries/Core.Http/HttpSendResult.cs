namespace Core.Http;

public sealed record HttpSendResult(HttpCallFailure Failure, int? StatusCode, Exception? Exception)
{
    public bool Succeeded => Failure == HttpCallFailure.None;
}
