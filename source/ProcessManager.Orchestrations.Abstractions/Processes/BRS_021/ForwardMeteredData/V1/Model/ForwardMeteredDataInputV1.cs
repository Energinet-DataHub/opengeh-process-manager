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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;

/// <summary>
/// An immutable input to start the orchestration instance for "BRS_021_ForwardMeteredData" V1.
/// </summary>
public record ForwardMeteredDataInputV1(
    string ActorMessageId,
    string TransactionId,
    string ActorNumber,
    string ActorRole,
    string BusinessReason,
    string? MeteringPointId,
    string? MeteringPointType,
    string? ProductNumber,
    string? MeasureUnit,
    string RegistrationDateTime,
    string? Resolution,
    string StartDateTime,
    string? EndDateTime,
    string GridAccessProviderNumber,
    IReadOnlyCollection<ForwardMeteredDataInputV1.MeteredData> MeteredDataList,
    ForwardMeteredDataInputV1.DataSourceEnum DataSource = ForwardMeteredDataInputV1.DataSourceEnum.ActorSystem)
    : IInputParameterDto
{
    /// <summary>
    /// Specifies the channel through which we received the data.
    /// </summary>
    public enum DataSourceEnum
    {
        /// <summary>
        /// Data was send the traditional way, from the actor's system to EDI.
        /// </summary>
        ActorSystem = 0,

        /// <summary>
        /// Data was send by a trigger that listen's for data from the Migration subsystem.
        /// </summary>
        MigrationSubsystem = 1,
    }

    public record MeteredData(
        string? Position,
        string? EnergyQuantity,
        string? QuantityQuality);
}
