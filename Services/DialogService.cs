using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ImplementadorCUAD.Services
{
    public static class DialogService
    {
        public static MessageBoxResult Show(
            string message,
            string title,
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage image = MessageBoxImage.None,
            string? primaryButtonText = null,
            string? secondaryButtonText = null,
            string? tertiaryButtonText = null)
        {
            var dialog = new StyledDialogWindow(
                message,
                title,
                buttons,
                image,
                primaryButtonText,
                secondaryButtonText,
                tertiaryButtonText);
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                        ?? Application.Current?.MainWindow;

            if (owner != null && owner != dialog)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            dialog.ShowDialog();
            return dialog.Result;
        }

    }

    internal sealed class StyledDialogWindow : Window
    {
        private const double UiCornerRadius = 8;
        private readonly MessageBoxButton _buttons;
        private readonly string? _primaryButtonText;
        private readonly string? _secondaryButtonText;
        private readonly string? _tertiaryButtonText;
        private readonly Button _primaryButton;
        private readonly Button _secondaryButton;
        private readonly Button _tertiaryButton;

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public StyledDialogWindow(
            string message,
            string title,
            MessageBoxButton buttons,
            MessageBoxImage image,
            string? primaryButtonText,
            string? secondaryButtonText,
            string? tertiaryButtonText)
        {
            _buttons = buttons;
            _primaryButtonText = primaryButtonText;
            _secondaryButtonText = secondaryButtonText;
            _tertiaryButtonText = tertiaryButtonText;
            Title = title;
            Width = 520;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = true;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var panelBrush = TryGetBrush("PanelBrush") ?? new System.Windows.Media.SolidColorBrush(Color.FromRgb(31, 41, 51));
            var panelBorderBrush = TryGetBrush("PanelBorderBrush") ?? new System.Windows.Media.SolidColorBrush(Color.FromRgb(55, 65, 81));
            var textPrimary = TryGetBrush("TextPrimaryBrush") ?? Brushes.White;
            var textSecondary = TryGetBrush("TextSecondaryBrush") ?? new System.Windows.Media.SolidColorBrush(Color.FromRgb(229, 231, 235));

            var rootBorder = new Border
            {
                Background = panelBrush,
                BorderBrush = panelBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(UiCornerRadius),
                Padding = new Thickness(20, 12, 20, 12)
            };

            var panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = title,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPrimary
            };
            Grid.SetRow(header, 0);
            panel.Children.Add(header);

            var content = new Grid
            {
                Margin = new Thickness(0, 10, 0, 10),
                VerticalAlignment = VerticalAlignment.Center
            };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var icon = BuildIcon(image);
            if (icon != null)
            {
                Grid.SetColumn(icon, 0);
                content.Children.Add(icon);
            }

            var messageText = new TextBlock
            {
                Text = message,
                Foreground = textSecondary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(icon != null ? 12 : 0, 0, 0, 0)
            };
            Grid.SetColumn(messageText, 1);
            content.Children.Add(messageText);
            Grid.SetRow(content, 1);
            panel.Children.Add(content);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            _tertiaryButton = BuildButton("Cancelar", false);
            _secondaryButton = BuildButton("No", false);
            _primaryButton = BuildButton("Aceptar", true);

            _tertiaryButton.Click += (_, _) => CloseWithResult(MessageBoxResult.Cancel);
            _secondaryButton.Click += (_, _) => CloseWithResult(
                _buttons == MessageBoxButton.OKCancel ? MessageBoxResult.Cancel : MessageBoxResult.No);
            _primaryButton.Click += (_, _) => CloseWithResult(
                _buttons == MessageBoxButton.YesNo || _buttons == MessageBoxButton.YesNoCancel
                    ? MessageBoxResult.Yes
                    : MessageBoxResult.OK);

            buttonPanel.Children.Add(_tertiaryButton);
            buttonPanel.Children.Add(_secondaryButton);
            buttonPanel.Children.Add(_primaryButton);
            Grid.SetRow(buttonPanel, 2);
            panel.Children.Add(buttonPanel);

            rootBorder.Child = panel;
            Content = rootBorder;

            // Permitir arrastrar la ventana haciendo clic en cualquier parte del recuadro
            rootBorder.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            };

            ConfigureButtons();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Keyboard.Focus(_primaryButton);
        }

        private void ConfigureButtons()
        {
            _tertiaryButton.Visibility = Visibility.Collapsed;
            _secondaryButton.Visibility = Visibility.Collapsed;

            switch (_buttons)
            {
                case MessageBoxButton.OK:
                    _primaryButton.Content = "Aceptar";
                    break;
                case MessageBoxButton.OKCancel:
                    _primaryButton.Content = "Aceptar";
                    _secondaryButton.Content = "Cancelar";
                    _secondaryButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    _primaryButton.Content = "Si";
                    _secondaryButton.Content = "No";
                    _secondaryButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                    _primaryButton.Content = "Si";
                    _secondaryButton.Content = "No";
                    _tertiaryButton.Content = "Cancelar";
                    _secondaryButton.Visibility = Visibility.Visible;
                    _tertiaryButton.Visibility = Visibility.Visible;
                    break;
            }

            if (!string.IsNullOrWhiteSpace(_primaryButtonText))
            {
                _primaryButton.Content = _primaryButtonText;
            }

            if (!string.IsNullOrWhiteSpace(_secondaryButtonText))
            {
                _secondaryButton.Content = _secondaryButtonText;
            }

            if (!string.IsNullOrWhiteSpace(_tertiaryButtonText))
            {
                _tertiaryButton.Content = _tertiaryButtonText;
            }
        }

        private void CloseWithResult(MessageBoxResult result)
        {
            Result = result;
            Close();
        }

        private static UIElement? BuildIcon(MessageBoxImage image)
        {
            string? symbol = image switch
            {
                MessageBoxImage.Information => "i",
                MessageBoxImage.Warning => "!",
                MessageBoxImage.Error => "x",
                MessageBoxImage.Question => "?",
                _ => null
            };

            if (symbol == null)
            {
                return null;
            }

            var accentBrush = TryGetBrushStatic("AccentBrush") ?? new SolidColorBrush(Color.FromRgb(59, 130, 246));

            return new Border
            {
                Width = 26,
                Height = 26,
                Background = accentBrush,
                CornerRadius = new CornerRadius(UiCornerRadius),
                Child = new TextBlock
                {
                    Text = symbol,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
        }

        private static Button BuildButton(string text, bool accent)
        {
            var secondaryBg = TryGetBrushStatic("DialogSecondaryBackgroundBrush") ?? new SolidColorBrush(Color.FromRgb(71, 85, 105));
            var secondaryBorder = TryGetBrushStatic("DialogSecondaryBorderBrush") ?? new SolidColorBrush(Color.FromRgb(100, 116, 139));
            var accentBg = TryGetBrushStatic("AccentBrush") ?? new SolidColorBrush(Color.FromRgb(59, 130, 246));
            var accentBorder = TryGetBrushStatic("AccentBrushLight") ?? new SolidColorBrush(Color.FromRgb(96, 165, 250));

            return new Button
            {
                Content = text,
                Width = 96,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(10, 6, 10, 6),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = accent ? accentBg : secondaryBg,
                BorderBrush = accent ? accentBorder : secondaryBorder,
                BorderThickness = new Thickness(1),
                Template = CreateRoundedButtonTemplate()
            };
        }

        private static ControlTemplate CreateRoundedButtonTemplate()
        {
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            borderFactory.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            borderFactory.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(UiCornerRadius));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetBinding(ContentPresenter.MarginProperty, new System.Windows.Data.Binding("Padding")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentFactory);

            var template = new ControlTemplate(typeof(Button))
            {
                VisualTree = borderFactory
            };

            var hoverTrigger = new Trigger
            {
                Property = Button.IsMouseOverProperty,
                Value = true
            };
            hoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.9));
            template.Triggers.Add(hoverTrigger);

            var pressedTrigger = new Trigger
            {
                Property = Button.IsPressedProperty,
                Value = true
            };
            pressedTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.8));
            template.Triggers.Add(pressedTrigger);

            var disabledTrigger = new Trigger
            {
                Property = UIElement.IsEnabledProperty,
                Value = false
            };
            disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
            template.Triggers.Add(disabledTrigger);

            return template;
        }

        private static Brush? TryGetBrushStatic(string resourceKey)
        {
            return Application.Current?.TryFindResource(resourceKey) as Brush;
        }

        private Brush? TryGetBrush(string resourceKey)
        {
            return Application.Current?.TryFindResource(resourceKey) as Brush;
        }
    }
}
