using CommunityToolkit.Mvvm.ComponentModel;
using KeyboardStudio.Build;

namespace KeyboardStudio.App;

public sealed class BuildStageViewModel : ObservableObject
{
    private BuildStageState _state;

    public BuildStageViewModel(string name, BuildStageState state)
    {
        Name = name;
        _state = state;
    }

    public string Name { get; }

    public BuildStageState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    public void Update(BuildStageState state) => State = state;
}
