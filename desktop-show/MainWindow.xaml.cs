using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopShow;

public partial class MainWindow : Window
{
    private const double ExpandedWidth = 340;
    private const double CollapsedWidth = 36;
    private const string ItemDragFormat = "DesktopShow.Item";

    private readonly DesktopScanner _scanner = new();
    private readonly CategoryStore _categoryStore = new();
    private readonly ObservableCollection<DesktopItem> _items = new();
    private readonly ICollectionView _itemsView;
    private readonly DispatcherTimer _collapseTimer;

    private Point _dragStart;
    private bool _isDragging;
    private bool _isExpanded = true;

    public MainWindow()
    {
        InitializeComponent();

        _itemsView = CollectionViewSource.GetDefaultView(_items);
        _itemsView.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(DesktopItem.Category)));
        _itemsView.SortDescriptions.Add(
            new SortDescription(nameof(DesktopItem.Name), ListSortDirection.Ascending));
        ItemsList.ItemsSource = _itemsView;

        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        _collapseTimer.Tick += CollapseTimer_Tick;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        await RefreshItemsAsync();
        ScheduleCollapse();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshItemsAsync();

    private async Task RefreshItemsAsync()
    {
        RefreshButton.IsEnabled = false;
        StatusText.Text = "正在读取桌面项目...";

        try
        {
            var scannedItems = await Task.Run(() =>
            {
                var result = _scanner.Scan();
                foreach (var item in result)
                {
                    item.Icon = DesktopScanner.GetIcon(item.Path);
                }

                return result;
            });
            var savedCategories = _categoryStore.Load();

            _items.Clear();
            foreach (var item in scannedItems)
            {
                if (savedCategories.TryGetValue(item.Path, out var category))
                {
                    item.Category = category;
                }

                _items.Add(item);
            }

            _itemsView.Refresh();
            StatusText.Text = $"{_items.Count} 个项目 · 双击打开，拖到分组标题分类";

            try
            {
                _categoryStore.Save(_items);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, $"分组已加载，但无法保存配置。\n{exception.Message}",
                    "桌面秀", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception exception)
        {
            StatusText.Text = "读取桌面失败";
            MessageBox.Show(this, $"无法读取桌面项目。\n{exception.Message}",
                "桌面秀", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private void Item_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DesktopItem item })
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            try
            {
                DesktopScanner.Open(item);
            }
            catch (Exception exception) when (exception is Win32Exception
                or InvalidOperationException
                or FileNotFoundException)
            {
                MessageBox.Show(this, $"无法打开“{item.Name}”。\n{exception.Message}",
                    "桌面秀", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            e.Handled = true;
            return;
        }

        _dragStart = e.GetPosition(this);
    }

    private void Item_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || _isDragging
            || sender is not FrameworkElement { Tag: DesktopItem item })
        {
            return;
        }

        var currentPosition = e.GetPosition(this);
        if (Math.Abs(currentPosition.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(currentPosition.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _isDragging = true;
        try
        {
            var data = new DataObject(ItemDragFormat, item);
            _ = DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        }
        finally
        {
            _isDragging = false;
        }

        ScheduleCollapse();
        e.Handled = true;
    }

    private void Category_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(ItemDragFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Category_Drop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string category }
            && e.Data.GetDataPresent(ItemDragFormat)
            && e.Data.GetData(ItemDragFormat) is DesktopItem item)
        {
            item.Category = category;
            _itemsView.Refresh();

            try
            {
                _categoryStore.Save(_items);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, $"分组已更新，但无法保存配置。\n{exception.Message}",
                    "桌面秀", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
        ScheduleCollapse();
    }

    private void Handle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isExpanded)
        {
            PinToggle.IsChecked = false;
            SetExpanded(false);
        }
        else
        {
            SetExpanded(true);
        }

        e.Handled = true;
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        SetExpanded(true);
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e) => ScheduleCollapse();

    private void PinToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (PinToggle.IsChecked == true)
        {
            _collapseTimer.Stop();
            SetExpanded(true);
        }
        else
        {
            ScheduleCollapse();
        }
    }

    private void CollapseTimer_Tick(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();
        if (PinToggle.IsChecked != true && !_isDragging && !IsMouseOver)
        {
            SetExpanded(false);
        }
    }

    private void ScheduleCollapse()
    {
        if (PinToggle.IsChecked != true && !_isDragging && !IsMouseOver)
        {
            _collapseTimer.Stop();
            _collapseTimer.Start();
        }
    }

    private void SetExpanded(bool expanded)
    {
        _isExpanded = expanded;
        PositionWindow();
    }

    private void Window_DpiChanged(object sender, DpiChangedEventArgs e) => PositionWindow();

    private void PositionWindow()
    {
        var workArea = GetWorkArea();
        Width = ExpandedWidth;
        Height = workArea.Height;
        Top = workArea.Top;
        Left = _isExpanded
            ? workArea.Right - ExpandedWidth
            : workArea.Right - CollapsedWidth;
    }

    private Rect GetWorkArea()
    {
        var monitor = MonitorFromWindow(new WindowInteropHelper(this).Handle, 2);
        var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref monitorInfo))
        {
            // Monitor APIs return physical pixels; WPF positions use device-independent units.
            var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
                ?? Matrix.Identity;
            var topLeft = transform.Transform(
                new Point(monitorInfo.rcWork.Left, monitorInfo.rcWork.Top));
            var bottomRight = transform.Transform(
                new Point(monitorInfo.rcWork.Right, monitorInfo.rcWork.Bottom));
            return new Rect(topLeft, bottomRight);
        }

        return SystemParameters.WorkArea;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }
}
