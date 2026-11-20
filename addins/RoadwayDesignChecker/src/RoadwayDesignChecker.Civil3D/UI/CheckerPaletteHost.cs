using Autodesk.AutoCAD.Windows;

namespace RoadwayDesignChecker.Civil3D.UI;

/// <summary>
/// Manages the singleton AutoCAD / Civil 3D dockable PaletteSet hosting the WPF CheckerPaletteView.
/// </summary>
public static class CheckerPaletteHost
{
    private static readonly Guid PaletteGuid = new("D94F4E7B-9A25-4E6E-B5A1-73E0B93F301A");
    private static PaletteSet? _paletteSet;
    private static CheckerPaletteView? _view;

    public static void ShowAndSelectTab(int tabIndex)
    {
        EnsureCreated();
        if (_paletteSet == null || _view == null)
            return;

        _paletteSet.Visible = true;
        _view.RefreshDrawingObjects();
        _view.SelectMainTab(tabIndex);
    }

    public static void ShowAndRunQuickAudit()
    {
        EnsureCreated();
        if (_paletteSet == null || _view == null)
            return;

        _paletteSet.Visible = true;
        _view.RefreshDrawingObjects();
        _view.SelectMainTab(0);
        _view.RunAuditFromCurrentSelection();
    }

    public static void DisposePalette()
    {
        if (_paletteSet != null)
        {
            _paletteSet.Dispose();
            _paletteSet = null;
            _view = null;
        }
    }

    private static void EnsureCreated()
    {
        if (_paletteSet != null)
            return;

        _view = new CheckerPaletteView();
        _paletteSet = new PaletteSet("Roadway Design Checker (SHC 301 / AASHTO)", PaletteGuid)
        {
            Style = PaletteSetStyles.ShowPropertiesMenu
                  | PaletteSetStyles.ShowAutoHideButton
                  | PaletteSetStyles.ShowCloseButton,
            MinimumSize = new System.Drawing.Size(440, 560),
            Size = new System.Drawing.Size(520, 760),
            DockEnabled = DockSides.Left | DockSides.Right | DockSides.None
        };

        _paletteSet.AddVisual("Design Review", _view);
    }
}
