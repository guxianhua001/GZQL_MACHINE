using System.Windows.Media;
using Prism.Commands;
using System;
using Framework.ViewModels;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using Core.Abstraction;

namespace Framework.Helpers
{
    public static class ColorPickerHelper
    {
        private static readonly Lazy<DelegateCommand<ColorParameterItem>> _selectColorCommand =
            new Lazy<DelegateCommand<ColorParameterItem>>(() =>
                new DelegateCommand<ColorParameterItem>(SelectColor));

        public static DelegateCommand<ColorParameterItem> SelectColorCommand => _selectColorCommand.Value;

        private static void SelectColor(ColorParameterItem colorItem)
        {
            if (colorItem == null) return;

            try
            {
                var dialog = new ColorPickerDialog(colorItem.Value as Color? ?? Colors.Black);
                if (dialog.ShowDialog() == true)
                {
                    colorItem.Value = dialog.SelectedColor;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"颜色选择错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class ColorPickerDialog : Window
    {
        public Color SelectedColor { get; set; }

        public ColorPickerDialog(Color initialColor)
        {
            SelectedColor = initialColor;

            Title = "选择颜色";
            Width = 350;
            Height = 450;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var stackPanel = new StackPanel { Margin = new Thickness(10) };

            // 使用 MaterialDesign 的 ColorPicker
            var colorPicker = new ColorPicker
            {
                Color = SelectedColor,
                Margin = new Thickness(0, 10, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button
            {
                Content = "确定",
                Width = 80,
                Margin = new Thickness(5),
                Style = (Style)Application.Current.FindResource("MaterialDesignRaisedButton")
            };
            okButton.Click += (s, e) =>
            {
                SelectedColor = colorPicker.Color;
                DialogResult = true;
            };

            var cancelButton = new Button
            {
                Content = "取消",
                Width = 80,
                Margin = new Thickness(5),
                Style = (Style)Application.Current.FindResource("MaterialDesignOutlinedButton")
            };
            cancelButton.Click += (s, e) => DialogResult = false;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(colorPicker);
            stackPanel.Children.Add(buttonPanel);

            Content = stackPanel;
        }
    }
}