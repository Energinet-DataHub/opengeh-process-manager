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

using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X05_FailingOrchestrationInstanceExample.V1.Activities;

/// <summary>
/// An activity that always fails.
/// </summary>
internal class FailingActivity_Brs_X05_V1
{
    public const string ExceptionMessage = "This activity always fails";

    [Function(nameof(FailingActivity_Brs_X05_V1))]
    public Task<string> Run(
        [ActivityTrigger] FunctionContext functionContext)
    {
        throw new Exception(ExceptionMessage);
    }
}
