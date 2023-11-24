#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.SQLiteStore;
using FTOptix.DataLogger;
using FTOptix.HMIProject;
using FTOptix.NativeUI;
using FTOptix.UI;
using FTOptix.WebUI;
using FTOptix.Store;
using FTOptix.Retentivity;
using FTOptix.CoreBase;
using FTOptix.Core;
using FTOptix.NetLogic;
#endregion

public class RuntimeNetLogic1 : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
        var variable1 = Project.Current.GetVariable("Model/Variable1");
        var variable2 = Project.Current.GetVariable("Model/Variable2");

        task = new PeriodicTask(() =>
        {
            variable1.Value = getSinValue(100);
            if (count > 30)
            {
                variable2.Value = getSinValue(100);
                count = 0;
            }

            count++;
        }, period, LogicObject);

        task.Start();
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    private double getSinValue(double amplitude)
    {
        double sinValue = amplitude * Math.Sin(phase);

        // Update the phase for the next iteration
        phase += increment;

        // Ensure phase stays within one full cycle (2 * PI)
        if (phase >= 2 * Math.PI)
            phase -= 2 * Math.PI;

        return sinValue;
    }

    PeriodicTask task;
    private int count = 0;
    private static int period = 200;
    private static double totalTime = 60000; // 20 seconds in milliseconds
    private static double phase = 0;
    private static double increment = (2 * Math.PI) / (totalTime / 200);
}
