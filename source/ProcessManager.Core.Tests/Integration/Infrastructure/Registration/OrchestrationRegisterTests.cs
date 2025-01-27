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

using Energinet.DataHub.ProcessManager.Core.Application.Registration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Registration;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using NJsonSchema;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Integration.Infrastructure.Registration;

public class OrchestrationRegisterTests : IClassFixture<ProcessManagerCoreFixture>, IAsyncLifetime
{
    private readonly ProcessManagerCoreFixture _fixture;

    public OrchestrationRegisterTests(ProcessManagerCoreFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        using var context = _fixture.DatabaseManager.CreateDbContext();
        await context.Database.ExecuteSqlAsync($"DELETE FROM [pm].[StepDescription]");
        await context.Database.ExecuteSqlAsync($"DELETE FROM [pm].[OrchestrationDescription]");
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task
        Given_ExistingOrchestrationDescription_AndGiven_AllowBreakingChanges_When_OrchestrationDescriptionsSynchronized_PropertiesAreUpdated()
    {
        // Given existing orchestration description & allow breaking changes
        var optionsMock = new Mock<IOptions<ProcessManagerOptions>>();
        var hostName = "test-host";

        optionsMock
            .Setup(o => o.Value)
            .Returns(new ProcessManagerOptions
            {
                AllowOrchestrationDescriptionBreakingChanges = true,
            });

        var uniqueName = new OrchestrationDescriptionUniqueName("TestOrchestration", 1);
        var existingOrchestrationDescription = new OrchestrationDescription(
            uniqueName,
            functionName: "TestFunctionV1",
            canBeScheduled: true);

        existingOrchestrationDescription.HostName = hostName;
        existingOrchestrationDescription.ParameterDefinition.SetFromType<ParameterDefitionString>();
        existingOrchestrationDescription.AppendStepDescription("Step 1a");
        existingOrchestrationDescription.AppendStepDescription("Step 2a");

        using (var createContext = _fixture.DatabaseManager.CreateDbContext())
        {
            createContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            await createContext.SaveChangesAsync();
        }

        // When syncronized
        using (var updateContext = _fixture.DatabaseManager.CreateDbContext())
        {
            var orchestrationRegister = new OrchestrationRegister(
                optionsMock.Object,
                updateContext);

            var orchestrationDescriptionWithBreakingChanges = new OrchestrationDescription(
                uniqueName,
                functionName: "TestFunctionV2",
                canBeScheduled: false);

            orchestrationDescriptionWithBreakingChanges.ParameterDefinition.SetFromType<ParameterDefitionInt>();
            orchestrationDescriptionWithBreakingChanges.AppendStepDescription("Step 1b");
            orchestrationDescriptionWithBreakingChanges.AppendStepDescription("Step 2b");
            orchestrationDescriptionWithBreakingChanges.AppendStepDescription("Step 3b");

            await orchestrationRegister.SynchronizeAsync(
                hostName,
                newDescriptions: [orchestrationDescriptionWithBreakingChanges]);

            await updateContext.SaveChangesAsync();
        }

        // Then existing orchestration description is updated
        using var queryContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationDescription = queryContext.OrchestrationDescriptions.Single(od => od.UniqueName == uniqueName);

        Assert.Equal(expected: existingOrchestrationDescription.Id, actual: actualOrchestrationDescription.Id);
        Assert.Equal(expected: "TestFunctionV2", actual: actualOrchestrationDescription.FunctionName);
        Assert.False(actualOrchestrationDescription.CanBeScheduled);
        Assert.Equal(
            expected: JsonSchema.FromType<ParameterDefitionInt>().ToJson(),
            actual: actualOrchestrationDescription.ParameterDefinition.SerializedParameterDefinition);
        Assert.Collection(
            collection: actualOrchestrationDescription.Steps.OrderBy(s => s.Sequence),
            elementInspectors:
            [
                step =>
                {
                    Assert.Equal(1, step.Sequence);
                    Assert.Equal("Step 1b", step.Description);
                },
                step =>
                {
                    Assert.Equal(2, step.Sequence);
                    Assert.Equal("Step 2b", step.Description);
                },
                step =>
                {
                    Assert.Equal(3, step.Sequence);
                    Assert.Equal("Step 3b", step.Description);
                },
            ]);
    }

    [Fact]
    public async Task
        Given_ExistingOrchestrationDescription_AndGiven_DisallowBreakingChanges_When_OrchestrationDescriptionsSynchronized_ExceptionIsThrown()
    {
        // Given existing orchestration description & allow breaking changes
        var optionsMock = new Mock<IOptions<ProcessManagerOptions>>();
        var hostName = "test-host";

        optionsMock
            .Setup(o => o.Value)
            .Returns(new ProcessManagerOptions
            {
                AllowOrchestrationDescriptionBreakingChanges = false,
            });

        var uniqueName = new OrchestrationDescriptionUniqueName("TestOrchestration", 1);
        var existingOrchestrationDescription = new OrchestrationDescription(
            uniqueName,
            functionName: "TestFunctionV1",
            canBeScheduled: true);

        existingOrchestrationDescription.HostName = hostName;
        existingOrchestrationDescription.ParameterDefinition.SetFromType<ParameterDefitionString>();
        existingOrchestrationDescription.AppendStepDescription("Step 1a");
        existingOrchestrationDescription.AppendStepDescription("Step 2a");

        using (var createContext = _fixture.DatabaseManager.CreateDbContext())
        {
            createContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            await createContext.SaveChangesAsync();
        }

        // When syncronized
        using (var updateContext = _fixture.DatabaseManager.CreateDbContext())
        {
            var orchestrationRegister = new OrchestrationRegister(
                optionsMock.Object,
                updateContext);

            var orchestrationDescriptionWithBreakingChanges = new OrchestrationDescription(
                uniqueName,
                functionName: "TestFunctionV2",
                canBeScheduled: false);

            orchestrationDescriptionWithBreakingChanges.ParameterDefinition.SetFromType<ParameterDefitionInt>();
            orchestrationDescriptionWithBreakingChanges.AppendStepDescription("Step 1b");
            orchestrationDescriptionWithBreakingChanges.AppendStepDescription("Step 2b");
            orchestrationDescriptionWithBreakingChanges.AppendStepDescription("Step 3b");

            var act = () => orchestrationRegister.SynchronizeAsync(
                hostName,
                newDescriptions: [orchestrationDescriptionWithBreakingChanges]);

            await Assert.ThrowsAsync<InvalidOperationException>(act);
            await updateContext.SaveChangesAsync();
        }

        // Then existing orchestration description is updated
        using var queryContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationDescription = queryContext.OrchestrationDescriptions.Single(od => od.UniqueName == uniqueName);

        Assert.Equal(expected: existingOrchestrationDescription.Id, actual: actualOrchestrationDescription.Id);
        Assert.Equal(expected: "TestFunctionV1", actual: actualOrchestrationDescription.FunctionName);
        Assert.True(actualOrchestrationDescription.CanBeScheduled);
        Assert.Equal(
            expected: JsonSchema.FromType<ParameterDefitionString>().ToJson(),
            actual: actualOrchestrationDescription.ParameterDefinition.SerializedParameterDefinition);
        Assert.Collection(
            collection: actualOrchestrationDescription.Steps.OrderBy(s => s.Sequence),
            elementInspectors:
            [
                step =>
                {
                    Assert.Equal(1, step.Sequence);
                    Assert.Equal("Step 1a", step.Description);
                },
                step =>
                {
                    Assert.Equal(2, step.Sequence);
                    Assert.Equal("Step 2a", step.Description);
                },
            ]);
    }

    private record ParameterDefitionString(string StringProperty);

    private record ParameterDefitionInt(int IntProperty);
}
