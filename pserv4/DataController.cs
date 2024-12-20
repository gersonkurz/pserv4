﻿using System;
using System.Collections;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Xml;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using pserv4.Properties;
using log4net;
using System.Reflection;
using GSharpTools;
using GSharpTools.WPF;
using System.Runtime.Versioning;

namespace pserv4
{
    [SupportedOSPlatform("windows")]

    public abstract class DataController
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public abstract IEnumerable<DataObjectColumn> Columns { get; }
        protected readonly string ControllerName;
        protected readonly string ItemName;
        protected bool HasProperties;
        protected List<DataObjectColumn> ActualColumns;

        public bool IsControlStartEnabled { get; protected set; }
        public bool IsControlStopEnabled { get; protected set; }
        public bool IsControlRestartEnabled { get; protected set; }
        public bool IsControlPauseEnabled { get; protected set; }
        public bool IsControlContinueEnabled { get; protected set; }

        public readonly string ControlStartDescription;
        public readonly string ControlStopDescription;
        public readonly string ControlRestartDescription;
        public readonly string ControlPauseDescription;
        public readonly string ControlContinueDescription;

        protected bool HasFilenames;

        public string Caption {get; protected set; }

        public DataController(
            string controllerName, 
            string itemName,
            string controlStartDescription,
            string controlStopDescription,
            string controlRestartDescription,
            string controlPauseDescription,
            string controlContinueDescription
            )
        {
            ItemName = itemName;
            Caption = controllerName;
            HasProperties = false;
            HasFilenames = true;
            ControllerName = controllerName;
            ControlStartDescription = controlStartDescription;
            ControlStopDescription = controlStopDescription;
            ControlRestartDescription = controlRestartDescription;
            ControlPauseDescription = controlPauseDescription;
            ControlContinueDescription = controlContinueDescription;
        }

        public ListView MainListView;

        /// <summary>
        /// Given the input list, refresh it with the current object list 
        /// </summary>
        /// <param name="objects"></param>
        public abstract void Refresh(ObservableCollection<DataObject> objects);

        public void SaveColumnSortOrder(GridViewColumnCollection columns)
        {
            StringBuilder content = new StringBuilder();
            bool first = true;

            foreach(var column in columns)
            {
                var binding = column.DisplayMemberBinding as System.Windows.Data.Binding;
                if( binding != null )
                {
                    if (first)
                        first = false;
                    else
                        content.Append(",");
                    content.Append(binding.Path.Path);
                }
            }
            string propertyName = string.Format("ColumnOrder{0}", ControllerName);

            Type t = App.Settings.GetType();
            PropertyInfo pi = t.GetProperty(propertyName);
            pi.SetValue(App.Settings, content.ToString(), null);
        }

        protected void CreateColumns(params DataObjectColumn[] args)
        {
            ActualColumns = new List<DataObjectColumn>();

            // step 1: create lookup dictionary
            var lookup = new Dictionary<string, DataObjectColumn>();
            foreach (DataObjectColumn c in args)
            {
                lookup[c.BindingName] = c;
            }

            string propertyName = string.Format("ColumnOrder{0}", ControllerName);
            Type t = App.Settings.GetType();
            PropertyInfo pi = t.GetProperty(propertyName);
            string expectedSortOrder = pi.GetValue(App.Settings, null) as string;
            if (!string.IsNullOrEmpty(expectedSortOrder))
            {
                // apply sort order
                string[] sortOrder = expectedSortOrder.Split(',');

                foreach(string sortKey in sortOrder)
                {
                    if( lookup.ContainsKey(sortKey) )
                    {
                        if (lookup[sortKey] != null )
                        {
                            ActualColumns.Add(lookup[sortKey]);
                            lookup[sortKey] = null;
                        }
                    }
                }
            }

            // finally, add all items not in the list to ActualColumns
            foreach (DataObjectColumn c in args)
            {
                if( lookup[c.BindingName] != null )
                {
                    // this item wasn't added to the list just yet, so do it now
                    ActualColumns.Add(c);
                }
            }
        }

        

        protected void SetMenuItemEnabled(ContextMenu menu, int index, bool enabled)
        {
            MenuItem mi = menu.Items[index] as MenuItem;
            if (mi != null)
            {
                mi.IsEnabled = enabled;
            }
        }

