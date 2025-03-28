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
/// i.e. "FeatureManagement__EnableOrchestrationDescriptionBreakingChanges".
/// </summary>
public enum FeatureFlag
{
    /// <summary>
    /// <p>THIS FEATURE MUST NOT BE USED ON PRE-PROD AND PROD!</p>
    /// Enables "silent mode" for PM core.
    /// When silent mode is enabled, some errors are instead turned into logged warnings.
    /// The intended use case for this is to prevent the system from reporting exceptions that are not critical to the
    /// system's operation and that are expected to occur as part of executing e.g. subsystem tests in other subsystems.
    /// An example of this could be the error when receiving a notify on an orchestration instance id that does not exist.
    /// As part of the subsystem tests in e.g. EDI, such events will be generated, but they won't correspond to any
    /// actual orchestration. If these errors were not silenced in the environment in which these tests are executed,
    /// the log and dead-letter queue for the PM core will be flooded with false errors.
    /// </summary>
    SilentMode,

    /// <summary>
    /// Enables the use of the BRS_021_ForwardMeteredData business validation for metering points.
    /// If this feature flag is enabled, the business validation rule will return a business validation error
    /// if the metering point does not exist.
    /// </summary>
    EnableBrs021ForwardMeteredDataBusinessValidationForMeteringPoint,

    /// <summary>
    /// Enables enqueue messages for the orchestration BRS_021_ElectricalHeatingCalculation.
    /// If this flag is enabled, electrical heating messages will be generated and enqueued.
    /// </summary>
    EnableBrs021ElectricalHeatingEnqueueMessages,

    /// <summary>
    /// Enables enqueue messages for the orchestration BRS_021_CapacitySettlementCalculation.
    /// If this flag is enabled, capacity settlement messages will be generated and enqueued.
    /// </summary>
    EnableBrs021CapacitySettlementEnqueueMessages,

    /// <summary>
    /// Enables enqueue messages for the orchestration BRS_021_NetConsumptionCalculation.
    /// If this flag is enabled, net consumption (group 6) messages will be generated and enqueued.
    /// </summary>
    EnableBrs021NetConsumptionEnqueueMessages,

    /// <summary>
    /// Enables enqueue messages for the orchestration BRS_045_MissingMeasurementsLogCalculation.
    /// If this flag is enabled, missing measurements log messages will be generated and enqueued.
    /// </summary>
    EnableBrs045MissingMeasurementsLogEnqueueMessages,

    /// <summary>
    /// Enables enqueue messages for the orchestration BRS_045_MissingMeasurementsLogOnDemandCalculation.
    /// If this flag is enabled, missing measurements log on demand messages will be generated and enqueued.
    /// </summary>
    EnableBrs045MissingMeasurementsLogOnDemandEnqueueMessages,
}
