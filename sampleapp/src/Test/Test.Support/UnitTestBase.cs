// ═══════════════════════════════════════════════════════════════
// Pattern: UnitTestBase — base class for all unit tests.
// Uses MockRepository factory pattern with DefaultValue.Mock for auto-mocking.
// All unit test classes inherit from this.
// ═══════════════════════════════════════════════════════════════

using Moq;

namespace Test.Support;

/// <summary>
/// Pattern: Mock factory base — consistent MockRepository configuration.
/// MockBehavior.Default (Loose) — no need to Setup every called method.
/// DefaultValue.Mock — auto-generates mock values for nested properties.
/// </summary>
public abstract class UnitTestBase
{
    protected readonly MockRepository _mockFactory;

    protected UnitTestBase()
    {
        _mockFactory = new MockRepository(MockBehavior.Default) { DefaultValue = DefaultValue.Mock };
    }
}
