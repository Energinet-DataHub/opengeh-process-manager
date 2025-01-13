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

using Azure.Messaging.ServiceBus;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;
using Google.Protobuf;

namespace Energinet.DataHub.ProcessManager.Shared.Extensions;

public static class ServiceBusMessageExtensions
{
    private const string MajorVersionKey = "MajorVersion";
    private const string BodyFormatKey = "BodyFormat";

    public static string GetMajorVersion(this ServiceBusReceivedMessage message)
    {
        return (string?)message.ApplicationProperties.GetValueOrDefault(MajorVersionKey)
               ?? throw new ArgumentNullException(
                   nameof(message.ApplicationProperties),
                   $"{MajorVersionKey} must be present in the ApplicationProperties of the received service bus message (MessageId={message.MessageId}, Subject={message.Subject}).");
    }

    public static string GetBodyFormat(this ServiceBusReceivedMessage message)
    {
        return (string?)message.ApplicationProperties.GetValueOrDefault(BodyFormatKey)
               ?? throw new ArgumentNullException(
                   nameof(message.ApplicationProperties),
                   $"{BodyFormatKey} must be present in the ApplicationProperties of the received service bus message (MessageId={message.MessageId}, Subject={message.Subject}).");
    }

    /// <summary>
    /// Wrap the protobuf message in a service bus message, with the given subject and idempotency key.
    /// </summary>
    public static ServiceBusMessage ToServiceBusMessage(this IMessage message, string subject, string idempotencyKey)
    {
        ServiceBusMessage serviceBusMessage = new(JsonFormatter.Default.Format(message))
        {
            Subject = subject,
            MessageId = idempotencyKey,
            ContentType = "application/json",
        };

        serviceBusMessage.ApplicationProperties.Add(MajorVersionKey, message.GetType().Name);
        serviceBusMessage.ApplicationProperties.Add(BodyFormatKey, "application/json");

        return serviceBusMessage;
    }
}
