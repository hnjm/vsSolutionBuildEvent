﻿/* 
 * Boost Software License - Version 1.0 - August 17th, 2003
 * 
 * Copyright (c) 2013-2014 Developed by reg [Denis Kuzmin] <entry.reg@gmail.com>
 * 
 * Permission is hereby granted, free of charge, to any person or organization
 * obtaining a copy of the software and accompanying documentation covered by
 * this license (the "Software") to use, reproduce, display, distribute,
 * execute, and transmit the Software, and to prepare derivative works of the
 * Software, and to permit third-parties to whom the Software is furnished to
 * do so, all subject to the following:
 * 
 * The copyright notices in the Software and this entire statement, including
 * the above license grant, this restriction and the following disclaimer,
 * must be included in all copies of the Software, in whole or in part, and
 * all derivative works of the Software, unless such copies or derivative
 * works are solely in the form of machine-executable object code generated by
 * a source language processor.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT
 * SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE
 * FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE. 
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using net.r_eg.vsSBE.Exceptions;
using net.r_eg.vsSBE.MSBuild;
using net.r_eg.vsSBE.SBEScripts.Exceptions;

namespace net.r_eg.vsSBE.SBEScripts.Components
{
    public class UserVariableComponent: IComponent
    {
        /// <summary>
        /// Type of implementation
        /// </summary>
        public ComponentType Type
        {
            get { return ComponentType.UserVariable; }
        }

        /// <summary>
        /// Allows post-processing with MSBuild core
        /// </summary>
        public bool PostProcessingMSBuild
        {
            get { return postProcessingMSBuild; }
            set { postProcessingMSBuild = value; }
        }
        protected bool postProcessingMSBuild = false;

        /// <summary>
        /// For evaluating with SBE-Script
        /// </summary>
        protected ISBEScript script;

        /// <summary>
        /// For evaluating with MSBuild
        /// </summary>
        protected IMSBuild msbuild;

        /// <summary>
        /// Current user-variables
        /// </summary>
        protected IUserVariable uvariable;

        /// <summary>
        /// Handling with current type
        /// </summary>
        /// <param name="data">mixed data</param>
        /// <returns>prepared and evaluated data</returns>
        public string parse(string data)
        {
            Match m = Regex.Match(data, @"^\[var
                                              \s+
                                              ([A-Za-z_0-9]+)  #1 - name 
                                              (?:
                                                :([^=\]]+)     #2 - project (optional)
                                              )?
                                              \s*
                                              (?:
                                                =\s*
                                                (.+)           #3 - mixed data for definition (optional)
                                              )?
                                           \]$", // #3 - greedy, however it's controlled by main container of SBE-Script
                                           RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);

            if(!m.Success) {
                throw new SyntaxIncorrectException("Failed UserVariableComponent - '{0}'", data);
            }

            string name     = m.Groups[1].Value;
            string project  = (m.Groups[2].Success)? m.Groups[2].Value.Trim() : null;
            string value    = (m.Groups[3].Success)? m.Groups[3].Value : null;
            
            if(value != null) {
                set(name, project, value);
                return String.Empty;
            }
            return get(name, project);
        }

        /// <param name="env">Used environment</param>
        /// <param name="uvariable">Instance of used user-variables</param>
        public UserVariableComponent(IEnvironment env, IUserVariable uvariable)
        {
            this.uvariable  = uvariable;
            script          = new Script(env, uvariable);
            msbuild         = new MSBuildParser(env, uvariable);
        }

        /// <summary>
        /// Setting user-variable with the scope of project
        /// </summary>
        /// <param name="name">Variable name</param>
        /// <param name="project">Project name</param>
        /// <param name="value">Mixed value for variable</param>
        protected void set(string name, string project, string value)
        {
            uvariable.set(name, project, value);
            evaluate(name, project);
        }

        /// <summary>
        /// Setting user-variable only with simple name
        /// </summary>
        /// <param name="name">Variable name</param>
        /// <param name="value">Mixed value for variable</param>
        protected void set(string name, string value)
        {
            set(name, null, value);
        }

        /// <summary>
        /// Getting value from user-value
        /// </summary>
        /// <param name="name">variable name</param>
        /// <param name="project">scope of project</param>
        /// <returns>evaluated value from found variable</returns>
        /// <exception cref="NotFoundException">if not found</exception>
        protected string get(string name, string project = null)
        {
            if(!uvariable.isExist(name, project)) {
                throw new NotFoundException("UVariable '{0}:{1}' not found", name, project);
            }

            if(uvariable.isUnevaluated(name, project)) {
                evaluate(name, project);
            }
            return uvariable.get(name, project);
        }

        protected virtual void evaluate(string name, string project = null)
        {
            uvariable.evaluate(name, project, (IEvaluator)script, true);
            if(postProcessingMSBuild) {
                uvariable.evaluate(name, project, (IEvaluator)msbuild, false);
            }
        }
    }
}
