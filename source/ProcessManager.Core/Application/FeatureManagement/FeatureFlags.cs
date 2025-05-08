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

namespace Energinet.DataHub.ProcessManager.Core.Application.FeatureManagement;

/// <summary>
/// Manage feature flags in Process Manager.
/// </summary>
internal static class FeatureFlags
{
    public static Task<bool> UseSilentMode(this IFeatureManager featureManager)
    {
        return featureManager.IsEnabledAsync(Names.SilentMode);
    }

    /// <summary>
    /// Names of all Feature Flags that exists in the Process Manager.
    ///
    /// The feature flags can be configured:
    ///  * Locally through app settings
    ///  * In Azure App Configuration
    ///
    /// If configured locally the name of a feature flag configuration
    /// must be prefixed with "FeatureManagement__",
    /// ie. "FeatureManagement__SilentMode".
    /// </summary>
    /// <remarks>
    /// We use "const" for feature flags instead of a enum, because "Produkt Måls"
    /// feature flags contain "-" in their name.
    /// </remarks>
    public static class Names
    {
        /// <summary>
        /// <p>THIS FEATURE MUST NOT BE USED ON PRE-PROD AND PROD!</p>
        /// Enables "silent mode" for PM core.
        /// When silent mode is enabled, some errors are instead turned into logged warnings.
        /// </summary>
        /// <remarks>
        /// The intended use case for this is to prevent the system from reporting exceptions that are not critical to the
        /// system's operation and that are expected to occur as part of executing e.g. subsystem tests in other subsystems.
        /// An example of this could be the error when receiving a notify on an orchestration instance id that does not exist.
        /// As part of the subsystem tests in e.g. EDI, such events will be generated, but they won't correspond to any
        /// actual orchestration. If these errors were not silenced in the environment in which these tests are executed,
        /// the log and dead-letter queue for the PM core will be flooded with false errors.
        /// </remarks>
        public const string SilentMode = "SilentMode";
    }
}
