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

namespace Energinet.DataHub.ProcessManager.Client.Extensions.Options;

/// <summary>
/// Options for configuration of Process Manager Service Bus clients using the Process Manager.
/// </summary>
public class ProcessManagerServiceBusClientOptions
{
    public const string SectionName = "ProcessManagerServiceBusClient";

    /// <summary>
    /// Name of the topic which the Process Manager receives start commands (service bus messages) on.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string StartTopicName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the topic which the Process Manager receives notify events (service bus messages) on.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string NotifyTopicName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the topic which the Process Manager receives BRS-021 Forward Metered Data start commands (service bus messages) on.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Brs021ForwardMeteredDataStartTopicName { get; set; } = string.Empty; // TODO #786: Update when infrastructure is released

    /// <summary>
    /// Name of the topic which the Process Manager receives BRS-021 Forward Metered Data notify events (service bus messages) on.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Brs021ForwardMeteredDataNotifyTopicName { get; set; } = string.Empty; // TODO #786: Update when infrastructure is released
}
