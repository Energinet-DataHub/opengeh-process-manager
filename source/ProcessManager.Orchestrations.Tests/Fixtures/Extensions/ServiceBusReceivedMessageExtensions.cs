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
