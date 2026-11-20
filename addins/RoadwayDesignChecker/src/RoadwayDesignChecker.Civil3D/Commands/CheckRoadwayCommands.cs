using Autodesk.AutoCAD.Runtime;
using RoadwayDesignChecker.Civil3D.Commands;
using RoadwayDesignChecker.Civil3D.UI;

[assembly: CommandClass(typeof(CheckRoadwayCommands))]

namespace RoadwayDesignChecker.Civil3D.Commands;

/// <summary>
/// AutoCAD / Civil 3D command entry points exposed on the command line and Ribbon.
/// </summary>
public sealed class CheckRoadwayCommands
{
    [CommandMethod("CHECKROADWAY", CommandFlags.Modal)]
    public void OpenRoadwayChecker()
    {
        CheckerPaletteHost.ShowAndSelectTab(0);
    }

    [CommandMethod("CHECKROADWAYQUICK", CommandFlags.Modal)]
    public void RunQuickAudit()
    {
        CheckerPaletteHost.ShowAndRunQuickAudit();
    }

    [CommandMethod("ROADWAYSTANDARDS", CommandFlags.Modal)]
    public void OpenDesignValuesTab()
    {
        CheckerPaletteHost.ShowAndSelectTab(1);
    }

    [CommandMethod("ROADWAYEXPORT", CommandFlags.Modal)]
    public void OpenExportTab()
    {
        CheckerPaletteHost.ShowAndSelectTab(2);
    }
}
