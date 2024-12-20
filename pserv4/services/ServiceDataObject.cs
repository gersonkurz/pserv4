﻿using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using log4net;
using System.Reflection;
using GSharpTools;
using System.Runtime.Versioning;

namespace pserv4.services
{
    [SupportedOSPlatform("windows")]
    public class ServiceDataObject : DataObject
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public string DisplayName { get; private set; }

        public string ServiceTypeString
        {
            get
            {
                return ServicesLocalisation.Localized(ServiceType);
            }
        }

        public override string ToString()
        {
            StringBuilder output = new StringBuilder();
            output.Append("ServiceDataObject{");
            output.Append(InternalID);
            output.Append(",StartType=");
            output.Append(StartTypeString);
            output.Append(",CurrentState=");
            output.Append(CurrentStateString);
            output.Append("}");
            return output.ToString();
        }

        public override string FileName
        {
            get
            {
                string result = PathSanitizer.GetExecutable(BinaryPathName, ensureQuotes: true);

                return result;
            }
        }


        private SC_SERVICE_TYPE _ServiceType;
        public SC_SERVICE_TYPE ServiceType
        {
            get
            {
                return _ServiceType;
            }
            private set
            {
                if (_ServiceType != value)
                {
                    _ServiceType = value;
                    NotifyPropertyChanged("ServiceTypeString");
                }
            }
        }
        
        public string StartTypeString
        {
            get
            {
                return ServicesLocalisation.Localized(StartType);
            }
        }

        private SC_START_TYPE _StartType;
        public SC_START_TYPE StartType
        {
            get
            {
                return _StartType;
            }
            private set
            {
                if (_StartType != value)
                {
                    _StartType = value;
                    NotifyPropertyChanged("StartTypeString");
                }
            }
        }

        public string BinaryPathName { get; private set; }
        public string LoadOrderGroup { get; private set; }
        public string ErrorControl { get; private set; }
        public string TagId { get; private set; }
        public string Description { get; private set; }
        public string User { get; private set; }

