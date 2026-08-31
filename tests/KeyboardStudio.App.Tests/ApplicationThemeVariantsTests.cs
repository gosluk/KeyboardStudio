using Avalonia.Styling;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class ApplicationThemeVariantsTests
{
    [Theory]
    [InlineData(ApplicationTheme.White, "White")]
    [InlineData(ApplicationTheme.Gray, "Gray")]
    [InlineData(ApplicationTheme.Black, "Black")]
    [Trait("Category", "Unit")]
    public void For_WhenThemeIsSupported_ReturnsTheMatchingCustomVariant(
        ApplicationTheme theme,
        string expectedKey)
    {
        var variant = ApplicationThemeVariants.For(theme);

        Assert.Equal(expectedKey, variant.Key);
    }

    [Theory]
    [InlineData(ApplicationTheme.White)]
    [InlineData(ApplicationTheme.Gray)]
    [Trait("Category", "Unit")]
    public void For_WhenThemeIsLight_InheritsLightControlSemantics(ApplicationTheme theme) =>
        Assert.Equal(ThemeVariant.Light, ApplicationThemeVariants.For(theme).InheritVariant);

    [Fact]
    [Trait("Category", "Unit")]
    public void For_WhenThemeIsBlack_InheritsDarkControlSemantics() =>
        Assert.Equal(ThemeVariant.Dark, ApplicationThemeVariants.For(ApplicationTheme.Black).InheritVariant);

    [Fact]
    [Trait("Category", "Unit")]
    public void For_WhenCalledRepeatedly_ReturnsTheSameVariantInstance() =>
        Assert.Same(
            ApplicationThemeVariants.For(ApplicationTheme.Black),
            ApplicationThemeVariants.For(ApplicationTheme.Black));

    [Fact]
    [Trait("Category", "Unit")]
    public void For_WhenVariantsDiffer_TheyAreNotEquivalent()
    {
        Assert.NotEqual(
            ApplicationThemeVariants.For(ApplicationTheme.White),
            ApplicationThemeVariants.For(ApplicationTheme.Gray));
        Assert.NotEqual(ThemeVariant.Default, ApplicationThemeVariants.For(ApplicationTheme.Gray));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void For_WhenThemeIsNotDefined_Rejects() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ApplicationThemeVariants.For((ApplicationTheme)99));
}
