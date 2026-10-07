namespace Core.Http;

public sealed record HttpJsonReply<TResponse>(TResponse? Value, HttpCallFailure Failure, int? StatusCode, Exception? Exception)
{
    public bool Succeeded => Failure == HttpCallFailure.None;
}
