namespace StreamManager.App.ViewModels;

public static class StreamFormLimits
{
    public const int TitleMaxLength = 100;
    public const int DescriptionMaxLength = 5000;
    public const int TagsCombinedMaxLength = 500;
    public const int BroadcastStreamDelayMinMs = 0;
    public const int BroadcastStreamDelayMaxMs = 60_000;
}
