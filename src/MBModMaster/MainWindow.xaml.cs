using MBModMaster.Core;
using MBModMaster.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace MBModMaster;

public partial class MainWindow : Window
{
    private readonly GameLocator _gameLocator = new();
    private readonly ModuleScanner _moduleScanner = new();
    private readonly LauncherConfigService _launcherConfigService = new();
    private readonly LoadOrderService _loadOrderService = new();
    private readonly ArchiveInstaller _archiveInstaller = new();
    private readonly GameLauncher _gameLauncher = new();
    private readonly ObservableCollection<BannerlordModule> _modules = [];
    private System.Windows.Point? _dragStartPoint;
    private BannerlordModule? _draggedModule;
    private int? _dropInsertIndex;
    private bool _isDraggingModule;
    private double _dragPointerOffsetY;
    private Rect? _restoreBounds;
    private bool _isCustomMaximized;

    public MainWindow()
    {
        InitializeComponent();
        ModulesList.ItemsSource = _modules;
        CollectionViewSource.GetDefaultView(_modules).Filter = ModuleMatchesSearch;
        SourceInitialized += MainWindow_SourceInitialized;
        StateChanged += MainWindow_StateChanged;
        LoadDetectedGameDirectory();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var source = (HwndSource?)PresentationSource.FromVisual(this);
        source?.AddHook(WindowMessageHook);
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateWindowFrameShape();
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmGetMinMaxInfo = 0x0024;
        if (msg == wmGetMinMaxInfo)
        {
            AdjustMaximizedBounds(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void WindowFrame_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWindowFrameShape();
    }

    private void UpdateWindowFrameShape()
    {
        if (_isCustomMaximized || WindowState == WindowState.Maximized)
        {
            WindowFrame.Clip = null;
            WindowFrame.CornerRadius = new CornerRadius(0);
            return;
        }

        WindowFrame.CornerRadius = new CornerRadius(12);
        WindowFrame.Clip = new RectangleGeometry(
            new Rect(0, 0, WindowFrame.ActualWidth, WindowFrame.ActualHeight),
            12,
            12);
    }

    private void LoadDetectedGameDirectory()
    {
        var gameDirectory = _gameLocator.LocateBannerlord();
        if (gameDirectory is null)
        {
            StatusTextBlock.Text = "未自动找到骑砍2目录，请手动选择游戏根目录。";
            return;
        }

        GameDirectoryTextBox.Text = gameDirectory;
        RefreshModules();
    }

    private void ChooseGameDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择 Mount & Blade II Bannerlord 游戏根目录",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(GameDirectoryTextBox.Text) ? GameDirectoryTextBox.Text : string.Empty
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        GameDirectoryTextBox.Text = dialog.SelectedPath;
        RefreshModules();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshModules();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CollectionViewSource.GetDefaultView(_modules).Refresh();
        var searchText = SearchTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var matchedCount = CollectionViewSource.GetDefaultView(_modules).Cast<object>().Count();
            StatusTextBlock.Text = $"已筛选到{matchedCount}个Mod。";
        }
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Clear();
        StatusTextBlock.Text = $"已显示全部{_modules.Count}个Mod。";
    }

    private void OpenModulesFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var gameDirectory = GameDirectoryTextBox.Text;
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
        {
            StatusTextBlock.Text = "请先选择有效的游戏目录。";
            return;
        }

