using FlaUI.Core.AutomationElements;
using Xunit;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Base class for all E2E tests. Provides access to AppFixture.
/// SPEC-HELP-001: REQ-HELP-051
/// </summary>
[Collection("E2E")]
public abstract class E2ETestBase
{
    protected readonly AppFixture Fixture;
    protected AutomationElement MainWindow =>
        Fixture.IsDesktopAvailable
            ? Fixture.MainWindow ?? throw new InvalidOperationException("Main window not available")
            : throw new InvalidOperationException(
                "E2E tests require an interactive desktop session. " +
                "Run from PowerShell terminal or Visual Studio (not CI/bash). " +
                "Use [RequiresDesktopFact] to auto-skip in non-interactive environments.");

    protected E2ETestBase(AppFixture fixture)
    {
        Fixture = fixture;
    }
}

/// <summary>
/// xUnit collection definition - E2E tests share one AppFixture instance.
/// </summary>
[CollectionDefinition("E2E")]
public sealed class E2ECollection : ICollectionFixture<AppFixture> { }
