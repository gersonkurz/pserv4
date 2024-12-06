using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using log4net;
using System.Reflection;
using System.Runtime.Versioning;

namespace pserv4
{
    [SupportedOSPlatform("windows")]

    public class ApplyTemplate : BackgroundAction
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly DataController DC;
        public readonly List<ActionTemplateInfo> Requests;

        public ApplyTemplate(DataController controller, List<ActionTemplateInfo> requests)
        {
            DC = controller;
            Requests = requests;
        }

        public override void DoWork()
        {
            foreach (ActionTemplateInfo request in Requests)
            {
                if (Worker.CancellationPending)
                {
                    Log.WarnFormat("ApplyTemplate.DoWork was cancelled");
                    break;
                }
                    
                try
                {
                    DC.ApplyTemplateInfo(request, this);
                }
                catch (Exception e)
                {
                    Log.Error("Exception caught while applying templates", e);
                }
            }
        }
    }
}
