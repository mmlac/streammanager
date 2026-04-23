namespace StreamManager.Core.Auth;

public interface IUserInfoClient
{
    Task<AccountInfo> FetchAsync(string accessToken, CancellationToken ct);
}
