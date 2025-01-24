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

namespace Energinet.DataHub.ProcessManager.Example.Consumer.Extensions.Options;

public class EdiTopicOptions
{
    public const string SectionName = "EdiTopic";

    /// <summary>
    /// EDI Service Bus topic name
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// EDI Service Bus topic subscription name for enqueueing BRS-X03 actor messages.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string EnqueueBrsX03SubscriptionName { get; set; } = string.Empty;
}
