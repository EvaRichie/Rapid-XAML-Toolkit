﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RapidXamlToolkit.Resources;
using RapidXamlToolkit.VisualStudioIntegration;

namespace RapidXamlToolkit.Options
{
    public class Settings : CanNotifyPropertyChanged
    {
        public Dictionary<string, string> ActiveProfileNames { get; set; } = new Dictionary<string, string>();

        public string FallBackProfileName { get; set; }

        public List<Profile> Profiles { get; set; }

        public bool ExtendedOutputEnabled { get; set; }

        public ObservableCollection<ProfileSummary> ProfilesList
        {
            get
            {
                var list = new ObservableCollection<ProfileSummary>();

                // If multile profiles have the same name as the active (or fall back) profile, use the first one in the list with matching name
                Dictionary<string, bool> activeIndicated = new Dictionary<string, bool>();
                activeIndicated.Add(ProjectType.Uwp.GetDescription(), false);
                activeIndicated.Add(ProjectType.Wpf.GetDescription(), false);
                activeIndicated.Add(ProjectType.XamarinForms.GetDescription(), false);

                bool fallBackIndicated = false;

                for (var index = 0; index < this.Profiles.Count; index++)
                {
                    var profile = this.Profiles[index];

                    var summary = new ProfileSummary
                    {
                        Index = index,
                        Name = profile.Name,
                        ProjectType = profile.ProjectTypeDescription,
                        IsActive = !activeIndicated[profile.ProjectType.GetDescription()]
                                && this.ActiveProfileNames.Keys.Contains(profile.ProjectTypeDescription)
                                && profile.Name == this.ActiveProfileNames[profile.ProjectTypeDescription],
                        IsFallBack = !fallBackIndicated && profile.Name == this.FallBackProfileName,
                    };

                    if (summary.IsActive)
                    {
                        activeIndicated[profile.ProjectType.GetDescription()] = true;
                    }

                    if (summary.IsFallBack)
                    {
                        fallBackIndicated = true;
                    }

                    list.Add(summary);
                }

                return list;
            }
        }

        public Profile GetFallBackProfile()
        {
            Profile result = null;

            if (!string.IsNullOrWhiteSpace(this.FallBackProfileName))
            {
                result = this.Profiles.FirstOrDefault(p => p.Name == this.FallBackProfileName);
            }

            // As a final option just return anything.
            return result ?? this.Profiles.FirstOrDefault();
        }

        public Profile GetActiveProfile(ProjectType projectType)
        {
            Profile result = null;

            var key = projectType.GetDescription();

            // Try and get the active item for this project type.
            if (this.ActiveProfileNames.Keys.Contains(key))
            {
                var apn = this.ActiveProfileNames[key];

                if (!string.IsNullOrWhiteSpace(apn))
                {
                    result = this.Profiles.FirstOrDefault(p => p.ProjectType == projectType
                                                            && p.Name == apn);
                }
            }

            // If that doesn't exist, just get the first one for that project type.
            if (result == null)
            {
                result = this.Profiles.FirstOrDefault(p => p.ProjectType == projectType);
            }

            // If there still isn't one, try the fall back.
            if (result == null)
            {
                result = this.GetFallBackProfile();
            }

            RapidXamlPackage.Logger?.RecordInfo(StringRes.Info_UsingProfile.WithParams(result.Name));

            return result;
        }

        public List<string> GetAllProfileNames()
        {
            return this.Profiles.Select(p => p.Name).ToList();
        }

        public void RefreshProfilesList()
        {
            this.OnPropertyChanged(nameof(this.ProfilesList));
        }
    }
}
