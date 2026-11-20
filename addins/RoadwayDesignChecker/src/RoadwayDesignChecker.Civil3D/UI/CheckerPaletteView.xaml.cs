using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.AutoCAD.DatabaseServices;
using Microsoft.Win32;
using RoadwayDesignChecker.Civil3D.Adapters;
using RoadwayDesignChecker.Civil3D.Services;
using RoadwayDesignChecker.Core.Criteria;
using RoadwayDesignChecker.Core.Models;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfMessageBox = System.Windows.MessageBox;
using WpfVisibility = System.Windows.Visibility;

namespace RoadwayDesignChecker.Civil3D.UI;

public partial class CheckerPaletteView : System.Windows.Controls.UserControl
{
    private readonly AuditWorkflowService _workflow = new();
    private readonly List<DesignStandardProfile> _standards = new();
    private readonly ObservableCollection<EditableSpeedRowVm> _editableRows = new();

    private DesignRuleOverrideSet? _activeOverrides;
    private AuditReport? _currentReport;
    private ObjectId _currentAlignmentId = ObjectId.Null;
    private string _statusFilter = "ALL";
    private string _categoryFilter = "ALL";

    public CheckerPaletteView()
    {
        InitializeComponent();
        LoadInitialStandards();
        RefreshDrawingObjects();
    }

    public void SelectMainTab(int index)
    {
        if (index >= 0 && index < MainTabs.Items.Count)
            MainTabs.SelectedIndex = index;
    }

    public void RefreshDrawingObjects()
    {
        try
        {
            var alignments = _workflow.GetAlignments();
            CmbAlignment.ItemsSource = alignments;
            if (alignments.Count > 0 && CmbAlignment.SelectedItem == null)
                CmbAlignment.SelectedIndex = 0;

            SetStatus(alignments.Count == 0
                ? "No Civil 3D Alignments found in the active drawing."
                : $"Found {alignments.Count} Alignment(s) in active drawing.");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not read drawing alignments: {ex.Message}");
        }
    }

    public void RunAuditFromCurrentSelection()
    {
        OnRunCheckClick(this, new RoutedEventArgs());
    }

    private void LoadInitialStandards()
    {
        _standards.Clear();
        _standards.AddRange(_workflow.LoadBuiltInStandards());
        CmbStandard.ItemsSource = null;
        CmbStandard.ItemsSource = _standards;

        if (_standards.Count > 0)
            CmbStandard.SelectedIndex = 0; // Defaults to SHC 301 Metric
    }

