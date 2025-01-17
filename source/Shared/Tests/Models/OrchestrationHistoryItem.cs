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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;

/// <summary>
/// History item for the orchestration. Which one may use to verify that the orchestration ran the expected activities.
/// </summary>
/// <param name="EventType">The event type which is made by the durable function. Most likely one of:
/// "ExecutionStarted" (Function has started),
/// "TaskCompleted" (Activity completed),
/// "TimerCreated" (When the function is waiting for something, can be an external event),
/// "EventRaised" (Event raised in the function, may be via the durableTaskClient.RaiseEventAsync(...) method),
/// "ExecutionCompleted" (The function has run to completion) </param>
/// <param name="Name">Can contain the name of the event raised for the function </param>
/// <param name="FunctionName">Function name of the activity which was run </param>
public record OrchestrationHistoryItem(
    string? EventType,
    string? Name = null,
    string? FunctionName = null);
