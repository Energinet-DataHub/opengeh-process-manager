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
/// Process Manager Start topic and subscriptions.
/// </summary>
public class ProcessManagerStartTopicOptions
{
    /// <summary>
    /// Name of the section in the <see cref="IConfiguration"/> / appsettings.json file
    /// </summary>
    public const string SectionName = "ProcessManagerStartTopic";

    /// <summary>
    /// Name of the Process Manager Start topic.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the subscription for BRS024 to the Process Manager Start topic.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Brs024SubscriptionName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the subscription for BRS025 to the Process Manager Start topic.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Brs025SubscriptionName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the subscription for BRS026 to the Process Manager Start topic.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Brs026SubscriptionName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the subscription for BRS026 to the Process Manager Start topic.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Brs028SubscriptionName { get; set; } = string.Empty;
}
