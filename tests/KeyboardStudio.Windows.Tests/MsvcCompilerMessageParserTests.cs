using KeyboardStudio.Build;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class MsvcCompilerMessageParserTests
{
    [Fact]
    public void Parse_MapsSourceAndLinkerDiagnostics()
    {
        var result = new ProcessResult(
            "cl.exe",
            [],
            "keyboard.c(42,7): warning C4100: unreferenced parameter\n",
            "LINK : fatal error LNK1104: cannot open file 'missing.lib'\n",
            2,
            TimeSpan.FromSeconds(1));

        var messages = MsvcCompilerMessageParser.Parse(result);

        Assert.Collection(
            messages,
            message =>
            {
                Assert.Equal("C4100", message.Code);
                Assert.Equal(CompilerMessageSeverity.Warning, message.Severity);
                Assert.Equal("keyboard.c", message.FilePath);
                Assert.Equal(42, message.Line);
                Assert.Equal(7, message.Column);
            },
            message =>
            {
                Assert.Equal("LNK1104", message.Code);
                Assert.Equal(CompilerMessageSeverity.Error, message.Severity);
                Assert.Null(message.FilePath);
            });
    }
}
