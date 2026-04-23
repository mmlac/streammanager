namespace StreamManager.Core.Tests.Youtube;

// Minimal test clock — avoids pulling the Microsoft.Extensions.TimeProvider.Testing
// package just for a stable UtcNow in provider tests.
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);

    public void Set(DateTimeOffset value) => _now = value;
}