        var modulesDirectory = Path.Combine(gameDirectory, "Modules");
        if (!Directory.Exists(modulesDirectory))
        {
            StatusTextBlock.Text = $"没有找到Mod目录：{modulesDirectory}";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = modulesDirectory,
            UseShellExecute = true
        });
        StatusTextBlock.Text = $"已打开Mod目录：{modulesDirectory}";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        if (RestoreMaximizedWindowForDrag(e))
        {
            e.Handled = true;
            Dispatcher.BeginInvoke(BeginNativeTitleBarDrag);
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void MaximizeToWorkingArea()
    {
        _restoreBounds = new Rect(Left, Top, Width, Height);
        WindowState = WindowState.Normal;

        var screen = Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        var area = screen.WorkingArea;
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = transform.Transform(new System.Windows.Point(area.Left, area.Top));
        var bottomRight = transform.Transform(new System.Windows.Point(area.Right, area.Bottom));

        Left = topLeft.X;
        Top = topLeft.Y;
        Width = bottomRight.X - topLeft.X;
        Height = bottomRight.Y - topLeft.Y;

        _isCustomMaximized = true;
        WindowFrame.Clip = null;
        WindowFrame.CornerRadius = new CornerRadius(0);
    }

    private void RestoreFromCustomMaximized()
    {
        if (_restoreBounds is not Rect bounds)
        {
            _isCustomMaximized = false;
            return;
        }

        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        _isCustomMaximized = false;
        UpdateWindowFrameShape();
    }

    private bool RestoreMaximizedWindowForDrag(MouseButtonEventArgs e)
    {
        if (!_isCustomMaximized && WindowState != WindowState.Maximized)
        {
            return false;
        }

        var pointerXRatio = ActualWidth <= 0 ? 0.5 : e.GetPosition(this).X / ActualWidth;

        if (_isCustomMaximized && _restoreBounds is Rect customBounds)
        {
            Width = customBounds.Width;
            Height = customBounds.Height;
            Left = PointToScreen(e.GetPosition(this)).X / GetDpiScaleX() - Width * pointerXRatio;
            Top = PointToScreen(e.GetPosition(this)).Y / GetDpiScaleY() - 18;
            _isCustomMaximized = false;
            UpdateWindowFrameShape();
            return true;
        }

        WindowState = WindowState.Normal;
        return true;
    }

    private void BeginNativeTitleBarDrag()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(hwnd, WmNcLeftButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
    }

    private double GetDpiScaleX()
    {
        return PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
    }

    private double GetDpiScaleY()
    {
        return PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M22 ?? 1;
    }

    private static void AdjustMaximizedBounds(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo();
        monitorInfo.cbSize = Marshal.SizeOf<MonitorInfo>();
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var workArea = monitorInfo.rcWork;
        var monitorArea = monitorInfo.rcMonitor;

        minMaxInfo.ptMaxPosition.x = workArea.Left - monitorArea.Left;
        minMaxInfo.ptMaxPosition.y = workArea.Top - monitorArea.Top;
        minMaxInfo.ptMaxSize.x = workArea.Right - workArea.Left;
        minMaxInfo.ptMaxSize.y = workArea.Bottom - workArea.Top;

        Marshal.StructureToPtr(minMaxInfo, lParam, true);
    }

    private void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = SaveCurrentState();
            _gameLauncher.LaunchDirect(GameDirectoryTextBox.Text, _modules);
            StatusTextBlock.Text = $"已按当前顺序应用{result.EnabledCount}个启用Mod，并直接启动游戏。";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = $"启动失败：{exception.Message}";
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var gameDirectory = GameDirectoryTextBox.Text;
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
        {
            StatusTextBlock.Text = "导入前请先选择有效的游戏目录。";
            return;
        }

        using var dialog = new Forms.OpenFileDialog
        {
            Title = "选择骑砍2 Mod压缩包",
            Filter = "Mod压缩包 (*.zip;*.7z;*.rar)|*.zip;*.7z;*.rar|所有文件 (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        try
        {
            var result = _archiveInstaller.Install(dialog.FileName, gameDirectory);
            RefreshModules();

            var moduleNames = string.Join(", ", result.InstalledModules.Select(module => module.ModuleName));
            var backupCount = result.InstalledModules.Count(module => module.BackupPath is not null);
            var backupText = backupCount == 0 ? "没有覆盖旧Mod。" : $"已备份{backupCount}个旧Mod。";
            StatusTextBlock.Text = $"已导入{result.InstalledModules.Count}个Mod：{moduleNames}。{backupText}";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = $"导入失败：{exception.Message}";
        }
    }

    private void AutoSortButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = _loadOrderService.Sort(_modules);
            ReplaceModules(result.Modules);
            var saveResult = SaveCurrentState();

            var warningParts = new List<string>();
            if (result.HasCycle)
            {
                warningParts.Add("发现循环依赖");
            }

            if (result.MissingDependencies.Count > 0)
            {
                warningParts.Add($"缺失{result.MissingDependencies.Count}个依赖");
            }

            var warningText = warningParts.Count == 0 ? "未发现依赖问题。" : string.Join("，", warningParts) + "。";
            StatusTextBlock.Text = $"已自动排序并应用，当前启用{saveResult.EnabledCount}个Mod。{warningText}";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = $"自动排序失败：{exception.Message}";
        }
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedModule(-1);
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedModule(1);
    }

    private void MoveUpItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: BannerlordModule module })
        {
            MoveModule(module, -1);
        }
    }

    private void MoveDownItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: BannerlordModule module })
        {
            MoveModule(module, 1);
        }
    }

    private void EnableAllButton_Click(object sender, RoutedEventArgs e)
    {
        SetAllToggleableModules(true);
        UpdateSummary();
        StatusTextBlock.Text = "已启用全部可选Mod，官方基础模块保持启用。";
    }

    private void DisableAllButton_Click(object sender, RoutedEventArgs e)
    {
        SetAllToggleableModules(false);
        UpdateSummary();
        StatusTextBlock.Text = "已禁用全部可选Mod，官方基础模块保持启用。";
    }

    private void RefreshModules()
    {
        _modules.Clear();

        var gameDirectory = GameDirectoryTextBox.Text;
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
        {
            StatusTextBlock.Text = "游戏目录不存在。";
            return;
        }

        var scannedModules = _moduleScanner.Scan(gameDirectory).ToList();
        var hiddenModulesCount = scannedModules.Count(module => !module.IsSinglePlayerCompatible);
        var modules = scannedModules
            .Where(module => module.IsSinglePlayerCompatible)
            .ToList();

        ApplyLauncherState(modules);
        modules = OrderByLauncherState(modules);

        foreach (var module in modules)
        {
            _modules.Add(module);
        }

        var modulesDirectory = Path.Combine(gameDirectory, "Modules");
        if (!Directory.Exists(modulesDirectory))
        {
            StatusTextBlock.Text = "所选目录下没有Modules文件夹，请确认选择的是游戏根目录。";
            return;
        }

        var config = _launcherConfigService.Read();
        var configText = config.Exists
            ? $"已找到官方配置：{config.ConfigPath}"
            : $"尚未找到官方配置：{config.ConfigPath}";

        var hiddenText = hiddenModulesCount == 0 ? string.Empty : $" 已隐藏{hiddenModulesCount}个非单人模块。";
        UpdateSummary();
        StatusTextBlock.Text = $"已扫描{modules.Count}个单人Mod。{hiddenText} {configText}";
    }

    private void ApplyLauncherState(IReadOnlyList<BannerlordModule> modules)
    {
        var config = _launcherConfigService.Read();
        var stateById = config.Modules.ToDictionary(entry => entry.ModuleId, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < modules.Count; index++)
        {
            var module = modules[index];
            module.LoadOrder = index + 1;

            if (module.IsBaseModule)
            {
                module.IsEnabled = true;
                continue;
            }

            if (!module.IsSinglePlayerCompatible)
            {
                module.IsEnabled = false;
                continue;
            }

            module.IsEnabled = stateById.TryGetValue(module.Id, out var entry) && entry.IsEnabled;
        }
    }

    private List<BannerlordModule> OrderByLauncherState(IReadOnlyList<BannerlordModule> modules)
    {
        var config = _launcherConfigService.Read();
        var orderById = config.Modules.ToDictionary(entry => entry.ModuleId, entry => entry.LoadOrder, StringComparer.OrdinalIgnoreCase);

        var orderedModules = modules
            .OrderBy(module => orderById.GetValueOrDefault(module.Id, int.MaxValue))
            .ThenBy(module => module.LoadOrder ?? int.MaxValue)
            .ThenBy(module => module.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        ApplyLoadOrderNumbers(orderedModules);
        return orderedModules;
    }

    private void MoveSelectedModule(int direction)
    {
        if (ModulesList.SelectedItem is not BannerlordModule selected)
        {
            StatusTextBlock.Text = "请先选择一个Mod。";
            return;
        }

        MoveModule(selected, direction);
    }

    private void MoveModule(BannerlordModule selected, int direction)
    {
        if (!selected.CanToggle)
        {
            StatusTextBlock.Text = "官方基础模块不能手动移动。";
            return;
        }

        var currentIndex = _modules.IndexOf(selected);
        var targetIndex = FindMovableTargetIndex(currentIndex, direction);
        if (targetIndex < 0)
        {
            return;
        }

        _modules.Move(currentIndex, targetIndex);
        UpdateLoadOrderNumbers();
        ModulesList.SelectedItem = selected;
        ModulesList.ScrollIntoView(selected);
        StatusTextBlock.Text = $"已移动：{selected.Name}。启动游戏时会自动应用当前排序。";
    }

    private void ModulesList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null
            || FindAncestor<System.Windows.Controls.CheckBox>(e.OriginalSource as DependencyObject) is not null)
        {
            _dragStartPoint = null;
            _draggedModule = null;
            return;
        }

        _dragStartPoint = e.GetPosition(ModulesList);
        _draggedModule = FindModuleFromElement(e.OriginalSource as DependencyObject);
        var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        _dragPointerOffsetY = item is null ? 0 : e.GetPosition(item).Y;
    }

    private void ModulesList_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragStartPoint is null || _draggedModule is null)
        {
            return;
        }

        var currentPoint = e.GetPosition(ModulesList);
        if (_isDraggingModule)
        {
            UpdateDragPreviewPosition(currentPoint);
            UpdateDropTarget(currentPoint, _draggedModule);
            return;
        }

        if (Math.Abs(currentPoint.X - _dragStartPoint.Value.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(currentPoint.Y - _dragStartPoint.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (!_draggedModule.CanToggle)
        {
            StatusTextBlock.Text = "官方基础模块不能手动拖动。";
            _dragStartPoint = null;
            _draggedModule = null;
            return;
        }

        _isDraggingModule = true;
        ModulesList.CaptureMouse();
        ShowDragPreview(_draggedModule, currentPoint);
        UpdateDropTarget(currentPoint, _draggedModule);
    }

    private void ModulesList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingModule || _draggedModule is null)
        {
            ResetDragState();
            return;
        }

        StatusTextBlock.Text = $"已拖动排序：{_draggedModule.Name}。启动游戏时会自动应用当前排序。";
        ResetDragState();
        e.Handled = true;
    }

    private void ModulesList_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDraggingModule)
        {
            ResetDragState();
        }
    }

    private void MoveDraggedModuleToInsertIndex(BannerlordModule dragged)
    {
        if (_dropInsertIndex is null)
        {
            return;
        }

        var currentIndex = _modules.IndexOf(dragged);
        if (currentIndex < 0)
        {
            return;
        }

        var finalIndex = _dropInsertIndex.Value;
        if (currentIndex < finalIndex)
        {
            finalIndex--;
        }

        finalIndex = Math.Clamp(finalIndex, 0, _modules.Count - 1);
        if (finalIndex == currentIndex)
        {
            return;
        }

        _modules.Move(currentIndex, finalIndex);
        UpdateLoadOrderNumbers();
        ModulesList.SelectedItem = dragged;
    }

    private void UpdateDropTarget(System.Windows.Point pointer, BannerlordModule dragged)
    {
        var targetItem = FindListBoxItemAt(pointer);
        var target = targetItem?.DataContext as BannerlordModule;
        if (targetItem is null || target is null || !target.CanToggle || ReferenceEquals(target, dragged))
        {
            HideDropIndicator();
            return;
        }

        var pointerInItem = Mouse.GetPosition(targetItem);
        var insertAfterTarget = pointerInItem.Y > targetItem.ActualHeight / 2;
        var targetIndex = _modules.IndexOf(target);
        _dropInsertIndex = insertAfterTarget ? targetIndex + 1 : targetIndex;

        var itemTop = targetItem.TransformToAncestor(ModulesList).Transform(new System.Windows.Point(0, 0)).Y;
        var indicatorY = insertAfterTarget ? itemTop + targetItem.ActualHeight + 2 : itemTop - 2;
        DropIndicator.Width = Math.Max(0, ModulesList.ActualWidth - 26);
        Canvas.SetLeft(DropIndicator, 8);
        Canvas.SetTop(DropIndicator, Math.Max(0, indicatorY));
        DropIndicator.Visibility = Visibility.Visible;

        MoveDraggedModuleToInsertIndex(dragged);
    }

    private ListBoxItem? FindListBoxItemAt(System.Windows.Point pointer)
    {
        var hit = VisualTreeHelper.HitTest(ModulesList, pointer);
        return FindAncestor<ListBoxItem>(hit?.VisualHit);
    }

    private void ResetDragState()
    {
        if (ModulesList.IsMouseCaptured)
        {
            ModulesList.ReleaseMouseCapture();
        }

        _isDraggingModule = false;
        _dragStartPoint = null;
        _draggedModule = null;
        HideDragPreview();
        HideDropIndicator();
    }

    private void HideDropIndicator()
    {
        DropIndicator.Visibility = Visibility.Collapsed;
        _dropInsertIndex = null;
    }

    private void ShowDragPreview(BannerlordModule module, System.Windows.Point pointer)
    {
        DragPreview.Width = Math.Max(0, ModulesList.ActualWidth - 28);
        DragPreviewOrder.Text = module.LoadOrderText;
        DragPreviewName.Text = module.Name;
        DragPreviewVersion.Text = module.VersionText;
        DragPreviewTagText.Text = module.DisplayTagText;
        DragPreviewTag.Visibility = string.IsNullOrWhiteSpace(module.DisplayTagText) ? Visibility.Collapsed : Visibility.Visible;
        DragPreview.Visibility = Visibility.Visible;
        UpdateDragPreviewPosition(pointer);
    }

    private void UpdateDragPreviewPosition(System.Windows.Point pointer)
    {
        var previewHeight = DragPreview.ActualHeight > 0 ? DragPreview.ActualHeight : 58;
        var maxY = Math.Max(0, ModulesList.ActualHeight - previewHeight);

        Canvas.SetLeft(DragPreview, 0);
        Canvas.SetTop(DragPreview, Math.Clamp(pointer.Y - _dragPointerOffsetY, 0, maxY));
    }

    private void HideDragPreview()
    {
        DragPreview.Visibility = Visibility.Collapsed;
    }

    private void ModuleCheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateSummary();
    }

    private int FindMovableTargetIndex(int currentIndex, int direction)
    {
        for (var index = currentIndex + direction; index >= 0 && index < _modules.Count; index += direction)
        {
            var candidate = _modules[index];
            if (candidate.CanToggle)
            {
                return index;
            }
        }

        return -1;
    }

    private void ReplaceModules(IReadOnlyList<BannerlordModule> modules)
    {
        _modules.Clear();
        foreach (var module in modules)
        {
            _modules.Add(module);
        }

        UpdateLoadOrderNumbers();
    }

    private void UpdateLoadOrderNumbers()
    {
        ApplyLoadOrderNumbers(_modules);
        ModulesList.Items.Refresh();
        UpdateSummary();
    }

    private void SetAllToggleableModules(bool isEnabled)
    {
        foreach (var module in _modules.Where(module => module.CanToggle))
        {
            module.IsEnabled = isEnabled;
        }

        ModulesList.Items.Refresh();
    }

    private static void ApplyLoadOrderNumbers(IReadOnlyList<BannerlordModule> modules)
    {
        for (var index = 0; index < modules.Count; index++)
        {
            modules[index].LoadOrder = index + 1;
        }
    }

    private SaveLauncherConfigResult SaveCurrentState()
    {
        UpdateLoadOrderNumbers();

        if (_modules.Count == 0)
        {
            throw new InvalidOperationException("没有可应用的Mod。");
        }

        var result = _launcherConfigService.SaveSingleplayerState(_modules);
        UpdateSummary();
        return result;
    }

    private void UpdateSummary()
    {
        HeaderSummaryTextBlock.Text = $"{_modules.Count}个Mod · 已启用{_modules.Count(module => module.IsEnabled)}个 · 单人模式";
    }

    private bool ModuleMatchesSearch(object item)
    {
        if (item is not BannerlordModule module)
        {
            return false;
        }

        var searchText = SearchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return ContainsSearchText(module.Name, searchText)
            || ContainsSearchText(module.Id, searchText)
            || ContainsSearchText(module.DirectoryName, searchText)
            || ContainsSearchText(module.VersionText, searchText);
    }

    private static bool ContainsSearchText(string? value, string searchText)
    {
        return value?.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) == true;
    }

    private static BannerlordModule? FindModuleFromElement(DependencyObject? element)
    {
        return FindAncestor<ListBoxItem>(element)?.DataContext as BannerlordModule;
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T matched)
            {
                return matched;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private const uint MonitorDefaultToNearest = 0x00000002;
    private const int WmNcLeftButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct PointInfo
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public PointInfo ptReserved;
        public PointInfo ptMaxSize;
        public PointInfo ptMaxPosition;
        public PointInfo ptMinTrackSize;
        public PointInfo ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectInfo
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public RectInfo rcMonitor;
        public RectInfo rcWork;
        public uint dwFlags;
    }
}
