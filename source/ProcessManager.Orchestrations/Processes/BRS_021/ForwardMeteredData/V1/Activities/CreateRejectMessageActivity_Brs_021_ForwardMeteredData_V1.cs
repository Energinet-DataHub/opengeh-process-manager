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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;

internal class CreateRejectMessageActivity_Brs_021_ForwardMeteredData_V1
{
    [Function(nameof(CreateRejectMessageActivity_Brs_021_ForwardMeteredData_V1))]
    public Task<ActivityOutput> Run([ActivityTrigger] ActivityInput activityInput)
    {
        var result = new ActivityOutput(null!);
            // new ForwardMeteredDataRejectedV1(
            //     Guid.NewGuid().ToString("N"),
            //     BusinessReason.PeriodicMetering,
            //     activityInput.Recipient,
            //     activityInput.OrchestrationInstanceId.Value,
            //     Guid.NewGuid(),
            //     /*
            //      * For `AcknowledgementV1` only `received_MarketDocument.mRID`
            //      * and `received_MarketDocument.process.processType` should be set.
            //      * The remaining properties should be null.
            //      */
            //     new AcknowledgementV1(
            //         null,
            //         activityInput.InputMessageId,
            //         activityInput.InputProcessType,
            //         null,
            //         null,
            //         null,
            //         activityInput.GeneralErrors.Select(err => new ReasonV1(err.ErrorCode, err.Message)).ToList(),
            //         [],
            //         [
            //             new SeriesV1(
            //                 activityInput.InputTransactionId,
            //                 activityInput.SeriesErrors.Select(err => new ReasonV1(err.ErrorCode, err.Message))
            //                     .ToList()),
            //         ],
            //         [],
            //         []));

        return Task.FromResult(result);
    }

    public sealed record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        string InputMessageId,
        string InputTransactionId,
        string InputProcessType,
        MarketActorRecipientV1 Recipient,
        IReadOnlyCollection<ValidationError> GeneralErrors,
        IReadOnlyCollection<ValidationError> SeriesErrors);

    public sealed record ActivityOutput(ForwardMeteredDataRejectedV1 RejectMessage);
}
