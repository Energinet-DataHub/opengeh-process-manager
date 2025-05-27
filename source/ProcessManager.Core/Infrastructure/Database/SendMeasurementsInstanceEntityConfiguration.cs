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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;

internal class SendMeasurementsInstanceEntityConfiguration : IEntityTypeConfiguration<SendMeasurementsInstance>
{
    public void Configure(EntityTypeBuilder<SendMeasurementsInstance> builder)
    {
        builder.ToTable("SendMeasurementsInstance");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                dbValue => new SendMeasurementsInstanceId(dbValue));

        builder.Property(o => o.RowVersion)
            .IsRowVersion();

        builder.Property(o => o.IdempotencyKey)
            .HasColumnType("BINARY(32)");

        builder.Property(o => o.CreatedAt);
        builder.Property(o => o.CreatedByActorNumber)
            .HasConversion(
                state => state.Value,
                dbValue => ActorNumber.Create(dbValue));
        builder.Property(o => o.CreatedByActorRole)
            .HasConversion(
                state => state.ByteValue,
                dbValue => ActorRole.FromByteValue(dbValue));

        builder.Property(o => o.TransactionId)
            .HasConversion(
                state => state.Value,
                dbValue => new TransactionId(dbValue));

        builder.Property(o => o.MeteringPointId)
            .HasConversion(
                state => state != null
                    ? state.Value
                    : null,
                dbValue => dbValue != null
                    ? new MeteringPointId(dbValue)
                    : null);

        builder.Property(o => o.SentToMeasurementsAt);
    }
}
