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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.SendMeasurements;
using Energinet.DataHub.ProcessManager.Core.Application.SendMeasurements;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Api;

internal class GetSendMeasurementsInstanceByIdempotencyKeyTrigger(
    ILogger<GetSendMeasurementsInstanceByIdempotencyKeyTrigger> logger,
    ISendMeasurementsInstanceRepository repository)
{
    private readonly ILogger _logger = logger;
    private readonly ISendMeasurementsInstanceRepository _repository = repository;

    /// <summary>
    /// Get Send Measurements instance by idempotency key.
    /// </summary>
    [Function(nameof(GetSendMeasurementsInstanceByIdempotencyKeyTrigger))]
    [Authorize]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "sendmeasurements/query/idempotencykey")]
        HttpRequest httpRequest,
        [FromBody]
        GetSendMeasurementsInstanceByIdempotencyKeyQuery query,
        FunctionContext executionContext)
    {
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        //
        // NOTICE:
        // The query also carries information about the user executing the query,
        // so if necessary we can validate their data access.
        //
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        var sendMeasurementsInstance = await _repository
            .GetOrDefaultAsync(new IdempotencyKey(query.IdempotencyKey))
            .ConfigureAwait(false);

        var dto = sendMeasurementsInstance != null
            ? MapToDto(sendMeasurementsInstance)
            : null;

        return new OkObjectResult(dto);
    }

    private SendMeasurementsInstanceDto MapToDto(SendMeasurementsInstance instance)
    {
        return new SendMeasurementsInstanceDto(
            Id: instance.Id.Value,
            IdempotencyKey: Convert.ToBase64String(instance.IdempotencyKey),
            TransactionId: instance.TransactionId.Value,
            MeteringPointId: instance.MeteringPointId?.Value,
            CreatedAt: instance.CreatedAt.ToDateTimeOffset(),
            BusinessValidationSucceededAt: instance.BusinessValidationSucceededAt?.ToDateTimeOffset(),
            ValidationErrors: instance.ValidationErrors.AsExpandoObject(),
            SentToMeasurementsAt: instance.SentToMeasurementsAt?.ToDateTimeOffset(),
            ReceivedFromMeasurementsAt: instance.ReceivedFromMeasurementsAt?.ToDateTimeOffset(),
            SentToEnqueueActorMessagesAt: instance.SentToEnqueueActorMessagesAt?.ToDateTimeOffset(),
            ReceivedFromEnqueueActorMessagesAt: instance.ReceivedFromEnqueueActorMessagesAt?.ToDateTimeOffset(),
            TerminatedAt: instance.TerminatedAt?.ToDateTimeOffset(),
            FailedAt: instance.FailedAt?.ToDateTimeOffset());
    }
}
