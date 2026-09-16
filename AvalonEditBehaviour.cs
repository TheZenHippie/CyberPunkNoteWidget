using System;
using System.Windows;
using ICSharpCode.AvalonEdit;

namespace CyberPunkNoteWidget
{
    public static class AvalonEditBehaviour
    {
        public static readonly DependencyProperty BindableTextProperty =
            DependencyProperty.RegisterAttached(
                "BindableText",
                typeof(string),
                typeof(AvalonEditBehaviour),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnBindableTextChanged));

        public static string GetBindableText(DependencyObject dp) => (string)dp.GetValue(BindableTextProperty);
        public static void SetBindableText(DependencyObject dp, string value) => dp.SetValue(BindableTextProperty, value);

        private static void OnBindableTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextEditor editor)
            {
                var newText = (string)e.NewValue ?? string.Empty;
                if (editor.Text != newText)
                {
                    editor.Text = newText;
                }

                editor.TextChanged -= Editor_TextChanged;
                editor.TextChanged += Editor_TextChanged;
            }
        }

        private static void Editor_TextChanged(object? sender, EventArgs e)
        {
            if (sender is TextEditor editor)
            {
                SetBindableText(editor, editor.Text);
            }
        }
    }
}