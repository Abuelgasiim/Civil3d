using System.Windows.Input;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using RoadwayDesignChecker.Civil3D;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(PluginEntry))]

namespace RoadwayDesignChecker.Civil3D;

/// <summary>
/// AutoCAD / Civil 3D plugin entry point that registers the "Roadway QA" Ribbon panel
/// under the Analyze tab (or creates one if absent) when Civil 3D finishes loading.
/// </summary>
public sealed class PluginEntry : IExtensionApplication
{
    private const string PanelId = "RDC_ROADWAY_QA_PANEL";
    private bool _ribbonInitialized;

    public void Initialize()
    {
        TryAttachRibbon();
        if (!_ribbonInitialized)
        {
            AcadApp.Idle += OnApplicationIdle;
        }
    }

    public void Terminate()
    {
        AcadApp.Idle -= OnApplicationIdle;
        UI.CheckerPaletteHost.DisposePalette();
    }

    private void OnApplicationIdle(object? sender, EventArgs e)
    {
        TryAttachRibbon();
        if (_ribbonInitialized)
        {
            AcadApp.Idle -= OnApplicationIdle;
        }
    }

    private void TryAttachRibbon()
    {
        var ribbon = ComponentManager.Ribbon;
        if (ribbon == null || ribbon.Tabs.Count == 0)
            return;

        // Place inside Civil 3D's Analyze tab if present, otherwise fall back to Home tab or first tab.
        var targetTab = ribbon.Tabs.FirstOrDefault(t =>
                            string.Equals(t.Id, "ACAD.ID_TabAnalyze", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t.Title, "Analyze", StringComparison.OrdinalIgnoreCase))
                        ?? ribbon.Tabs.FirstOrDefault(t =>
                            string.Equals(t.Title, "Home", StringComparison.OrdinalIgnoreCase))
                        ?? ribbon.Tabs[0];

        if (targetTab.Panels.Any(p => p.Source?.Id == PanelId))
        {
            _ribbonInitialized = true;
            return;
        }

        var panelSource = new RibbonPanelSource
        {
            Id = PanelId,
            Title = "Roadway QA"
        };

        var mainButton = new RibbonButton
        {
            Id = "RDC_BTN_CHECK",
            Text = "Roadway\nChecker",
            ShowText = true,
            Size = RibbonItemSize.Large,
            Orientation = System.Windows.Controls.Orientation.Vertical,
            ToolTip = "Open the human-readable Roadway Design Checker palette (SHC 301 & AASHTO 2018).",
            CommandParameter = "CHECKROADWAY ",
            CommandHandler = new RibbonCommandHandler()
        };

        var stackPanel = new RibbonRowPanel();
        stackPanel.Items.Add(CreateSmallButton("RDC_BTN_QUICK", "Quick Audit", "CHECKROADWAYQUICK ", "Re-run checks on the active alignment and profile."));
        stackPanel.Items.Add(new RibbonRowBreak());
        stackPanel.Items.Add(CreateSmallButton("RDC_BTN_STANDARDS", "Design Values", "ROADWAYSTANDARDS ", "View or override SHC 301 / AASHTO design values."));
        stackPanel.Items.Add(new RibbonRowBreak());
        stackPanel.Items.Add(CreateSmallButton("RDC_BTN_EXPORT", "Export Report", "ROADWAYEXPORT ", "Export a human-readable HTML, CSV, or JSON review sheet."));

        panelSource.Items.Add(mainButton);
        panelSource.Items.Add(stackPanel);

        targetTab.Panels.Add(new RibbonPanel { Source = panelSource });
        _ribbonInitialized = true;
    }

    private static RibbonButton CreateSmallButton(string id, string text, string command, string tooltip)
    {
        return new RibbonButton
        {
            Id = id,
            Text = text,
            ShowText = true,
            Size = RibbonItemSize.Standard,
            ToolTip = tooltip,
            CommandParameter = command,
            CommandHandler = new RibbonCommandHandler()
        };
    }

    private sealed class RibbonCommandHandler : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            var cmd = parameter?.ToString() ?? "CHECKROADWAY ";
            doc.SendStringToExecute(cmd, true, false, false);
        }
    }
}
