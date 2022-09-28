// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if !UNIX

using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Security;

namespace Microsoft.PowerShell.Commands
{
    /// <summary>
    /// Defines the implementation of the 'Get-WinEnviromentVariable' cmdlet.
    /// This cmdlet get the content from EnvironemtVariable.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "WinEnvironmentVariable", DefaultParameterSetName = "DefaultSet")]
    [OutputType(typeof(PSObject), ParameterSetName = new[] { "DefaultSet" })]
    [OutputType(typeof(string), ParameterSetName = new[] { "RawSet" })]
    public class GetWinEnvironmentVariableCommand : PSCmdlet
    {
        /// <summary>
        /// Gets or sets specifies the Name EnvironmentVariable.
        /// </summary>
        [Parameter(Position = 0, ParameterSetName = "DefaultSet", Mandatory = false, ValueFromPipelineByPropertyName = true)]
        [Parameter(Position = 0, ParameterSetName = "RawSet", Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the EnvironmentVariableTarget.
        /// </summary>
        [Parameter(Position = 1, Mandatory = false, ParameterSetName = "DefaultSet")]
        [Parameter(Position = 1, Mandatory = false, ParameterSetName = "RawSet")]
        [ValidateNotNullOrEmpty]
        public EnvironmentVariableTarget Target { get; set; } = EnvironmentVariableTarget.Process;
        
        /// <summary>
        /// Gets or sets property that sets delimiter.
        /// </summary>
        [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true, ParameterSetName = "DefaultSet")]
        [ValidateNotNullOrEmpty]
        public char? Delimiter { get; set; } = null;

        /// <summary>
        /// Gets or sets raw parameter. This will allow EnvironmentVariable return text or file list as one string.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "RawSet")]
        public SwitchParameter Raw { get; set; }

        private static readonly List<string> DetectedDelimiterEnvrionmentVariable = new List<string> { "Path", "PATHEXT", "PSModulePath" };

        /// <summary>
        /// This method implements the ProcessRecord method for Get-WinEnvironmentVariable command.
        /// Returns the Specify Name EnvironmentVariable content as text format.
        /// </summary>
        protected override void BeginProcessing()
        {
            RegistryKey regkey = null;

            try
            {
                switch (Target)
                {
                    case EnvironmentVariableTarget.User:
                        regkey = Registry.CurrentUser.OpenSubKey(@"Environment");
                        break;

                    case EnvironmentVariableTarget.Machine:
                        regkey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
                        break;
        
                    default:
                        break;
                }

                PSObject env = null;
                PSNoteProperty envname = null;
                PSNoteProperty envvalue = null; 
                PSNoteProperty envtype = null;

                if (string.IsNullOrEmpty(Name))
                {
                    if (Target == EnvironmentVariableTarget.Process)
                    {
                        foreach (DictionaryEntry kvp in Environment.GetEnvironmentVariables(Target))
                        {
                            env = new PSObject();
                            envname = new PSNoteProperty("Name", kvp.Key.ToString());
                            envvalue = new PSNoteProperty("Value", kvp.Value.ToString());
                            env.Properties.Add(envname);
                            env.Properties.Add(envvalue);

                            this.WriteObject(env, true);
                        }
                    }
                    else
                    {
                        foreach (string name in regkey.GetValueNames())
                        {
                            env = new PSObject();
                            envname = new PSNoteProperty("Name", String.IsNullOrEmpty(name) ? "(Default)" : name);
                            envtype = new PSNoteProperty("RegistryValueKind", regkey.GetValueKind(name));
                            envvalue = new PSNoteProperty("Value", regkey.GetValue(name));
                            env.Properties.Add(envname);
                            env.Properties.Add(envtype);
                            env.Properties.Add(envvalue);

                            this.WriteObject(env, true);
                        }
                    }

                    return;
                }

                var contentList = new List<string>();

                string textContent = string.Empty;
                RegistryValueKind type = RegistryValueKind.None;

                if (Target == EnvironmentVariableTarget.Process)
                {
                    textContent = Environment.GetEnvironmentVariable(Name, Target);
                }
                else
                {
                    try
                    {
                        textContent = (regkey.GetValue(Name))?.ToString() ?? String.Empty;
                        type = regkey.GetValueKind(Name);
                    }
                    catch (IOException)
                    {
                    }
                }

                if (string.IsNullOrEmpty(textContent))
                {
                    var message = StringUtil.Format(
                        WinEnvironmentVariableResources.EnvironmentVariableNotFoundOrEmpty, Name);

                    ArgumentException argumentException = new ArgumentException(message);
                    ErrorRecord errorRecord = new ErrorRecord(
                        argumentException,
                        "EnvironmentVariableNotFoundOrEmpty",
                        ErrorCategory.ObjectNotFound,
                        Name);
                    ThrowTerminatingError(errorRecord);
                    return;
                }

                if (ParameterSetName == "RawSet")
                {
                    contentList.Add(textContent);
                    this.WriteObject(textContent, true);
                    return;
                }
                else
                {
                    if (DetectedDelimiterEnvrionmentVariable.Contains(Name))
                    {
                        Delimiter = Path.PathSeparator;
                    }

                    contentList.AddRange(textContent.Split(Delimiter.ToString() ?? string.Empty, StringSplitOptions.None));
                }

                env = new PSObject();
                envname = new PSNoteProperty("Name", Name);
                envtype = new PSNoteProperty("RegistryValueKind", type);
                envvalue = new PSNoteProperty("Value", contentList);

                env.Properties.Add(envname);
                env.Properties.Add(envtype);
                env.Properties.Add(envvalue);

                this.WriteObject(env, true);
            }
            finally
            {
                regkey?.Close();
            }
        }
    }
}

#endif
