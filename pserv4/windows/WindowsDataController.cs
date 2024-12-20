﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using pserv4.Properties;
using System.Windows;
using System.Runtime.Versioning;

namespace pserv4.windows
{
    [SupportedOSPlatform("windows")]

    public class WindowsDataController : DataController
    {
        public WindowsDataController()
            :   base(
                    "Windows", 
                    "Window",
                    Resources.WINDOWS_CTRL_START_Description,
                    Resources.WINDOWS_CTRL_STOP_Description,
                    Resources.WINDOWS_CTRL_RESTART_Description,
                    Resources.WINDOWS_CTRL_PAUSE_Description,
                    Resources.WINDOWS_CTRL_CONTINUE_Description)    
        {
            IsControlStartEnabled = true;
            IsControlStopEnabled = true;
            IsControlRestartEnabled = true;
            IsControlPauseEnabled = true; 
            IsControlContinueEnabled = true;
            HasFilenames = false;
        }

        public override IEnumerable<DataObjectColumn> Columns
        {
            get
            {
                if (ActualColumns == null)
                {
                    CreateColumns(
                        new DataObjectColumn(Resources.WINDOW_C_HWND, "InternalID"),
                        new DataObjectColumn(Resources.WINDOW_C_Title, "Title"),
                        new DataObjectColumn(Resources.WINDOW_C_Class, "Class"),
                        new DataObjectColumn(Resources.WINDOW_C_Size, "Size"),
                        new DataObjectColumn(Resources.WINDOW_C_Position, "Position"),
                        new DataObjectColumn(Resources.WINDOW_C_Style, "Style"),
                        new DataObjectColumn(Resources.WINDOW_C_ExStyle, "ExStyle"),
                        new DataObjectColumn(Resources.WINDOW_C_ID, "ID"),
                        new DataObjectColumn(Resources.WINDOW_C_ProcessID, "ProcessID"),
                        new DataObjectColumn(Resources.WINDOW_C_ThreadID, "ThreadID"),
                        new DataObjectColumn(Resources.WINDOW_C_Process, "Process"));
                }
                return ActualColumns;
            }
        }

        public override ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = base.ContextMenu;

                return AppendDefaultItems(menu);
            }
        }

        private void OnShowWindow(int state)
        {
            foreach (WindowDataObject wdo in MainListView.SelectedItems)
            {
                NativeWindowFunctions.ShowWindow(wdo.Handle, state);
                wdo.Refresh(wdo.Handle);
            }
        }
        
        public override void OnControlStart(object sender, RoutedEventArgs e)
        {
            OnShowWindow(NativeWindowFunctions.SW_SHOW);
        }

        public override void OnControlStop(object sender, RoutedEventArgs e)
        {
            OnShowWindow(NativeWindowFunctions.SW_HIDE);
        }

        public override void OnControlRestart(object sender, RoutedEventArgs e)
        {
            foreach (WindowDataObject wdo in MainListView.SelectedItems)
            {
                NativeWindowFunctions.BringWindowToTop(wdo.Handle);
                wdo.Refresh(wdo.Handle);
            }
        }

        public override void OnControlPause(object sender, RoutedEventArgs e)
        {
            OnShowWindow(NativeWindowFunctions.SW_MINIMIZE);
        }

        public override void OnControlContinue(object sender, RoutedEventArgs e)
        {
            OnShowWindow(NativeWindowFunctions.SW_MAXIMIZE);
        }

        public override void Refresh(ObservableCollection<DataObject> objects)
        {
            using (var manager = new RefreshManager<WindowDataObject>(objects))
            {
                foreach (int hwnd in NativeWindowFunctions.EnumWindows())
                {
                    string internalID = hwnd.ToString("X4");
                    WindowDataObject wdo;

                    if (manager.Contains(internalID, out wdo))
                    {
                        if( !wdo.Refresh(hwnd) )
                        {
                            objects.Remove(wdo);
                        }
                    }
                    else
                    {
                        wdo = new WindowDataObject(hwnd);
                        if (!string.IsNullOrEmpty(wdo.Title))
                        {
                            objects.Add(wdo);
                        }
                    }
                }
            }
        }
    }
}
