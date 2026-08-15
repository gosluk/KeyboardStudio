using CommunityToolkit.Mvvm.ComponentModel;

namespace KeyboardStudio.App;

public sealed class BuildProfileSettingViewModel : ObservableObject
{
    private string _value;

    public BuildProfileSettingViewModel(string key, string label, string value)
    {
        Key = key;
        Label = label;
        _value = value;
    }

    public string Key { get; }

    public string Label { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}
