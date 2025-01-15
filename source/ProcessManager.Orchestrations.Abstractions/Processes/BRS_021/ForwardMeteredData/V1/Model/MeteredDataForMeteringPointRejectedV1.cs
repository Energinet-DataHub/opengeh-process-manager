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

using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;

public sealed record MeteredDataForMeteringPointRejectedV1(
    string EventId,
    string BusinessReason,
    string ReceiverId,
    string ReceiverRole,
    Guid ProcessId,
    Guid ExternalId,
    AcknowledgementV1 AcknowledgementV1);

public sealed record AcknowledgementV1(
    string TransactionId,
    Instant? ReceivedMarketDocumentCreatedDateTime,
    string? ReceivedMarketDocumentTransactionId,
    string? ReceivedMarketDocumentProcessProcessType,
    string? ReceivedMarketDocumentRevisionNumber,
    string? ReceivedMarketDocumentTitle,
    string? ReceivedMarketDocumentType,
    IReadOnlyCollection<ReasonV1> Reason,
    IReadOnlyCollection<TimePeriodV1> InErrorPeriod,
    IReadOnlyCollection<SeriesV1> Series,
    IReadOnlyCollection<MktActivityRecordV1> OriginalMktActivityRecord,
    IReadOnlyCollection<TimeSeriesV1> RejectedTimeSeries);

public sealed record ReasonV1(string Code, string? Text);

public sealed record TimePeriodV1(Interval TimeInterval, IReadOnlyCollection<ReasonV1> Reason);

public sealed record SeriesV1(string MRID, IReadOnlyCollection<ReasonV1> Reason);

public sealed record MktActivityRecordV1(string MRID, IReadOnlyCollection<ReasonV1> Reason);

public sealed record TimeSeriesV1(
    string MRID,
    string Version,
    IReadOnlyCollection<TimePeriodV1> InErrorPeriod,
    IReadOnlyCollection<ReasonV1> Reason);
