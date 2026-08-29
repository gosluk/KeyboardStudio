namespace KeyboardStudio.Linux;

internal static class XkbKeyTypeNames
{
    public static string Get(XkbKeyType type) => type switch
    {
        XkbKeyType.OneLevel => "ONE_LEVEL",
        XkbKeyType.TwoLevel => "TWO_LEVEL",
        XkbKeyType.Alphabetic => "ALPHABETIC",
        XkbKeyType.Keypad => "KEYPAD",
        XkbKeyType.FourLevel => "FOUR_LEVEL",
        XkbKeyType.FourLevelAlphabetic => "FOUR_LEVEL_ALPHABETIC",
        XkbKeyType.FourLevelSemialphabetic => "FOUR_LEVEL_SEMIALPHABETIC",
        XkbKeyType.FourLevelMixedKeypad => "FOUR_LEVEL_MIXED_KEYPAD",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown XKB key type.")
    };
}
