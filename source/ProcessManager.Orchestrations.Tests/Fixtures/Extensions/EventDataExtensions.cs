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

using Azure.Messaging.EventHubs;
using Energinet.DataHub.Measurements.Contracts;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;

public static class EventDataExtensions
{
    public static bool TryParseAsPersistSubmittedTransaction(
        this EventData eventData,
        out PersistSubmittedTransaction persistSubmittedTransaction)
    {
        persistSubmittedTransaction = PersistSubmittedTransaction.Parser.ParseFrom(eventData.EventBody.ToArray());
        return true;
    }
}
