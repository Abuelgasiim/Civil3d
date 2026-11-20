using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace RoadwayDesignChecker.Civil3D.Services;

/// <summary>
/// Pans and zooms the active Civil 3D viewport directly to any alignment station
/// and highlights the governing Alignment entity.
/// </summary>
public sealed class DrawingNavigator
{
    public bool ZoomToStation(ObjectId alignmentId, double station, double spanLengthMeters = 120.0)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null || alignmentId.IsNull || alignmentId.IsErased)
            return false;

        try
        {
            using var docLock = doc.LockDocument();
            using var tr = doc.Database.TransactionManager.StartTransaction();

            if (tr.GetObject(alignmentId, OpenMode.ForRead) is not Alignment alignment)
                return false;

            var clampedStation = Math.Clamp(station, alignment.StartingStation, alignment.EndingStation);
            double easting = 0.0, northing = 0.0;
            alignment.PointLocation(clampedStation, 0.0, ref easting, ref northing);

            var editor = doc.Editor;
            using var view = editor.GetCurrentView();

            var viewSpan = Math.Clamp(spanLengthMeters * 1.8, 80.0, 500.0);
            var aspect = view.Height > 1e-6 ? (view.Width / view.Height) : 1.6;

            view.CenterPoint = new Point2d(easting, northing);
            view.Height = viewSpan;
            view.Width = viewSpan * aspect;

            editor.SetCurrentView(view);
            alignment.Highlight();
            editor.UpdateScreen();

            tr.Commit();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
