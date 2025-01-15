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

using Azure.Messaging.EventHubs.Producer;

namespace Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;

/// <summary>
/// Constants used for naming <see cref="EventHubProducerClient"/> instances
/// </summary>
public static class EventHubProducerClientNames
{
    /// <summary>
    /// Event hub producer for sending events on the measurements event hub.
    /// </summary>
    public const string MeasurementsEventHub = "MeasurementsEventHub";
}
