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
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;

public static class ServiceBusReceivedMessageExtensions
{
    /// <summary>
    /// Try to parse the received service bus message as a EnqueueActorMessagesV1 message.
    /// <remarks>
    /// If the major version isn't correct then the method will return false.
    /// </remarks>
    /// </summary>
    /// <param name="message"></param>
    /// <param name="orchestrationUniqueName">The name of the orchestration that is enqueued for, used to verify the message subject.</param>
    /// <param name="enqueueActorMessagesV1">The parsed <see cref="EnqueueActorMessagesV1"/>. Will have default value if parsing fails (and false will be returned).</param>
    /// <returns>Returns true if succeeded, else false.</returns>
    public static bool TryParseAsEnqueueActorMessages(
        this ServiceBusReceivedMessage message,
        string orchestrationUniqueName,
        out EnqueueActorMessagesV1 enqueueActorMessagesV1)
    {
        enqueueActorMessagesV1 = new EnqueueActorMessagesV1();
        if (message.Subject != $"Enqueue_{orchestrationUniqueName.ToLower()}")
            return false;

        var majorVersion = message.ApplicationProperties["MajorVersion"].ToString();
        if (majorVersion != nameof(EnqueueActorMessagesV1))
        {
            return false;
        }

        var messageBody = message.Body.ToString();
        enqueueActorMessagesV1 = EnqueueActorMessagesV1.Parser.ParseJson(messageBody);
        if (enqueueActorMessagesV1 == null)
        {
            return false;
        }

        return true;
    }
}
