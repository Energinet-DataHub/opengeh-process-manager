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

using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Extensions.DurableTask;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

internal class Orchestration_Brs_021_ForwardMeteredData_V1
{
    internal static StepIdentifierDto[] Steps =>
    [
        ValidatingStep, StoringMeteredDataStep, FindReceiverStep, EnqueueMessagesStep
    ];

    internal static StepIdentifierDto ValidatingStep => new(2, "Asynkron validering");

    internal static StepIdentifierDto StoringMeteredDataStep => new(3, "Gemmer");

    internal static StepIdentifierDto FindReceiverStep => new(3, "Finder modtagere");

    internal static StepIdentifierDto EnqueueMessagesStep => new(3, "Sætter beskeder i kø");

    [Function(nameof(Orchestration_Brs_021_ForwardMeteredData_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetOrchestrationParameterValue<MeteredDataForMeasurementPointMessageInputV1>();

        if (input == null)
            return "Error: No input specified.";

        await Task.CompletedTask;

        /*
         * Activities:
         */
        // TODO: Implementing activities coming in next PR.

        return "Success";
    }
}
