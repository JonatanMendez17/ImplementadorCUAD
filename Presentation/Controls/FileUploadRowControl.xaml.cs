using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ImplementadorCUAD.Presentation.Controls;

public partial class FileUploadRowControl : UserControl
{
    public FileUploadRowControl()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(FileUploadRowControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(FileUploadRowControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(FileUploadRowControl), new PropertyMetadata("↑"));

    public static readonly DependencyProperty ToolTipTextProperty =
        DependencyProperty.Register(nameof(ToolTipText), typeof(string), typeof(FileUploadRowControl), new PropertyMetadata(null));

    public static readonly DependencyProperty IsFileLoadedProperty =
        DependencyProperty.Register(nameof(IsFileLoaded), typeof(bool), typeof(FileUploadRowControl), new PropertyMetadata(false));

    public static readonly DependencyProperty SelectCommandProperty =
        DependencyProperty.Register(nameof(SelectCommand), typeof(ICommand), typeof(FileUploadRowControl), new PropertyMetadata(null));

    public static readonly DependencyProperty ClearCommandProperty =
        DependencyProperty.Register(nameof(ClearCommand), typeof(ICommand), typeof(FileUploadRowControl), new PropertyMetadata(null));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? ToolTipText
    {
        get => (string?)GetValue(ToolTipTextProperty);
        set => SetValue(ToolTipTextProperty, value);
    }

    public bool IsFileLoaded
    {
        get => (bool)GetValue(IsFileLoadedProperty);
        set => SetValue(IsFileLoadedProperty, value);
    }

    public ICommand? SelectCommand
    {
        get => (ICommand?)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public ICommand? ClearCommand
    {
        get => (ICommand?)GetValue(ClearCommandProperty);
        set => SetValue(ClearCommandProperty, value);
    }
}
