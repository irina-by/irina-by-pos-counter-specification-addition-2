using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace PosCounter.Net.Models
{
    public class PosSettings
    {
        public string DefaultLayer { get; set; } = "0. В выноски";
        public string LastLayer { get; set; } = string.Empty;
        public string Mode { get; set; } = "layer";
        public string LayerScope { get; set; } = "current";
        public string ViewMode { get; set; } = "dwg";
        public bool AutoOpenExcel { get; set; } = true;
        public bool CountAllInModel { get; set; } = false;
    }

    public class PositionCount
    {
        public int Position { get; set; }
        public int Count { get; set; }
    }

    public class LayerResult
    {
        public string LayerName { get; set; }
        public List<PositionCount> Positions { get; set; } = new List<PositionCount>();
    }

    public class ViewportResult
    {
        public string LayoutName { get; set; }
        public string ViewportHandle { get; set; }
        public string LayerName { get; set; }
        public List<PositionCount> CurrentLayerPositions { get; set; } = new List<PositionCount>();
        public List<LayerResult> AllLayerPositions { get; set; } = new List<LayerResult>();
    }

    public class CountComputationResult
    {
        public bool Success { get; set; }
        public string ViewMode { get; set; }
        public string Scope { get; set; }
        public string SelectedLayer { get; set; }
        public string StatusMessage { get; set; }
        public string WarningMessage { get; set; }
        public bool UsedViewportPolygonSelection { get; set; }
        public List<PositionCount> CurrentLayerPositions { get; set; } = new List<PositionCount>();
        public List<LayerResult> AllLayerPositions { get; set; } = new List<LayerResult>();
        public List<ViewportResult> ViewportResults { get; set; } = new List<ViewportResult>();
        public List<ObjectId> HighlightObjectIds { get; set; } = new List<ObjectId>();
    }
}
