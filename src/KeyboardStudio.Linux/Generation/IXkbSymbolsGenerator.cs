namespace KeyboardStudio.Linux;

public interface IXkbSymbolsGenerator
{
    XkbGeneratedSymbols Generate(XkbKeyboardLayout layout);
}
