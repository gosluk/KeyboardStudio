namespace KeyboardStudio.Core;

public sealed record KeyboardTemplateDescriptor(
    string Id,
    string Name,
    int ExpectedKeyCount,
    double UnitWidth,
    double UnitGap);
