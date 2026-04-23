using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StreamManager.App.ViewModels;

public partial class StreamFormViewModel : ObservableValidator
{
    private readonly ObservableCollection<string> _tags = new();
    private StreamFormSnapshot? _liveBaseline;
    private StreamFormSnapshot? _presetBaseline;
    private PresetLineage? _presetLineage;
    private bool _suppressRecompute;
    private bool _isDirtyVsLive;
    private bool _isDirtyVsPreset;

    public StreamFormViewModel()
    {
        _tags.CollectionChanged += OnTagsCollectionChanged;
        ValidateAllProperties();
        RecomputeDirty();
    }

    // ---- Basics ----

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(
        StreamFormLimits.TitleMaxLength,
        ErrorMessage = "Title must be ≤ 100 characters.")]
    private string _title = "";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(
        StreamFormLimits.DescriptionMaxLength,
        ErrorMessage = "Description must be ≤ 5000 characters.")]
    private string _description = "";

    [ObservableProperty]
    private string? _categoryId;

    [CustomValidation(typeof(StreamFormViewModel), nameof(ValidateTags))]
    public ObservableCollection<string> Tags => _tags;

    [ObservableProperty]
    private string _pendingTagInput = "";

    // ---- Privacy ----

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [EnumWhitelist("public", "unlisted", "private")]
    private string _privacyStatus = StreamFormEnums.PrivacyStatuses.Public;

    [ObservableProperty]
    private bool _selfDeclaredMadeForKids;

    // ---- Playback & DVR ----

    [ObservableProperty] private bool _enableAutoStart = true;
    [ObservableProperty] private bool _enableAutoStop = true;
    [ObservableProperty] private bool _enableClosedCaptions;
    [ObservableProperty] private bool _enableDvr = true;
    [ObservableProperty] private bool _enableEmbed = true;
    [ObservableProperty] private bool _recordFromStart = true;
    [ObservableProperty] private bool _startWithSlate;
    [ObservableProperty] private bool _enableContentEncryption;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [EnumWhitelist(
        "closedCaptionsDisabled",
        "closedCaptionsHttpPost",
        "closedCaptionsEmbedInVideo")]
    private string _closedCaptionsType = StreamFormEnums.ClosedCaptionsTypes.Disabled;

    // ---- Advanced ----

    [ObservableProperty] private bool _enableLowLatency;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [EnumWhitelist("normal", "low", "ultraLow")]
    private string _latencyPreference = StreamFormEnums.LatencyPreferences.Normal;

    [ObservableProperty] private bool _enableMonitorStream = true;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(
        StreamFormLimits.BroadcastStreamDelayMinMs,
        StreamFormLimits.BroadcastStreamDelayMaxMs,
        ErrorMessage = "Broadcast stream delay must be between 0 and 60000 ms.")]
    private int _broadcastStreamDelayMs;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [EnumWhitelist("rectangular", "360", "mesh")]
    private string _projection = StreamFormEnums.Projections.Rectangular;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [EnumWhitelist("mono", "left_right", "top_bottom")]
    private string _stereoLayout = StreamFormEnums.StereoLayouts.Mono;

    // ---- Scheduling (text-driven, parsed to DateTimeOffset?) ----

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(StreamFormViewModel), nameof(ValidateScheduledTimeText))]
    private string _scheduledStartTimeText = "";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(StreamFormViewModel), nameof(ValidateScheduledTimeText))]
    private string _scheduledEndTimeText = "";

    // ---- Language ----

    [ObservableProperty] private string? _defaultLanguage;
    [ObservableProperty] private string? _defaultAudioLanguage;

    // ---- Thumbnail ----

    [ObservableProperty] private string? _thumbnailPath;

    // ---- Connection / live-broadcast state ----
    // IsConnected is driven by IAuthState (set by MainWindowViewModel on auth
    // events). HasLiveBroadcast is driven by IStreamFetchCoordinator after a
    // fetch (§6.2). Both feed CanApply.

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    private bool _hasLiveBroadcast;
    public bool HasLiveBroadcast
    {
        get => _hasLiveBroadcast;
        set
        {
            if (SetProperty(ref _hasLiveBroadcast, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    // ---- Remote thumbnail URL (read-only preview from the active broadcast). ----
    // Distinct from ThumbnailPath, which is a locally-picked file (slice 8).
    // Cleared when no broadcast is active.

    [ObservableProperty]
    private string? _remoteThumbnailUrl;

    // ---- Identifiers from the last successful fetch. ----
    // ApplyOrchestrator (§6.6) needs both to address liveBroadcasts.update
    // (broadcast resource) and videos.update (video resource). They are not
    // user-editable form fields, so they're held separately from the snapshot.
    // Cleared when no broadcast is active so a stale ID can't be used after
    // a Disconnect / NotLive transition.

    public string? LastFetchedBroadcastId { get; private set; }

    public string? LastFetchedVideoId { get; private set; }

    // ---- Dropdown option sources ----

    public IReadOnlyList<string> PrivacyStatusOptions => StreamFormEnums.PrivacyStatuses.All;
    public IReadOnlyList<string> LatencyPreferenceOptions => StreamFormEnums.LatencyPreferences.All;
    public IReadOnlyList<string> ProjectionOptions => StreamFormEnums.Projections.All;
    public IReadOnlyList<string> StereoLayoutOptions => StreamFormEnums.StereoLayouts.All;
    public IReadOnlyList<string> ClosedCaptionsTypeOptions => StreamFormEnums.ClosedCaptionsTypes.All;

    // ---- Dirty / lineage surface ----

    public bool IsDirtyVsLive
    {
        get => _isDirtyVsLive;
        private set => SetProperty(ref _isDirtyVsLive, value);
    }

    public bool IsDirtyVsPreset
    {
        get => _isDirtyVsPreset;
        private set => SetProperty(ref _isDirtyVsPreset, value);
    }

    public PresetLineage? PresetLineage
    {
        get => _presetLineage;
        private set
        {
            if (SetProperty(ref _presetLineage, value))
            {
                OnPropertyChanged(nameof(HasPresetLineage));
                OnPropertyChanged(nameof(CanUpdatePreset));
            }
        }
    }

    public bool HasPresetLineage => _presetLineage is not null;

    public bool CanUpdatePreset => _presetLineage is not null && IsDirtyVsPreset;

    public bool CanApply => IsConnected && HasLiveBroadcast && !HasErrors;

    public string DirtyStatusLine
    {
        get
        {
            var parts = new List<string>();
            parts.Add(IsDirtyVsLive ? "Dirty vs live" : "Clean vs live");
            parts.Add(PresetLineage is null
                ? "No preset"
                : $"Loaded from preset \"{PresetLineage.Name}\"{(IsDirtyVsPreset ? " (modified)" : "")}");
            return string.Join(" · ", parts);
        }
    }

    // ---- Public commands ----

    [RelayCommand]
    private void AddPendingTag()
    {
        var raw = (PendingTagInput ?? "").Trim();
        if (raw.Length == 0) return;

        var combined = _tags.Sum(t => t.Length) + raw.Length;
        if (combined > StreamFormLimits.TagsCombinedMaxLength)
        {
            // Enforce the limit at add-time; UI surfaces via Tags validation.
            return;
        }

        _tags.Add(raw);
        PendingTagInput = "";
    }

    [RelayCommand]
    private void RemoveTag(string? tag)
    {
        if (tag is null) return;
        _tags.Remove(tag);
    }

    [RelayCommand]
    private void RemoveLastTag()
    {
        if (_tags.Count == 0) return;
        _tags.RemoveAt(_tags.Count - 1);
    }

    // ---- Baselines ----

    public void SetLiveBaseline(StreamFormSnapshot? snapshot)
    {
        SetLiveBaseline(snapshot, broadcastId: null, videoId: null);
    }

    // Slice 5: the orchestrator needs the broadcast/video IDs from the same
    // fetch that produced the form snapshot, so SetLiveBaseline takes them
    // alongside. The fetch coordinator passes them in; preset / test paths
    // that only have form data fall through to the no-id overload above.
    public void SetLiveBaseline(StreamFormSnapshot? snapshot, string? broadcastId, string? videoId)
    {
        _liveBaseline = snapshot;
        LastFetchedBroadcastId = broadcastId;
        LastFetchedVideoId = videoId;
        if (snapshot is not null)
        {
            ApplySnapshotToForm(snapshot);
        }
        RecomputeDirty();
    }

    // §6.6 step 4 — the thumbnails.set call fires only when the form's
    // thumbnailPath has been changed since the last fetch. The baseline's
    // ThumbnailPath is always null after a fetch (the API returns a remote
    // URL, not a local file), so any non-null current path counts as
    // "changed". Kept here so callers don't need to peek at _liveBaseline.
    public bool IsThumbnailChangedFromLive =>
        !string.Equals(ThumbnailPath, _liveBaseline?.ThumbnailPath, StringComparison.Ordinal);

    public void SetPresetBaseline(StreamFormSnapshot snapshot, PresetLineage lineage)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(lineage);

        _presetBaseline = snapshot;
        PresetLineage = lineage;
        ApplySnapshotToForm(snapshot);
        RecomputeDirty();
    }

    public void ClearPresetLineage()
    {
        _presetBaseline = null;
        PresetLineage = null;
        RecomputeDirty();
    }

    // Used by "Save as preset…" — the form already holds the values being
    // saved, so don't re-apply the snapshot (which would fire property
    // change notifications and confuse any bound UI). Just adopt the
    // current values as the new preset baseline and set lineage.
    public void SetPresetBaselineFromCurrent(PresetLineage lineage)
    {
        ArgumentNullException.ThrowIfNull(lineage);
        _presetBaseline = CaptureSnapshot();
        PresetLineage = lineage;
        RecomputeDirty();
    }

    // Used by "Update preset 'X'" — overwrite the preset baseline with
    // the current form state so dirty-vs-preset resets to clean while
    // lineage stays intact.
    public void RebaselineCurrentPreset()
    {
        if (_presetLineage is null) return;
        _presetBaseline = CaptureSnapshot();
        RecomputeDirty();
    }

    public StreamFormSnapshot CaptureSnapshot() => new()
    {
        Title = Title,
        Description = Description,
        CategoryId = CategoryId,
        Tags = _tags.ToArray(),
        PrivacyStatus = PrivacyStatus,
        SelfDeclaredMadeForKids = SelfDeclaredMadeForKids,
        EnableAutoStart = EnableAutoStart,
        EnableAutoStop = EnableAutoStop,
        EnableClosedCaptions = EnableClosedCaptions,
        EnableDvr = EnableDvr,
        EnableEmbed = EnableEmbed,
        RecordFromStart = RecordFromStart,
        StartWithSlate = StartWithSlate,
        EnableContentEncryption = EnableContentEncryption,
        EnableLowLatency = EnableLowLatency,
        LatencyPreference = LatencyPreference,
        EnableMonitorStream = EnableMonitorStream,
        BroadcastStreamDelayMs = BroadcastStreamDelayMs,
        Projection = Projection,
        StereoLayout = StereoLayout,
        ClosedCaptionsType = ClosedCaptionsType,
        ScheduledStartTime = TryParseDateTimeOffset(ScheduledStartTimeText),
        ScheduledEndTime = TryParseDateTimeOffset(ScheduledEndTimeText),
        DefaultLanguage = DefaultLanguage,
        DefaultAudioLanguage = DefaultAudioLanguage,
        ThumbnailPath = ThumbnailPath,
    };

    // ---- Validation methods used by [CustomValidation] ----

    public static ValidationResult? ValidateTags(object? value, ValidationContext ctx)
    {
        if (value is IEnumerable<string> tags)
        {
            var total = 0;
            foreach (var t in tags) total += (t ?? "").Length;
            if (total > StreamFormLimits.TagsCombinedMaxLength)
            {
                return new ValidationResult(
                    $"Tags combined length must be ≤ {StreamFormLimits.TagsCombinedMaxLength} characters (currently {total}).",
                    new[] { nameof(Tags) });
            }
        }
        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateScheduledTimeText(object? value, ValidationContext ctx)
    {
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s)) return ValidationResult.Success;
        return DateTimeOffset.TryParse(
                s,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _)
            ? ValidationResult.Success
            : new ValidationResult(
                "Must be an ISO-8601 date/time (e.g. 2026-04-23T18:00:00Z).",
                new[] { ctx.MemberName ?? "" });
    }

    // ---- Internals ----

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_suppressRecompute) return;
        if (IsPassiveProperty(e.PropertyName)) return;
        RecomputeDirty();
    }

    private static bool IsPassiveProperty(string? name) => name is
        nameof(IsDirtyVsLive) or
        nameof(IsDirtyVsPreset) or
        nameof(HasErrors) or
        nameof(HasPresetLineage) or
        nameof(CanUpdatePreset) or
        nameof(CanApply) or
        nameof(DirtyStatusLine) or
        nameof(PresetLineage) or
        nameof(PendingTagInput) or
        nameof(IsConnected) or
        nameof(HasLiveBroadcast) or
        nameof(RemoteThumbnailUrl);

    partial void OnTitleChanged(string value) => OnValidatablePropertyChanged();
    partial void OnDescriptionChanged(string value) => OnValidatablePropertyChanged();
    partial void OnPrivacyStatusChanged(string value) => OnValidatablePropertyChanged();
    partial void OnLatencyPreferenceChanged(string value) => OnValidatablePropertyChanged();
    partial void OnProjectionChanged(string value) => OnValidatablePropertyChanged();
    partial void OnStereoLayoutChanged(string value) => OnValidatablePropertyChanged();
    partial void OnClosedCaptionsTypeChanged(string value) => OnValidatablePropertyChanged();
    partial void OnBroadcastStreamDelayMsChanged(int value) => OnValidatablePropertyChanged();
    partial void OnScheduledStartTimeTextChanged(string value) => OnValidatablePropertyChanged();
    partial void OnScheduledEndTimeTextChanged(string value) => OnValidatablePropertyChanged();

    private void OnValidatablePropertyChanged()
    {
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(CanApply));
    }

    private void OnTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ValidateProperty(_tags, nameof(Tags));
        OnValidatablePropertyChanged();
        if (_suppressRecompute) return;
        RecomputeDirty();
    }

    private void RecomputeDirty()
    {
        var snap = CaptureSnapshot();
        IsDirtyVsLive = _liveBaseline is null ? false : !snap.ValueEquals(_liveBaseline);
        IsDirtyVsPreset = _presetBaseline is null ? false : !snap.ValueEquals(_presetBaseline);
        OnPropertyChanged(nameof(CanUpdatePreset));
        OnPropertyChanged(nameof(DirtyStatusLine));
    }

    private void ApplySnapshotToForm(StreamFormSnapshot s)
    {
        _suppressRecompute = true;
        try
        {
            Title = s.Title;
            Description = s.Description;
            CategoryId = s.CategoryId;
            _tags.Clear();
            foreach (var t in s.Tags) _tags.Add(t);
            PrivacyStatus = s.PrivacyStatus;
            SelfDeclaredMadeForKids = s.SelfDeclaredMadeForKids;
            EnableAutoStart = s.EnableAutoStart;
            EnableAutoStop = s.EnableAutoStop;
            EnableClosedCaptions = s.EnableClosedCaptions;
            EnableDvr = s.EnableDvr;
            EnableEmbed = s.EnableEmbed;
            RecordFromStart = s.RecordFromStart;
            StartWithSlate = s.StartWithSlate;
            EnableContentEncryption = s.EnableContentEncryption;
            EnableLowLatency = s.EnableLowLatency;
            LatencyPreference = s.LatencyPreference;
            EnableMonitorStream = s.EnableMonitorStream;
            BroadcastStreamDelayMs = s.BroadcastStreamDelayMs;
            Projection = s.Projection;
            StereoLayout = s.StereoLayout;
            ClosedCaptionsType = s.ClosedCaptionsType;
            ScheduledStartTimeText = FormatDateTimeOffset(s.ScheduledStartTime);
            ScheduledEndTimeText = FormatDateTimeOffset(s.ScheduledEndTime);
            DefaultLanguage = s.DefaultLanguage;
            DefaultAudioLanguage = s.DefaultAudioLanguage;
            ThumbnailPath = s.ThumbnailPath;
        }
        finally
        {
            _suppressRecompute = false;
        }
        ValidateAllProperties();
        ValidateProperty(_tags, nameof(Tags));
    }

    private static DateTimeOffset? TryParseDateTimeOffset(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var result)
            ? result
            : null;
    }

    private static string FormatDateTimeOffset(DateTimeOffset? value) =>
        value is null ? "" : value.Value.ToString("O", CultureInfo.InvariantCulture);
}
