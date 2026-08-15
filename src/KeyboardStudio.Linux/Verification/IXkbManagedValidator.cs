namespace KeyboardStudio.Linux;

public interface IXkbManagedValidator
{
    IReadOnlyList<XkbDiagnostic> Validate(
        XkbKeyboardLayout layout,
        XkbGeneratedSymbols generated);
}
