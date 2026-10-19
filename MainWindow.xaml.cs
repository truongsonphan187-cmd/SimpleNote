using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Color = System.Windows.Media.Color;

namespace SimpleNote
{
    public partial class MainWindow : Window
    {
        private const double HandleWidth = 6.0;
        private const double HandleHeight = 170.0;
        private const double BottomMargin = 28.0;

        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private readonly DispatcherTimer _saveDebounceTimer;
        private readonly DispatcherTimer _toastTimer;

        private double _expandedWidth;
        private double _expandedHeight;
        private double _expandedTop;
        private double _handleTop;

        private bool _isExpanded = false;
        private bool _isPinned = false;
        private bool _isMasterHidden = false;
        private bool _isAnimating = false;
        private bool _isEnforcingListStructure = false;

        public MainWindow()
        {
            InitializeComponent();

            // Auto-save debounce timer (350ms after typing ceases)
            _saveDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _saveDebounceTimer.Tick += (s, e) =>
            {
                _saveDebounceTimer.Stop();
                NoteStorage.SaveNotes(GetNoteContent(), _isPinned);
            };

            // Toast notification timer
            _toastTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.8)
            };
            _toastTimer.Tick += (s, e) =>
            {
                _toastTimer.Stop();
                var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));
                ToastBadge.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };

            InitializeTray();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Strip window from Windows Alt+Tab and Task View via Win32 WS_EX_TOOLWINDOW
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.SetToolWindow(hwnd);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RecalculateDimensions();

            // Load saved notes & pin state
            var savedData = NoteStorage.LoadNotes();
            SetNoteContent(savedData.Content);
            SetPinState(savedData.IsPinned);

            UpdateStats();
            UpdatePlaceholder();
            UpdateStartupMenuItem();

            // Attach TextChanged event handler AFTER component initialization and initial load
            NoteEditor.TextChanged += NoteEditor_TextChanged;

            // Position initially in collapsed edge-handle state
            CollapsePanelImmediate();
        }

        /// <summary>
        /// Recalculates screen working area bounds and target dimensions.
        /// Scaled to 80% width (~400px) with 1:1 square aspect ratio.
        /// </summary>
        private void RecalculateDimensions()
        {
            Rect workArea = SystemParameters.WorkArea;

            // Target Expanded Width: 80% of previous ~500px, clamped to ~390px - 420px (nominally 400px)
            double baseWidth = Math.Clamp(workArea.Width * 0.20, 390.0, 420.0);
            _expandedWidth = Math.Round(baseWidth);

            // Aspect ratio ~1:1 (roughly square)
            _expandedHeight = _expandedWidth;

            _expandedTop = Math.Round(workArea.Bottom - BottomMargin - _expandedHeight);

            // Align vertical position of collapsed handle with expanded handle
            _handleTop = Math.Round(_expandedTop + (_expandedHeight - HandleHeight) / 2.0);
        }

        #region Edge Panel Expand / Collapse Architecture

        private void ExpandPanel()
        {
            if (_isExpanded || _isMasterHidden) return;

            RecalculateDimensions();
            Rect workArea = SystemParameters.WorkArea;

            _isAnimating = true;

            // Switch to full expanded window bounds
            this.Left = workArea.Right - _expandedWidth;
            this.Top = _expandedTop;
            this.Width = _expandedWidth;
            this.Height = _expandedHeight;

            CollapsedHandle.Visibility = Visibility.Collapsed;
            EdgeTriggerStrip.Visibility = Visibility.Collapsed;
            ExpandedContainer.Visibility = Visibility.Visible;
            ExpandedContainer.IsHitTestVisible = true;

            // Start slide-in animation from right edge to 0
            SlideTransform.X = _expandedWidth;
            var slideIn = new DoubleAnimation
            {
                From = _expandedWidth,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(240),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            slideIn.Completed += (s, e) =>
            {
                _isAnimating = false;
                _isExpanded = true;
            };

            SlideTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
        }

        private void CollapsePanel()
        {
            if (!_isExpanded || _isPinned || _isMasterHidden) return;

            _isAnimating = true;

            var slideOut = new DoubleAnimation
            {
                From = SlideTransform.X,
                To = _expandedWidth,
                Duration = TimeSpan.FromMilliseconds(190),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            slideOut.Completed += (s, e) =>
            {
                _isAnimating = false;

                if (!IsMouseOver && !_isPinned)
                {
                    CollapsePanelImmediate();
                    NoteStorage.SaveNotes(GetNoteContent(), _isPinned);
                }
            };

            SlideTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
        }

        private void CollapsePanelImmediate()
        {
            SlideTransform.BeginAnimation(TranslateTransform.XProperty, null);
            SlideTransform.X = 0;

            ExpandedContainer.Visibility = Visibility.Collapsed;
            CollapsedHandle.Visibility = Visibility.Visible;
            EdgeTriggerStrip.Visibility = Visibility.Visible;

            Rect workArea = SystemParameters.WorkArea;
            this.Left = workArea.Right - HandleWidth;
            this.Top = _handleTop;
            this.Width = HandleWidth;
            this.Height = HandleHeight;

            _isExpanded = false;
            _isAnimating = false;
        }

        private void EdgeTriggerStrip_MouseEnter(object sender, MouseEventArgs e)
        {
            ExpandPanel();
        }

        private void EdgeTriggerStrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ExpandPanel();
        }

        private void CollapsedHandle_MouseEnter(object sender, MouseEventArgs e)
        {
            ExpandPanel();
        }

        private void CollapsedHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ExpandPanel();
        }

        private void ExpandedHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isExpanded)
            {
                if (_isPinned) SetPinState(false);
                CollapsePanel();
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isExpanded && !_isPinned && !_isMasterHidden)
            {
                CollapsePanel();
            }
        }

        private void ExpandedContainer_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_isAnimating && SlideTransform.X > 0)
            {
                var slideBack = new DoubleAnimation
                {
                    From = SlideTransform.X,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(180),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                slideBack.Completed += (s, args) =>
                {
                    _isAnimating = false;
                    _isExpanded = true;
                };
                SlideTransform.BeginAnimation(TranslateTransform.XProperty, slideBack);
            }
        }

        #endregion

        #region Pin Mode Logic

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            SetPinState(!_isPinned);
        }

        private void SetPinState(bool pinned)
        {
            _isPinned = pinned;

            if (_isPinned)
            {
                PinButton.Background = new SolidColorBrush(Color.FromArgb(0x35, 0xF5, 0x9E, 0x0B));
                PinButton.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0xF5, 0x9E, 0x0B));
                PinIcon.Fill = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
                PinButton.ToolTip = "Pinned (Scratchpad stays open)";
                MenuPin.Header = "Unpin Scratchpad";
            }
            else
            {
                PinButton.Background = new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
                PinButton.BorderBrush = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF));
                PinIcon.Fill = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
                PinButton.ToolTip = "Pin Scratchpad (Stay open while typing elsewhere)";
                MenuPin.Header = "Pin Scratchpad";
            }

            NoteStorage.SaveNotes(GetNoteContent(), _isPinned);
        }

        #endregion

        #region Note Editor & Bullet Logic (Native FlowDocument List)

        private ListItem? GetCurrentListItem()
        {
            var element = NoteEditor?.CaretPosition?.Parent as TextElement;
            while (element != null)
            {
                if (element is ListItem li) return li;
                element = element.Parent as TextElement;
            }
            return null;
        }

        private List EnsureList()
        {
            if (NoteEditor?.Document?.Blocks == null || NoteEditor.Document.Blocks.Count == 0 || !(NoteEditor.Document.Blocks.FirstBlock is List list))
            {
                NoteEditor?.Document?.Blocks.Clear();
                list = new List
                {
                    MarkerStyle = TextMarkerStyle.Disc,
                    Margin = new Thickness(0),
                    Padding = new Thickness(24, 0, 0, 0)
                };
                var li = CreateListItem(string.Empty);
                list.ListItems.Add(li);
                NoteEditor?.Document?.Blocks.Add(list);
                if (NoteEditor != null && li.Blocks.FirstBlock is Paragraph p)
                {
                    NoteEditor.CaretPosition = p.ContentStart;
                }
                return list;
            }

            if (list.ListItems.Count == 0)
            {
                var li = CreateListItem(string.Empty);
                list.ListItems.Add(li);
                if (NoteEditor != null && li.Blocks.FirstBlock is Paragraph p)
                {
                    NoteEditor.CaretPosition = p.ContentStart;
                }
            }

            return list;
        }

        private void ResetToSingleEmptyBullet()
        {
            if (NoteEditor?.Document == null) return;

            try
            {
                _isEnforcingListStructure = true;
                NoteEditor.Document.Blocks.Clear();
                var list = new List
                {
                    MarkerStyle = TextMarkerStyle.Disc,
                    Margin = new Thickness(0),
                    Padding = new Thickness(24, 0, 0, 0)
                };
                var li = CreateListItem(string.Empty);
                list.ListItems.Add(li);
                NoteEditor.Document.Blocks.Add(list);

                if (li.Blocks.FirstBlock is Paragraph p)
                {
                    NoteEditor.CaretPosition = p.ContentStart;
                }
            }
            finally
            {
                _isEnforcingListStructure = false;
            }

            UpdatePlaceholder();
            UpdateStats();
        }

        private void EnsureBulletStructure()
        {
            if (_isEnforcingListStructure || NoteEditor?.Document?.Blocks == null) return;

            // SimpleNote must ALWAYS have an active bullet point on line 1.
            // Check if FirstBlock is a List containing at least one ListItem.
            bool hasValidList = NoteEditor.Document.Blocks.FirstBlock is List firstList && firstList.ListItems.Count > 0;

            if (!hasValidList)
            {
                try
                {
                    _isEnforcingListStructure = true;

                    // Extract any text user might have typed/pasted before list structure was lost
                    var tr = new TextRange(NoteEditor.Document.ContentStart, NoteEditor.Document.ContentEnd);
                    string rawText = tr.Text.Trim();

                    NoteEditor.Document.Blocks.Clear();
                    var list = new List
                    {
                        MarkerStyle = TextMarkerStyle.Disc,
                        Margin = new Thickness(0),
                        Padding = new Thickness(24, 0, 0, 0)
                    };

                    if (!string.IsNullOrEmpty(rawText))
                    {
                        var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var rawLine in lines)
                        {
                            string line = rawLine.Trim();
                            if (line.StartsWith("•")) line = line.TrimStart('•', ' ', '\t');
                            if (!string.IsNullOrEmpty(line))
                            {
                                list.ListItems.Add(CreateListItem(line));
                            }
                        }
                    }

                    if (list.ListItems.Count == 0)
                    {
                        list.ListItems.Add(CreateListItem(string.Empty));
                    }

                    NoteEditor.Document.Blocks.Add(list);

                    var targetLi = list.ListItems.FirstListItem;
                    if (targetLi?.Blocks.FirstBlock is Paragraph fp)
                    {
                        NoteEditor.CaretPosition = string.IsNullOrEmpty(rawText) ? fp.ContentStart : fp.ContentEnd;
                    }
                }
                finally
                {
                    _isEnforcingListStructure = false;
                }
            }
        }

        private ListItem CreateListItem(string text = "")
        {
            var p = new Paragraph { Margin = new Thickness(0, 0, 0, 4), LineHeight = 28 };
            if (!string.IsNullOrEmpty(text))
            {
                p.Inlines.Add(new Run(text));
            }
            var li = new ListItem(p) { Margin = new Thickness(0) };
            return li;
        }

        private string GetNoteContent()
        {
            if (NoteEditor?.Document?.Blocks == null) return string.Empty;

            var sb = new System.Text.StringBuilder();
            foreach (var block in NoteEditor.Document.Blocks)
            {
                if (block is List list)
                {
                    foreach (var li in list.ListItems)
                    {
                        var tr = new TextRange(li.ContentStart, li.ContentEnd);
                        string line = tr.Text.Trim();
                        if (!string.IsNullOrEmpty(line))
                        {
                            if (sb.Length > 0) sb.AppendLine();
                            sb.Append("• " + line);
                        }
                    }
                }
                else if (block is Paragraph p)
                {
                    var tr = new TextRange(p.ContentStart, p.ContentEnd);
                    string line = tr.Text.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append("• " + line);
                    }
                }
            }
            return sb.ToString();
        }

        private void SetNoteContent(string content)
        {
            if (NoteEditor?.Document?.Blocks == null) return;

            NoteEditor.Document.Blocks.Clear();
            var list = new List
            {
                MarkerStyle = TextMarkerStyle.Disc,
                Margin = new Thickness(0),
                Padding = new Thickness(24, 0, 0, 0)
            };

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Strip bullet marker if present in persisted string
                if (line.StartsWith("•"))
                {
                    line = line.TrimStart('•', ' ', '\t');
                }

                list.ListItems.Add(CreateListItem(line));
            }

            // Always ensure at least one empty ListItem exists so the first line immediately has an active bullet
            if (list.ListItems.Count == 0)
            {
                list.ListItems.Add(CreateListItem(string.Empty));
            }

            NoteEditor.Document.Blocks.Add(list);

            // Position caret in the first item
            var firstLi = list.ListItems.FirstListItem;
            if (firstLi?.Blocks.FirstBlock is Paragraph fp)
            {
                NoteEditor.CaretPosition = fp.ContentEnd;
            }
        }

        private void NoteEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Enter key: create a new ListItem in the List
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                var currentLi = GetCurrentListItem();
                if (currentLi != null)
                {
                    var currentP = NoteEditor.CaretPosition?.Paragraph;
                    string textAfter = string.Empty;

                    if (currentP != null)
                    {
                        var trAfter = new TextRange(NoteEditor.CaretPosition, currentP.ContentEnd);
                        textAfter = trAfter.Text.TrimEnd('\r', '\n');
                        if (!string.IsNullOrEmpty(textAfter))
                        {
                            trAfter.Text = "";
                        }
                    }

                    var newLi = CreateListItem(textAfter);
                    var list = currentLi.Parent as List ?? EnsureList();
                    list.ListItems.InsertAfter(currentLi, newLi);

                    if (newLi.Blocks.FirstBlock is Paragraph np)
                    {
                        NoteEditor.CaretPosition = np.ContentStart;
                    }
                }
                else
                {
                    var list = EnsureList();
                    var newLi = CreateListItem(string.Empty);
                    list.ListItems.Add(newLi);
                    if (newLi.Blocks.FirstBlock is Paragraph np)
                    {
                        NoteEditor.CaretPosition = np.ContentStart;
                    }
                }
                return;
            }

            // Selection deletion (Backspace or Delete when text is selected, e.g. Ctrl+A -> Backspace/Delete)
            if ((e.Key == Key.Back || e.Key == Key.Delete) && NoteEditor.Selection != null && !NoteEditor.Selection.IsEmpty)
            {
                var docRange = new TextRange(NoteEditor.Document.ContentStart, NoteEditor.Document.ContentEnd);
                string totalText = docRange.Text.Trim();
                string selText = NoteEditor.Selection.Text.Trim();

                bool isWholeDocSelected = string.IsNullOrEmpty(totalText) ||
                    totalText == selText ||
                    (NoteEditor.Selection.Start.CompareTo(docRange.Start) <= 0 && NoteEditor.Selection.End.CompareTo(docRange.End) >= 0);

                if (!isWholeDocSelected)
                {
                    var firstBlock = NoteEditor.Document.Blocks.FirstBlock;
                    var lastBlock = NoteEditor.Document.Blocks.LastBlock;
                    if (firstBlock != null && lastBlock != null)
                    {
                        if (NoteEditor.Selection.Start.CompareTo(firstBlock.ContentStart) <= 0 &&
                            NoteEditor.Selection.End.CompareTo(lastBlock.ContentEnd) >= 0)
                        {
                            isWholeDocSelected = true;
                        }
                    }
                }

                if (isWholeDocSelected)
                {
                    e.Handled = true;
                    ResetToSingleEmptyBullet();
                    return;
                }
            }

            // Smart Backspace key: clean delete on empty bullets or merge with previous item
            if (e.Key == Key.Back && (NoteEditor.Selection == null || NoteEditor.Selection.IsEmpty))
            {
                var currentLi = GetCurrentListItem();
                if (currentLi == null)
                {
                    // Caret is not in any ListItem: reset to clean bullet
                    e.Handled = true;
                    ResetToSingleEmptyBullet();
                    return;
                }

                var list = currentLi.Parent as List ?? NoteEditor.Document.Blocks.OfType<List>().FirstOrDefault();
                int listCount = list?.ListItems.Count ?? 1;

                var tr = new TextRange(currentLi.ContentStart, currentLi.ContentEnd);
                string text = tr.Text.Trim();

                // Requirement: Pressing Backspace on the very last remaining empty bullet should do nothing
                if (listCount <= 1 && string.IsNullOrEmpty(text))
                {
                    e.Handled = true;
                    return;
                }

                // Case 1: Empty bullet line (when multiple bullets exist) - remove this empty bullet
                if (string.IsNullOrEmpty(text))
                {
                    e.Handled = true;
                    if (list != null && list.ListItems.Count > 1)
                    {
                        var prevLi = currentLi.PreviousListItem;
                        var nextLi = currentLi.NextListItem;
                        list.ListItems.Remove(currentLi);
                        if (prevLi?.Blocks.LastBlock is Paragraph pp)
                        {
                            NoteEditor.CaretPosition = pp.ContentEnd;
                        }
                        else if (nextLi?.Blocks.FirstBlock is Paragraph np)
                        {
                            NoteEditor.CaretPosition = np.ContentStart;
                        }
                    }
                    return;
                }

                // Case 2: Caret is at the very beginning of the item text
                var currentP = NoteEditor.CaretPosition?.Paragraph;
                if (currentP != null && NoteEditor.CaretPosition?.CompareTo(currentP.ContentStart) == 0)
                {
                    e.Handled = true;
                    var prevLi = currentLi.PreviousListItem;
                    if (prevLi != null && prevLi.Blocks.LastBlock is Paragraph prevP)
                    {
                        // Merge current bullet content with previous bullet
                        var prevEnd = prevP.ContentEnd;
                        string content = tr.Text.TrimEnd('\r', '\n');
                        list?.ListItems.Remove(currentLi);
                        if (!string.IsNullOrEmpty(content))
                        {
                            prevP.Inlines.Add(new Run(content));
                        }
                        NoteEditor.CaretPosition = prevEnd;
                    }
                    // If prevLi is null (line 1 bullet): doing nothing (e.Handled = true) prevents
                    // default WPF Backspace from stripping the bullet off line 1!
                    return;
                }
            }

            // Delete key handling on last remaining empty bullet
            if (e.Key == Key.Delete && (NoteEditor.Selection == null || NoteEditor.Selection.IsEmpty))
            {
                var currentLi = GetCurrentListItem();
                if (currentLi != null)
                {
                    var list = currentLi.Parent as List ?? NoteEditor.Document.Blocks.OfType<List>().FirstOrDefault();
                    int listCount = list?.ListItems.Count ?? 1;
                    var tr = new TextRange(currentLi.ContentStart, currentLi.ContentEnd);
                    string text = tr.Text.Trim();

                    if (listCount <= 1 && string.IsNullOrEmpty(text))
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }

            // Escape key: Collapse
            if (e.Key == Key.Escape)
            {
                if (_isPinned) SetPinState(false);
                CollapsePanel();
                e.Handled = true;
                return;
            }

            // Ctrl+S: Immediate save with feedback
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                NoteStorage.SaveNotes(GetNoteContent(), _isPinned);
                ShowToast("Saved to disk");
                e.Handled = true;
                return;
            }
        }

        private void NoteEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded || NoteEditor == null || _isEnforcingListStructure) return;

            EnsureBulletStructure();

            UpdatePlaceholder();
            UpdateStats();

            // Reset and trigger debounced auto-save
            _saveDebounceTimer?.Stop();
            _saveDebounceTimer?.Start();
        }

        private void UpdatePlaceholder()
        {
            if (PlaceholderText == null || NoteEditor == null) return;

            string content = GetNoteContent().Trim();
            bool isEmpty = string.IsNullOrEmpty(content) || content == "•";
            PlaceholderText.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateStats()
        {
            if (!IsLoaded || NoteEditor?.Document?.Blocks == null || StatsText == null) return;

            int bullets = 0;
            int words = 0;

            foreach (var block in NoteEditor.Document.Blocks)
            {
                if (block is List list)
                {
                    foreach (var li in list.ListItems)
                    {
                        var tr = new TextRange(li.ContentStart, li.ContentEnd);
                        string text = tr.Text.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            bullets++;
                            var tokens = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            words += tokens.Length;
                        }
                    }
                }
            }

            if (StatsText != null)
            {
                StatsText.Text = $"{bullets} bullets • {words} words";
            }
        }

        #endregion

        #region Header Actions

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string text = GetNoteContent();
            if (!string.IsNullOrEmpty(text))
            {
                System.Windows.Clipboard.SetText(text);
                ShowToast("Copied to clipboard!");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ResetToSingleEmptyBullet();
            NoteEditor.Focus();
            NoteStorage.SaveNotes(GetNoteContent(), _isPinned);
            ShowToast("Notes cleared");
        }

        private void CollapseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPinned) SetPinState(false);
            CollapsePanel();
        }

        private void ShowToast(string message)
        {
            if (ToastBadge.Child is TextBlock tb)
            {
                tb.Text = message;
            }

            _toastTimer.Stop();
            var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150));
            ToastBadge.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            _toastTimer.Start();
        }

        #endregion

        #region System Tray NotifyIcon

        private void InitializeTray()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = GenerateProceduralTrayIcon(false),
                Text = "SimpleNote - Desktop Scratchpad",
                Visible = true
            };

            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        }

        private void NotifyIcon_MouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // Master On/Off Switch: Toggle show/hide of edge handle and panel
                ToggleMasterVisibility();
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                // Open WPF dark glassmorphic Context Menu at cursor point
                UpdateStartupMenuItem();
                var hwnd = new WindowInteropHelper(this).Handle;
                NativeMethods.SetForegroundWindow(hwnd);
                TrayMenu.Placement = PlacementMode.MousePoint;
                TrayMenu.IsOpen = true;
            }
        }

        private void ToggleMasterVisibility()
        {
            _isMasterHidden = !_isMasterHidden;

            // Update tray icon with crisp diagonal slash line when toggled OFF
            if (_notifyIcon != null)
            {
                _notifyIcon.Icon = GenerateProceduralTrayIcon(_isMasterHidden);
            }

            if (_isMasterHidden)
            {
                this.Visibility = Visibility.Hidden;
            }
            else
            {
                this.Visibility = Visibility.Visible;
                RecalculateDimensions();
                CollapsePanelImmediate();
            }
        }

        /// <summary>
        /// Generates an enlarged, high-DPI 48x48 icon for the Windows 11 system tray.
        /// Displays a diagonal slash line when isOff == true.
        /// </summary>
        private System.Drawing.Icon GenerateProceduralTrayIcon(bool isOff)
        {
            const int size = 48;
            using var bitmap = new System.Drawing.Bitmap(size, size);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(System.Drawing.Color.Transparent);

                if (!isOff)
                {
                    // === ACTIVE ON STATE ===
                    // 1. Dark circular glass badge
                    using var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(240, 15, 16, 22));
                    g.FillEllipse(bgBrush, 2, 2, 44, 44);

                    // 2. Glowing purple / violet accent ring
                    using var ringPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(230, 139, 92, 246), 2.4f);
                    g.DrawEllipse(ringPen, 2, 2, 43, 43);

                    // 3. Crisp white note pad outline
                    using var padPen = new System.Drawing.Pen(System.Drawing.Color.White, 2.2f);
                    g.DrawRectangle(padPen, 13, 10, 22, 27);

                    // 4. Note lines in bright lavender
                    using var linePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(235, 196, 181, 253), 2.0f);
                    g.DrawLine(linePen, 18, 16, 29, 16);
                    g.DrawLine(linePen, 18, 22, 29, 22);
                    g.DrawLine(linePen, 18, 28, 25, 28);
                }
                else
                {
                    // === MUTED / OFF STATE ===
                    // 1. Dimmed circular glass badge
                    using var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(200, 20, 21, 28));
                    g.FillEllipse(bgBrush, 2, 2, 44, 44);

                    // 2. Muted ring
                    using var ringPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(120, 100, 105, 120), 2.0f);
                    g.DrawEllipse(ringPen, 2, 2, 43, 43);

                    // 3. Dimmed note pad outline
                    using var padPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(140, 160, 165, 180), 2.0f);
                    g.DrawRectangle(padPen, 13, 10, 22, 27);

                    // 4. Dimmed lines
                    using var linePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(110, 140, 145, 160), 1.8f);
                    g.DrawLine(linePen, 18, 16, 29, 16);
                    g.DrawLine(linePen, 18, 22, 29, 22);
                    g.DrawLine(linePen, 18, 28, 25, 28);

                    // 5. Crisp white diagonal strike-through / slash line (with dark shadow underlay for contrast)
                    using var slashShadow = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 10, 10, 15), 4.5f);
                    slashShadow.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    slashShadow.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawLine(slashShadow, 9, 9, 39, 39);

                    using var slashPen = new System.Drawing.Pen(System.Drawing.Color.White, 3.0f);
                    slashPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    slashPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawLine(slashPen, 9, 9, 39, 39);
                }
            }

            IntPtr hIcon = bitmap.GetHicon();
            var icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(hIcon).Clone();
            NativeMethods.DestroyIcon(hIcon);
            return icon;
        }

        private void MenuToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isMasterHidden)
            {
                ToggleMasterVisibility();
                ExpandPanel();
                return;
            }

            if (_isExpanded)
            {
                CollapsePanel();
            }
            else
            {
                ExpandPanel();
            }
        }

        private void MenuPin_Click(object sender, RoutedEventArgs e)
        {
            SetPinState(!_isPinned);
        }

        private void TrayMenu_Opened(object sender, RoutedEventArgs e)
        {
            UpdateStartupMenuItem();
        }

        private void MenuStartup_Click(object sender, RoutedEventArgs e)
        {
            bool shouldEnable = MenuStartup.IsChecked;
            TrayService.SetStartupEnabled(shouldEnable);
            UpdateStartupMenuItem();
        }

        private void UpdateStartupMenuItem()
        {
            if (MenuStartup != null)
            {
                MenuStartup.IsChecked = TrayService.IsStartupEnabled();
            }
        }

        private void MenuClear_Click(object sender, RoutedEventArgs e)
        {
            ClearButton_Click(sender, e);
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            NoteStorage.SaveNotes(GetNoteContent(), _isPinned);

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            NoteStorage.SaveNotes(GetNoteContent(), _isPinned);

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            base.OnClosed(e);
        }

        #endregion
    }
}
