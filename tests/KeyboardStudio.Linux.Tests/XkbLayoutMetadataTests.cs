using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbLayoutMetadataTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_SanitizesPortableIdentifiers()
    {
        var metadata = new XkbLayoutMetadata(" 42 My/Layout ", "Fancy Variant!", " Example ");

        Assert.Equal("layout-42-mylayout", metadata.LayoutId);
        Assert.Equal("fancy-variant", metadata.SectionId);
        Assert.Equal("Example", metadata.Description);
    }
}
