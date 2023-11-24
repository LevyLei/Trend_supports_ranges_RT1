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
using System.Threading;
#endregion

public class RuntimeNetLogic3 : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
        trend = Session.Get<Trend>("UIRoot/Trend/Trend1");
        var pensNode = trend.Get("Pens");
        referencesObserver = new ReferencesObserver(pensNode, LogicObject.Owner.Get<Item>("ScrollView1/Container"));

        referencesEventRegistration = pensNode.RegisterEventObserver(
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

    [ExportMethod]
    public void AddPen()
    {
        var pen = InformationModel.MakeVariable<TrendPen>("Pen" + count, OpcUa.DataTypes.Float);
        var variable = InformationModel.MakeVariable("Variable" + count, OpcUa.DataTypes.Float);
        pen.Color = new Color(255, (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255));
        trend.Pens.Add(pen);
        Project.Current.Get("Model/RuntimeAdded").Add(variable);
        pen.SetDynamicLink(variable);
        count++;
        //var threshold = InformationModel.MakeObject<TrendThreshold>("Threshold" + count++);
        //threshold.Color = new Color(255, 0, 0, 0);
        //(pen as TrendPen).Thresholds.Add(threshold);
    }

    private class ReferencesObserver : IReferenceObserver
    {
        public ReferencesObserver(IUANode pensNode, Item uiContainer)
        {
            this.uiContainer = uiContainer;
            pensNode.Children.ToList().ForEach(CreatePenUI);
        }

        public void OnReferenceAdded(IUANode sourceNode, IUANode targetNode, NodeId referenceTypeId, ulong senderId)
        {
            CreatePenUI(targetNode);
        }

        public void OnReferenceRemoved(IUANode sourceNode, IUANode targetNode, NodeId referenceTypeId, ulong senderId)
        {
            var uiThreshold = uiContainer.Get(targetNode.BrowseName);
            if (uiThreshold != null)
                uiThreshold.Delete();
        }

        public bool IsReferenceAllowed(NodeId referenceTypeId)
        {
            return referenceTypeId == UAManagedCore.OpcUa.ReferenceTypes.HasComponent ||
                   referenceTypeId == UAManagedCore.OpcUa.ReferenceTypes.HasOrderedComponent;
        }

        void CreatePenUI(IUANode penNode)
        {
            Log.Info("add " + penNode.BrowseName);
            var penUI = InformationModel.MakeObject<PenUI>(penNode.BrowseName);
            penUI.GetVariable("Pen").Value = penNode.NodeId;
            uiContainer.Add(penUI);
        }

        Item uiContainer;
    }

    Trend trend;
    int count = 0;
    Random rnd = new Random();
    private ReferencesObserver referencesObserver;
    private IEventRegistration referencesEventRegistration;
}
