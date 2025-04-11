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
using Microsoft.Extensions.Logging;
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
        await _fixture.DatabaseManager.ExecuteDeleteOnEntitiesAsync();
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Given_NewOrchestrationDescription_When_OrchestrationDescriptionsSynchronized_Then_IsAddedToDatabase()
    {
        // Given new orchestration description
        var loggerMock = new Mock<ILogger<IOrchestrationRegister>>();
        var optionsMock = new Mock<IOptions<ProcessManagerOptions>>();
        const string hostName = "test-host";

        optionsMock
            .Setup(o => o.Value)
            .Returns(new ProcessManagerOptions
            {
                AllowOrchestrationDescriptionBreakingChanges = false,
            });

        var uniqueName = new OrchestrationDescriptionUniqueName("TestOrchestration", 1);
        const string functionName = "TestFunctionV1";
        var newOrchestrationDescription = new OrchestrationDescription(
            uniqueName,
            functionName: functionName,
            canBeScheduled: true);

        newOrchestrationDescription.IsUnderDevelopment = true;

        newOrchestrationDescription.ParameterDefinition.SetFromType<ParameterDefinitionString>();

        const string step1Description = "Step 1";
        const string step2Description = "Step 2";
        newOrchestrationDescription.AppendStepDescription(step1Description);
        newOrchestrationDescription.AppendStepDescription(step2Description);

        // When synchronized
        await using (var updateContext = _fixture.DatabaseManager.CreateDbContext())
        {
            var orchestrationRegister = new OrchestrationRegister(
                optionsMock.Object,
                loggerMock.Object,
                updateContext);

            // Save changes is called inside SynchronizeAsync()
            await orchestrationRegister.SynchronizeAsync(
                hostName,
                newDescriptions: [newOrchestrationDescription]);
        }

        // Then is added to database
        await using var queryContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationDescription = queryContext.OrchestrationDescriptions.Single();

        Assert.Equal(expected: uniqueName, actual: actualOrchestrationDescription.UniqueName);
        Assert.Equal(expected: newOrchestrationDescription.Id, actual: actualOrchestrationDescription.Id);
        Assert.Equal(expected: functionName, actual: actualOrchestrationDescription.FunctionName);
        Assert.True(actualOrchestrationDescription.CanBeScheduled);
        Assert.True(actualOrchestrationDescription.IsUnderDevelopment);
        Assert.Equal(
            expected: JsonSchema.FromType<ParameterDefinitionString>().ToJson(),
            actual: actualOrchestrationDescription.ParameterDefinition.SerializedParameterDefinition);
        Assert.Collection(
            collection: actualOrchestrationDescription.Steps.OrderBy(s => s.Sequence),
            elementInspectors:
            [
                step =>
                {
                    Assert.Equal(1, step.Sequence);
                    Assert.Equal(step1Description, step.Description);
                },
                step =>
                {
                    Assert.Equal(2, step.Sequence);
                    Assert.Equal(step2Description, step.Description);
                },
            ]);
    }

    [Fact]
    public async Task
        Given_ExistingOrchestrationDescription_AndGiven_AllowBreakingChanges_When_OrchestrationDescriptionsSynchronized_Then_PropertiesAreUpdated()
    {
        // Given existing orchestration description & allow breaking changes
        var loggerMock = new Mock<ILogger<IOrchestrationRegister>>();
        var optionsMock = new Mock<IOptions<ProcessManagerOptions>>();
        const string hostName = "test-host";

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
            canBeScheduled: true) { HostName = hostName };

        existingOrchestrationDescription.ParameterDefinition.SetFromType<ParameterDefinitionString>();
        existingOrchestrationDescription.AppendStepDescription("Step 1a");
        existingOrchestrationDescription.AppendStepDescription("Step 2a");

        await using (var createContext = _fixture.DatabaseManager.CreateDbContext())
        {
            createContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            await createContext.SaveChangesAsync();
        }

        // When synchronized
        const string functionNameBreakingChange = "TestFunctionBreakingChangeV1";
        await using (var updateContext = _fixture.DatabaseManager.CreateDbContext())
        {
            var orchestrationRegister = new OrchestrationRegister(
                optionsMock.Object,
                loggerMock.Object,
                updateContext);

            var orchestrationDescriptionWithBreakingChanges = new OrchestrationDescription(
                uniqueName,
                functionName: functionNameBreakingChange,
                canBeScheduled: false);

            orchestrationDescriptionWithBreakingChanges.ParameterDefinition.SetFromType<ParameterDefinitionInt>();
            orchestrationDescriptionWithBreakingChanges.AppendStepDescription("Step 1b");
            orchestrationDescriptionWithBreakingChanges.AppendStepDescription("Step 2b");

            // Save changes is called inside SynchronizeAsync()
            await orchestrationRegister.SynchronizeAsync(
                hostName,
                newDescriptions: [orchestrationDescriptionWithBreakingChanges]);
        }

        // Then existing orchestration description is updated
        await using var queryContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationDescription = queryContext.OrchestrationDescriptions.Single(od => od.UniqueName == uniqueName);

        Assert.Equal(expected: existingOrchestrationDescription.Id, actual: actualOrchestrationDescription.Id);
        Assert.Equal(expected: functionNameBreakingChange, actual: actualOrchestrationDescription.FunctionName);
        Assert.False(actualOrchestrationDescription.CanBeScheduled);
        Assert.False(actualOrchestrationDescription.IsUnderDevelopment);
        Assert.Equal(
            expected: JsonSchema.FromType<ParameterDefinitionInt>().ToJson(),
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
            ]);
    }

    [Fact]
    public async Task
        Given_ExistingOrchestrationDescription_AndGiven_IsUnderDevelopment_When_OrchestrationDescriptionsSynchronized_Then_PropertiesAreUpdated()
    {
        // Given existing orchestration description
        var loggerMock = new Mock<ILogger<IOrchestrationRegister>>();
        var optionsMock = new Mock<IOptions<ProcessManagerOptions>>();
        const string hostName = "test-host";

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
            canBeScheduled: true) { HostName = hostName, };

        existingOrchestrationDescription.ParameterDefinition.SetFromType<ParameterDefinitionString>();
        existingOrchestrationDescription.AppendStepDescription("Step 1a");
        existingOrchestrationDescription.AppendStepDescription("Step 2a");

        await using (var createContext = _fixture.DatabaseManager.CreateDbContext())
        {
            createContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            await createContext.SaveChangesAsync();
        }

        // When synchronized
        const string functionNameBreakingChange = "TestFunctionBreakingChangeV1";
        await using (var updateContext = _fixture.DatabaseManager.CreateDbContext())
        {
            var orchestrationRegister = new OrchestrationRegister(
                optionsMock.Object,
                loggerMock.Object,
                updateContext);

            var orchestrationDescriptionUnderDevelopment = new OrchestrationDescription(
                uniqueName,
                functionName: functionNameBreakingChange,
                canBeScheduled: false) { IsUnderDevelopment = true, };

            orchestrationDescriptionUnderDevelopment.ParameterDefinition.SetFromType<ParameterDefinitionInt>();
            orchestrationDescriptionUnderDevelopment.AppendStepDescription("Step 1b");
            orchestrationDescriptionUnderDevelopment.AppendStepDescription("Step 2b");

            // Save changes is called inside SynchronizeAsync()
            await orchestrationRegister.SynchronizeAsync(
                hostName,
                newDescriptions: [orchestrationDescriptionUnderDevelopment]);
        }

        // Then existing orchestration description is updated
        await using var queryContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationDescription = queryContext.OrchestrationDescriptions.Single(od => od.UniqueName == uniqueName);

        Assert.Equal(expected: existingOrchestrationDescription.Id, actual: actualOrchestrationDescription.Id);
        Assert.Equal(expected: functionNameBreakingChange, actual: actualOrchestrationDescription.FunctionName);
        Assert.True(actualOrchestrationDescription.IsUnderDevelopment);
        Assert.False(actualOrchestrationDescription.CanBeScheduled);
        Assert.Equal(
            expected: JsonSchema.FromType<ParameterDefinitionInt>().ToJson(),
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
            ]);
    }

    [Fact]
    public async Task
        Given_ExistingOrchestrationDescription_AndGiven_DisallowBreakingChanges_When_OrchestrationDescriptionsSynchronized_Then_ExceptionIsThrown()
    {
        // Given existing orchestration description
        var loggerMock = new Mock<ILogger<IOrchestrationRegister>>();
        var optionsMock = new Mock<IOptions<ProcessManagerOptions>>();
        const string hostName = "test-host";

        optionsMock
            .Setup(o => o.Value)
            .Returns(new ProcessManagerOptions
            {
                AllowOrchestrationDescriptionBreakingChanges = false,
            });

        var uniqueName = new OrchestrationDescriptionUniqueName("TestOrchestration", 1);
        const string functionName = "TestFunctionV1";
        var existingOrchestrationDescription = new OrchestrationDescription(
            uniqueName,
            functionName: functionName,
            canBeScheduled: true) { HostName = hostName };

        existingOrchestrationDescription.ParameterDefinition.SetFromType<ParameterDefinitionString>();
        existingOrchestrationDescription.AppendStepDescription("Step 1a");
        existingOrchestrationDescription.AppendStepDescription("Step 2a");

        await using (var createContext = _fixture.DatabaseManager.CreateDbContext())
        {
            createContext.OrchestrationDescriptions.Add(existingOrchestrationDescription);
            await createContext.SaveChangesAsync();
        }

        // When synchronized
        await using (var updateContext = _fixture.DatabaseManager.CreateDbContext())
        {
            var orchestrationRegister = new OrchestrationRegister(
                optionsMock.Object,
                loggerMock.Object,
                updateContext);

            var orchestrationDescriptionWithBreakingChanges = new OrchestrationDescription(
                uniqueName,
                functionName: "TestFunctionBreakingChangeV1",
                canBeScheduled: false);

            orchestrationDescriptionWithBreakingChanges.ParameterDefinition.SetFromType<ParameterDefinitionInt>();
            orchestrationDescriptionWithBreakingChanges.AppendStepDescription("Step 1b");
            orchestrationDescriptionWithBreakingChanges.AppendStepDescription("Step 2b");
            orchestrationDescriptionWithBreakingChanges.AppendStepDescription("Step 3b");

            var act = () => orchestrationRegister.SynchronizeAsync(
                hostName,
                newDescriptions: [orchestrationDescriptionWithBreakingChanges]);

            await Assert.ThrowsAsync<InvalidOperationException>(act);
            await updateContext.SaveChangesAsync();
        }

        // Then existing orchestration description is NOT updated
        await using var queryContext = _fixture.DatabaseManager.CreateDbContext();
        var actualOrchestrationDescription = queryContext.OrchestrationDescriptions.Single(od => od.UniqueName == uniqueName);

        Assert.Equal(expected: existingOrchestrationDescription.Id, actual: actualOrchestrationDescription.Id);
        Assert.Equal(expected: functionName, actual: actualOrchestrationDescription.FunctionName);
        Assert.True(actualOrchestrationDescription.CanBeScheduled);
        Assert.Equal(
            expected: JsonSchema.FromType<ParameterDefinitionString>().ToJson(),
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

    private record ParameterDefinitionString(string StringProperty);

    private record ParameterDefinitionInt(int IntProperty);
}
