using KeyboardStudio.Core;
using KeyboardStudio.Windows;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsVirtualKeyMapperTests
{
    public static TheoryData<LogicalKey, WindowsVirtualKey> RepresentativeMappings => new()
    {
        { LogicalKey.A, WindowsVirtualKey.A },
        { LogicalKey.Digit7, WindowsVirtualKey.Digit7 },
        { LogicalKey.Enter, WindowsVirtualKey.Return },
        { LogicalKey.Space, WindowsVirtualKey.Space },
        { LogicalKey.InternationalBackslash, WindowsVirtualKey.Oem102 },
        { LogicalKey.NumpadEnter, WindowsVirtualKey.Return },
        { LogicalKey.RightAlt, WindowsVirtualKey.RightMenu },
        { LogicalKey.ArrowLeft, WindowsVirtualKey.Left }
    };

    [Theory]
    [Trait("Category", "Unit")]
    [MemberData(nameof(RepresentativeMappings))]
    public void TryMap_WhenLogicalKeyIsSupported_ReturnsExplicitVirtualKey(
        LogicalKey logicalKey,
        WindowsVirtualKey expected)
    {
        var mapped = WindowsVirtualKeyMapper.TryMap(logicalKey, out var actual);

        Assert.True(mapped);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryMap_WhenLogicalKeyIsNone_ReturnsFalse()
    {
        Assert.False(WindowsVirtualKeyMapper.TryMap(LogicalKey.None, out _));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryMap_WhenV1LogicalKeyIsDefined_MapsEveryKeyExplicitly()
    {
        var unmapped = Enum.GetValues<LogicalKey>()
            .Where(key => key != LogicalKey.None && !WindowsVirtualKeyMapper.TryMap(key, out _));

        Assert.Empty(unmapped);
    }
}
