using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ImplementadorCUAD.Services
{
    public static class DialogService
    {
        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        {
            var dialog = new StyledDialogWindow(message, title, buttons, image);
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
        private readonly Button _primaryButton;
        private readonly Button _secondaryButton;
        private readonly Button _tertiaryButton;

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public StyledDialogWindow(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            _buttons = buttons;
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

            var rootBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(31, 41, 51)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
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
                Foreground = Brushes.White
            };
            Grid.SetRow(header, 0);
            panel.Children.Add(header);

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 10),
                VerticalAlignment = VerticalAlignment.Center
            };

            var icon = BuildIcon(image);
            if (icon != null)
            {
                content.Children.Add(icon);
            }

            content.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                MaxWidth = 460
            });
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

            return new Border
            {
                Width = 26,
                Height = 26,
                Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
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
            return new Button
            {
                Content = text,
                Width = 96,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(10, 6, 10, 6),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = accent
                    ? new SolidColorBrush(Color.FromRgb(59, 130, 246))
                    : new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderBrush = accent
                    ? new SolidColorBrush(Color.FromRgb(96, 165, 250))
                    : new SolidColorBrush(Color.FromRgb(100, 116, 139)),
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
    }
}
