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
#endregion

public class RuntimeNetLogic2 : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
        thresholds = LogicObject.GetAlias("Thresholds");
        referencesObserver = new ReferencesObserver(thresholds, LogicObject.Owner.Get<Item>("Accordion1/Content/Container"));

        referencesEventRegistration = thresholds.RegisterEventObserver(
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
    public void AddThreshold()
    {
        var threshold = InformationModel.MakeObject<TrendThreshold>("Threshold" + count++);
        threshold.Color = new Color(255, (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255));
        threshold.Thickness = 3;
        thresholds.Add(threshold);
    }

    private class ReferencesObserver : IReferenceObserver
    {
        public ReferencesObserver(IUANode thresholdsNode, Item uiContainer)
        {
            this.uiContainer = uiContainer;
            thresholdsNode.Children.ToList().ForEach(CreateThresholdUI);
        }

        public void OnReferenceAdded(IUANode sourceNode, IUANode targetNode, NodeId referenceTypeId, ulong senderId)
        {
            CreateThresholdUI(targetNode);
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

        void CreateThresholdUI(IUANode thresholdNode)
        {
            Log.Info("add " + thresholdNode.BrowseName);
            var thresholdUI = InformationModel.MakeObject<ThresholdUI>(thresholdNode.BrowseName);
            thresholdUI.GetVariable("Threshold").Value = thresholdNode.NodeId;
            uiContainer.Add(thresholdUI);
        }

        Item uiContainer;
    }

    int count = 0;
    IUANode thresholds;
    Random rnd = new Random();
    private ReferencesObserver referencesObserver;
    private IEventRegistration referencesEventRegistration;
}
