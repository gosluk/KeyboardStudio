using KeyboardStudio.App;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class ApplicationReleaseInfoTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void VersionProperties_WhenRead_KeepApplicationAndSchemasIndependent()
    {
        Assert.Equal("0.1.0", ApplicationReleaseInfo.Version);
        Assert.Equal("KeyboardStudio 0.1.0", ApplicationReleaseInfo.DisplayVersion);
        Assert.Equal(1, ApplicationReleaseInfo.ProjectSchemaVersion);

        // The envelope moved to 2 when it gained import provenance, while the release and the
        // Core project schema stayed where they were. That the three numbers disagree is the
        // point of having three of them.
        Assert.Equal(2, ApplicationReleaseInfo.DocumentSchemaVersion);
    }
}
