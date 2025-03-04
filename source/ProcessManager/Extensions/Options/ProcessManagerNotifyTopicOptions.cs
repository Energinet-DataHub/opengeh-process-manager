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

namespace Energinet.DataHub.ProcessManager.Extensions.Options;

/// <summary>
/// Contains options required for connection to the "Notify Orchestration Instance" subscription on the Process Manager Notify topic.
/// </summary>
public class ProcessManagerNotifyTopicOptions
{
    /// <summary>
    /// Name of the section in the <see cref="IConfiguration"/> / appsettings.json file
    /// </summary>
    public const string SectionName = "NotifyOrchestrationInstanceV2";

    /// <summary>
    /// Name of the Process Manager Notify topic
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the subscription on the Process Manager Notify topic.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string SubscriptionName { get; set; } = string.Empty;
}
