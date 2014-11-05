﻿/*
 * Copyright (c) 2013-2014 Developed by reg [Denis Kuzmin] <entry.reg@gmail.com>
 * Distributed under the Boost Software License, Version 1.0
 * (See accompanying file LICENSE or copy at http://www.boost.org/LICENSE_1_0.txt)
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using net.r_eg.vsSBE.MSBuild;
using net.r_eg.vsSBE.SBEScripts;

namespace net.r_eg.vsSBE.UI.WForms
{
    public partial class ScriptCheckFrm: Form
    {
        /// <summary>
        /// Clean container of user-variables
        /// </summary>
        protected IUserVariable uvariable = new UserVariable();

        /// <summary>
        /// Work with MSBuild
        /// </summary>
        protected IMSBuild msbuild;

        /// <summary>
        /// Work with SBE-Scripts
        /// </summary>
        protected ISBEScript script;

        /// <summary>
        /// Flag of supporting MSBuild
        /// </summary>
        protected bool MSBuildSupport
        {
            get { return checkBoxMSBuildSupport.Checked; }
        }

        /// <summary>
        /// Flag of sample
        /// </summary>
        private bool _isHiddenSample = false;

        public ScriptCheckFrm(IEnvironment env)
        {
            script  = new Script(env, uvariable);
            msbuild = new MSBuildParser(env, uvariable);
            InitializeComponent();
        }

        protected void updateVariableList()
        {
            listBoxUVariables.Items.Clear();
            richTextBoxUVariables.Text = String.Empty;

            foreach(string var in uvariable.Definitions) {
                listBoxUVariables.Items.Add(var);
            }
        }

        protected void getVariable(string ident)
        {
            try {
                evaluateVariable(ident);
                richTextBoxUVariables.Text = uvariable.get(ident);
            }
            catch(Exception ex) {
                richTextBoxUVariables.Text = String.Format("Fail: {0}", ex.Message);
            }
        }

        protected void evaluateVariable(string ident)
        {
            if(!uvariable.isUnevaluated(ident)) {
                return;
            }

            uvariable.evaluate(ident, (IEvaluator)script, true);
            if(MSBuildSupport) {
                uvariable.evaluate(ident, (IEvaluator)msbuild, false);
            }
        }

        private void setCommand(string str, Color foreColor)
        {
            richTextBoxCommand.Text = str;
            richTextBoxCommand.ForeColor = foreColor;
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            try {
                if(MSBuildSupport) {
                    richTextBoxExecuted.Text = msbuild.parse(script.parse(richTextBoxCommand.Text, true));
                }
                else {
                    richTextBoxExecuted.Text = script.parse(richTextBoxCommand.Text);
                }
                updateVariableList();
            }
            catch(Exception ex) {
                richTextBoxExecuted.Text = String.Format("Fail: {0}", ex.Message);
            }
        }

        private void richTextBoxCommand_Click(object sender, EventArgs e)
        {
            if(_isHiddenSample) {
                return;
            }
            _isHiddenSample = true;
            setCommand("", Color.FromArgb(0, 0, 0));
        }

        private void DTECheckFrm_Load(object sender, EventArgs e)
        {
            setCommand(richTextBoxCommand.Text, Color.FromArgb(128, 128, 128));
        }

        private void menuItemUVarUnsetSel_Click(object sender, EventArgs e)
        {
            richTextBoxUVariables.Text = String.Empty;
            if(listBoxUVariables.SelectedIndex == -1) {
                return;
            }
            uvariable.unset(listBoxUVariables.Text);
            listBoxUVariables.Items.RemoveAt(listBoxUVariables.SelectedIndex);
        }

        private void menuItemUVarUnsetAll_Click(object sender, EventArgs e)
        {
            uvariable.unsetAll();
            listBoxUVariables.Items.Clear();
            richTextBoxUVariables.Text = String.Empty;
        }

        private void listBoxUVariables_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(listBoxUVariables.SelectedIndex == -1) {
                return;
            }
            getVariable(listBoxUVariables.Text);
        }

        private void btnDoc_Click(object sender, EventArgs e)
        {
            Util.openUrl("https://bitbucket.org/3F/vssolutionbuildevent/wiki/Scripts_&_Commands/SBE-Scripts");
        }
    }
}
