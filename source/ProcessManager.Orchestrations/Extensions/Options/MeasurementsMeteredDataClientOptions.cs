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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;

/// <summary>
/// Options for the configuration of Measurements metered data event hub producer clients.
/// </summary>
public class MeasurementsMeteredDataClientOptions
{
    public const string SectionName = "MeasurementsEventHub";

    /// <summary>
    /// The namespace name of the event hub which the Process Manager sends events on
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string NamespaceName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the event hub which the Process Manager sends events on
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string EventHubName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the event hub which the Process Manager receives events on
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ProcessManagerEventHubName { get; set; } = string.Empty;
}
