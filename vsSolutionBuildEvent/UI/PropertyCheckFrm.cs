﻿/*
 * Copyright (c) 2013-2014 Developed by reg <entry.reg@gmail.com>
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

namespace net.r_eg.vsSBE.UI
{
    public partial class PropertyCheckFrm: Form
    {
        /// <summary>
        /// Work with MSBuild
        /// </summary>
        private MSBuildParser _parser;
        /// <summary>
        /// Flag of sample
        /// </summary>
        private bool _isHiddenSample = false;

        public PropertyCheckFrm(IEnvironment env)
        {
            _parser = new MSBuildParser(env);
            InitializeComponent();
        }

        private void btnEvaluate_Click(object sender, EventArgs e)
        {
            string evaluated;
            try {
                // for a specific project use like this: $($(var):project)
                evaluated = _parser.parseVariablesMSBuild(textBoxUnevaluated.Text.Trim());
            }
            catch(Exception ex) {
                evaluated = ex.Message;
            }
            richTextBoxEvaluated.Text = evaluated;
        }

        private void textBoxUnevaluated_Click(object sender, EventArgs e)
        {
            if(_isHiddenSample) {
                return;
            }
            _isHiddenSample = true;
            setUnevaluated("", Color.FromArgb(0, 0, 0));
        }

        private void PropertyCheckFrm_Load(object sender, EventArgs e)
        {
            setUnevaluated("$([System.Guid]::NewGuid())", Color.FromArgb(128, 128, 128));
        }

        private void setUnevaluated(string str, Color foreColor)
        {
            textBoxUnevaluated.Text         = str;
            textBoxUnevaluated.ForeColor    = foreColor;
        }
    }
}
