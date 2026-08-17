using System.ComponentModel;
using System.Windows.Controls;
using See.ViewModels;

namespace See.Views;

public partial class HexEditorView : UserControl
{
    private HexContentViewModel? _vm;

    public HexEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }
        _vm = DataContext as HexContentViewModel;
        if (_vm is null) return;
        _vm.AttachEditor(Editor);
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HexContentViewModel.InsertMode) && _vm is not null)
        {
            Editor.InsertMode = _vm.InsertMode;
        }
    }
}
