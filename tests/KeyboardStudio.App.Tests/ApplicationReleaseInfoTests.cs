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
        Assert.Equal(1, ApplicationReleaseInfo.DocumentSchemaVersion);
    }
}