        protected MenuItem AppendMenuItem(ContextMenu menu, string header, BitmapImage[] images, bool enabled, RoutedEventHandler callback)
        {
            MenuItem mi = new MenuItem();
            mi.Header = header;
            if (images != null)
            {
                Image i = new Image();
                i.Source = enabled ? images[1] : images[0];
                mi.Icon = i;
            }
            mi.IsEnabled = enabled;
            mi.Click += callback;
            menu.Items.Add(mi);
            return mi;
        }

        public virtual void ApplyChanges(List<IDataObjectDetails> changedItems)
        {
            // default implementation: do nothing
        }

        protected MenuItem AppendMenuItem(ContextMenu menu, string header, string imageName, RoutedEventHandler callback)
        {
            MenuItem mi = new MenuItem();
            mi.Header = header;
            Image i = new Image();
            string filename = string.Format(@"pack://application:,,,/images/{0}.png", imageName);
            i.Source = new BitmapImage(new Uri(filename));
            mi.Icon = i;
            mi.Click += callback;
            menu.Items.Add(mi);
            return mi;
        }

        protected ContextMenu AppendDefaultItems(ContextMenu menu)
        {
            menu.Items.Add(new Separator());
            AppendMenuItem(menu, Resources.IDS_SAVE_AS_XML, "database_save", MainWindow.Instance.SaveAsXML);
            AppendMenuItem(menu, Resources.IDS_COPY_TO_CLIPBOARD, "database_lightning", MainWindow.Instance.CopyToClipboard);
            menu.Items.Add(new Separator());
            if (HasFilenames)
            {
                AppendMenuItem(menu, Resources.IDS_SYSTEM_PROPERTIES, "database_table", ShowSystemProperties);
                menu.Items.Add(new Separator());
            }
            AppendMenuItem(menu, Resources.IDS_PROPERTIES, "database_gear", ShowProperties);
            return menu;
        }


        /// <summary>
        /// Return the context menu used for all items on this list
        /// </summary>
        public virtual ContextMenu ContextMenu
        {
            get
            {
                if (string.IsNullOrEmpty(ControlStartDescription))
                    return null;

                ContextMenu menu = new ContextMenu();
                AppendMenuItem(
                    menu,
                    ControlStartDescription,
                    MainWindow.BIStart,
                    IsControlStartEnabled,
                    OnControlStart);
                AppendMenuItem(
                    menu,
                    ControlStopDescription,
                    MainWindow.BIStop,
                    IsControlStopEnabled,
                    OnControlStop);
                AppendMenuItem(
                    menu,
                    ControlRestartDescription,
                    MainWindow.BIRestart,
                    IsControlRestartEnabled,
                    OnControlRestart);
                AppendMenuItem(
                    menu,
                    ControlPauseDescription,
                    MainWindow.BIPause,
                    IsControlPauseEnabled,
                    OnControlPause);
                AppendMenuItem(
                    menu,
                    ControlContinueDescription,
                    MainWindow.BIContinue,
                    IsControlContinueEnabled,
                    OnControlContinue);

                return menu;
            }
        }

        public virtual void OnSelectionChanged(IList selectedItems)
        {
        }

        public virtual void OnContextMenuOpening(IList selectedItems, ContextMenu menu)
        {
        }

