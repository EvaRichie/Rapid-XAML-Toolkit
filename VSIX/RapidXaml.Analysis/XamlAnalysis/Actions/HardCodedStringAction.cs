﻿// Copyright (c) Matt Lacey Ltd. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Text.Editor;
using RapidXamlToolkit.Resources;
using RapidXamlToolkit.VisualStudioIntegration;
using RapidXamlToolkit.XamlAnalysis.Tags;

namespace RapidXamlToolkit.XamlAnalysis.Actions
{
    public class HardCodedStringAction : BaseSuggestedAction
    {
        private readonly IVisualStudioTextManipulation vstm;

        public HardCodedStringAction(string file, ITextView view, HardCodedStringTag tag, IVisualStudioTextManipulation vstm = null)
            : base(file)
        {
            this.View = view;
            this.DisplayText = StringRes.UI_MoveHardCodedString;
            this.Tag = tag;
            this.vstm = vstm;
        }

        public override ImageMoniker IconMoniker => KnownMonikers.GenerateResource;

        public HardCodedStringTag Tag { get; protected set; }

        public override void Execute(CancellationToken cancellationToken)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            var resPath = this.GetResourceFilePath();

            if (resPath == null)
            {
                return;
            }

            var vs = this.vstm ?? new VisualStudioTextManipulation(ProjectHelpers.Dte);

            var undo = vs.StartSingleUndoOperation(StringRes.UI_UndoContextMoveStringToResourceFile);

            try
            {
                if (this.Tag.ProjType == ProjectType.Uwp)
                {
                    // If the resource file is open with unsaved changes VS will prompt about data being lost.
                    this.AddResource(resPath, $"{this.Tag.UidValue}.{this.Tag.AttributeName}", this.Tag.Value);

                    if (this.Tag.AttributeType == AttributeType.Inline)
                    {
                        var currentAttribute = $"{this.Tag.AttributeName}=\"{this.Tag.Value}\"";

                        if (this.Tag.UidExists)
                        {
                            vs.RemoveInActiveDocOnLine(currentAttribute, this.Tag.GetDesignerLineNumber());
                        }
                        else
                        {
                            var uidTag = $"x:Uid=\"{this.Tag.UidValue}\"";
                            vs.ReplaceInActiveDocOnLine(currentAttribute, uidTag, this.Tag.GetDesignerLineNumber());
                        }
                    }
                    else if (this.Tag.AttributeType == AttributeType.Element)
                    {
                        var currentAttribute = $"<{this.Tag.ElementName}.{this.Tag.AttributeName}>{this.Tag.Value}</{this.Tag.ElementName}.{this.Tag.AttributeName}>";

                        vs.RemoveInActiveDocOnLine(currentAttribute, this.Tag.GetDesignerLineNumber());

                        if (!this.Tag.UidExists)
                        {
                            var uidTag = $"<{this.Tag.ElementName} x:Uid=\"{this.Tag.UidValue}\"";
                            vs.ReplaceInActiveDocOnLineOrAbove($"<{this.Tag.ElementName}", uidTag, this.Tag.GetDesignerLineNumber());
                        }
                    }
                    else if (this.Tag.AttributeType == AttributeType.DefaultValue)
                    {
                        var current = $">{this.Tag.Value}</{this.Tag.ElementName}>";
                        var replaceWith = this.Tag.UidExists ? " />" : $" x:Uid=\"{this.Tag.UidValue}\" />";

                        vs.ReplaceInActiveDocOnLine(current, replaceWith, this.Tag.GetDesignerLineNumber());
                    }
                }
                else if (this.Tag.ProjType == ProjectType.Wpf)
                {
                    // TODO: ISSUE#163 Implement WPF resource creation
                    var resourceName = "NEED TO GENERATE THIS";
                    this.AddResource(resPath, resourceName, this.Tag.Value);

                    switch (this.Tag.AttributeType)
                    {
                        case AttributeType.Inline:
                            break;
                        case AttributeType.Element:
                            break;
                        case AttributeType.DefaultValue:
                            break;
                    }
                }
                else if (this.Tag.ProjType == ProjectType.XamarinForms)
                {
                    var resourceName = "NEED TO GENERATE THIS";
                    this.AddResource(resPath, resourceName, this.Tag.Value);

                    // TODO: ISSUE#163 Implement Xamarin.Forms resource creation
                    switch (this.Tag.AttributeType)
                    {
                        case AttributeType.Inline:
                            var currentAttribute = $"{this.Tag.AttributeName}=\"{this.Tag.Value}\"";
                            var localizedAttribute = $"{this.Tag.AttributeName}=\"{{x:Static resources:{System.IO.Path.GetFileNameWithoutExtension(resPath)}.{resourceName}}}\"";

                            vs.ReplaceInActiveDocOnLine(currentAttribute, localizedAttribute, this.Tag.GetDesignerLineNumber());
                            break;
                        case AttributeType.Element:
                            break;
                        case AttributeType.DefaultValue:
                            break;
                    }
                }

                RapidXamlDocumentCache.TryUpdate(this.File);
            }
            finally
            {
                if (undo)
                {
                    vs.EndSingleUndoOperation();
                }
            }
        }

