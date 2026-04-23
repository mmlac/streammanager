namespace StreamManager.Core.Auth;

public interface IAuthState
{
    bool IsConnected { get; }
    AccountInfo? Account { get; }
    string? AccessToken { get; }

    event EventHandler? Changed;

    void SetConnected(AccountInfo account, string accessToken);
    void SetDisconnected();
}
