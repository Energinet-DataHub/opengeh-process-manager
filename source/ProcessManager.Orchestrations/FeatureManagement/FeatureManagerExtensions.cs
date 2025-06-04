// Copyright 2020 Energinet DataHub A/S
//
// Licensed under the Apache License, Version 2.0 (the "License2");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.FeatureManagement;

namespace Energinet.DataHub.ProcessManager.Orchestrations.FeatureManagement;

/// <summary>
/// Extensions for reading feature flags in Process Manager Orchestrations.
/// </summary>
internal static class FeatureManagerExtensions
{
    // Add extension methods for each feature flag name...

    /// <summary>
    /// Whether to send measurement data to additional recipients during BRS-021 process.
    /// </summary>
    public static Task<bool> AreAdditionalRecipientsEnabled(this IFeatureManager featureManager)
    {
        return featureManager.IsEnabledAsync(FeatureFlagNames.EnableAdditionalRecipients);
    }

    /// <summary>
    /// Whether to use the new BRS-021 Send Measurements database, where the processes are stored in a separate database
    /// table.
    /// </summary>
    public static Task<bool> UseNewSendMeasurementsTable(this IFeatureManager featureManager)
    {
        return featureManager.IsEnabledAsync(FeatureFlagNames.UseNewSendMeasurementsTable);
    }
}
