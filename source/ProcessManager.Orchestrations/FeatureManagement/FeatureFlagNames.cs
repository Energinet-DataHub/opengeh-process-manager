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

namespace Energinet.DataHub.ProcessManager.Orchestrations.FeatureManagement;

/// <summary>
/// Names of Feature Flags that exists in the Process Manager Orchestrations.
///
/// The feature flags can be configured:
///  * Using App Settings (locally or in Azure)
///  * In Azure App Configuration
///
/// If configured using App Settings, the name of a feature flag
/// configuration must be prefixed with <see cref="SectionName"/>,
/// ie. "FeatureManagement__SilentMode".
/// </summary>
/// <remarks>
/// We use "const" for feature flags instead of a enum, because "Produkt Mål's"
/// feature flags contain "-" in their name.
/// </remarks>
internal static class FeatureFlagNames
{
    /// <summary>
    /// Configuration section name when configuring feature flags as App Settings.
    /// </summary>
    public const string SectionName = "FeatureManagement";

    public const string EnableAdditionalRecipients = "PG29-Additional-recipients";
    public const string UseNewSendMeasurementsTable = "UseNewSendMeasurementsTable";
}
