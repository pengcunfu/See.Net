using System.ComponentModel;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Highlighting;
using See.Net.ViewModels;

namespace See.Net.Views;

public partial class TextView : UserControl
{
    private TextContentViewModel? _vm;
    private bool _loading;

    public TextView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            Editor.TextChanged -= OnEditorTextChanged;
        }

        _vm = DataContext as TextContentViewModel;
        if (_vm is null) return;

        _vm.PropertyChanged += OnVmPropertyChanged;
        Editor.TextChanged += OnEditorTextChanged;
        _loading = true;
        try
        {
            Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(_vm.Highlighting);
            Editor.Text = _vm.Text;
            Editor.WordWrap = _vm.WordWrap;
            Editor.ScrollToHome();
        }
        finally
        {
            _loading = false;
            _vm.EndLoad();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TextContentViewModel.WordWrap) && _vm is not null)
        {
            Editor.WordWrap = _vm.WordWrap;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_loading || _vm is null) return;
        _vm.Text = Editor.Text;
        _vm.MarkDirty();
    }
}
