﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using log4net;
using System.Reflection;
using System.Runtime.Versioning;

namespace pserv4.services
{
    [SupportedOSPlatform("windows")]
    public class PerformServiceStateRequest : BackgroundAction
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly ServiceStateRequest SSR;
        public readonly List<ServiceDataObject> Services = new List<ServiceDataObject>();

        public PerformServiceStateRequest(ServiceStateRequest ssr)
        {
            SSR = ssr;
        }

        public override void DoWork()
        {
            ACCESS_MASK ServiceAccessMask = SSR.GetServiceAccessMask() | ACCESS_MASK.STANDARD_RIGHTS_READ | ACCESS_MASK.SERVICE_QUERY_STATUS;

            ServicesDataController sdc = MainWindow.CurrentController as ServicesDataController;

            using (NativeSCManager scm = new NativeSCManager(sdc.MachineName))
            {
                int serviceIndex = 0;
                foreach (ServiceDataObject so in Services)
                {
                    ++serviceIndex;

                    try
                    {
                        SetOutputText(string.Format("Service {0}/{1}: {2} is initially in state {3}",
                                        serviceIndex,
                                        Services.Count,
                                        so.DisplayName,
                                        ServicesLocalisation.Localized(so.CurrentState)));

                        if( so.CurrentState == SC_RUNTIME_STATUS.SERVICE_STOPPED )
                        {
                            ServiceAccessMask &= ~(ACCESS_MASK.SERVICE_STOP);
                        }

                        using (NativeService ns = new NativeService(scm, so.InternalID, ServiceAccessMask))
                        {
                            bool requestedStatusChange = false;

                            Log.InfoFormat("BEGIN backgroundWorker1_Process for {0}", ns.Description);
                            using (ServiceStatus ss = new ServiceStatus(ns))
                            {
                                for (int i = 0; i < 100; ++i)
                                {
                                    if (Worker.CancellationPending)
                                        break;

                                    if (!ss.Refresh())
                                        break;

                                    SetOutputText(string.Format("Service {0}/{1}: {2} is now in state {3}",
                                        serviceIndex,
                                        Services.Count,
                                        so.DisplayName,
                                        ServicesLocalisation.Localized(ss.Status.CurrentState)));
                                    
                                    if (SSR.HasSuccess(ss.Status.CurrentState))
                                    {
                                        Log.Info("Reached target status, done...");
                                        break; // TODO: reached 100% of this service' status reqs. 
                                    }

                                    // if we haven't asked the service to change its status yet, do so now. 
                                    if (!requestedStatusChange)
                                    {
                                        requestedStatusChange = true;
                                        Log.InfoFormat("Ask {0} to issue its status request on {1}", SSR, ss);
                                        if (!SSR.Request(ss))
                                            break;
                                    }
                                    else if (SSR.HasFailed(ss.Status.CurrentState))
                                    {
                                        Log.Error("ERROR, target state is one of the failed ones :(");
                                        break;
                                    }
                                    Thread.Sleep(500);
                                }
                                so.UpdateFrom(ss.Status);
                                Log.Info("END backgroundWorker1_Process");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception caught in PerformServiceStateRequest", ex);
                    }
                    if (Worker.CancellationPending)
                        break;
                }
            }
        }
    }
}
