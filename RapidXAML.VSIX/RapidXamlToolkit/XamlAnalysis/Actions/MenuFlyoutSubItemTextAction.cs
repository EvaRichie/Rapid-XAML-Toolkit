﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.VisualStudio.Text.Editor;
using RapidXamlToolkit.XamlAnalysis.Tags;

namespace RapidXamlToolkit.XamlAnalysis.Actions
{
    public class MenuFlyoutSubItemTextAction : HardCodedStringAction
    {
        private MenuFlyoutSubItemTextAction(string file, ITextView textView)
            : base(file, textView, Elements.MenuFlyoutSubItem, Attributes.Text)
        {
        }

        public static MenuFlyoutSubItemTextAction Create(HardCodedStringTag tag, string file, ITextView view)
        {
            var result = new MenuFlyoutSubItemTextAction(file, view)
            {
                Tag = tag,
            };

            return result;
        }
    }
}
