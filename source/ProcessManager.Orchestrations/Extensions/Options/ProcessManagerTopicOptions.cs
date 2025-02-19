﻿// Copyright 2020 Energinet DataHub A/S
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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;

/// <summary>
/// Contains options required for the orchestrations app to connect to the
/// ProcessManager Service Bus topic.
/// </summary>
public class ProcessManagerTopicOptions
{
    /// <summary>
    /// Name of the section in the <see cref="IConfiguration"/> / appsettings.json file
    /// </summary>
    public const string SectionName = "ProcessManagerTopic";

    /// <summary>
    /// Name of the ProcessManager Service Bus topic
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the subscription for BRS026 to the ProcessManager Service Bus topic
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Brs026SubscriptionName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the subscription for BRS026 to the ProcessManager Service Bus topic
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Brs028SubscriptionName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the subscription for BRS021 ForwardMeteredData to the ProcessManager Service Bus topic
    /// </summary>
    //[Required(AllowEmptyStrings = false)] // TODO: Removed required for now since tests cannot be run (yet)
    public string Brs021ForwardMeteredDataSubscriptionName { get; set; } = string.Empty;
}
