using CommunityToolkit.Mvvm.ComponentModel;

namespace KeyboardStudio.App;

public sealed class BuildProfileSettingViewModel : ObservableObject
{
    private string _value;

    public BuildProfileSettingViewModel(
        string key,
        string label,
        string value,
        bool isVisible = true)
    {
        Key = key;
        Label = label;
        _value = value;
        IsVisible = isVisible;
    }

    public string Key { get; }

    public string Label { get; }

    public bool IsVisible { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}
