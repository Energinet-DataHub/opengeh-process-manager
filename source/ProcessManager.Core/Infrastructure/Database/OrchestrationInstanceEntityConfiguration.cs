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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;

internal class OrchestrationInstanceEntityConfiguration : IEntityTypeConfiguration<OrchestrationInstance>
{
    public void Configure(EntityTypeBuilder<OrchestrationInstance> builder)
    {
        builder.ToTable("OrchestrationInstance");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                dbValue => new OrchestrationInstanceId(dbValue));

        // Unfortunately we cannot use 'ComplexProperty' as it doesn't yet support nullability
        // and we need to be able to set 'CanceledBy' as null.
        builder.OwnsOne(
            o => o.Lifecycle,
            b =>
            {
                b.Property(l => l.State);
                b.Property(l => l.TerminationState);

                b.OwnsOne(
                    l => l.CreatedBy,
                    lb =>
                    {
                        lb.Ignore(ct => ct.Value);

                        lb.Property(ct => ct.IdentityType);

                        lb.Property(ct => ct.ActorNumber)
                            .HasConversion(
                                actorNumber => actorNumber != null ? actorNumber.Value : null,
                                dbValue => dbValue != null ? ActorNumber.Create(dbValue) : null);

                        lb.Property(ct => ct.ActorRole)
                            .HasConversion(
                                actorRole => actorRole != null ? actorRole.Name : null,
                                dbValue => dbValue != null ? ActorRole.FromName(dbValue) : null);

                        lb.Property(ct => ct.UserId);
                    });

                b.Property(l => l.CreatedAt);
                b.Property(l => l.ScheduledToRunAt);
                b.Property(l => l.QueuedAt);
                b.Property(l => l.StartedAt);
                b.Property(l => l.TerminatedAt);

                b.OwnsOne(
                    l => l.CanceledBy,
                    lb =>
                    {
                        lb.Ignore(ct => ct.Value);

                        lb.Property(ct => ct.IdentityType);

                        lb.Property(ct => ct.ActorNumber)
                            .HasConversion(
                                actorNumber => actorNumber != null ? actorNumber.Value : null,
                                dbValue => dbValue != null ? ActorNumber.Create(dbValue) : null);

                        lb.Property(ct => ct.ActorRole)
                            .HasConversion(
                                actorRole => actorRole != null ? actorRole.Name : null,
                                dbValue => dbValue != null ? ActorRole.FromName(dbValue) : null);

                        lb.Property(ct => ct.UserId);
                    });
            });

        builder.ComplexProperty(
            o => o.ParameterValue,
            b =>
            {
                b.Property(l => l.SerializedParameterValue)
                    .HasColumnName(nameof(OrchestrationInstance.ParameterValue.SerializedParameterValue));
            });

        builder.OwnsMany(
            o => o.Steps,
            b =>
            {
                b.ToTable("StepInstance");

                b.HasKey(s => s.Id);
                b.Property(s => s.Id)
                    .ValueGeneratedNever()
                    .HasConversion(
                        id => id.Value,
                        dbValue => new StepInstanceId(dbValue));

                b.OwnsOne(
                    o => o.Lifecycle,
                    lb =>
                    {
                        lb.Property(l => l.State);
                        lb.Property(l => l.TerminationState);

                        lb.Property(l => l.StartedAt);
                        lb.Property(l => l.TerminatedAt);

                        lb.Property(l => l.CanBeSkipped);
                    });

                b.Property(s => s.Description);
                b.Property(s => s.Sequence);

                // Cannot use .ComplexProperty() in a nested configuration builder, so we have to use .OwnsOne().
                b.OwnsOne(
                    s => s.CustomState,
                    csb =>
                    {
                        csb.Property(cs => cs.Value)
                            .HasColumnName(nameof(StepInstance.CustomState));
                    });

                // Relation to parent
                b.Property(s => s.OrchestrationInstanceId)
                    .HasConversion(
                        id => id.Value,
                        dbValue => new OrchestrationInstanceId(dbValue));

                b.WithOwner().HasForeignKey(s => s.OrchestrationInstanceId);
            });

        builder.ComplexProperty(
            o => o.CustomState,
            csb =>
            {
                csb.Property(cs => cs.SerializedValue)
                    .HasColumnName(nameof(OrchestrationInstance.CustomState));
            });

        builder.Property(o => o.IdempotencyKey)
            .HasConversion(
                state => state == null
                    ? null
                    : state.Value,
                dbValue => dbValue == null
                    ? null
                    : new IdempotencyKey(dbValue));

        builder.Property(o => o.RowVersion)
            .IsRowVersion();

        // Relation to description
        builder.Property(o => o.OrchestrationDescriptionId)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                dbValue => new OrchestrationDescriptionId(dbValue));

        builder.Property(o => o.ActorMessageId)
            .HasConversion(
                state => state == null
                    ? null
                    : state.Value,
                dbValue => dbValue == null
                    ? null
                    : new ActorMessageId(dbValue));

        builder.Property(o => o.TransactionId)
            .HasConversion(
                state => state == null
                    ? null
                    : state.Value,
                dbValue => dbValue == null
                    ? null
                    : new TransactionId(dbValue));

        builder.Property(o => o.MeteringPointId)
            .HasConversion(
                state => state == null
                    ? null
                    : state.Value,
                dbValue => dbValue == null
                    ? null
                    : new MeteringPointId(dbValue));
    }
}
