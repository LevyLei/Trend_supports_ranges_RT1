#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.UI;
using FTOptix.DataLogger;
using FTOptix.NativeUI;
using FTOptix.WebUI;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.Retentivity;
using FTOptix.CoreBase;
using FTOptix.Core;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Collections.Generic;
#endregion

public class RuntimeNetLogic4 : BaseNetLogic
{

    static void PrintTable(object[,] table)
    {
        int numRows = table.GetLength(0);
        int numCols = table.GetLength(1);

        // Calculate column widths
        int[] columnWidths = new int[numCols];
        for (int col = 0; col < numCols; col++)
        {
            for (int row = 0; row < numRows; row++)
            {
                string cellValue;
                if (table[row, col] == null)
                {
                    cellValue = "no value";
                } else {
                    cellValue = table[row, col].ToString();
                }
                int cellLength = cellValue.Length;
                if (cellLength > columnWidths[col])
                {
                    columnWidths[col] = cellLength;
                }
            }
        }

        // Print the table
        for (int row = 0; row < numRows; row++)
        {
            for (int col = 0; col < numCols; col++)
            {
                string cellValue;
                if (table[row, col] == null)
                {
                    cellValue = "no value";
                }
                else
                {
                    cellValue = table[row, col].ToString();
                }
                Console.Write(cellValue.PadRight(columnWidths[col] + 2)); // Add padding for alignment
            }
            Console.WriteLine(); // Move to the next row
        }
    }

    static void PrintHeader(string[] header)
    {
        for(int i = 0; i < header.Length; i++)
        {
            Console.WriteLine(header[i].PadRight(10));
        }
    }

    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
        var trend = Session.Get<Trend>("UIRoot/Trend/Trend1");
        var pens = trend.Get("Pens");
        
        var store = (Store)Project.Current.GetObject("DataStores/EmbeddedDatabase1");
        var rangesNode = trend.Get("TimeRanges");
        referencesObserver = new ReferencesObserver(rangesNode, pens, LogicObject.Owner.Get<Item>("Scroll/Container"), store);

        referencesEventRegistration = rangesNode.RegisterEventObserver(
            referencesObserver, EventType.ForwardReferenceAdded | EventType.ForwardReferenceRemoved);
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
        if (referencesEventRegistration != null)
            referencesEventRegistration.Dispose();

        referencesEventRegistration = null;
        referencesObserver = null;
    }

    private class ReferencesObserver : IReferenceObserver
    {
        public ReferencesObserver(IUANode rangesNode, IUANode pens, Item uiContainer, Store store)
        {
            this.uiContainer = uiContainer;
            this.store = store;
            this.pens= pens;
            rangesNode.Children.ToList().ForEach(CreateRangeUI);
        }

        public void OnReferenceAdded(IUANode sourceNode, IUANode targetNode, NodeId referenceTypeId, ulong senderId)
        {
            CreateRangeUI(targetNode);
        }

        public void OnReferenceRemoved(IUANode sourceNode, IUANode targetNode, NodeId referenceTypeId, ulong senderId)
        {
            var uiRange = uiContainer.Get(targetNode.BrowseName);
            if (uiRange != null)
                uiRange.Delete();
        }

        public bool IsReferenceAllowed(NodeId referenceTypeId)
        {
            return referenceTypeId == UAManagedCore.OpcUa.ReferenceTypes.HasComponent ||
                   referenceTypeId == UAManagedCore.OpcUa.ReferenceTypes.HasOrderedComponent;
        }

        void CreateRangeUI(IUANode rangeNode)
        {
            Log.Info("add " + rangeNode.BrowseName);
            TimeRange range = (TimeRange)(rangeNode as IUAVariable).Value.Value;
            var trendTimeRange = InformationModel.MakeObject<TrendTimeRange>("TimeRange");
            trendTimeRange.Start = range.StartTime;
            trendTimeRange.End = range.EndTime;
            var timeSpan = range.EndTime - range.StartTime;
            trendTimeRange.TimeSpan = timeSpan.TotalMilliseconds;
            var pensAndColumns = GetPenAndColumns();
            pensAndColumns.ForEach(p =>
            {
                var rangeStatistics = InformationModel.MakeObject<RangeStatistics>(p.column);
                var stats = GetFromStore(p.column, range.StartTime, range.EndTime);
                if (stats == null) return;
                rangeStatistics.Avg = stats.Value.Avg;
                rangeStatistics.Min = stats.Value.Min;
                rangeStatistics.Max = stats.Value.Max;
                rangeStatistics.Pen = p.pen.NodeId;
                trendTimeRange.Get("Statistics").Add(rangeStatistics);
            });

            var rangeUI = InformationModel.MakeObject<RangeUI>(rangeNode.BrowseName);
            rangeUI.Add(trendTimeRange);
            rangeUI.GetVariable("Range").Value = trendTimeRange.NodeId;
            uiContainer.Add(rangeUI);
        }

        struct Statistics
        {
            public TrendPen pen;
            public double Avg;
            public long Min;
            public long Max;
        }

        struct PenColumn
        {
            public TrendPen pen;
            public string column;
        }

        private List<PenColumn> GetPenAndColumns()
        {
            var penColumns = new List<PenColumn>();
            pens.Children.OfType<TrendPen>().ToList().ForEach((pen) =>
            {
                pen.Children.OfType<DynamicLink>().ToList().ForEach((dynamicLink) =>
                {
                    var pointedVar = dynamicLink.Refs.GetVariable(FTOptix.Core.ReferenceTypes.Resolves);
                    if (pointedVar!= null && pointedVar.BrowseName == "LastValue")
                    {
                        PenColumn penColumn = new PenColumn();
                        penColumn.pen = pen;
                        penColumn.column = pointedVar.Owner.BrowseName;
                        Console.WriteLine(pointedVar.Owner.BrowseName);
                        penColumns.Add(penColumn);
                    }
                });
            });
            return penColumns;
        }

        Statistics? GetFromStore(string column, DateTime start, DateTime end)
        {
            string[] header;
            object[,] output;
            {
                store.Query($"SELECT * FROM Table1 WHERE Timestamp BETWEEN \"{start.ToString("o", CultureInfo.InvariantCulture)}\" AND \"{end.ToString("o", CultureInfo.InvariantCulture)}\" ORDER BY Timestamp", out header, out output);
                PrintHeader(header);
            }
            store.Query($"SELECT AVG({column}), MAX({column}), MIN({column}) FROM Table1 WHERE Timestamp BETWEEN \"{start.ToString("o", CultureInfo.InvariantCulture)}\" AND \"{end.ToString("o", CultureInfo.InvariantCulture)}\" ORDER BY Timestamp", out header, out output);
            PrintHeader(header);
            PrintTable(output);
            if (output[0,0] != null)
            {
                Statistics statistics= new Statistics();
                statistics.Avg = (double)output[0, 0];
                statistics.Max = (long)output[0, 1];
                statistics.Min = (long)output[0, 2];
                return statistics;
            }
            return null;
        }

        Item uiContainer;
        Store store;
        IUANode pens;
    }

    private ReferencesObserver referencesObserver;
    private IEventRegistration referencesEventRegistration;
}
