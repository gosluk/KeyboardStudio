namespace KeyboardStudio.Linux;

/// <summary>
/// Which of the places a Linux host records its keyboard choice actually supplied the answer.
///
/// Carried on the result because the sources disagree in kind, not only in precedence: an
/// environment variable is a deliberate override for this process, a configuration file is what the
/// system was set up with, and the fallback is a guess. A caller that wants to explain what it did
/// needs to know which of those it got.
/// </summary>
public enum XkbActiveLayoutOrigin
{
    /// <summary><c>XKB_DEFAULT_LAYOUT</c> and <c>XKB_DEFAULT_VARIANT</c>.</summary>
    Environment = 0,

    /// <summary><c>Option "XkbLayout"</c> in the X server's keyboard configuration.</summary>
    XorgConfiguration = 1,

    /// <summary><c>/etc/vconsole.conf</c>, where systemd records the keyboard for the whole system.</summary>
    VirtualConsole = 2,

    /// <summary><c>/etc/default/keyboard</c>, the Debian family's equivalent.</summary>
    KeyboardDefaults = 3,

    /// <summary>Nothing said anything, so <c>us</c> was assumed.</summary>
    Fallback = 4
}
