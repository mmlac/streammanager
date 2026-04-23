namespace StreamManager.Core.Auth;

public sealed class AuthState : IAuthState
{
    private readonly object _lock = new();
    private AccountInfo? _account;
    private string? _accessToken;

    public bool IsConnected => _account is not null;
    public AccountInfo? Account => _account;
    public string? AccessToken => _accessToken;

    public event EventHandler? Changed;

    public void SetConnected(AccountInfo account, string accessToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        lock (_lock)
        {
            _account = account;
            _accessToken = accessToken;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetDisconnected()
    {
        lock (_lock)
        {
            _account = null;
            _accessToken = null;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