        public bool IsSystemAccount
        {
            get
            {
                return string.IsNullOrEmpty(User) ||
                        User.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase);
            }
        }

        public string ControlsAcceptedString
        {
            get
            {
                return ServicesLocalisation.Localized(ControlsAccepted);
            }
        }

        public string Win32ExitCode { get; private set; }
        public string ServiceSpecificExitCode { get; private set; }
        public string CheckPoint { get; private set; }
        public string WaitHint { get; private set; }
        public string ServiceFlags { get; private set; }
        public string PID { get; private set; }

        public string CurrentStateString
        { 
            get
            {
                return ServicesLocalisation.Localized(CurrentState);
            }
        }

        private SC_RUNTIME_STATUS _CurrentState;
        public SC_RUNTIME_STATUS CurrentState
        {
            get
            {
                return _CurrentState;
            }
            private set
            {
                if( _CurrentState != value )
                {
                    _CurrentState = value;
                    NotifyPropertyChanged("CurrentStateString");

                    bool isRunning = (_CurrentState == SC_RUNTIME_STATUS.SERVICE_RUNNING);
                    if( isRunning != IsRunning )
                    {
                        IsRunning = isRunning;
                        NotifyPropertyChanged("IsRunning");
                    }
                }
            }
        }

        private SC_CONTROLS_ACCEPTED _ControlsAccepted;
        public SC_CONTROLS_ACCEPTED ControlsAccepted
        {
            get
            {
                return _ControlsAccepted;
            }
            private set
            {
                if (_ControlsAccepted != value)
                {
                    _ControlsAccepted = value;
                    NotifyPropertyChanged("ControlsAcceptedString");
                }
            }
        }

        public void UpdateFrom(ENUM_SERVICE_STATUS_PROCESS essp)
        {
            CurrentState = essp.CurrentState;
            ControlsAccepted = essp.ControlsAccepted;
            SetNonZeroStringProperty("PID", essp.ProcessID);
        }


        public void ApplyChanges(
            NativeSCManager scm, 
            SC_START_TYPE startupType,
            string displayName,
            string binaryPathName,
            string description)
        {
            using (NativeService ns = new NativeService(scm,
                InternalID,
                ACCESS_MASK.SERVICE_CHANGE_CONFIG | ACCESS_MASK.SERVICE_QUERY_STATUS))
            {
                bool success = NativeServiceFunctions.ChangeServiceConfig(ns.Handle,
                    StartType: startupType,
                    DisplayName: displayName,
                    BinaryPathName: binaryPathName);
                if (success)
                {
                    StartType = startupType;
                    if( displayName != null )
                    {
                        SetStringProperty("DisplayName", displayName);
                    }
                    if( binaryPathName != null )
                    {
                        SetStringProperty("BinaryPathName", binaryPathName);
                        NotifyPropertyChanged("InstallLocation");
                    }

                    if ((description != null) && !description.Equals(Description))
                    {
                        ns.Description = description;
                        SetStringProperty("Description", description);
                    }     
                }
            }
        }

        public bool ApplyStartupChanges(NativeSCManager scm, SC_START_TYPE startupType)
        {
            bool success = true;
            if (startupType != StartType)
            {
                Log.InfoFormat("{0}: Change SC_START_TYPE from {1} to {2}", InternalID, StartType, startupType);
                using (NativeService ns = new NativeService(scm,
                    InternalID,
                    ACCESS_MASK.SERVICE_CHANGE_CONFIG | ACCESS_MASK.SERVICE_QUERY_STATUS))
                {
                    success = NativeServiceFunctions.ChangeServiceConfig(ns.Handle,
                        StartType: startupType);
                    if( success )
                    {
                        StartType = startupType;
                    }
                }
            }
            return success;
        }

        public bool Uninstall(NativeSCManager scm)
        {
            try
            {
                using (NativeService ns = new NativeService(scm,
                        InternalID,
                        ACCESS_MASK.SERVICE_CHANGE_CONFIG | ACCESS_MASK.SERVICE_QUERY_STATUS | ACCESS_MASK.DELETE))
                {
                    return NativeServiceFunctions.DeleteService(ns.Handle);
                }
            }
            catch(Exception e)
            {
                Log.Error("DeleteService", e);
                return false;
            }
        }

        public void UpdateFrom(SERVICE_STATUS_PROCESS ssp)
        {
            CurrentState = ssp.CurrentState;
            ControlsAccepted = ssp.ControlsAccepted;
            SetNonZeroStringProperty("PID", ssp.ProcessID);
        }

        public bool ShowRegistryEditor()
        {
            return ProcessInfoTools.ShowRegistryEditor(string.Format("HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\{0}",
                InternalID));
        }

        public string InstallLocation
        {
            get
            {
                return PathSanitizer.GetDirectory(BinaryPathName);
            }
        }

        public bool BringUpExplorerInInstallLocation()
        {
            return ProcessInfoTools.ShowExplorer(InstallLocation);
        }

        public bool BringUpTerminalInInstallLocation()
        {
            return ProcessInfoTools.ShowTerminal(InstallLocation);
        }

        public bool RemoveRegistryKey()
        {
            try
            {
                Log.InfoFormat("RemoveRegistryKey {0}", this);
                RegistryKey rootKey = Registry.LocalMachine;

                using (RegistryKey regkey = rootKey.OpenSubKey("SYSTEM\\CurrentControlSet\\Services", true))
                {
                    if (regkey.OpenSubKey(InternalID) != null)
                    {
                        regkey.DeleteSubKeyTree(InternalID);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Log.Error(string.Format("unable to remove registry key SYSTEM\\CurrentControlSet\\Services\\{0}", InternalID), e);
                return false;
            }
        }

        public ServiceDataObject(NativeService service, ENUM_SERVICE_STATUS_PROCESS essp)
            :   base(essp.ServiceName)
        {
            DisplayName = essp.DisplayName;
            CurrentState = essp.CurrentState;
            ControlsAccepted = essp.ControlsAccepted;

            Win32ExitCode = essp.Win32ExitCode.ToString();
            ServiceSpecificExitCode = essp.ServiceSpecificExitCode.ToString();
            CheckPoint = essp.CheckPoint.ToString();
            WaitHint = essp.WaitHint.ToString();
            ServiceFlags = essp.ServiceFlags.ToString();
            ServiceType = essp.ServiceType;
            if (essp.ProcessID != 0)
                PID = essp.ProcessID.ToString();
            else
                PID = "";
            QUERY_SERVICE_CONFIG config = service.ServiceConfig;

            if (essp.CurrentState == SC_RUNTIME_STATUS.SERVICE_RUNNING)
            {
                IsRunning = true;
            }

            if (config != null)
            {
                if (config.StartType == SC_START_TYPE.SERVICE_DISABLED)
                {
                    IsDisabled = true;
                }
                StartType = config.StartType;
                BinaryPathName = config.BinaryPathName;
                LoadOrderGroup = config.LoadOrderGroup;
                ErrorControl = ServicesLocalisation.Localized(config.ErrorControl);
                TagId = config.TagId.ToString();
                User = config.ServiceStartName;

            }
            ToolTip = Description = service.Description;
            ToolTipCaption = DisplayName;
            ConstructionIsFinished = true;
        }
    }

}
