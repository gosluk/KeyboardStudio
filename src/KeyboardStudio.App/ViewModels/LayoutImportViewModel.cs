using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

/// <summary>
/// Drives the import dialog: browse what the host offers, see what importing one would cost, and
/// choose what to do with it.
///
/// It reaches the platform only through <see cref="ILayoutImportCatalog"/>, so the dialog knows
/// nothing about XKB, symbols files, or any other source that may be registered later. Its other
/// arguments are plain data — the geometry templates the application already has — rather than the
/// providers that produce them.
///
/// Selecting a layout imports it immediately. The import is the preview: a fidelity report the user
/// cannot see until after they commit is a report they cannot act on.
/// </summary>
public sealed class LayoutImportViewModel : ObservableObject
{
    private readonly ILayoutImportCatalog _catalog;
    private readonly KeyboardTemplateDescriptor? _currentTemplate;
    private readonly ImportableLayoutDescriptor? _pinnedDescriptor;

    private ImportableLayoutViewModel[] _allLayouts = [];
    private IReadOnlyList<ImportableLayoutViewModel> _layouts = [];
    private IReadOnlyList<ImportableVariantViewModel> _variants = [];
    private IReadOnlyList<KeyViewModel> _previewKeys = [];
    private CancellationTokenSource? _previewCancellation;
    private ImportableLayoutViewModel? _selectedLayout;
    private ImportableVariantViewModel? _selectedVariant;
    private KeyboardTemplateDescriptor _selectedTemplate;
    private LayoutImportCommitMode _commitMode;
    private LayoutImportReportViewModel? _report;
    private LayoutImportResult? _result;
    private string _searchText = string.Empty;
    private string _status = string.Empty;
    private bool _isLoading;
    private bool _isPreviewing;
    private bool _isApplyingResult;
    private bool _useSuggestedGeometry = true;
    private double _previewWidth;
    private double _previewHeight;

    /// <param name="catalog">The only way this view model reaches a platform.</param>
    /// <param name="templates">Geometries the import may be laid onto.</param>
    /// <param name="currentTemplate">
    /// The open document's geometry, when there is one. Its presence is what makes replacing the
    /// open document's mappings an option, because that is the geometry they would land on.
    /// </param>
    public LayoutImportViewModel(
        ILayoutImportCatalog catalog,
        IReadOnlyList<KeyboardTemplateDescriptor> templates,
        KeyboardTemplateDescriptor? currentTemplate = null)
        : this(catalog, templates, currentTemplate, pinnedDescriptor: null)
    {
    }

    private LayoutImportViewModel(
        ILayoutImportCatalog catalog,
        IReadOnlyList<KeyboardTemplateDescriptor> templates,
        KeyboardTemplateDescriptor? currentTemplate,
        ImportableLayoutDescriptor? pinnedDescriptor)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(templates);

        if (templates.Count == 0)
        {
            throw new ArgumentException("At least one geometry template is required.", nameof(templates));
        }

