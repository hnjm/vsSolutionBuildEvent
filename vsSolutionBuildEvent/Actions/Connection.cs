﻿/*
 * Copyright (c) 2013-2015  Denis Kuzmin (reg) <entry.reg@gmail.com>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using net.r_eg.vsSBE.Bridge;
using net.r_eg.vsSBE.Events;
using net.r_eg.vsSBE.Logger;
using net.r_eg.vsSBE.SBEScripts.Components;

namespace net.r_eg.vsSBE.Actions
{
    /// <summary>
    /// Binder / Coordinator of main routes.
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// Flag of permission for any actions.
        /// </summary>
        protected bool IsAllowActions
        {
            get { return !Settings._.IgnoreActions; }
        }

        /// <summary>
        /// Access to available events.
        /// </summary>
        protected ISolutionEvents SlnEvents
        {
            get { return Settings.Cfg; }
        }

        /// <summary>
        /// To support the 'execution order' features.
        /// Contains the current states of all projects.
        /// </summary>
        protected Dictionary<string, ExecutionOrderType> projects;

        /// <summary>
        /// Contains current incoming project.
        /// </summary>
        protected IExecutionOrder current = new ExecutionOrder();

        /// <summary>
        /// Used handler.
        /// </summary>
        protected ICommand cmd;

        /// <summary>
        /// object synch.
        /// </summary>
        private Object _lock = new Object();


        /// <summary>
        /// Binding 'PRE' of Solution
        /// </summary>
        public int bindPre(ref int pfCancelUpdate)
        {
            if(isDisabledAll(SlnEvents.PreBuild)) {
                return Codes.Success;
            }

            if(!IsAllowActions) {
                return _ignoredAction(SolutionEventType.Pre);
            }

            foreach(SBEEvent item in SlnEvents.PreBuild)
            {
                if(hasExecutionOrder(item)) {
                    Log.Info("[Pre] SBE has deferred action: '{0}' :: waiting... ", item.Caption);
                    Status._.add(SolutionEventType.Pre, StatusType.Deferred);
                }
                else {
                    Status._.add(SolutionEventType.Pre, (execPre(item) == Codes.Success)? StatusType.Success : StatusType.Fail);
                }
            }
            return Status._.contains(SolutionEventType.Pre, StatusType.Fail)? Codes.Failed : Codes.Success;
        }

        /// <summary>
        /// Binding 'POST' of Solution
        /// </summary>
        public int bindPost(int fSucceeded, int fModified, int fCancelCommand)
        {
            SBEEvent[] evt = SlnEvents.PostBuild;

            if(isDisabledAll(evt)) {
                return Codes.Success;
            }

            if(!IsAllowActions) {
                return _ignoredAction(SolutionEventType.Post);
            }

            foreach(SBEEvent item in evt)
            {
                if(fSucceeded != 1 && item.IgnoreIfBuildFailed) {
                    Log.Info("[Post] ignored action '{0}' :: Build FAILED. See option 'Ignore if the build failed'", item.Caption);
                    continue;
                }

                if(!isReached(item)) {
                    Log.Info("[Post] ignored action '{0}' ::  not reached selected projects in execution order", item.Caption);
                    continue;
                }

                try {
                    if(cmd.exec(item, SolutionEventType.Post)) {
                        Log.Info("[Post] finished SBE: {0}", item.Caption);
                    }
                    Status._.add(SolutionEventType.Post, StatusType.Success);
                }
                catch(Exception ex) {
                    Log.Error("Post-Build error: {0}", ex.Message);
                    Status._.add(SolutionEventType.Post, StatusType.Fail);
                }
            }
            return Status._.contains(SolutionEventType.Post, StatusType.Fail)? Codes.Failed : Codes.Success;
        }

        /// <summary>
        /// Binding 'PRE' of Projects
        /// </summary>
        public int bindProjectPre(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, ref int pfCancel)
        {
            onProject(pHierProj, ExecutionOrderType.Before);
            return Codes.Success;
        }

        /// <summary>
        /// Binding 'PRE' of Projects
        /// </summary>
        public int bindProjectPre(string project)
        {
            onProject(project, ExecutionOrderType.Before);
            return Codes.Success;
        }

        /// <summary>
        /// Binding 'POST' of Projects
        /// </summary>
        public int bindProjectPost(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, int fSuccess, int fCancel)
        {
            onProject(pHierProj, ExecutionOrderType.After, fSuccess == 1 ? true : false);
            return Codes.Success;
        }

        /// <summary>
        /// Binding 'POST' of Projects
        /// </summary>
        public int bindProjectPost(string project, int fSuccess)
        {
            onProject(project, ExecutionOrderType.After, fSuccess == 1 ? true : false);
            return Codes.Success;
        }

        /// <summary>
        /// Binding 'Cancel/Abort' of Solution
        /// </summary>
        public int bindCancel()
        {
            SBEEvent[] evt = SlnEvents.CancelBuild;

            if(isDisabledAll(evt)) {
                return Codes.Success;
            }

            if(!IsAllowActions) {
                return _ignoredAction(SolutionEventType.Cancel);
            }

            foreach(SBEEvent item in evt)
            {
                if(!isReached(item)) {
                    Log.Info("[Cancel] ignored action '{0}' :: not reached selected projects in execution order", item.Caption);
                    continue;
                }

                try {
                    if(cmd.exec(item, SolutionEventType.Cancel)) {
                        Log.Info("[Cancel] finished SBE: {0}", item.Caption);
                    }
                    Status._.add(SolutionEventType.Cancel, StatusType.Success);
                }
                catch(Exception ex) {
                    Log.Error("Cancel-Build error: {0}", ex.Message);
                    Status._.add(SolutionEventType.Cancel, StatusType.Fail);
                }
            }
            return Status._.contains(SolutionEventType.Cancel, StatusType.Fail)? Codes.Failed : Codes.Success;
        }

        /// <summary>
        /// Full process of building.
        /// </summary>
        /// <param name="data">Raw data</param>
        public void bindBuildRaw(string data)
        {
            Receiver.Output.Item._.Build.updateRaw(data); //TODO:
            if(!IsAllowActions)
            {
                if(!isDisabledAll(SlnEvents.Transmitter)) {
                    _ignoredAction(SolutionEventType.Transmitter);
                }
                else if(!isDisabledAll(SlnEvents.WarningsBuild)) {
                    _ignoredAction(SolutionEventType.Warnings);
                }
                else if(!isDisabledAll(SlnEvents.ErrorsBuild)) {
                    _ignoredAction(SolutionEventType.Errors);
                }
                else if(!isDisabledAll(SlnEvents.OWPBuild)) {
                    _ignoredAction(SolutionEventType.OWP);
                }
                return;
            }

            //TODO: IStatus

            foreach(SBETransmitter evt in SlnEvents.Transmitter)
            {
                if(!isExecute(evt, current)) {
                    Log.Info("[Transmitter] ignored action '{0}' :: by execution order", evt.Caption);
                }
                else {
                    try {
                        if(cmd.exec(evt, SolutionEventType.Transmitter)) {
                            //Log.Trace("[Transmitter]: " + SlnEvents.transmitter.caption);
                        }
                    }
                    catch(Exception ex) {
                        Log.Error("Transmitter error: {0}", ex.Message);
                    }
                }
            }

            //TODO: ExecStateType

            foreach(SBEEventEW evt in SlnEvents.WarningsBuild) {
                if(evt.Enabled) {
                    sbeEW(evt, Receiver.Output.BuildItem.Type.Warnings);
                }
            }

            foreach(SBEEventEW evt in SlnEvents.ErrorsBuild) {
                if(evt.Enabled) {
                    sbeEW(evt, Receiver.Output.BuildItem.Type.Errors);
                }
            }

            foreach(SBEEventOWP evt in SlnEvents.OWPBuild) {
                if(evt.Enabled) {
                    sbeOutput(evt, ref data);
                }
            }
        }

        /// <summary>
        /// Binding of the execution Command ID for EnvDTE /Before.
        /// </summary>
        /// <param name="guid">The GUID.</param>
        /// <param name="id">The command ID.</param>
        /// <param name="customIn">Custom input parameters.</param>
        /// <param name="customOut">Custom output parameters.</param>
        /// <param name="cancelDefault">Whether the command has been cancelled.</param>
        /// <returns>If the method succeeds, it returns Codes.Success. If it fails, it returns an error code.</returns>
        public int bindCommandDtePre(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
        {
            return commandEvent(true, guid, id, customIn, customOut, ref cancelDefault);
        }

        /// <summary>
        /// Binding of the execution Command ID for EnvDTE /After.
        /// </summary>
        /// <param name="guid">The GUID.</param>
        /// <param name="id">The command ID.</param>
        /// <param name="customIn">Custom input parameters.</param>
        /// <param name="customOut">Custom output parameters.</param>
        /// <returns>If the method succeeds, it returns Codes.Success. If it fails, it returns an error code.</returns>
        public int bindCommandDtePost(string guid, int id, object customIn, object customOut)
        {
            bool cancelDefault = false;
            return commandEvent(false, guid, id, customIn, customOut, ref cancelDefault);
        }

        /// <summary>
        /// Resetting all progress of handling events
        /// </summary>
        /// <returns>true value if successful resetted</returns>
        public bool reset()
        {
            if(!IsAllowActions) {
                return false;
            }
            projects.Clear();
            current = new ExecutionOrder();
            Status._.flush();
            return true;
        }

        public Connection(ICommand cmd)
        {
            this.cmd = cmd;
            projects = new Dictionary<string, ExecutionOrderType>();
            attachLoggingEvent();
        }


        /// <summary>
        /// Entry point to Errors/Warnings
        /// </summary>
        protected int sbeEW(ISolutionEventEW evt, Receiver.Output.BuildItem.Type type)
        {
            Receiver.Output.BuildItem info = Receiver.Output.Item._.Build;

            // TODO: capture code####, message..
            if(!info.checkRule(type, evt.IsWhitelist, evt.Codes)) {
                return Codes.Success;
            }

            if(!isExecute(evt, current)) {
                Log.Info("[{0}] ignored action '{1}' :: by execution order", type, evt.Caption);
                return Codes.Success;
            }

            try {
                if(cmd.exec(evt, (type == Receiver.Output.BuildItem.Type.Warnings)? SolutionEventType.Warnings : SolutionEventType.Errors)) {
                    Log.Info("[{0}] finished SBE: {1}", type, evt.Caption);
                }
                return Codes.Success;
            }
            catch(Exception ex) {
                Log.Error("SBE '{0}' error: {1}", type, ex.Message);
            }
            return Codes.Failed;
        }

        /// <summary>
        /// Entry point to the OWP
        /// </summary>
        protected int sbeOutput(ISolutionEventOWP evt, ref string raw)
        {
            if(!(new Receiver.Output.Matcher()).match(evt.Match, raw)) {
                return Codes.Success;
            }

            if(!isExecute(evt, current)) {
                Log.Info("[Output] ignored action '{0}' :: by execution order", evt.Caption);
                return Codes.Success;
            }

            try {
                if(cmd.exec(evt, SolutionEventType.OWP)) {
                    Log.Info("[Output] finished SBE: {0}", evt.Caption);
                }
                return Codes.Success;
            }
            catch(Exception ex) {
                Log.Error("SBE 'Output' error: {0}", ex.Message);
            }
            return Codes.Failed;
        }

        /// <param name="pre">Flag of Before/After execution.</param>
        /// <param name="guid">The GUID.</param>
        /// <param name="id">The command ID.</param>
        /// <param name="customIn">Custom input parameters.</param>
        /// <param name="customOut">Custom output parameters.</param>
        /// <param name="cancelDefault">Whether the command has been cancelled.</param>
        /// <returns>If the method succeeds, it returns Codes.Success. If it fails, it returns an error code.</returns>
        protected int commandEvent(bool pre, string guid, int id, object customIn, object customOut, ref bool cancelDefault)
        {
            if(SlnEvents == null) { // activation of this event type can be before opening solution
                return Codes.Failed;
            }
            CommandEvent[] evt = SlnEvents.CommandEvent;

            if(isDisabledAll(evt)) {
                return Codes.Success;
            }

            if(!IsAllowActions) {
                return _ignoredAction(SolutionEventType.CommandEvent);
            }

            foreach(CommandEvent item in evt)
            {
                if(item.Filters == null) {
                    // well, should be some protection for user if we will listen all events... otherwise we can lose control
                    continue;
                }

                var Is = item.Filters.Where(f => 
                            (
                              ((pre && f.Pre == true) || (!pre && f.Post == true))
                                && 
                                (
                                  (f.Id == id && f.Guid == guid) 
                                   && 
                                   (
                                     (
                                       (f.CustomIn != null && f.CustomIn == customIn)
                                       || (f.CustomIn == null && customIn == null)
                                     )
                                     &&
                                     (
                                       (f.CustomOut != null && f.CustomOut == customOut)
                                       || (f.CustomOut == null && customOut == null)
                                     )
                                   )
                                )
                            )).Select(f => f.Cancel);

                if(Is.Count() < 1) {
                    continue;
                }

                Log.Trace("[CommandEvent] catched: '{0}', '{1}', '{2}', '{3}', '{4}' /'{5}'",
                                                        guid, id, customIn, customOut, cancelDefault, pre);

                commandEvent(item);

                if(pre && Is.Any(f => f)) {
                    cancelDefault = true;
                    Log.Info("[CommandEvent] original command has been canceled for action: '{0}'", item.Caption);
                }
            }
            return Status._.contains(SolutionEventType.CommandEvent, StatusType.Fail)? Codes.Failed : Codes.Success;
        }

        protected void commandEvent(CommandEvent item)
        {
            try
            {
                if(cmd.exec(item, SolutionEventType.CommandEvent)) {
                    Log.Info("[CommandEvent] finished: '{0}'", item.Caption);
                }
                Status._.add(SolutionEventType.CommandEvent, StatusType.Success);
            }
            catch(Exception ex) {
                Log.Error("CommandEvent error: '{0}'", ex.Message);
            }
            Status._.add(SolutionEventType.CommandEvent, StatusType.Fail);
        }

        /// <summary>
        /// Immediately execute 'PRE' of Solution
        /// </summary>
        protected int execPre(SBEEvent evt)
        {
            try {
                if(cmd.exec(evt, SolutionEventType.Pre)) {
                    Log.Info("[Pre] finished SBE: {0}", evt.Caption);
                }
                return Codes.Success;
            }
            catch(Exception ex) {
                Log.Error("Pre-Build error: {0}", ex.Message);
            }
            return Codes.Failed;
        }

        protected int execPre()
        {
            int idx = 0;
            foreach(SBEEvent item in SlnEvents.PreBuild) {
                if(hasExecutionOrder(item)) {
                    Status._.update(SolutionEventType.Pre, idx, (execPre(item) == Codes.Success)? StatusType.Success : StatusType.Fail);
                }
                ++idx;
            }
            return Status._.contains(SolutionEventType.Pre, StatusType.Fail)? Codes.Failed : Codes.Success;
        }

        protected string getProjectName(IVsHierarchy pHierProj)
        {
            string projectName = ((IEnvironmentExt)cmd.Env).getProjectNameFrom(pHierProj, true);
            if(!String.IsNullOrEmpty(projectName)) {
                return projectName;
            }

            object name;
            // http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.shell.interop.ivshierarchy.getproperty.aspx
            // http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.shell.interop.__vshpropid.aspx
            pHierProj.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_Name, out name);

            return name.ToString();
        }

        /// <summary>
        /// Checking state from incoming projects
        /// In general, this checking for single the event-action like a POST/Cacnel
        /// note: 
        ///   * The 'POST' - exists as successfully completed event, therefore we should getting only the 'After' state.
        ///   * The 'Cancel' - works differently and contains realy the reached state: 'Before' or 'After'.
        /// </summary>
        protected bool isReached(ISolutionEvent evt)
        {
            if(!hasExecutionOrder(evt)) {
                return true;
            }
            Log.Debug("hasExecutionOrder(->isReached) for '{0}' is true", evt.Caption);

            return evt.ExecutionOrder.Any(e =>
                                            projects.ContainsKey(e.Project)
                                               && projects[e.Project] == e.Order
                                          );
        }

        /// <summary>
        /// Checking state with current range of order.
        /// 
        /// In general, this checking for multiple the event-action like a EW/OWP:
        ///   * Before1 -> After1|Cancel
        ///   * After1  -> POST/Cancel
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="incoming">Current incoming project</param>
        /// <returns>true value if it's allowed to execute for current state</returns>
        protected bool isExecute(ISolutionEvent evt, IExecutionOrder incoming)
        {
            if(!hasExecutionOrder(evt)) {
                return true;
            }
            Log.Debug("hasExecutionOrder(->isExecute) for '{0}' is true", evt.Caption);

            foreach(IExecutionOrder eo in evt.ExecutionOrder)
            {
                if(!projects.ContainsKey(eo.Project)) {
                    continue;
                }

                // The 'Before' base:
                if(eo.Order == ExecutionOrderType.Before)
                {
                    // Before1 -> After1
                    return (projects[eo.Project] == ExecutionOrderType.Before);
                }

                // The 'After' base:
                if(projects[eo.Project] != ExecutionOrderType.After) {
                    return false; // waiting for 'After' state..
                }

                if(incoming.Project != eo.Project) {
                    return true; // After1  -> POST/Cancel
                }
                return (incoming.Order == ExecutionOrderType.After); // 'After' is reached ?
            }
            return false;
        }

        protected void onProject(IVsHierarchy pHierProj, ExecutionOrderType type, bool fSuccess = true)
        {
            onProject(getProjectName(pHierProj), type, fSuccess);
        }

        protected void onProject(string project, ExecutionOrderType type, bool fSuccess = true)
        {
            projects[project]   = type;
            current.Project     = project;
            current.Order       = type;

            Log.Trace("onProject: '{0}':{1} == {2}", project, type, fSuccess);

            if(Status._.contains(SolutionEventType.Pre, StatusType.Deferred)) {
                monitoringPre(project, type, fSuccess);
            }
        }

        /// <summary>
        /// Monitoring for deferred PRE-actions - "it's time or not"
        /// </summary>
        /// <param name="project">incoming project name</param>
        /// <param name="type">type of execution order</param>
        /// <param name="fSuccess">Flag indicating success</param>
        protected void monitoringPre(string project, ExecutionOrderType type, bool fSuccess)
        {
            SBEEvent[] evt = SlnEvents.PreBuild;
            for(int i = 0; i < evt.Length; ++i)
            {
                if(!evt[i].Enabled || Status._.get(SolutionEventType.Pre, i) != StatusType.Deferred) {
                    continue;
                }

                if(!IsAllowActions) {
                    _ignoredAction(SolutionEventType.DeferredPre);
                    return;
                }

                if(!fSuccess && evt[i].IgnoreIfBuildFailed) {
                    Log.Info("[PRE] ignored action '{0}' :: Build FAILED. See option 'Ignore if the build failed'", evt[i].Caption);
                    continue;
                }

                if(!hasExecutionOrder(evt[i])) {
                    Log.Trace("[PRE] deferred: executionOrder is null or not contains elements :: {0}", evt[i].Caption);
                    return;
                }

                if(evt[i].ExecutionOrder.Any(o => o.Project == project && o.Order == type)) {
                    Log.Info("Incoming '{0}'({1}) :: Execute the deferred action: '{2}'", project, type, evt[i].Caption);
                    Status._.update(SolutionEventType.Pre, i, (execPre(evt[i]) == Codes.Success)? StatusType.Success : StatusType.Fail);
                }
            }
        }

        /// <param name="evt">Array of handling events</param>
        /// <returns>true value if all event are disabled for present array</returns>
        protected bool isDisabledAll(ISolutionEvent[] evt)
        {
            foreach(ISolutionEvent item in evt) {
                if(item.Enabled) {
                    return false;
                }
            }
            return true;
        }

        protected bool hasExecutionOrder(ISolutionEvent evt)
        {
            if(evt.ExecutionOrder == null || evt.ExecutionOrder.Length < 1) {
                return false;
            }
            return true;
        }

        protected void attachLoggingEvent()
        {
            lock(_lock) {
                detachLoggingEvent();
                Log._.Receiving += onLogging;
            }
        }

        protected void detachLoggingEvent()
        {
            Log._.Receiving -= onLogging;
        }
        
        /// <summary>
        /// Works with all processes of internal logging.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void onLogging(object sender, MessageArgs e)
        {
            if(SlnEvents == null) {
                return; // can be early initialization
            }

            if(Thread.CurrentThread.Name == Events.LoggingEvent.IDENT_TH) {
                return; // self protection
            }

            if(isDisabledAll(SlnEvents.Logging)) {
                return;
            }

            if(!IsAllowActions) {
                _ignoredAction(SolutionEventType.Logging);
                return;
            }

            (new Task(() => {
                
                Thread.CurrentThread.Name = Events.LoggingEvent.IDENT_TH;
                lock(_lock)
                {
                    IComponent component = cmd.SBEScript.Bootloader.getComponentByType(typeof(OWPComponent));
                    if(component != null) {
                        ((ILogData)component).updateLogData(e.Message, e.Level);
                    }

                    foreach(LoggingEvent evt in SlnEvents.Logging)
                    {
                        if(!isExecute(evt, current)) {
                            Log.Info("[Logging] ignored action '{0}' :: by execution order", evt.Caption);
                            continue;
                        }

                        try {
                            if(cmd.exec(evt, SolutionEventType.Logging)) {
                                Log.Trace("[Logging]: " + evt.Caption);
                            }
                        }
                        catch(Exception ex) {
                            Log.Error("LoggingEvent error: {0}", ex.Message);
                        }
                    }
                }

            })).Start();
        }

        private int _ignoredAction(SolutionEventType type)
        {
            Log.Trace("[{0}] Ignored action. It's already started in other processes of VS.", type);
            return Codes.Success;
        }
    }
}
