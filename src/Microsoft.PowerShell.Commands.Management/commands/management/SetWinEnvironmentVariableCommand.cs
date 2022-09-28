// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if !UNIX

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Security;
using System.Text.RegularExpressions;

namespace Microsoft.PowerShell.Commands
{
    /// <summary>
    /// Defines the implementation of the 'Set-WinEnvironmentVariable' cmdlet.
    /// This cmdlet gets the content from EnvironmentVariable.
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "WinEnvironmentVariable", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public class SetWinEnvironmentVariableCommand : PSCmdlet
    {
        /// <summary>
        /// Gets or sets EnvironmentVariable value.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [AllowEmptyString]
        public string[] Value { get; set; }

        /// <summary>
        /// Gets or sets specifies the Name EnvironmentVariable.
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the EnvironmentVariableTarget.
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public EnvironmentVariableTarget Target { get; set; } = EnvironmentVariableTarget.Process;

        /// <summary>
        /// Gets or sets property that sets delimiter.
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public char? Delimiter { get; set; } = null;

        /// <summary>
        /// Gets or sets the Type parameter as a dynamic parameter for
        /// the registry provider's SetItem method.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DefaultSet")]
        [ValidateSet("String", "ExpandString", IgnoreCase = true)]
        public RegistryValueKind Type { get; set; } = RegistryValueKind.None;

        /// <summary>
        /// Gets or sets append parameter. This will allow to append EnvironmentVariable without remove it.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "AppendSet")]
        public SwitchParameter Append { get; set; }

        /// <summary>
        /// Gets or sets force parameter. This will allow to remove or set the EnvironmentVariable.
        /// </summary>
        [Parameter]
        public SwitchParameter Force { get; set; } = false;

        private readonly List<string> _contentList = new();

        private static readonly List<string> DetectedDelimiterEnvrionmentVariable = new List<string> { "Path", "PATHEXT", "PSModulePath" };

        /// <summary>
        /// This method implements the BeginProcessing method for Set-WinEnvironmentVariable command.
        /// </summary>
        protected override void BeginProcessing()
        {
            _contentList.Clear();

            if (DetectedDelimiterEnvrionmentVariable.Contains(Name))
            {
                Delimiter = Path.PathSeparator;
            }

            if (Append)
            {
                string content = string.Empty;

                if (Target == EnvironmentVariableTarget.Process)
                {
                    content = Environment.GetEnvironmentVariable(Name, Target);
                }
                else
                {
                    RegistryKey regkey = null;

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

                    try
                    {
                        Type = regkey.GetValueKind(Name);
                        if (Type != RegistryValueKind.String && Type != RegistryValueKind.ExpandString)
                        {
                            Type = RegistryValueKind.None;
                            string setWinEnvironmentVariableShouldProcessTarget;
                            setWinEnvironmentVariableShouldProcessTarget = string.Format(CultureInfo.InvariantCulture, WinEnvironmentVariableResources.RegistryKindValueWrong, Name);
                            var message = StringUtil.Format(
                                WinEnvironmentVariableResources.RegistryKindValueWrong);

                            var argumentException = new RuntimeException(message);
                            ErrorRecord errorRecord = new ErrorRecord(
                                argumentException,
                                "RegistryKindValueWrong",
                                ErrorCategory.ParserError,
                                Name);
                            ThrowTerminatingError(errorRecord);

                            this.WriteObject("Hello World");
                            return;
                        }

                        content = (regkey.GetValue(Name))?.ToString() ?? String.Empty;
                        // 値が無い時点で、GetValueKindの処理をしないという風が自然な作りでは？
                    }
                    catch (IOException)
                    {
                        // var message = StringUtil.Format(
                        //     WinEnvironmentVariableResources.SetWinEnvironmentVariableArgumentError,
                        //     Name);

                        // ArgumentException argumentException = new ArgumentException(message, ex.InnerException);
                        // ErrorRecord errorRecord = new ErrorRecord(
                        //     argumentException,
                        //     "ArgumentError",
                        //     ErrorCategory.ParserError,
                        //     Name);
                        // ThrowTerminatingError(errorRecord);
                    }
                    finally
                    {
                        regkey?.Close();
                    }
                }

                if (!string.IsNullOrEmpty(content))
                {
                    _contentList.Insert(0, content);
                }
            }
        }

        /// <summary>
        /// This method implements the ProcessRecord method for Set-WinEnvironmentVariable command.
        /// </summary>
        protected override void ProcessRecord()
        {
            if (Value != null)
            {
                _contentList.AddRange(Value);
            }
        }

        /// <summary>
        /// This method removes all leading and trailing occurrences of a set of blank character.
        /// </summary>
        /// <param name="environmentVariable">EnvironmentVariable has been trimmed.</param>
        /// <param name="separatorSymbol">EnvironmentVariable separator.</param>
        /// <returns>Trim fixed string.</returns>
        public string TrimEnvironmentVariable(string environmentVariable, char separatorSymbol)
        {
            Regex duplicateSymbol = new Regex(Delimiter + "{2,}");
            Regex headSymbol = new Regex("^" + Delimiter);
            Regex trailingSymbol = new Regex(Delimiter + "$");
            Regex trimSymbolSpace = new Regex(@"[\n\r\s\t]*" + Delimiter + @"[\n\r\s\t]*");

            return trailingSymbol.Replace(
                headSymbol.Replace(
                    trimSymbolSpace.Replace(
                        duplicateSymbol.Replace(
                            environmentVariable,
                            separatorSymbol.ToString()),
                        separatorSymbol.ToString()),
                    string.Empty),
                string.Empty).Trim();
        }

        /// <summary>
        /// This method Set the EnvironmentVariable content.
        /// </summary>
        public void SetEnvironmentVariable()
        {
            string setWinEnvironmentVariableShouldProcessTarget;

            if (Type == RegistryValueKind.None)
            {
                var message = StringUtil.Format(
                    WinEnvironmentVariableResources.RegistryKindValueWrong, Name);

                var argumentException = new RuntimeException(message);
                ErrorRecord errorRecord = new ErrorRecord(
                    argumentException,
                    "RegistryKindValueWrong",
                    ErrorCategory.ParserError,
                    Name);
                ThrowTerminatingError(errorRecord);

                return;
            }

            RegistryKey regkey = null;

            try
            {
                if (Target == EnvironmentVariableTarget.Process)
                {
                    this.CommandInfo.CommandMetadata.ConfirmImpact = ConfirmImpact.Medium;
                }

                try
                {
                    switch (Target)
                    {
                        case EnvironmentVariableTarget.User:
                            regkey = Registry.CurrentUser.CreateSubKey(@"Environment");
                            break;

                        case EnvironmentVariableTarget.Machine:
                            regkey = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
                            break;
            
                        default:
                            break;
                    }
                }
                catch (SecurityException ex)
                {
                    var message = StringUtil.Format(
                        WinEnvironmentVariableResources.CantSetWinEnvironmentVariable,
                        Name,
                        Target);

                    var argumentException = new SecurityException(message, ex.InnerException);
                    ErrorRecord errorRecord = new ErrorRecord(
                        argumentException,
                        "PermissionDenied",
                        ErrorCategory.PermissionDenied,
                        Name);
                    ThrowTerminatingError(errorRecord);
                    return;
                }

                if (_contentList.Count == 1 && string.IsNullOrEmpty(_contentList[0]) && !Append)
                {
                    setWinEnvironmentVariableShouldProcessTarget = string.Format(CultureInfo.InvariantCulture, WinEnvironmentVariableResources.WinEnvironmentVariableRemoved, Name);
                    if (Force || ShouldProcess(setWinEnvironmentVariableShouldProcessTarget, "Set-WinEnvironmentVariable"))
                    {
                        this.CommandInfo.CommandMetadata.ConfirmImpact = ConfirmImpact.Medium;
                        if (Target == EnvironmentVariableTarget.Process)
                        {
                            Environment.SetEnvironmentVariable(Name, null, Target);
                        }
                        else
                        {
                            regkey.DeleteValue(Name, false);
                        }
                    }

                    return;
                }

                string result = string.Join(Delimiter.ToString() ?? string.Empty, _contentList);

                if (string.IsNullOrEmpty(Delimiter.ToString()))
                {
                    setWinEnvironmentVariableShouldProcessTarget = string.Format(CultureInfo.InvariantCulture, WinEnvironmentVariableResources.SetWinEnvironmentVariable, result, Name, Type);
                }
                else 
                {
                    result = TrimEnvironmentVariable(result, Delimiter.Value);

                    Regex symbol2newLine = new Regex(Delimiter.ToString());
                    string verboseString = symbol2newLine.Replace(result, Environment.NewLine) + Environment.NewLine;
                    setWinEnvironmentVariableShouldProcessTarget = string.Format(CultureInfo.InvariantCulture, WinEnvironmentVariableResources.SetMultipleEnvironmentVariable, Name, Type, Delimiter, verboseString);
                }

                if (Force || ShouldProcess(setWinEnvironmentVariableShouldProcessTarget, "Set-WinEnvironmentVariable"))
                {
                    this.CommandInfo.CommandMetadata.ConfirmImpact = ConfirmImpact.Medium;
                    try
                    {
                        if (Target == EnvironmentVariableTarget.Process)
                        {
                            Environment.SetEnvironmentVariable(Name, result, Target);
                        }
                        else
                        {
                            regkey.SetValue(Name, result, Type);
                        }
                    }
                    catch (SecurityException ex)
                    {
                        var message = StringUtil.Format(
                            WinEnvironmentVariableResources.CantSetWinEnvironmentVariable,
                            Name,
                            Target);

                        SecurityException argumentException = new SecurityException(message, ex.InnerException);
                        ErrorRecord errorRecord = new ErrorRecord(
                            argumentException,
                            "PermissionDenied",
                            ErrorCategory.PermissionDenied,
                            Name);
                        ThrowTerminatingError(errorRecord);
                    }
                    catch (ArgumentException ex)
                    {
                        var message = StringUtil.Format(
                            WinEnvironmentVariableResources.SetWinEnvironmentVariableArgumentError,
                            Name);

                        ArgumentException argumentException = new ArgumentException(message, ex.InnerException);
                        ErrorRecord errorRecord = new ErrorRecord(
                            argumentException,
                            "ArgumentError",
                            ErrorCategory.ParserError,
                            Name);
                        ThrowTerminatingError(errorRecord);
                    }
                }
            }
            finally
            {
                this.CommandInfo.CommandMetadata.ConfirmImpact = ConfirmImpact.High;
                regkey?.Close();
            }
        }

        /// <summary>
        /// This method implements the EndProcessing method for Set-WinEnvironmentVariable command.
        /// Set the EnvironmentVariable content.
        /// </summary>
        protected override void EndProcessing()
        {
            if (string.IsNullOrEmpty(Delimiter.ToString()) && (Append || _contentList.Count > 1)) 
            {
                var message = StringUtil.Format(
                    WinEnvironmentVariableResources.DelimterNotDetected);

                ArgumentException argumentException = new ArgumentException(message);
                ErrorRecord errorRecord = new ErrorRecord(
                    argumentException,
                    "DelimiterNotDetected",
                    ErrorCategory.ParserError,
                    Name);
                ThrowTerminatingError(errorRecord);

                return;
            }

            SetEnvironmentVariable();
        }
    }
}

#endif
