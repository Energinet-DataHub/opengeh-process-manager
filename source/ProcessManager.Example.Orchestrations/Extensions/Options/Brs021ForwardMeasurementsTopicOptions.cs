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

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Extensions.Options;

/// <summary>
/// Contains options required for the Example Orchestration app to connect to the
/// BRS-021 Forward Measurements service bus topics.
/// </summary>
public class Brs021ForwardMeasurementsTopicOptions
{
    /// <summary>
    /// Name of the section in the <see cref="IConfiguration"/> / appsettings.json file
    /// </summary>
    public const string SectionName = "Brs021ForwardMeasurementsTopic";

    /// <summary>
    /// Name of the topic used to start BRS-021 orchestration instances.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string StartTopicName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the topic used to notify BRS-021 orchestration instances.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string NotifyTopicName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the subscription used to start BRS-021 orchestration instances.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string StartSubscriptionName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the subscription used to notify BRS-021 orchestration instances.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string NotifySubscriptionName { get; set; } = string.Empty;
}