        private void AddResource(string resPath, string name, string value)
        {
            var xdoc = XDocument.Load(resPath);

            // Don't want to create a duplicate entry.
            var alreadyExists = false;

            foreach (var element in xdoc.Descendants("data"))
            {
                if (element.Attribute("name")?.Value == name)
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (!alreadyExists)
            {
                var newData = new XElement("data");

                newData.Add(new XAttribute("name", name));

                newData.Add(new XAttribute(XNamespace.Xml + "space", "preserve"));
                newData.Add(new XElement("value", value));
                xdoc.Element("root").Add(newData);
                xdoc.Save(resPath);
            }
        }

        private string GetResourceFilePath()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            var resFiles = new List<string>();

            // See also https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.ivssolution.getprojectenum?view=visualstudiosdk-2017#Microsoft_VisualStudio_Shell_Interop_IVsSolution_GetProjectEnum_System_UInt32_System_Guid__Microsoft_VisualStudio_Shell_Interop_IEnumHierarchies__
            void IterateProjItems(EnvDTE.ProjectItem projectItem)
            {
                var item = projectItem.ProjectItems.GetEnumerator();

                while (item.MoveNext())
                {
                    if (item.Current is EnvDTE.ProjectItem projItem)
                    {
                        if (projItem.ProjectItems.Count > 0)
                        {
                            IterateProjItems(projItem);
                        }
                        else if (projItem.Name.EndsWith(".resw")
                              || projItem.Name.EndsWith(".resx"))
                        {
                            // Get either type of res file. Don't have a reason for a project to contain both.
                            resFiles.Add(projItem.FileNames[0]);
                        }
                    }
                }
            }

            void IterateProject(EnvDTE.Project project)
            {
                var item = project.ProjectItems.GetEnumerator();

                while (item.MoveNext())
                {
                    if (item.Current is EnvDTE.ProjectItem projItem)
                    {
                        IterateProjItems(projItem);
                    }
                }
            }

            var proj = ProjectHelpers.Dte.Solution.GetProjectContainingFile(this.File);

            // For very large projects this can be very inefficient. When an issue, consider caching or querying the file system directly.
            IterateProject(proj);

            if (resFiles.Count == 0)
            {
                SharedRapidXamlPackage.Logger?.RecordInfo(StringRes.Info_NoResourceFileFound);
                return null;
            }
            else if (resFiles.Count == 1)
            {
                return resFiles.First();
            }
            else
            {
                var langOfInterest = string.Empty;

                var neutralLang = proj.Properties.Item("NeutralResourcesLanguage").Value.ToString();

                if (string.IsNullOrWhiteSpace(neutralLang))
                {
                    var xProj = XDocument.Load(proj.FileName);

                    XNamespace xmlns = "http://schemas.microsoft.com/developer/msbuild/2003";

                    var defLang = xProj.Descendants(xmlns + "DefaultLanguage").FirstOrDefault();

                    if (defLang != null)
                    {
                        langOfInterest = defLang.Value;
                    }
                }
                else
                {
                    langOfInterest = neutralLang;
                }

                if (!string.IsNullOrWhiteSpace(langOfInterest))
                {
                    return resFiles.FirstOrDefault(f => f.IndexOf(langOfInterest, StringComparison.OrdinalIgnoreCase) > 0);
                }
                else
                {
                    // Find neutral language file to return
                    // RegEx to match if lang identifier in path or file name
                    return resFiles.FirstOrDefault(f => Regex.Matches(f, "([\\.][a-zA-Z]{2}-[a-zA-Z]{2}[\\.])").Count == 0);
                }
            }
        }
    }
}