    private void OnAlignmentSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbAlignment.SelectedItem is not AlignmentListItem align)
        {
            CmbProfile.ItemsSource = null;
            return;
        }

        _currentAlignmentId = align.AlignmentId;
        var profileOptions = new List<ProfileListItem>
        {
            new(ObjectId.Null, "(Horizontal Alignment Only)")
        };
        profileOptions.AddRange(align.Profiles);

        CmbProfile.ItemsSource = profileOptions;
        CmbProfile.SelectedIndex = align.Profiles.Count > 0 ? 1 : 0;
    }

    private void OnStandardSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BindDesignValuesGrid();
    }

    private void OnPickAlignmentClick(object sender, RoutedEventArgs e)
    {
        var pickedId = _workflow.PickAlignment();
        if (!pickedId.HasValue)
            return;

        RefreshDrawingObjects();
        if (CmbAlignment.ItemsSource is IEnumerable<AlignmentListItem> list)
        {
            var match = list.FirstOrDefault(a => a.AlignmentId == pickedId.Value);
            if (match != null)
                CmbAlignment.SelectedItem = match;
        }
    }

    private void OnRefreshDrawingClick(object sender, RoutedEventArgs e)
    {
        RefreshDrawingObjects();
    }

    private void OnLoadXmlClick(object sender, RoutedEventArgs e)
    {
        if (CmbStandard.SelectedItem is not DesignStandardProfile currentStd)
            return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Autodesk Civil 3D Design Criteria XML",
            Filter = "Civil 3D Design Criteria XML (*.xml)|*.xml|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var xmlStandard = _workflow.LoadCivil3DCriteriaXml(dialog.FileName, currentStd);
            _standards.Add(xmlStandard);
            CmbStandard.ItemsSource = null;
            CmbStandard.ItemsSource = _standards;
            CmbStandard.SelectedItem = xmlStandard;
            SetStatus($"Loaded Civil 3D Criteria XML: {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Could not parse Civil 3D XML file:\n{ex.Message}", "Roadway Design Checker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnRunCheckClick(object sender, RoutedEventArgs e)
    {
        if (CmbAlignment.SelectedItem is not AlignmentListItem align)
        {
            SetStatus("Please select a Civil 3D Alignment first.");
            return;
        }

        if (CmbStandard.SelectedItem is not DesignStandardProfile baseStd)
        {
            SetStatus("No design standard is loaded.");
            return;
        }

        try
        {
            var selectedProfile = CmbProfile.SelectedItem as ProfileListItem;
            ObjectId? profileId = selectedProfile != null && !selectedProfile.ProfileId.IsNull
                ? selectedProfile.ProfileId
                : null;

            var options = BuildAuditOptions();
            var overrides = BuildOverridesFromGrid() ?? _activeOverrides;

            _currentAlignmentId = align.AlignmentId;
            _currentReport = _workflow.RunAudit(align.AlignmentId, profileId, baseStd, overrides, options);

            TxtSummaryHeadline.Text = _currentReport.PlainEnglishSummary;
            BtnFilterAll.Content = $"All ({_currentReport.TotalCount})";
            BtnFilterFail.Content = $"Needs Fix ({_currentReport.FailCount})";
            BtnFilterWarn.Content = $"Advisory ({_currentReport.WarningCount})";
            BtnFilterPass.Content = $"Passed ({_currentReport.PassCount})";

            SetupExpander.IsExpanded = false; // Give maximum vertical space to results
            RefreshFilteredCards();
            SetStatus($"Checked '{align.Name}' — {_currentReport.FailCount} need fix, {_currentReport.WarningCount} advisory, {_currentReport.PassCount} passed.");
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Audit failed:\n{ex.Message}", "Roadway Design Checker", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private AuditOptions BuildAuditOptions()
    {
        var roadClassTag = (CmbRoadClass.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var terrainTag = (CmbTerrain.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var emaxTag = (CmbEmax.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        _ = Enum.TryParse<RoadwayClassification>(roadClassTag, out var roadClass);
        _ = Enum.TryParse<TerrainType>(terrainTag, out var terrain);
        var emax = double.TryParse(emaxTag, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedEmax) ? parsedEmax : 6.0;
        var fallbackSpeed = double.TryParse(TxtFallbackSpeed.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var sp) ? sp : 100.0;
        var lateralM = double.TryParse(TxtLateralM.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : 5.0;

        return new AuditOptions
        {
            RoadwayClass = roadClass,
            Terrain = terrain,
            MaxSuperelevationPercent = emax,
            FallbackDesignSpeedKph = fallbackSpeed,
            AvailableLateralClearanceM = lateralM,
            CheckHorizontal = ChkHoriz.IsChecked == true,
            CheckVertical = ChkVert.IsChecked == true,
            CheckSightDistance = ChkSight.IsChecked == true,
            EvaluatePassingSightDistance = ChkPsd.IsChecked == true
        };
    }

    private void OnStatusFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tag)
        {
            _statusFilter = tag;
            RefreshFilteredCards();
        }
    }

    private void OnCategoryFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbCategoryFilter.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _categoryFilter = tag;
            RefreshFilteredCards();
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshFilteredCards();
    }

    private void RefreshFilteredCards()
    {
        if (_currentReport == null)
        {
            FindingsList.ItemsSource = null;
            CoachDrawer.Visibility = WpfVisibility.Collapsed;
            return;
        }

        var query = (TxtSearch.Text ?? string.Empty).Trim();
        var filtered = _currentReport.Results.Where(r =>
        {
            if (_statusFilter == "FAIL" && r.Severity != CheckSeverity.Fail) return false;
            if (_statusFilter == "WARN" && r.Severity != CheckSeverity.Warning) return false;
            if (_statusFilter == "PASS" && r.Severity != CheckSeverity.Pass) return false;
            if (_categoryFilter != "ALL" && !string.Equals(r.Category.ToString(), _categoryFilter, StringComparison.OrdinalIgnoreCase)) return false;

            if (!string.IsNullOrEmpty(query))
            {
                var hay = $"{r.Headline} {r.LocationText} {r.CategoryLabel} {r.WhyItMatters}";
                if (!hay.Contains(query, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }).Select(r => new FindingCardVm(r)).ToList();

        FindingsList.ItemsSource = filtered;
        if (filtered.Count > 0)
            FindingsList.SelectedIndex = 0;
        else
            CoachDrawer.Visibility = WpfVisibility.Collapsed;
    }

    private void OnFindingSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FindingsList.SelectedItem is not FindingCardVm vm)
        {
            CoachDrawer.Visibility = WpfVisibility.Collapsed;
            return;
        }

        var r = vm.Result;
        CoachDrawer.Visibility = WpfVisibility.Visible;
        TxtCoachHeadline.Text = r.Headline;
        BarCompliance.Value = r.CompliancePercent;
        BarCompliance.Foreground = vm.SeverityBrush;
        TxtWhyItMatters.Text = r.WhyItMatters;
        TxtHowToFix.Text = r.HowToFix;
        TxtTechnicalRef.Text = r.TechnicalReference;
        TxtEngineerNote.Text = r.EngineerNote;
    }

    private void OnFindingDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindingsList.SelectedItem is FindingCardVm vm)
            ZoomToFinding(vm.Result);
    }

    private void OnZoomCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is DesignCheckResult result)
            ZoomToFinding(result);
    }

    private void OnZoomSelectedClick(object sender, RoutedEventArgs e)
    {
        if (FindingsList.SelectedItem is FindingCardVm vm)
            ZoomToFinding(vm.Result);
    }

    private void ZoomToFinding(DesignCheckResult finding)
    {
        if (_currentAlignmentId.IsNull)
            return;

        var ok = _workflow.ZoomToFinding(_currentAlignmentId, finding);
        SetStatus(ok
            ? $"Zoomed Civil 3D viewport to {finding.LocationText}."
            : "Could not zoom viewport to selected station.");
    }

    private void OnSaveNoteClick(object sender, RoutedEventArgs e)
    {
        if (FindingsList.SelectedItem is not FindingCardVm vm)
            return;

        vm.Result.EngineerNote = TxtEngineerNote.Text.Trim();
        SetStatus($"Saved engineer note for {vm.Result.EntityLabel}.");
    }

    // --- Tab 2: Design Values Editor ---

    private void BindDesignValuesGrid()
    {
        if (CmbStandard.SelectedItem is not DesignStandardProfile baseStd)
            return;

        var effective = StandardRepository.ApplyOverrides(baseStd, _activeOverrides);
        TxtStandardDetails.Text = $"{effective.DisplayName} — {effective.Description}";

        _editableRows.Clear();
        foreach (var row in effective.SpeedTable)
        {
            _editableRows.Add(new EditableSpeedRowVm
            {
                DesignSpeedKph = row.DesignSpeedKph,
                MinRadius6 = row.MinRadiusByEmax.TryGetValue("6", out var r6) ? r6 : 0,
                MinRadius8 = row.MinRadiusByEmax.TryGetValue("8", out var r8) ? r8 : 0,
                MinSpiralLengthM = row.MinSpiralLengthM,
                MaxDesirableGradePercent = row.MaxDesirableGradePercent,
                MaxAllowableGradePercent = row.MaxAllowableGradePercent,
                MinCrestKSsd = row.MinCrestKSsd,
                MinSagK = row.MinSagK,
                StoppingSightDistanceLevelM = row.StoppingSightDistanceLevelM
            });
        }

        GridSpeedTable.ItemsSource = _editableRows;
    }

    private DesignRuleOverrideSet? BuildOverridesFromGrid()
    {
        if (_editableRows.Count == 0 || CmbStandard.SelectedItem is not DesignStandardProfile baseStd)
            return _activeOverrides;

        var overrides = new DesignRuleOverrideSet
        {
            Name = _activeOverrides?.Name ?? "Custom Table Edits",
            BaseStandardId = baseStd.Id
        };

        foreach (var edited in _editableRows)
        {
            var baseRow = baseStd.SpeedTable.FirstOrDefault(r => Math.Abs(r.DesignSpeedKph - edited.DesignSpeedKph) < 0.01);
            if (baseRow == null) continue;

            var baseR6 = baseRow.MinRadiusByEmax.TryGetValue("6", out var r6) ? r6 : 0;
            var baseR8 = baseRow.MinRadiusByEmax.TryGetValue("8", out var r8) ? r8 : 0;

            var changed =
                Math.Abs(edited.MinRadius6 - baseR6) > 1e-4 ||
                Math.Abs(edited.MinRadius8 - baseR8) > 1e-4 ||
                Math.Abs(edited.MinSpiralLengthM - baseRow.MinSpiralLengthM) > 1e-4 ||
                Math.Abs(edited.MaxDesirableGradePercent - baseRow.MaxDesirableGradePercent) > 1e-4 ||
                Math.Abs(edited.MaxAllowableGradePercent - baseRow.MaxAllowableGradePercent) > 1e-4 ||
                Math.Abs(edited.MinCrestKSsd - baseRow.MinCrestKSsd) > 1e-4 ||
                Math.Abs(edited.MinSagK - baseRow.MinSagK) > 1e-4 ||
                Math.Abs(edited.StoppingSightDistanceLevelM - baseRow.StoppingSightDistanceLevelM) > 1e-4;

            if (!changed) continue;

            overrides.SpeedOverrides.Add(new SpeedCriteriaOverrideRow
            {
                DesignSpeedKph = edited.DesignSpeedKph,
                MinRadiusByEmax = new Dictionary<string, double>
                {
                    ["6"] = edited.MinRadius6,
                    ["8"] = edited.MinRadius8
                },
                MinSpiralLengthM = edited.MinSpiralLengthM,
                MaxDesirableGradePercent = edited.MaxDesirableGradePercent,
                MaxAllowableGradePercent = edited.MaxAllowableGradePercent,
                MinCrestKSsd = edited.MinCrestKSsd,
                MinSagK = edited.MinSagK,
                StoppingSightDistanceLevelM = edited.StoppingSightDistanceLevelM
            });
        }

        return overrides.SpeedOverrides.Count > 0 ? overrides : _activeOverrides;
    }

    private void OnLoadOverrideClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Load Custom Design Values Override (.json)",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _activeOverrides = _workflow.LoadOverrideJson(dialog.FileName);
            BindDesignValuesGrid();
            SetStatus($"Loaded override file: {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Could not load JSON override file:\n{ex.Message}", "Roadway Design Checker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnSaveOverrideClick(object sender, RoutedEventArgs e)
    {
        var overrides = BuildOverridesFromGrid() ?? new DesignRuleOverrideSet { Name = "Custom Design Values" };
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Custom Design Values Override (.json)",
            Filter = "JSON Files (*.json)|*.json",
            FileName = "project-roadway-overrides.json"
        };

        if (dialog.ShowDialog() != true)
            return;

        _workflow.SaveOverrideJson(dialog.FileName, overrides);
        _activeOverrides = overrides;
        SetStatus($"Saved custom design values to {Path.GetFileName(dialog.FileName)}.");
    }

    private void OnResetOverridesClick(object sender, RoutedEventArgs e)
    {
        _activeOverrides = null;
        BindDesignValuesGrid();
        SetStatus("Reset design values to built-in standard.");
    }

    // --- Tab 3: Export Reports ---

    private void OnExportHtmlClick(object sender, RoutedEventArgs e) => ExportWithDialog("html", "HTML Review Sheet (*.html)|*.html");
    private void OnExportCsvClick(object sender, RoutedEventArgs e) => ExportWithDialog("csv", "CSV Spreadsheet (*.csv)|*.csv");
    private void OnExportJsonClick(object sender, RoutedEventArgs e) => ExportWithDialog("json", "JSON Findings (*.json)|*.json");

    private void ExportWithDialog(string format, string filter)
    {
        if (_currentReport == null)
        {
            SetStatus("Run a roadway check before exporting a report.");
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = $"Export Roadway Review ({format.ToUpperInvariant()})",
            Filter = filter,
            FileName = $"{_currentReport.AlignmentName}_RoadwayReview.{format}"
        };

        if (dialog.ShowDialog() != true)
            return;

        _workflow.ExportReport(dialog.FileName, _currentReport, format, ChkIncludePassed.IsChecked == true);
        SetStatus($"Exported {format.ToUpperInvariant()} report to {Path.GetFileName(dialog.FileName)}.");
    }

    private void SetStatus(string message) => TxtStatusBar.Text = message;

    // --- Presentation ViewModels ---

    private sealed class FindingCardVm
    {
        private static readonly WpfBrush FailBrush = new SolidColorBrush(WpfColor.FromRgb(239, 68, 68));
        private static readonly WpfBrush WarnBrush = new SolidColorBrush(WpfColor.FromRgb(245, 158, 11));
        private static readonly WpfBrush PassBrush = new SolidColorBrush(WpfColor.FromRgb(34, 197, 94));
        private static readonly WpfBrush FailBg = new SolidColorBrush(WpfColor.FromArgb(40, 239, 68, 68));
        private static readonly WpfBrush WarnBg = new SolidColorBrush(WpfColor.FromArgb(40, 245, 158, 11));
        private static readonly WpfBrush PassBg = new SolidColorBrush(WpfColor.FromArgb(40, 34, 197, 94));

        public FindingCardVm(DesignCheckResult result) => Result = result;

        public DesignCheckResult Result { get; }
        public string StatusLabel => Result.StatusLabel;
        public string CategoryLabel => Result.CategoryLabel;
        public string Headline => Result.Headline;
        public string LocationAndSpeed => $"{Result.LocationText} · Design Speed: {Result.DesignSpeedKph:0} km/h";
        public string YourDrawingText => Result.YourDrawingText;
        public string StandardRequiresText => Result.StandardRequiresText;
        public string DifferenceText => Result.DifferenceText;

        public WpfBrush SeverityBrush => Result.Severity switch
        {
            CheckSeverity.Fail => FailBrush,
            CheckSeverity.Warning => WarnBrush,
            _ => PassBrush
        };

        public WpfBrush BadgeBackground => Result.Severity switch
        {
            CheckSeverity.Fail => FailBg,
            CheckSeverity.Warning => WarnBg,
            _ => PassBg
        };
    }

    private sealed class EditableSpeedRowVm
    {
        public double DesignSpeedKph { get; set; }
        public double MinRadius6 { get; set; }
        public double MinRadius8 { get; set; }
        public double MinSpiralLengthM { get; set; }
        public double MaxDesirableGradePercent { get; set; }
        public double MaxAllowableGradePercent { get; set; }
        public double MinCrestKSsd { get; set; }
        public double MinSagK { get; set; }
        public double StoppingSightDistanceLevelM { get; set; }
    }
}
