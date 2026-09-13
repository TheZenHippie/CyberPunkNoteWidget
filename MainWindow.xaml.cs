using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using CyberPunkNoteWidget.Settings;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace CyberPunkNoteWidget
{
    public partial class MainWindow : Window
    {
        private const string FileFilter =
            "All Supported Notes (*.txt;*.md;*.css;*.xaml;*.xml;*.html;*.htm;*.json;*.yaml;*.yml;*.ps1;*.bat;*.cmd;*.js;*.ts;*.py;*.cs)|*.txt;*.md;*.css;*.xaml;*.xml;*.html;*.htm;*.json;*.yaml;*.yml;*.ps1;*.bat;*.cmd;*.js;*.ts;*.py;*.cs|" +
            "Text Files (*.txt)|*.txt|" +
            "Markdown (*.md;*.markdown)|*.md;*.markdown|" +
            "CSS Stylesheets (*.css)|*.css|" +
            "XAML & XML (*.xaml;*.xml)|*.xaml;*.xml|" +
            "HTML & Web (*.html;*.htm)|*.html;*.htm|" +
            "JSON & YAML (*.json;*.yaml;*.yml)|*.json;*.yaml;*.yml|" +
            "Source Code (*.ps1;*.bat;*.cmd;*.js;*.ts;*.py;*.cs)|*.ps1;*.bat;*.cmd;*.js;*.ts;*.py;*.cs|" +
            "All Files (*.*)|*.*";

        private static readonly Random _random = new Random();

        // Cached pre-frozen brushes and static noise bitmaps for performance
        private static readonly Brush[] GlitchBrushes = CreateGlitchBrushes();
        private static readonly List<WriteableBitmap> SharedSnowBitmaps = new List<WriteableBitmap>();
        private static readonly object SnowLock = new object();
        private static readonly DoubleAnimation RainbowAnimation = CreateRainbowAnimation();

        private readonly WidgetSettings _settings;
        private string? _currentFilePath = null;
        private string _lastSavedContent = "";
        private bool _isDirty = false;

        private FontFamily _currentFontFamily = new FontFamily("Cascadia Code, Consolas, Courier New");
        private double _currentFontSize = 13.0;
        private Color _currentFontColor = Color.FromRgb(0, 255, 102); // Matrix Green
        private SolidColorBrush _currentFontBrush = new SolidColorBrush(Color.FromRgb(0, 255, 102));

        private double _windowOpacity = 0.80;
        private double _fontOpacity = 1.0;
        private bool _wordWrap = true;

        // Visual Effects State
        private bool _rainbowBorderEnabled = false;
        private double _borderWidth = 4.0;

        private bool _crtScanlinesEnabled = false;
        private double _scanlineThickness = 3.0;

        private bool _crtGlitchEnabled = false;
        private double _glitchChance = 15.0;
        private readonly DispatcherTimer _glitchTimer;
        private int _glitchCooldown = 0;

        private bool _crtSnowEnabled = false;
        private double _snowAmount = 25.0;
        private readonly DispatcherTimer _snowTimer;
        private int _snowFrameIndex = 0;

        private readonly DispatcherTimer _effectsCloseTimer;
        private FrameworkElement? _subscribedPopupChild = null;

        private bool _isInitialized = false;
        private bool _isClosed = false;

        public MainWindow()
        {
            _settings = WidgetSettings.Load();

            // Set up CRT glitch timer (120ms tick rate)
            _glitchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _glitchTimer.Tick += GlitchTimer_Tick;

            // Set up CRT snow static animation timer (65ms tick rate, ~15 fps)
            _snowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(65) };
            _snowTimer.Tick += SnowTimer_Tick;

            // Set up Effects submenu hover close delay timer (700ms)
            _effectsCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _effectsCloseTimer.Tick += EffectsCloseTimer_Tick;

            // Force submenus to fly out to the right away from the checkmark/icon gutter
            App.EnsureStandardMenuDropAlignment();

            InitializeComponent();

            ApplyLoadedSettings();

            _isInitialized = true;

            // Wire rounded clip update for background image
            if (BackgroundImageBorder != null)
            {
                BackgroundImageBorder.SizeChanged += (s, e) => UpdateBackgroundClip();
            }
            UpdateBackgroundClip();

            // Handle display / monitor changes (resolution changes, multi-monitor shifts)
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        #region Initialization & Settings Persistence

        private void ApplyLoadedSettings()
        {
            // Restore Window Bounds
            if (_settings.WindowWidth.HasValue && _settings.WindowWidth.Value >= this.MinWidth)
            {
                this.Width = _settings.WindowWidth.Value;
            }
            if (_settings.WindowHeight.HasValue && _settings.WindowHeight.Value >= this.MinHeight)
            {
                this.Height = _settings.WindowHeight.Value;
            }
            if (_settings.WindowLeft.HasValue && _settings.WindowTop.HasValue)
            {
                double left = _settings.WindowLeft.Value;
                double top = _settings.WindowTop.Value;

                // Validate coordinates are on active monitor bounds
                double virtualLeft = SystemParameters.VirtualScreenLeft;
                double virtualTop = SystemParameters.VirtualScreenTop;
                double virtualWidth = SystemParameters.VirtualScreenWidth;
                double virtualHeight = SystemParameters.VirtualScreenHeight;

                if (left >= virtualLeft && left + 100 <= virtualLeft + virtualWidth &&
                    top >= virtualTop && top + 100 <= virtualTop + virtualHeight)
                {
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                    this.Left = left;
                    this.Top = top;
                }
            }

            // Restore Opacities
            _windowOpacity = Math.Clamp(_settings.WindowOpacity, 0.0, 1.0);
            WindowOpacitySlider.Value = _windowOpacity;
            if (WindowBackgroundBorder != null) WindowBackgroundBorder.Opacity = _windowOpacity;
            if (BackgroundImageBorder != null) BackgroundImageBorder.Opacity = _windowOpacity;
            if (WindowOpacityValueText != null) WindowOpacityValueText.Text = $"{Math.Round(_windowOpacity * 100)}%";
            if (FooterBarBorder != null)
            {
                byte footerBgAlpha = (byte)Math.Clamp((int)(0x18 * _windowOpacity), 0, 255);
                byte footerBorderAlpha = (byte)Math.Clamp((int)(0x25 * _windowOpacity), 0, 255);
                FooterBarBorder.Background = new SolidColorBrush(Color.FromArgb(footerBgAlpha, 0, 0, 0));
                FooterBarBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(footerBorderAlpha, 255, 255, 255));
            }

            _fontOpacity = Math.Clamp(_settings.FontOpacity, 0.1, 1.0);
            FontOpacitySlider.Value = _fontOpacity;
            if (NoteContentGrid != null) NoteContentGrid.Opacity = _fontOpacity;
            if (FontOpacityValueText != null) FontOpacityValueText.Text = $"{Math.Round(_fontOpacity * 100)}%";

            // Restore Font Settings
            if (!string.IsNullOrWhiteSpace(_settings.FontFamily))
            {
                _currentFontFamily = new FontFamily(_settings.FontFamily);
            }
            _currentFontSize = _settings.FontSize > 6 ? _settings.FontSize : 13.0;

            if (!string.IsNullOrWhiteSpace(_settings.FontColorHex))
            {
                try
                {
                    _currentFontColor = (Color)ColorConverter.ConvertFromString(_settings.FontColorHex);
                }
                catch
                {
                    _currentFontColor = Color.FromRgb(0, 255, 102);
                }
            }

            _currentFontBrush = new SolidColorBrush(_currentFontColor);
            _currentFontBrush.Freeze();

            ApplyFontAndColor();
            PopulateFontFamilies();
            UpdateFontSizeMenuChecks();
            UpdateFontColorMenuChecks();

            // Word wrap
            _wordWrap = _settings.WordWrap;
            SetWordWrapState(_wordWrap);

            // Restore Background Image
            if (!string.IsNullOrWhiteSpace(_settings.BackgroundImagePath) && File.Exists(_settings.BackgroundImagePath))
            {
                LoadBackgroundImage(_settings.BackgroundImagePath);
            }

            // Restore Visual Effects
            _rainbowBorderEnabled = _settings.RainbowBorderEnabled;
            _borderWidth = Math.Clamp(_settings.BorderWidth, 1.0, 20.0);
            BorderWidthSlider.Value = _borderWidth;
            RainbowBorderMenuItem.IsChecked = _rainbowBorderEnabled;

            _crtScanlinesEnabled = _settings.CrtScanlinesEnabled;
            _scanlineThickness = Math.Clamp(_settings.ScanlineThickness, 1.0, 20.0);
            ScanlineThicknessSlider.Value = _scanlineThickness;
            CrtScanlinesMenuItem.IsChecked = _crtScanlinesEnabled;

            _crtGlitchEnabled = _settings.CrtGlitchEnabled;
            _glitchChance = Math.Clamp(_settings.GlitchChance, 0.0, 100.0);
            GlitchChanceSlider.Value = _glitchChance;
            CrtGlitchMenuItem.IsChecked = _crtGlitchEnabled;

            _crtSnowEnabled = _settings.CrtSnowEnabled;
            _snowAmount = Math.Clamp(_settings.SnowAmount, 0.0, 100.0);
            SnowAmountSlider.Value = _snowAmount;
            CrtSnowMenuItem.IsChecked = _crtSnowEnabled;

            ApplyEffectsState();

            // Window Options
            this.Topmost = _settings.AlwaysOnTop;
            AlwaysOnTopMenuItem.IsChecked = _settings.AlwaysOnTop;
            SetShadowEnabled(_settings.WindowShadow);

            // Restore Last File or Scratchpad Note
            if (!string.IsNullOrWhiteSpace(_settings.LastOpenedFilePath) && File.Exists(_settings.LastOpenedFilePath))
            {
                LoadFile(_settings.LastOpenedFilePath);
            }
            else if (!string.IsNullOrEmpty(_settings.LastScratchpadContent))
            {
                _currentFilePath = null;
                _lastSavedContent = _settings.LastScratchpadContent;
                NoteTextBox.Text = _settings.LastScratchpadContent;
                _isDirty = false;
                UpdateDocumentHeader();
                UpdateStatusFooter();
            }
            else
            {
                _currentFilePath = null;
                _lastSavedContent = "";
                NoteTextBox.Text = "";
                _isDirty = false;
                UpdateDocumentHeader();
                UpdateStatusFooter();
            }
        }

        private void SaveCurrentSettings()
        {
            if (_isClosed) return;

            _settings.WindowWidth = this.Width;
            _settings.WindowHeight = this.Height;
            _settings.WindowLeft = this.Left;
            _settings.WindowTop = this.Top;

            _settings.WindowOpacity = _windowOpacity;
            _settings.FontOpacity = _fontOpacity;
            _settings.WordWrap = _wordWrap;

            _settings.RainbowBorderEnabled = _rainbowBorderEnabled;
            _settings.BorderWidth = _borderWidth;
            _settings.CrtScanlinesEnabled = _crtScanlinesEnabled;
            _settings.ScanlineThickness = _scanlineThickness;
            _settings.CrtGlitchEnabled = _crtGlitchEnabled;
            _settings.GlitchChance = _glitchChance;
            _settings.CrtSnowEnabled = _crtSnowEnabled;
            _settings.SnowAmount = _snowAmount;

            _settings.AlwaysOnTop = this.Topmost;
            _settings.WindowShadow = WindowDropShadow.Opacity > 0;

            _settings.LastOpenedFilePath = _currentFilePath;
            if (_currentFilePath == null)
            {
                _settings.LastScratchpadContent = NoteTextBox.Text;
            }
            else
            {
                _settings.LastScratchpadContent = null;
            }

            _settings.Save();
        }

        #endregion

        #region Document Management & File I/O

        private void NewDocument_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckDirtyAndPromptSave()) return;

            _currentFilePath = null;
            _lastSavedContent = "";
            NoteTextBox.Text = "";
            _isDirty = false;
            UpdateDocumentHeader();
            UpdateStatusFooter();
            SaveCurrentSettings();
        }

        private void OpenDocument_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckDirtyAndPromptSave()) return;

            var dlg = new OpenFileDialog
            {
                Title = "Open Note or Code File",
                Filter = FileFilter,
                FilterIndex = 1,
                CheckFileExists = true
            };

            if (dlg.ShowDialog(this) == true)
            {
                LoadFile(dlg.FileName);
            }
        }

        private void SaveDocument_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentDocument(promptSaveAsIfUntitled: true);
        }

        private void SaveAsDocument_Click(object sender, RoutedEventArgs e)
        {
            SaveDocumentAs();
        }

        private void ClearDocument_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(NoteTextBox.Text))
            {
                if (NoteTextBox.Text.Length > 100)
                {
                    var res = MessageBox.Show(
                        "Are you sure you want to clear the entire note?",
                        "Clear Note",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes) return;
                }

                NoteTextBox.Text = "";
                UpdateStatusFooter();
            }
        }

        private bool LoadFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;

                // Open with permissive sharing so external tools can read without locking
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                string text = reader.ReadToEnd();

                _currentFilePath = filePath;
                _lastSavedContent = text;
                NoteTextBox.Text = text;
                _isDirty = false;

                UpdateDocumentHeader();
                UpdateStatusFooter();
                SaveCurrentSettings();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open file:\n{ex.Message}", "Error Opening File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool SaveCurrentDocument(bool promptSaveAsIfUntitled)
        {
            if (string.IsNullOrWhiteSpace(_currentFilePath))
            {
                if (promptSaveAsIfUntitled)
                {
                    return SaveDocumentAs();
                }
                return false;
            }

            try
            {
                File.WriteAllText(_currentFilePath, NoteTextBox.Text, Encoding.UTF8);
                _lastSavedContent = NoteTextBox.Text;
                _isDirty = false;
                UpdateDocumentHeader();
                SaveCurrentSettings();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save file:\n{ex.Message}", "Error Saving File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool SaveDocumentAs()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save Note As...",
                Filter = FileFilter,
                FilterIndex = 2, // Default to Text Files (*.txt)
                FileName = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "Untitled.txt"
            };

            if (dlg.ShowDialog(this) == true)
            {
                _currentFilePath = dlg.FileName;
                return SaveCurrentDocument(promptSaveAsIfUntitled: false);
            }

            return false;
        }

        private bool CheckDirtyAndPromptSave()
        {
            if (!_isDirty) return true;

            string docName = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "Untitled";
            var result = MessageBox.Show(
                $"Do you want to save changes to '{docName}' before continuing?",
                "CyberPunk Note Widget",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                return SaveCurrentDocument(promptSaveAsIfUntitled: true);
            }
            else if (result == MessageBoxResult.No)
            {
                return true;
            }

            return false;
        }

        private void UpdateDocumentHeader()
        {
            string ext = _currentFilePath != null ? Path.GetExtension(_currentFilePath).ToUpperInvariant() : ".TXT";
            if (string.IsNullOrWhiteSpace(ext)) ext = ".TXT";

            FileBadgeText.Text = $"[ {ext} ]";
            DocumentTitleText.Text = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "Untitled.txt";
            ModifiedIndicatorText.Visibility = _isDirty ? Visibility.Visible : Visibility.Collapsed;

            // Tint badge border
            FileBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(140, _currentFontColor.R, _currentFontColor.G, _currentFontColor.B));
        }

        private void UpdateStatusFooter()
        {
            int caretIndex = Math.Max(0, NoteTextBox.CaretIndex);
            int lineIdx = NoteTextBox.GetLineIndexFromCharacterIndex(caretIndex);
            int line = lineIdx >= 0 ? lineIdx + 1 : 1;
            int lineStart = lineIdx >= 0 ? NoteTextBox.GetCharacterIndexFromLineIndex(lineIdx) : 0;
            if (lineStart < 0) lineStart = 0;
            int col = Math.Max(1, caretIndex - lineStart + 1);

            LineColStatusText.Text = $"Ln {line}, Col {col}";

            string text = NoteTextBox.Text;
            int charCount = text.Length;

            // Fast zero-allocation word count
            int wordCount = 0;
            bool inWord = false;
            for (int i = 0; i < charCount; i++)
            {
                if (char.IsLetterOrDigit(text[i]))
                {
                    if (!inWord)
                    {
                        wordCount++;
                        inWord = true;
                    }
                }
                else
                {
                    inWord = false;
                }
            }

            WordCharStatusText.Text = $"{wordCount:N0} words • {charCount:N0} chars";
            WordWrapStatusText.Text = _wordWrap ? "Wrap: ON" : "Wrap: OFF";
        }

        private void NoteTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;

            bool wasDirty = _isDirty;
            _isDirty = (NoteTextBox.Text != _lastSavedContent);

            if (wasDirty != _isDirty)
            {
                UpdateDocumentHeader();
            }

            UpdateStatusFooter();
        }

        private void NoteTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            UpdateStatusFooter();
        }

        private void SetWordWrapState(bool enableWrap)
        {
            _wordWrap = enableWrap;
            WordWrapMenuItem.IsChecked = enableWrap;
            NoteTextBox.TextWrapping = enableWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            NoteTextBox.HorizontalScrollBarVisibility = enableWrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            UpdateStatusFooter();
            SaveCurrentSettings();
        }

        private void WordWrap_Click(object sender, RoutedEventArgs e)
        {
            SetWordWrapState(WordWrapMenuItem.IsChecked);
        }

        #endregion

        #region Drag & Drop Support

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string file = files[0];
                    if (File.Exists(file))
                    {
                        if (CheckDirtyAndPromptSave())
                        {
                            LoadFile(file);
                        }
                    }
                }
            }
        }

        #endregion

        #region Keyboard Shortcuts

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            bool isAlt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

            if (isCtrl && !isShift && e.Key == Key.N)
            {
                NewDocument_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (isCtrl && !isShift && e.Key == Key.O)
            {
                OpenDocument_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (isCtrl && !isShift && e.Key == Key.S)
            {
                SaveDocument_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (isCtrl && isShift && e.Key == Key.S)
            {
                SaveAsDocument_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (isAlt && e.Key == Key.Z)
            {
                SetWordWrapState(!_wordWrap);
                e.Handled = true;
            }
        }

        #endregion

        #region Font Family, Size & Color Customization

        private void PopulateFontFamilies()
        {
            // Clear existing monospace items except "Choose Font..." and separator
            while (FontFamilyMenuItem.Items.Count > 2)
            {
                FontFamilyMenuItem.Items.RemoveAt(2);
            }

            string[] topMonospaceFonts =
            {
                "Cascadia Code",
                "Consolas",
                "Lucida Console",
                "Courier New",
                "Fira Code",
                "JetBrains Mono"
            };

            foreach (var font in topMonospaceFonts)
            {
                var item = new MenuItem
                {
                    Header = font,
                    Tag = font,
                    FontFamily = new FontFamily(font),
                    IsChecked = font.Equals(_currentFontFamily.Source, StringComparison.OrdinalIgnoreCase)
                };
                item.Click += FontFamily_Click;
                FontFamilyMenuItem.Items.Add(item);
            }
        }

        private void ChooseAllFonts_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FontPickerWindow(_currentFontFamily, _currentFontSize, _currentFontBrush)
            {
                Owner = this
            };

            if (picker.ShowDialog() == true)
            {
                _currentFontFamily = picker.SelectedFontFamily;
                _currentFontSize = picker.SelectedFontSize;
                _settings.FontFamily = _currentFontFamily.Source;
                _settings.FontSize = _currentFontSize;

                ApplyFontAndColor();
                PopulateFontFamilies();
                UpdateFontSizeMenuChecks();
                SaveCurrentSettings();
            }
        }

        private void FontFamily_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string fontName)
            {
                foreach (var item in FontFamilyMenuItem.Items.OfType<MenuItem>().Where(i => i.Tag != null))
                {
                    item.IsChecked = (item == menuItem);
                }

                _currentFontFamily = new FontFamily(fontName);
                _settings.FontFamily = fontName;
                ApplyFontAndColor();
                SaveCurrentSettings();
            }
        }

        private void FontSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string sizeStr && double.TryParse(sizeStr, out double size))
            {
                _currentFontSize = size;
                _settings.FontSize = size;
                UpdateFontSizeMenuChecks();
                ApplyFontAndColor();
                SaveCurrentSettings();
            }
        }

        private void UpdateFontSizeMenuChecks()
        {
            foreach (var item in FontSizeMenuItem.Items.OfType<MenuItem>())
            {
                if (item.Tag is string tagStr && double.TryParse(tagStr, out double s))
                {
                    item.IsChecked = Math.Abs(s - _currentFontSize) < 0.1;
                }
            }
        }

        private void FontColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string colorHex)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorHex);
                    SetNoteFontColor(color, colorHex);
                    UpdateFontColorMenuChecks(menuItem);
                }
                catch { }
            }
        }

        private void UpdateFontColorMenuChecks(MenuItem? activeItem = null)
        {
            foreach (var item in FontColorMenuItem.Items.OfType<MenuItem>())
            {
                if (item.Tag is string hex)
                {
                    item.IsChecked = activeItem != null ? (item == activeItem) : hex.Equals(_settings.FontColorHex, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        private void CustomFontColor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Select Custom Color",
                Width = 320,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(20, 24, 35)),
                Foreground = Brushes.White,
                WindowStyle = WindowStyle.ToolWindow
            };

            var sp = new StackPanel { Margin = new Thickness(16) };
            sp.Children.Add(new TextBlock
            {
                Text = "Enter Hex Color Code (e.g. #00F0FF, #FF007F):",
                Foreground = Brushes.WhiteSmoke,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var tb = new TextBox
            {
                Text = $"#{_currentFontColor.R:X2}{_currentFontColor.G:X2}{_currentFontColor.B:X2}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 12)
            };
            sp.Children.Add(tb);

            var btnOk = new Button
            {
                Content = "Apply Color",
                Width = 100,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsDefault = true
            };
            btnOk.Click += (s, args) =>
            {
                try
                {
                    string hex = tb.Text.Trim();
                    if (!hex.StartsWith("#")) hex = "#" + hex;
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    SetNoteFontColor(color, hex);
                    dialog.DialogResult = true;
                    dialog.Close();
                }
                catch
                {
                    MessageBox.Show("Please enter a valid hex color format (#RRGGBB).", "Invalid Color", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            sp.Children.Add(btnOk);

            dialog.Content = sp;
            dialog.ShowDialog();
        }

        private void SetNoteFontColor(Color color, string hexCode)
        {
            _currentFontColor = color;
            _settings.FontColorHex = hexCode;

            _currentFontBrush = new SolidColorBrush(color);
            _currentFontBrush.Freeze();

            ApplyFontAndColor();
            SaveCurrentSettings();
        }

        private void ApplyFontAndColor()
        {
            NoteTextBox.FontFamily = _currentFontFamily;
            NoteTextBox.FontSize = _currentFontSize;
            NoteTextBox.Foreground = _currentFontBrush;
            NoteTextBox.CaretBrush = _currentFontBrush;

            var selColor = Color.FromArgb(60, _currentFontColor.R, _currentFontColor.G, _currentFontColor.B);
            NoteTextBox.SelectionBrush = new SolidColorBrush(selColor);

            FileBadgeText.Foreground = _currentFontBrush;
            FileBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(140, _currentFontColor.R, _currentFontColor.G, _currentFontColor.B));

            // Minimalist scrollbar thumb indicator tint
            var thumbColor = Color.FromArgb(110, _currentFontColor.R, _currentFontColor.G, _currentFontColor.B);
            var thumbBrush = new SolidColorBrush(thumbColor);
            thumbBrush.Freeze();
            NoteTextBox.Resources["ScrollThumbBrush"] = thumbBrush;
        }

        #endregion

        #region Dual Transparency Sliders

        private void WindowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;

            _windowOpacity = e.NewValue;
            _settings.WindowOpacity = _windowOpacity;

            if (WindowOpacityValueText != null)
            {
                WindowOpacityValueText.Text = $"{Math.Round(_windowOpacity * 100)}%";
            }

            if (WindowBackgroundBorder != null)
            {
                WindowBackgroundBorder.Opacity = _windowOpacity;
            }

            if (BackgroundImageBorder != null)
            {
                BackgroundImageBorder.Opacity = _windowOpacity;
            }

            if (FooterBarBorder != null)
            {
                byte footerBgAlpha = (byte)Math.Clamp((int)(0x18 * _windowOpacity), 0, 255);
                byte footerBorderAlpha = (byte)Math.Clamp((int)(0x25 * _windowOpacity), 0, 255);
                FooterBarBorder.Background = new SolidColorBrush(Color.FromArgb(footerBgAlpha, 0, 0, 0));
                FooterBarBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(footerBorderAlpha, 255, 255, 255));
            }

            SaveCurrentSettings();
        }

        private void FontOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;

            _fontOpacity = e.NewValue;
            _settings.FontOpacity = _fontOpacity;

            if (FontOpacityValueText != null)
            {
                FontOpacityValueText.Text = $"{Math.Round(_fontOpacity * 100)}%";
            }

            if (NoteContentGrid != null)
            {
                NoteContentGrid.Opacity = _fontOpacity;
            }

            SaveCurrentSettings();
        }

        private void Slider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is Slider slider)
            {
                double delta = e.Delta > 0 ? slider.SmallChange : -slider.SmallChange;
                slider.Value = Math.Clamp(slider.Value + delta, slider.Minimum, slider.Maximum);
                e.Handled = true;
            }
        }

        #endregion

        #region Background Image Support

        private void SelectBackgroundImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Background Image",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog(this) == true)
            {
                LoadBackgroundImage(dlg.FileName);
            }
        }

        private void LoadBackgroundImage(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();

                BackgroundImageElement.Source = bitmap;
                BackgroundImageElement.Visibility = Visibility.Visible;
                ClearBackgroundImageMenuItem.IsEnabled = true;

                _settings.BackgroundImagePath = filePath;
                SaveCurrentSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load background image:\n{ex.Message}", "Image Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ClearBackgroundImage_Click(object sender, RoutedEventArgs e)
        {
            BackgroundImageElement.Source = null;
            BackgroundImageElement.Visibility = Visibility.Collapsed;
            ClearBackgroundImageMenuItem.IsEnabled = false;

            _settings.BackgroundImagePath = null;
            SaveCurrentSettings();
        }

        private void UpdateBackgroundClip()
        {
            if (BackgroundImageBorder == null) return;

            double width = BackgroundImageBorder.ActualWidth;
            double height = BackgroundImageBorder.ActualHeight;

            if (width > 0 && height > 0)
            {
                var clip = new RectangleGeometry
                {
                    Rect = new Rect(0, 0, width, height),
                    RadiusX = 8,
                    RadiusY = 8
                };
                clip.Freeze();
                BackgroundImageBorder.Clip = clip;
            }
        }

        #endregion

        #region Visual Effects Suite (Rainbow Border, CRT Scanlines, CRT Glitch, CRT Snow)

        private static DoubleAnimation CreateRainbowAnimation()
        {
            var anim = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(5)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            anim.Freeze();
            return anim;
        }

        private static Brush[] CreateGlitchBrushes()
        {
            Color[] colors =
            {
                Color.FromArgb(220, 0, 240, 255),   // Neon cyan
                Color.FromArgb(220, 255, 0, 127),   // Neon magenta
                Color.FromArgb(240, 255, 255, 255), // Phosphor white
                Color.FromArgb(235, 0, 0, 0),       // Black horizontal dropout
                Color.FromArgb(210, 0, 255, 102),   // Phosphor lime
                Color.FromArgb(200, 255, 230, 0)    // Cyber yellow
            };

            var brushes = new Brush[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                var brush = new SolidColorBrush(colors[i]);
                brush.Freeze();
                brushes[i] = brush;
            }
            return brushes;
        }

        private static List<WriteableBitmap> GetOrCreateSnowBitmaps()
        {
            if (SharedSnowBitmaps.Count == 8) return SharedSnowBitmaps;

            lock (SnowLock)
            {
                if (SharedSnowBitmaps.Count == 8) return SharedSnowBitmaps;

                int w = 256, h = 256;
                for (int f = 0; f < 8; f++)
                {
                    var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
                    int stride = w * 4;
                    byte[] pixels = new byte[stride * h];
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        byte val = (byte)_random.Next(256);
                        bool colorSpeck = _random.Next(30) == 0;
                        pixels[i] = colorSpeck ? (byte)_random.Next(256) : val;
                        pixels[i + 1] = colorSpeck ? (byte)_random.Next(256) : val;
                        pixels[i + 2] = colorSpeck ? (byte)_random.Next(256) : val;
                        pixels[i + 3] = 255;
                    }
                    wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
                    wb.Freeze();
                    SharedSnowBitmaps.Add(wb);
                }
                return SharedSnowBitmaps;
            }
        }

        private void ApplyEffectsState()
        {
            // Rainbow border
            if (_rainbowBorderEnabled)
            {
                CyberpunkRainbowBorder.BorderThickness = new Thickness(_borderWidth);
                CyberpunkRainbowBorder.Visibility = Visibility.Visible;
                RainbowRotateTransform.BeginAnimation(RotateTransform.AngleProperty, RainbowAnimation);
            }
            else
            {
                CyberpunkRainbowBorder.Visibility = Visibility.Collapsed;
                RainbowRotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            }

            // Scanlines
            CrtScanlinesOverlay.Visibility = _crtScanlinesEnabled ? Visibility.Visible : Visibility.Collapsed;
            if (_crtScanlinesEnabled)
            {
                UpdateScanlinesBrush(_scanlineThickness);
            }

            // Glitch
            if (_crtGlitchEnabled && _glitchChance > 0)
            {
                if (!_glitchTimer.IsEnabled) _glitchTimer.Start();
            }
            else
            {
                _glitchTimer.Stop();
                ResetGlitchState();
            }

            // Snow
            if (_crtSnowEnabled && _snowAmount > 0)
            {
                UpdateSnowOpacity();
                var bitmaps = GetOrCreateSnowBitmaps();
                if (bitmaps.Count > 0 && CrtSnowOverlay.Source == null)
                {
                    CrtSnowOverlay.Source = bitmaps[0];
                }
                CrtSnowOverlay.Visibility = Visibility.Visible;
                if (!_snowTimer.IsEnabled) _snowTimer.Start();
            }
            else
            {
                _snowTimer.Stop();
                CrtSnowOverlay.Visibility = Visibility.Collapsed;
                CrtSnowOverlay.Source = null;
            }
        }

        private void RainbowBorder_Click(object sender, RoutedEventArgs e)
        {
            _rainbowBorderEnabled = RainbowBorderMenuItem.IsChecked;
            ApplyEffectsState();
            SaveCurrentSettings();
        }

        private void BorderWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;

            _borderWidth = e.NewValue;
            if (BorderWidthValueText != null)
            {
                BorderWidthValueText.Text = $"{Math.Round(_borderWidth)}px";
            }

            if (_rainbowBorderEnabled && CyberpunkRainbowBorder != null)
            {
                CyberpunkRainbowBorder.BorderThickness = new Thickness(_borderWidth);
            }

            SaveCurrentSettings();
        }

        private void CrtScanlines_Click(object sender, RoutedEventArgs e)
        {
            _crtScanlinesEnabled = CrtScanlinesMenuItem.IsChecked;
            ApplyEffectsState();
            SaveCurrentSettings();
        }

        private void ScanlineThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;

            _scanlineThickness = e.NewValue;
            if (ScanlineThicknessValueText != null)
            {
                ScanlineThicknessValueText.Text = $"{Math.Round(_scanlineThickness)}px";
            }

            if (_crtScanlinesEnabled)
            {
                UpdateScanlinesBrush(_scanlineThickness);
            }

            SaveCurrentSettings();
        }

        private void UpdateScanlinesBrush(double thickness)
        {
            if (CrtDrawingBrush == null) return;

            double lineThickness = Math.Max(1.0, Math.Round(thickness));
            double period = lineThickness * 2.0;

            var darkBrush = new SolidColorBrush(Color.FromArgb(245, 0, 0, 0));
            darkBrush.Freeze();

            var transparentRect = new RectangleGeometry(new Rect(0, 0, 1, period));
            transparentRect.Freeze();

            var darkRect = new RectangleGeometry(new Rect(0, 0, 1, lineThickness));
            darkRect.Freeze();

            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, transparentRect));
            group.Children.Add(new GeometryDrawing(darkBrush, null, darkRect));
            group.Freeze();

            CrtDrawingBrush.Viewport = new Rect(0, 0, 1, period);
            CrtDrawingBrush.Drawing = group;
        }

        private void CrtGlitch_Click(object sender, RoutedEventArgs e)
        {
            _crtGlitchEnabled = CrtGlitchMenuItem.IsChecked;
            ApplyEffectsState();
            SaveCurrentSettings();
        }

        private void GlitchChanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;

            _glitchChance = e.NewValue;
            if (GlitchChanceValueText != null)
            {
                GlitchChanceValueText.Text = $"{Math.Round(_glitchChance)}%";
            }

            if (_glitchChance > 0 && !_crtGlitchEnabled)
            {
                _crtGlitchEnabled = true;
                CrtGlitchMenuItem.IsChecked = true;
            }
            else if (_glitchChance <= 0 && _crtGlitchEnabled)
            {
                _crtGlitchEnabled = false;
                CrtGlitchMenuItem.IsChecked = false;
            }

            ApplyEffectsState();
            SaveCurrentSettings();
        }

        private void GlitchTimer_Tick(object? sender, EventArgs e)
        {
            if (_isClosed || !_crtGlitchEnabled || _glitchChance <= 0)
            {
                ResetGlitchState();
                return;
            }

            if (_glitchCooldown > 0)
            {
                _glitchCooldown--;
                if (_glitchCooldown == 0) ResetGlitchState();
                return;
            }

            if (_random.NextDouble() * 100.0 >= _glitchChance)
            {
                ResetGlitchState();
                return;
            }

            _glitchCooldown = _random.Next(1, 4);

            double w = CrtGlitchCanvas.ActualWidth > 0 ? CrtGlitchCanvas.ActualWidth : this.Width;
            double h = CrtGlitchCanvas.ActualHeight > 0 ? CrtGlitchCanvas.ActualHeight : this.Height;
            if (w <= 0 || h <= 0) return;

            CrtGlitchCanvas.Children.Clear();
            CrtGlitchCanvas.Visibility = Visibility.Visible;

            int sliceCount = _random.Next(2, 6);
            for (int i = 0; i < sliceCount; i++)
            {
                double sliceTop = _random.NextDouble() * (h - 20);
                double sliceHeight = _random.NextDouble() * 22 + 4;
                double sliceWidth = w * (_random.NextDouble() * 0.7 + 0.3);
                double sliceLeft = (_random.NextDouble() * (w - sliceWidth * 0.5)) - 10;

                var rect = new Rectangle
                {
                    Width = sliceWidth,
                    Height = sliceHeight,
                    Fill = GlitchBrushes[_random.Next(GlitchBrushes.Length)],
                    Opacity = _random.NextDouble() * 0.5 + 0.45
                };

                Canvas.SetLeft(rect, sliceLeft);
                Canvas.SetTop(rect, sliceTop);
                CrtGlitchCanvas.Children.Add(rect);
            }

            // Subtle Jitter
            if (ContentJitterTransform != null)
            {
                double maxJitter = Math.Max(4.0, Math.Min(16.0, w * 0.02));
                ContentJitterTransform.X = (_random.NextDouble() * (maxJitter * 2.0)) - maxJitter;
            }
        }

        private void ResetGlitchState()
        {
            if (CrtGlitchCanvas != null)
            {
                CrtGlitchCanvas.Visibility = Visibility.Collapsed;
                CrtGlitchCanvas.Children.Clear();
            }
            if (ContentJitterTransform != null)
            {
                ContentJitterTransform.X = 0;
            }
        }

        private void CrtSnow_Click(object sender, RoutedEventArgs e)
        {
            _crtSnowEnabled = CrtSnowMenuItem.IsChecked;
            ApplyEffectsState();
            SaveCurrentSettings();
        }

        private void SnowAmountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;

            _snowAmount = e.NewValue;
            if (SnowAmountValueText != null)
            {
                SnowAmountValueText.Text = $"{Math.Round(_snowAmount)}%";
            }

            if (_snowAmount > 0 && !_crtSnowEnabled)
            {
                _crtSnowEnabled = true;
                CrtSnowMenuItem.IsChecked = true;
            }
            else if (_snowAmount <= 0 && _crtSnowEnabled)
            {
                _crtSnowEnabled = false;
                CrtSnowMenuItem.IsChecked = false;
            }

            ApplyEffectsState();
            SaveCurrentSettings();
        }

        private void UpdateSnowOpacity()
        {
            if (CrtSnowOverlay == null) return;
            double opacity = Math.Clamp((_snowAmount / 100.0) * 0.90, 0.0, 0.90);
            CrtSnowOverlay.Opacity = opacity;
        }

        private void SnowTimer_Tick(object? sender, EventArgs e)
        {
            if (_isClosed || !_crtSnowEnabled || CrtSnowOverlay == null) return;

            var bitmaps = GetOrCreateSnowBitmaps();
            if (bitmaps.Count == 0) return;

            _snowFrameIndex = (_snowFrameIndex + 1) % bitmaps.Count;
            CrtSnowOverlay.Source = bitmaps[_snowFrameIndex];
        }

        #endregion

        #region Window Options & Smart Context Menu

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private void Window_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            App.EnsureStandardMenuDropAlignment();
            if (MainContextMenu == null) return;

            if (GetCursorPos(out POINT pt))
            {
                IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
                var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMonitor, ref info))
                {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    double screenRightDip = info.rcWork.Right / dpi.DpiScaleX;
                    double screenLeftDip = info.rcWork.Left / dpi.DpiScaleX;
                    double screenBottomDip = info.rcWork.Bottom / dpi.DpiScaleY;
                    double screenTopDip = info.rcWork.Top / dpi.DpiScaleY;
                    double cursorXDip = pt.X / dpi.DpiScaleX;
                    double cursorYDip = pt.Y / dpi.DpiScaleY;

                    const double requiredSpaceRight = 500.0;
                    if (cursorXDip + requiredSpaceRight > screenRightDip)
                    {
                        double targetLeft = screenRightDip - requiredSpaceRight;
                        MainContextMenu.Placement = PlacementMode.AbsolutePoint;
                        MainContextMenu.HorizontalOffset = Math.Max(screenLeftDip + 10, targetLeft);

                        double minTop = screenTopDip + 10;
                        double maxTop = Math.Max(minTop, screenBottomDip - 500);
                        MainContextMenu.VerticalOffset = Math.Clamp(cursorYDip - 20, minTop, maxTop);
                    }
                    else
                    {
                        MainContextMenu.Placement = PlacementMode.MousePoint;
                        MainContextMenu.HorizontalOffset = 0;
                        MainContextMenu.VerticalOffset = 0;
                    }
                }
            }
        }

        private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            this.Topmost = AlwaysOnTopMenuItem.IsChecked;
            SaveCurrentSettings();
        }

        private void Shadow_Click(object sender, RoutedEventArgs e)
        {
            SetShadowEnabled(ShadowMenuItem.IsChecked);
            SaveCurrentSettings();
        }

        private void SetShadowEnabled(bool enabled)
        {
            if (WindowDropShadow != null)
            {
                WindowDropShadow.Opacity = enabled ? 0.35 : 0.0;
            }
            ShadowMenuItem.IsChecked = enabled;
        }

        private void NewWindow_Click(object sender, RoutedEventArgs e)
        {
            var newWin = new MainWindow();
            newWin.Left = this.Left + 40;
            newWin.Top = this.Top + 40;
            newWin.Show();
        }

        private void CloseWidget_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ExitAll_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSettings();
            App.CleanProcessExit(0);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    this.DragMove();
                }
                catch { }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging from background when not interacting with editor
            if (e.OriginalSource is not TextBox && e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    this.DragMove();
                }
                catch { }
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateBackgroundClip();
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            if (_isClosed) return;
            Dispatcher.InvokeAsync(() =>
            {
                if (!_isClosed)
                {
                    UpdateBackgroundClip();
                    this.InvalidateVisual();
                }
            });
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!CheckDirtyAndPromptSave())
            {
                e.Cancel = true;
                return;
            }

            SaveCurrentSettings();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            _glitchTimer.Stop();
            _snowTimer.Stop();
            _effectsCloseTimer.Stop();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            base.OnClosed(e);
        }

        #endregion

        #region Effects Submenu Smart Placement & Traversal Delay

        private Popup? GetEffectsPopup()
        {
            if (EffectsMenuItem == null) return null;
            var popup = EffectsMenuItem.Template?.FindName("PART_Popup", EffectsMenuItem) as Popup;
            if (popup != null) return popup;
            EffectsMenuItem.ApplyTemplate();
            popup = EffectsMenuItem.Template?.FindName("PART_Popup", EffectsMenuItem) as Popup;
            if (popup != null) return popup;
            return FindVisualChild<Popup>(EffectsMenuItem);
        }

        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var descendant = FindVisualChild<T>(child);
                if (descendant != null) return descendant;
            }
            return null;
        }

        private FrameworkElement? GetEffectsPopupChild()
        {
            var popup = GetEffectsPopup();
            return popup?.Child as FrameworkElement;
        }

        private bool IsElementInEffects(DependencyObject? element)
        {
            if (element == null) return false;
            var popupChild = GetEffectsPopupChild();

            DependencyObject? curr = element;
            while (curr != null)
            {
                if (curr == EffectsMenuItem || (popupChild != null && curr == popupChild)) return true;
                if (EffectsMenuItem != null && curr is MenuItem item && EffectsMenuItem.Items.Contains(item)) return true;

                DependencyObject? parent = null;
                if (curr is Visual visual) parent = VisualTreeHelper.GetParent(visual);
                if (parent == null && curr is FrameworkContentElement fce) parent = fce.Parent;
                if (parent == null) parent = LogicalTreeHelper.GetParent(curr);
                curr = parent;
            }
            return false;
        }

        private void SetSiblingHitTestVisible(bool visible)
        {
            if (MainContextMenu == null) return;
            foreach (var item in MainContextMenu.Items)
            {
                if (item != EffectsMenuItem && item is UIElement uie)
                {
                    uie.IsHitTestVisible = visible;
                }
            }
        }

        private void EffectsCloseTimer_Tick(object? sender, EventArgs e)
        {
            _effectsCloseTimer.Stop();
            if (EffectsMenuItem != null && EffectsMenuItem.IsSubmenuOpen)
            {
                var popupChild = GetEffectsPopupChild();
                bool inPopup = popupChild != null && (popupChild.IsMouseOver || popupChild.IsMouseCaptureWithin);
                if (!EffectsMenuItem.IsMouseOver && !inPopup)
                {
                    EffectsMenuItem.IsSubmenuOpen = false;
                    SetSiblingHitTestVisible(true);
                }
            }
        }

        private void MainContextMenu_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (EffectsMenuItem != null && EffectsMenuItem.IsSubmenuOpen)
            {
                var popupChild = GetEffectsPopupChild();
                bool inPopup = popupChild != null && (popupChild.IsMouseOver || popupChild.IsMouseCaptureWithin);
                if (inPopup || EffectsMenuItem.IsMouseOver)
                {
                    _effectsCloseTimer.Stop();
                    return;
                }

                if (!_effectsCloseTimer.IsEnabled)
                {
                    _effectsCloseTimer.Start();
                }
            }
        }

        private void MainContextMenu_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (EffectsMenuItem != null && EffectsMenuItem.IsSubmenuOpen)
            {
                var source = e.OriginalSource as DependencyObject;
                if (!IsElementInEffects(source))
                {
                    _effectsCloseTimer.Stop();
                    EffectsMenuItem.IsSubmenuOpen = false;
                    SetSiblingHitTestVisible(true);
                }
            }
        }

        private void MainContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            _effectsCloseTimer.Stop();
            if (EffectsMenuItem != null) EffectsMenuItem.IsSubmenuOpen = false;
            SetSiblingHitTestVisible(true);
            DetachPopupChildHandlers();
        }

        private void EffectsMenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            _effectsCloseTimer.Stop();
            if (EffectsMenuItem != null) EffectsMenuItem.IsSubmenuOpen = true;
        }

        private void EffectsMenuItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (EffectsMenuItem != null && EffectsMenuItem.IsSubmenuOpen)
            {
                var popupChild = GetEffectsPopupChild();
                bool inPopup = popupChild != null && (popupChild.IsMouseOver || popupChild.IsMouseCaptureWithin);
                if (!inPopup)
                {
                    _effectsCloseTimer.Stop();
                    _effectsCloseTimer.Start();
                }
            }
        }

        private void EffectsMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            _effectsCloseTimer.Stop();
            SetSiblingHitTestVisible(false);
            AttachPopupChildHandlers();
        }

        private void EffectsMenuItem_SubmenuClosed(object sender, RoutedEventArgs e)
        {
            _effectsCloseTimer.Stop();
            SetSiblingHitTestVisible(true);
            DetachPopupChildHandlers();
        }

        private void AttachPopupChildHandlers()
        {
            var child = GetEffectsPopupChild();
            if (child != null && child != _subscribedPopupChild)
            {
                DetachPopupChildHandlers();
                _subscribedPopupChild = child;
                _subscribedPopupChild.MouseEnter += PopupChild_MouseEnter;
                _subscribedPopupChild.MouseLeave += PopupChild_MouseLeave;
            }
        }

        private void DetachPopupChildHandlers()
        {
            if (_subscribedPopupChild != null)
            {
                _subscribedPopupChild.MouseEnter -= PopupChild_MouseEnter;
                _subscribedPopupChild.MouseLeave -= PopupChild_MouseLeave;
                _subscribedPopupChild = null;
            }
        }

        private void PopupChild_MouseEnter(object sender, MouseEventArgs e)
        {
            _effectsCloseTimer.Stop();
        }

        private void PopupChild_MouseLeave(object sender, MouseEventArgs e)
        {
            if (EffectsMenuItem != null && EffectsMenuItem.IsSubmenuOpen && !EffectsMenuItem.IsMouseOver)
            {
                _effectsCloseTimer.Stop();
                _effectsCloseTimer.Start();
            }
        }

        #endregion
    }
}

