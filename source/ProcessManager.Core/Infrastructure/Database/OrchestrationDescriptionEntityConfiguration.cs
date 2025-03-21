﻿// Copyright 2020 Energinet DataHub A/S
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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;

internal class OrchestrationDescriptionEntityConfiguration : IEntityTypeConfiguration<OrchestrationDescription>
{
    public void Configure(EntityTypeBuilder<OrchestrationDescription> builder)
    {
        builder.ToTable("OrchestrationDescription");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                dbValue => new OrchestrationDescriptionId(dbValue));

        builder.ComplexProperty(
            o => o.UniqueName,
            b =>
            {
                b.Property(un => un.Name)
                    .HasColumnName(nameof(OrchestrationDescriptionUniqueName.Name));
                b.Property(un => un.Version)
                    .HasColumnName(nameof(OrchestrationDescriptionUniqueName.Version));
            });

        builder.Property(o => o.CanBeScheduled);
        builder.Property(o => o.RecurringCronExpression);
        builder.Property(o => o.FunctionName);

        builder.ComplexProperty(
            o => o.ParameterDefinition,
            b =>
            {
                b.Property(pd => pd.SerializedParameterDefinition)
                    .HasColumnName(nameof(OrchestrationDescription.ParameterDefinition.SerializedParameterDefinition));
            });

        builder.Property(o => o.HostName);
        builder.Property(o => o.IsEnabled);
        builder.Property(o => o.RowVersion)
            .IsRowVersion();

        builder.Property(o => o.IsUnderDevelopment);

        builder.OwnsMany<StepDescription>(
            OrchestrationDescription.StepsPrivatePropertyName,
            b =>
            {
                b.ToTable("StepDescription");

                b.HasKey(s => s.Id);
                b.Property(s => s.Id)
                    .ValueGeneratedNever()
                    .HasConversion(
                        id => id.Value,
                        dbValue => new StepDescriptionId(dbValue));

                b.Property(s => s.Description);
                b.Property(s => s.Sequence);

                b.Property(s => s.CanBeSkipped);
                b.Property(s => s.SkipReason);

                b.Property(o => o.RowVersion)
                    .IsRowVersion();

                // Relation to parent
                b.Property(s => s.OrchestrationDescriptionId)
                    .HasConversion(
                        id => id.Value,
                        dbValue => new OrchestrationDescriptionId(dbValue));

                b.WithOwner().HasForeignKey(s => s.OrchestrationDescriptionId);
            });
        builder.Ignore(o => o.Steps); // Steps is a get-only property for the private _steps collection.
        builder.Ignore(o => o.IsDurableFunction);
    }
}
