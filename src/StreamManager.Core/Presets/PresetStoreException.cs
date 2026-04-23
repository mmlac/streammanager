namespace StreamManager.Core.Presets;

public sealed class PresetStoreException : Exception
{
    public PresetStoreException(PresetStoreErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public PresetStoreException(PresetStoreErrorCode code, string message, Exception inner)
        : base(message, inner)
    {
        Code = code;
    }

    public PresetStoreErrorCode Code { get; }
}
