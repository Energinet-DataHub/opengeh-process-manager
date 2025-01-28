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

using Energinet.DataHub.ProcessManager.Core.Infrastructure.FeatureFlags;

namespace Energinet.DataHub.ProcessManager.Core.Application.FeatureFlags;

/// <summary>
/// The current feature flags in the Process Manager. If using <see cref="MicrosoftFeatureFlagManager"/>
/// then the feature flags are managed through the app configuration, and the name
/// of a feature flag configuration is the enum value's name, prefixed with "FeatureManagement__",
/// ie. "FeatureManagement__EnableOrchestrationDescriptionBreakingChanges".
/// </summary>
public enum FeatureFlag
{
}