        public bool LoadFromXml(string filename)
        {
            List<ActionTemplateInfo> result = new List<ActionTemplateInfo>();
            try
            {
                ActionTemplateInfo recording = null;
                string lastKnownKey = null;
                using (XmlReader reader = XmlReader.Create(filename))
                {
                    while (reader.Read())
                    {
                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                if (reader.Name == ControllerName)
                                {
                                    // ok expected
                                }
                                else if (reader.Name == ItemName)
                                {
                                    recording = new ActionTemplateInfo(reader.GetAttribute("id"));
                                    result.Add(recording);
                                }
                                else if( recording != null )
                                {
                                    lastKnownKey = reader.Name;
                                }
                                else
                                {
                                    Log.WarnFormat("Warning: didn't expect element type {0}", reader.Name);
                                }
                                break;
                            case XmlNodeType.Text:
                                if (lastKnownKey != null)
                                {
                                    if (recording != null)
                                    {
                                        recording[lastKnownKey] = reader.Value;
                                    }
                                    else
                                    {
                                        Log.WarnFormat("Warning: found key '{0}', but recording is null", lastKnownKey);
                                    }
                                }
                                else
                                {
                                    Log.WarnFormat("Warning: didn't expect text '{0}'", reader.Value);
                                }
                                break;
                            case XmlNodeType.EndElement:
                                if ((lastKnownKey != null) && (reader.Name == lastKnownKey))
                                {
                                    lastKnownKey = null;
                                }
                                else if ((recording != null) && (reader.Name == ItemName))
                                {
                                    recording = null;
                                }
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(string.Format("Exception caught loading '{0}'", filename), e);
                result = null;
            }

            if (result == null)
                return false;

            new LongRunningFunctionWindow(new ApplyTemplate(this, result), Resources.IDS_LOADING_TEMPLATES).ShowDialog();
            return true;

        }

        public bool SaveAsXml(string filename, IList items)
        {
            using (new WaitCursor())
            {
                try
                {

                    bool result = true;
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    settings.Encoding = Encoding.UTF8;
                    settings.IndentChars = "\t";
                    settings.NewLineChars = "\r\n";

                    StringBuilder buffer = new StringBuilder();
                    using (XmlWriter xtw = (filename == null) ? XmlWriter.Create(buffer, settings) : XmlWriter.Create(filename, settings))
                    {
                        xtw.WriteStartDocument();
                        xtw.WriteStartElement(ControllerName);
                        xtw.WriteAttributeString("version", "3.0");
                        xtw.WriteAttributeString("count", items.Count.ToString());
                        result = OnSaveAsXML(xtw, items);
                        xtw.WriteEndElement();
                        xtw.Close();
                    }
                    if (filename == null)
                    {
                        Clipboard.SetText(buffer.ToString());
                    }
                    return result;
                }
                catch (Exception e)
                {
                    Log.Error(string.Format("Exception caught while saving '{0}'", filename), e);
                    return false;
                }
            }
        }

        protected bool OnSaveAsXML(XmlWriter xtw, System.Collections.IList items)
        {
            foreach(DataObject o in items)
            {
                xtw.WriteStartElement(ItemName);
                xtw.WriteAttributeString("id", o.InternalID);

                Type t = o.GetType();

                foreach (DataObjectColumn c in Columns)
                {
                    if( !c.BindingName.Equals("InternalID"))
                    {
                        try
                        {
                            object item = t.GetProperty(c.BindingName).GetValue(o, null);
                            if (item != null)
                            {
                                string value = item as string;
                                if (value == null)
                                    value = item.ToString();

                                xtw.WriteElementString(c.BindingName, value);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(string.Format("Exception caught while trying to access property {0}", c.BindingName), e);
                        }
                    }
                }
                xtw.WriteEndElement();
            }
            return true;
        }

        public void ShowProperties(object sender, RoutedEventArgs e)
        {
            if(HasProperties && (MainListView.SelectedItems.Count > 0))
            {
                new PropertiesWindow().Show();
            }
        }

        public void ShowSystemProperties(object sender, RoutedEventArgs e)
        {
            if(HasFilenames)
            {
                foreach (DataObject o in MainListView.SelectedItems)
                {
                    ProcessInfoTools.ShowFileProperties(o.FileName);
                }
            }
        }

        public virtual void ApplyTemplateInfo(ActionTemplateInfo ati, BackgroundAction action)
        {
            // default implementation: do nothing
        }

        public virtual UserControl CreateDetailsPage(DataObject o)
        {
            return null;
        }

        public virtual void OnControlStart(object sender, RoutedEventArgs e)
        {
            Log.WarnFormat("Warning: OnControlStart not implemented for {0}", this);
        }

        public virtual void OnControlStop(object sender, RoutedEventArgs e)
        {
            Log.WarnFormat("Warning: OnControlStop not implemented for {0}", this);
        }

        public virtual void OnControlRestart(object sender, RoutedEventArgs e)
        {
            Log.WarnFormat("Warning: OnControlRestart not implemented for {0}", this);
        }

        public virtual void OnControlPause(object sender, RoutedEventArgs e)
        {
            Log.WarnFormat("Warning: OnControlPause not implemented for {0}", this);
        }

        public virtual void OnControlContinue(object sender, RoutedEventArgs e)
        {
            Log.WarnFormat("Warning: OnControlContinue not implemented for {0}", this);
        }


    }
}

