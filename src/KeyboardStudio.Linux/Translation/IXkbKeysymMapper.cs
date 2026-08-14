using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

public interface IXkbKeysymMapper
{
    bool TryMap(KeyOutput output, out string keysym);

    bool TryMap(LogicalKey logicalKey, out string keysym);
}
