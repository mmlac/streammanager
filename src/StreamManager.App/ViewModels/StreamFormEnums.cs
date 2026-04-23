namespace StreamManager.App.ViewModels;

public static class StreamFormEnums
{
    public static class PrivacyStatuses
    {
        public const string Public = "public";
        public const string Unlisted = "unlisted";
        public const string Private = "private";
        public static readonly string[] All = [Public, Unlisted, Private];
    }

    public static class LatencyPreferences
    {
        public const string Normal = "normal";
        public const string Low = "low";
        public const string UltraLow = "ultraLow";
        public static readonly string[] All = [Normal, Low, UltraLow];
    }

    public static class Projections
    {
        public const string Rectangular = "rectangular";
        public const string ThreeSixty = "360";
        public const string Mesh = "mesh";
        public static readonly string[] All = [Rectangular, ThreeSixty, Mesh];
    }

    public static class StereoLayouts
    {
        public const string Mono = "mono";
        public const string LeftRight = "left_right";
        public const string TopBottom = "top_bottom";
        public static readonly string[] All = [Mono, LeftRight, TopBottom];
    }

    public static class ClosedCaptionsTypes
    {
        public const string Disabled = "closedCaptionsDisabled";
        public const string HttpPost = "closedCaptionsHttpPost";
        public const string EmbedInVideo = "closedCaptionsEmbedInVideo";
        public static readonly string[] All = [Disabled, HttpPost, EmbedInVideo];
    }
}
