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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Application.Registration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Examples.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.CustomQueries.Examples.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using ApiModel = Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.CustomQueries.Examples.V1;

public class SearchExamplesHandlerV1Tests :
    IClassFixture<ProcessManagerDatabaseFixture>,
    IAsyncLifetime
{
    private readonly ProcessManagerDatabaseFixture _fixture;
    private readonly ProcessManagerReaderContext _readerContext;
    private readonly SearchExamplesHandlerV1 _sut;

    private readonly UserIdentityDto _userIdentity = new UserIdentityDto(
        UserId: Guid.NewGuid(),
        ActorNumber: ActorNumber.Create("1111111111111"),
        ActorRole: ActorRole.DataHubAdministrator);

    private readonly IOrchestrationDescriptionBuilder _inputExampleDescriptionBuilder = new
        Orchestrations.Processes.BRS_X01.InputExample.V1
        .Orchestration.OrchestrationDescriptionBuilder();

    private readonly IOrchestrationDescriptionBuilder _noInputExampleDescriptionBuilder = new
        Orchestrations.Processes.BRS_X01.NoInputExample.V1
        .Orchestration.OrchestrationDescriptionBuilder();

    public SearchExamplesHandlerV1Tests(ProcessManagerDatabaseFixture fixture)
    {
        _fixture = fixture;
        _readerContext = fixture.DatabaseManager.CreateDbContext<ProcessManagerReaderContext>();

        _sut = new SearchExamplesHandlerV1(_readerContext);
    }

    public async Task InitializeAsync()
    {
        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        await dbContext.OrchestrationInstances.ExecuteDeleteAsync();
        await dbContext.OrchestrationDescriptions.ExecuteDeleteAsync();
    }

    public async Task DisposeAsync()
    {
        await _readerContext.DisposeAsync();
    }

    [Fact]
    public async Task Given_LifecycleDatasetInDatabase_When_SearchByPending_Then_ExpectedExamplesAreRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var inputExample = await SeedDatabaseWithInputExamplesDatasetAsync();
        var noInputExample = await SeedDatabaseWithLifecycleDatasetAsync(_noInputExampleDescriptionBuilder);

        // When
        var query = new ExamplesQueryV1(_userIdentity)
        {
            LifecycleStates = [ApiModel.OrchestrationInstanceLifecycleState.Pending],
        };

        var actual = await _sut.HandleAsync(query);

        // Assert
        actual.Should()
            .HaveCount(3)
            .And.Satisfy(
                result => result is InputExampleResultV1 && ((InputExampleResultV1)result).Id == inputExample.Skipped.Id.Value,
                result => result is InputExampleResultV1 && ((InputExampleResultV1)result).Id == inputExample.NotSkipped.Id.Value,
                result => result is NoInputExampleResultV1 && ((NoInputExampleResultV1)result).Id == noInputExample.IsPending.Id.Value);
    }

    /// <summary>
    /// The intention of this test is to use as much as possible of the query in SQL.
    /// </summary>
    [Fact]
    public async Task Given_MixOfExamplesDataset_When_SearchByAllCommonQueryParameters_Then_ExpectedExamplesAreRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var inputExample = await SeedDatabaseWithInputExamplesDatasetAsync();
        var noInputExample = await SeedDatabaseWithLifecycleDatasetAsync(_noInputExampleDescriptionBuilder);

        // When
        var query = new ExamplesQueryV1(_userIdentity)
        {
            // => Common fields
            ExampleTypes = [
                ExampleTypeQueryParameterV1.Input,
                ExampleTypeQueryParameterV1.NoInput],
            LifecycleStates = [
                ApiModel.OrchestrationInstanceLifecycleState.Pending,
                ApiModel.OrchestrationInstanceLifecycleState.Running],
            TerminationState = null,
            ScheduledAtOrLater = null,
            StartedAtOrLater = new DateTimeOffset(2020, 2, 22, 23, 00, 00, TimeSpan.Zero), // Wintertime
            TerminatedAtOrEarlier = null,
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .HaveCount(1)
            .And.Satisfy(
                result => result is NoInputExampleResultV1 && ((NoInputExampleResultV1)result).Id == noInputExample.IsRunning.Id.Value);
    }

    /// <summary>
    /// We also seed database with the John Doe dataset, to ensure the JSON search doesn't
    /// cause exceptions if there isn't JSON in the columns.
    /// </summary>
    [Fact]
    public async Task Given_SkippedInputExample_When_SearchBySkippedStepTwo_Then_SkippedIsRetrieved()
    {
        // Given
        await SeedDatabaseWithJohnDoeLifecycleDatasetAsync();
        var orchestrationInstances = await SeedDatabaseWithInputExamplesDatasetAsync();

        // When
        var query = new ExamplesQueryV1(_userIdentity)
        {
            SkippedStepTwo = true,
        };

        var actual = await _sut.HandleAsync(query);

        // Then
        actual
            .Should()
            .HaveCount(1)
            .And.Satisfy(
                result => result is InputExampleResultV1 && ((InputExampleResultV1)result).Id == orchestrationInstances.Skipped.Id.Value);
    }

    private async Task<(
            OrchestrationInstance Skipped,
            OrchestrationInstance NotSkipped)>
        SeedDatabaseWithInputExamplesDatasetAsync()
    {
        var orchestrationDescription = _inputExampleDescriptionBuilder.Build();

        var skipped = CreateInputExample(
            orchestrationDescription,
            new Abstractions.Processes.BRS_X01.InputExample.V1.Model.InputV1(
                ShouldSkipSkippableStep: true));

        var notSkipped = CreateInputExample(
            orchestrationDescription,
            new Abstractions.Processes.BRS_X01.InputExample.V1.Model.InputV1(
                ShouldSkipSkippableStep: false));

        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        dbContext.OrchestrationDescriptions.Add(orchestrationDescription);
        dbContext.OrchestrationInstances.Add(skipped);
        dbContext.OrchestrationInstances.Add(notSkipped);
        await dbContext.SaveChangesAsync();

        return (skipped, notSkipped);
    }

    private OrchestrationInstance CreateInputExample(
        OrchestrationDescription orchestrationDescription,
        Abstractions.Processes.BRS_X01.InputExample.V1.Model.InputV1 input)
    {
        var orchestrationInstance = OrchestrationInstance.CreateFromDescription(
            identity: _userIdentity.MapToDomain(),
            description: orchestrationDescription,
            skipStepsBySequence: [],
            clock: SystemClock.Instance);

        orchestrationInstance.ParameterValue.SetFromInstance(input);

        return orchestrationInstance;
    }

    /// <summary>
    /// Create an orchestration description using the given builder.
    /// Create orchestration instances in the following lifecycle states:
    ///  - Pending
    ///  - Queued
    ///  - Running
    ///  - Terminated as succeeded
    ///  - Terminated as failed
    ///
    /// If <paramref name="isRunningStartedAt"/> is specified, then this value
    /// is used when transitioning to Running.
    ///
    /// If <paramref name="isTerminatedAsSucceededAt"/> is specified, then this value
    /// is used when transitioning to terminated as succeeded.
    /// </summary>
    private async Task<(
            OrchestrationInstance IsPending,
            OrchestrationInstance IsQueued,
            OrchestrationInstance IsRunning,
            OrchestrationInstance IsTerminatedAsSucceeded,
            OrchestrationInstance IsTerminatedAsFailed)>
        SeedDatabaseWithLifecycleDatasetAsync(
            IOrchestrationDescriptionBuilder builder,
            Instant isRunningStartedAt = default,
            Instant isTerminatedAsSucceededAt = default)
    {
        var orchestrationDescription = builder.Build();
        var orchestrationInstances = DomainTestDataFactory.CreateLifecycleDataset(
            orchestrationDescription,
            isRunningStartedAt,
            isTerminatedAsSucceededAt);

        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        dbContext.OrchestrationDescriptions.Add(orchestrationDescription);
        dbContext.OrchestrationInstances.Add(orchestrationInstances.IsPending);
        dbContext.OrchestrationInstances.Add(orchestrationInstances.IsQueued);
        dbContext.OrchestrationInstances.Add(orchestrationInstances.IsRunning);
        dbContext.OrchestrationInstances.Add(orchestrationInstances.IsTerminatedAsSucceeded);
        dbContext.OrchestrationInstances.Add(orchestrationInstances.IsTerminatedAsFailed);
        await dbContext.SaveChangesAsync();

        return orchestrationInstances;
    }

    /// <summary>
    /// Create an orchestration description that isn't one of the calculation types orchestration descriptions.
    /// Create orchestration instances in all possible lifecycle states:
    ///  - Pending
    ///  - Queued
    ///  - Running
    ///  - Terminated as succeeded
    ///  - Terminated as failed
    ///  - Terminated as user cancelled (require the instance is scheduled)
    /// </summary>
    private async Task SeedDatabaseWithJohnDoeLifecycleDatasetAsync()
    {
        var johnDoeName = Guid.NewGuid().ToString();
        var johnDoeV1Description = new OrchestrationDescription(
            uniqueName: new OrchestrationDescriptionUniqueName(name: johnDoeName, version: 1),
            canBeScheduled: true,
            functionName: "TestOrchestrationFunction");

        var johnDoe = DomainTestDataFactory.CreateLifecycleDataset(johnDoeV1Description);

        var isTerminatedAsUserCancelled = OrchestrationInstance.CreateFromDescription(
            identity: _userIdentity.MapToDomain(),
            description: johnDoeV1Description,
            skipStepsBySequence: [],
            clock: SystemClock.Instance,
            runAt: SystemClock.Instance.GetCurrentInstant());
        isTerminatedAsUserCancelled.Lifecycle.TransitionToUserCanceled(SystemClock.Instance, _userIdentity.MapToDomain());

        await using var dbContext = _fixture.DatabaseManager.CreateDbContext();
        dbContext.OrchestrationDescriptions.Add(johnDoeV1Description);
        dbContext.OrchestrationInstances.Add(johnDoe.IsPending);
        dbContext.OrchestrationInstances.Add(johnDoe.IsQueued);
        dbContext.OrchestrationInstances.Add(johnDoe.IsRunning);
        dbContext.OrchestrationInstances.Add(johnDoe.IsTerminatedAsSucceeded);
        dbContext.OrchestrationInstances.Add(johnDoe.IsTerminatedAsFailed);
        dbContext.OrchestrationInstances.Add(isTerminatedAsUserCancelled);
        await dbContext.SaveChangesAsync();
    }
}