        _catalog = catalog;
        _currentTemplate = currentTemplate;
        _pinnedDescriptor = pinnedDescriptor;
        Templates = templates;
        _selectedTemplate = currentTemplate ?? templates[0];
        LoadCommand = new AsyncRelayCommand(LoadAsync);
    }

    /// <summary>
    /// Builds a dialog around one layout the user named directly, for a file that no catalog lists.
    /// Everything else — the geometry override, the preview, the report — works the same way,
    /// because from here a pinned entry is just a catalog with one row in it.
    /// </summary>
    public static LayoutImportViewModel ForDescriptor(
        ILayoutImportCatalog catalog,
        IReadOnlyList<KeyboardTemplateDescriptor> templates,
        ImportableLayoutDescriptor descriptor,
        KeyboardTemplateDescriptor? currentTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new LayoutImportViewModel(catalog, templates, currentTemplate, descriptor);
    }

    public IReadOnlyList<KeyboardTemplateDescriptor> Templates { get; }

    public IAsyncRelayCommand LoadCommand { get; }

    /// <summary>
    /// The most recent preview, exposed so a caller can wait for it. Selecting a layout starts one
    /// without being asked to, which leaves no other handle on it.
    /// </summary>
    public Task PreviewTask { get; private set; } = Task.CompletedTask;

    /// <summary>Whether the catalog is browsable, as opposed to pinned to one named entry.</summary>
    public bool IsSearchable => _pinnedDescriptor is null;

    public string Title => _pinnedDescriptor is null ? "Import layout" : "Import symbols file";

    public IReadOnlyList<ImportableLayoutViewModel> Layouts
    {
        get => _layouts;
        private set
        {
            if (SetProperty(ref _layouts, value))
            {
                OnPropertyChanged(nameof(HasLayouts));
            }
        }
    }

    public bool HasLayouts => Layouts.Count > 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplyFilter();
            }
        }
    }

    public ImportableLayoutViewModel? SelectedLayout
    {
        get => _selectedLayout;
        set
        {
            if (!SetProperty(ref _selectedLayout, value))
            {
                return;
            }

            Variants = value?.Variants ?? [];
            SelectedVariant = Variants.Count > 0 ? Variants[0] : null;
        }
    }

    public IReadOnlyList<ImportableVariantViewModel> Variants
    {
        get => _variants;
        private set => SetProperty(ref _variants, value);
    }

    public ImportableVariantViewModel? SelectedVariant
    {
        get => _selectedVariant;
        set
        {
            if (SetProperty(ref _selectedVariant, value))
            {
                OnPropertyChanged(nameof(SelectedDescriptor));
                StartPreview();
            }
        }
    }

    /// <summary>The catalog entry an accepted import came from, recorded as the document's provenance.</summary>
    public ImportableLayoutDescriptor? SelectedDescriptor => SelectedVariant?.Descriptor;

    /// <summary>
    /// Whether to take the geometry the source infers. The registry does not record physical
    /// geometry, so the inference is a good guess rather than a fact, and a user with the keyboard
    /// in front of them knows better.
    /// </summary>
    public bool UseSuggestedGeometry
    {
        get => _useSuggestedGeometry;
        set
        {
            if (SetProperty(ref _useSuggestedGeometry, value) && !_isApplyingResult)
            {
                StartPreview();
            }
        }
    }

    public KeyboardTemplateDescriptor SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _selectedTemplate, value) && !_isApplyingResult && !UseSuggestedGeometry)
            {
                StartPreview();
            }
        }
    }

    /// <summary>
    /// Whether the geometry may be chosen. Replacing an open document's mappings keeps that
    /// document's keyboard, so there is nothing to choose: the import lands on the geometry the
    /// document already has, and offering a second one would only invite keys that cannot fit.
    /// </summary>
    public bool IsGeometrySelectable => CommitMode == LayoutImportCommitMode.NewProject;

    public bool CanReplaceMappings => _currentTemplate is not null;

    public LayoutImportCommitMode CommitMode
    {
        get => _commitMode;
        set
        {
            if (value == LayoutImportCommitMode.ReplaceMappings && !CanReplaceMappings)
            {
                return;
            }

            if (!SetProperty(ref _commitMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsGeometrySelectable));
            OnPropertyChanged(nameof(CommitAsNewProject));
            OnPropertyChanged(nameof(CommitAsMappingReplacement));

            if (value == LayoutImportCommitMode.ReplaceMappings)
            {
                _isApplyingResult = true;
                UseSuggestedGeometry = false;
                SelectedTemplate = _currentTemplate!;
                _isApplyingResult = false;
                StartPreview();
            }
        }
    }

    /// <summary>
    /// The two commit choices as the pair of booleans a radio button binds to. The mode itself
    /// stays a single enum, because two independent booleans is exactly the state a radio group
    /// must never be able to reach.
    /// </summary>
    public bool CommitAsNewProject
    {
        get => CommitMode == LayoutImportCommitMode.NewProject;
        set
        {
            if (value)
            {
                CommitMode = LayoutImportCommitMode.NewProject;
            }
        }
    }

    /// <inheritdoc cref="CommitAsNewProject" />
    public bool CommitAsMappingReplacement
    {
        get => CommitMode == LayoutImportCommitMode.ReplaceMappings;
        set
        {
            if (value)
            {
                CommitMode = LayoutImportCommitMode.ReplaceMappings;
            }
        }
    }

    public LayoutImportReportViewModel? Report
    {
        get => _report;
        private set => SetProperty(ref _report, value);
    }

    /// <summary>The imported project and its report, or null when nothing has been imported yet.</summary>
    public LayoutImportResult? Result
    {
        get => _result;
        private set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertyChanged(nameof(CanAccept));
            }
        }
    }

    public bool CanAccept => Result?.Success == true;

    public IReadOnlyList<KeyViewModel> PreviewKeys
    {
        get => _previewKeys;
        private set => SetProperty(ref _previewKeys, value);
    }

    public double PreviewWidth
    {
        get => _previewWidth;
        private set => SetProperty(ref _previewWidth, value);
    }

    public double PreviewHeight
    {
        get => _previewHeight;
        private set => SetProperty(ref _previewHeight, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsPreviewing
    {
        get => _isPreviewing;
        private set => SetProperty(ref _isPreviewing, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>
    /// Reads the catalog and selects the first entry, so the dialog opens showing something rather
    /// than an empty pane the user has to click into.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        Status = "Reading the layout catalog…";
        try
        {
            var descriptors = _pinnedDescriptor is null
                ? await _catalog.ListAsync(cancellationToken)
                : [_pinnedDescriptor];

            // Ordered by the name on the row, not by the code behind it. Sorting on the identifier
            // puts Dari above Albanian and Chinese below English, which reads as a list in no order
            // at all: the code that explains it is not the text the user is scanning.
            _allLayouts = descriptors
                .GroupBy(descriptor => descriptor.LayoutId, StringComparer.Ordinal)
                .Select(group => new ImportableLayoutViewModel(group.Key, group.ToArray()))
                .OrderBy(layout => layout.DisplayName, StringComparer.InvariantCultureIgnoreCase)
                .ThenBy(layout => layout.LayoutId, StringComparer.Ordinal)
                .ToArray();

            // Set before filtering, because filtering selects an entry and starts importing it,
            // and the import's own account of itself is the later and more useful thing to say.
            Status = _allLayouts.Length == 0
                ? "No layouts are available to import on this host."
                : $"{_allLayouts.Length} layouts available.";
            ApplyFilter();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _allLayouts = [];
            ApplyFilter();
            Status = $"The layout catalog could not be read: {exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Imports the selected layout with the options as they now stand. Called whenever a choice
    /// that changes the outcome changes, and exposed so a caller can run one deterministically.
    /// </summary>
    public async Task PreviewAsync(CancellationToken cancellationToken = default)
    {
        var descriptor = SelectedDescriptor;
        if (descriptor is null)
        {
            ApplyResult(null, null);
            return;
        }

        IsPreviewing = true;
        try
        {
            var options = new LayoutImportOptions(
                UseSuggestedGeometry ? null : SelectedTemplate.Id,
                descriptor.DisplayName);

            var result = await _catalog.ImportAsync(descriptor.ToReference(), options, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyResult(result, descriptor);
        }
        catch (OperationCanceledException)
        {
            // A newer selection is already being imported; it owns the result.
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ApplyResult(null, null);
            Status = $"'{descriptor.DisplayName}' could not be imported: {exception.Message}";
        }
        finally
        {
            IsPreviewing = false;
        }
    }

    private void StartPreview()
    {
        var previous = _previewCancellation;
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        previous?.Cancel();
        previous?.Dispose();

        PreviewTask = PreviewAsync(cancellation.Token);
    }

    private void ApplyResult(LayoutImportResult? result, ImportableLayoutDescriptor? descriptor)
    {
        Result = result;

        if (result is null)
        {
            Report = null;
            PreviewKeys = [];
            PreviewWidth = 0;
            PreviewHeight = 0;

            // Status is left alone: there is nothing to say about an import that did not happen,
            // and whoever cleared the selection has already said why.
            return;
        }

        var geometry = Templates.FirstOrDefault(template =>
            string.Equals(template.Id, result.SuggestedTemplateId, StringComparison.Ordinal));

        // The suggestion is echoed into the selector so that a user who takes it can still see
        // which geometry they took, and can switch away from it in one click.
        if (geometry is not null && UseSuggestedGeometry)
        {
            _isApplyingResult = true;
            SelectedTemplate = geometry;
            _isApplyingResult = false;
        }

        Report = new LayoutImportReportViewModel(result.Report, geometry?.Name);
        PreviewKeys = result.Project is null
            ? []
            : BuildPreview(result.Project, geometry ?? SelectedTemplate);
        PreviewWidth = PreviewKeys.Select(key => key.Left + key.Width).DefaultIfEmpty().Max();
        PreviewHeight = PreviewKeys.Select(key => key.Top + key.Height).DefaultIfEmpty().Max();

        var name = descriptor?.DisplayName ?? result.Project?.Metadata.Name ?? "The layout";
        Status = result.Success
            ? $"{name}: {Report.Summary}."
            : $"{name} could not be imported.";
    }

    /// <summary>
    /// Builds the read-only keycaps. They are the editor's own key view models with no selection
    /// behaviour attached, so the preview shows exactly what the editor will show — the point of a
    /// preview being that it is not a second rendering that can disagree.
    /// </summary>
    private static KeyViewModel[] BuildPreview(
        KeyboardProject project,
        KeyboardTemplateDescriptor template)
    {
        var keys = project.Keyboard.Keys
            .Select(key => new KeyViewModel(
                key,
                project.Layout.Find(key.Id),
                static _ => { },
                template.UnitWidth,
                template.UnitGap))
            .ToArray();

        foreach (var key in keys)
        {
            key.Refresh(ModifierLayer.Default);
        }

        return keys;
    }

    private void ApplyFilter()
    {
        var previousLayoutId = SelectedLayout?.LayoutId;
        var previousVariantId = SelectedVariant?.VariantId;

        Layouts = _allLayouts.Where(layout => layout.Matches(SearchText)).ToArray();

        if (Layouts.Count == 0 && _allLayouts.Length > 0)
        {
            Status = $"Nothing matches '{SearchText.Trim()}'.";
        }

        // A search that still contains the current selection keeps it, so typing does not throw
        // away a layout the user has already chosen and previewed.
        var retained = previousLayoutId is null
            ? null
            : Layouts.FirstOrDefault(layout =>
                string.Equals(layout.LayoutId, previousLayoutId, StringComparison.Ordinal));

        if (retained is not null)
        {
            if (!ReferenceEquals(retained, SelectedLayout))
            {
                SelectedLayout = retained;
            }

            var variant = retained.Variants.FirstOrDefault(candidate =>
                string.Equals(candidate.VariantId, previousVariantId, StringComparison.Ordinal));
            if (variant is not null && !ReferenceEquals(variant, SelectedVariant))
            {
                SelectedVariant = variant;
            }

            return;
        }

        SelectedLayout = Layouts.Count > 0 ? Layouts[0] : null;
    }
}
