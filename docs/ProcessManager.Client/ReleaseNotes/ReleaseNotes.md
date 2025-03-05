# ProcessManager.Client Release Notes

## Version 2.0.0

- This version is a breaking change for use of `IProcessManagerMessageClient`.
- Refactored `ProcessManagerMessageClient` to send notify events to a specific "notify" topic, and start commands to a specific "start" topic.
- Refactored `ProcessManagerServiceBusClientOptions` to have options `StartTopicName` and `NotifyTopicName`.

## Version 1.5.0

- Add `CustomState` to `IOrchestrationInstanceTypedDto`.

## Version 1.4.0

- Extended `ProcessManagerHttpClientsOptions` with `ApplicationIdUri`.
- Refactored `ProcessManagerClient` to retrieve and add token when calling Process Manager API's.

## Version 1.3.2

- Update NuGet packages.

## Version 1.3.1

- No functional changes.

## Version 1.3.0

- `IOrchestrationInstanceQueries.SearchAsync` now takes `IReadOnlyCollection<OrchestrationInstanceLifecycleState>?` instead of `OrchestrationINstanceLifecycleState?`.
- `IOrchestrationInstanceQueries.SearchAsync` now takes and additional optional parameter `Instant? scheduledAtOrLater` to filter on scheduled calculations.

## Version 1.2.0

- Add `ActorNumber`, `ActorRole`, `DataHubRecordType` and `EnumerationRecordType` (moved from `ProcessManager.Components.Abstractions`).
- Add `ActorRoleV1`.

### Breaking changes

- Change `ActorId` to `ActorNumber` and `ActorRole` on `ActorIdentityDto`. This is a breaking change for all commands on `IProcessManagerMessageClient`.
- Change `ActorId` to `ActorNumber` and `ActorRole` on `UserIdentityDto`. This is a breaking change for all commands and queries on `IProcessManagerClient`.
- Change `OrchestrationStartedByActorId` to `OrchestrationStartedByActor` with `ActorNumber` and `ActorRole` on `EnqueueActorMessagesV1`.
- Change `StartedByActorId` to `StartedByActor` with `ActorNumber` and `ActorRole` on `StartOrchestrationInstanceV1`.

## Version 1.1.1

- Align the package versions for `ProcessManager.Client` and `ProcessManager.Abstractions`:
    - `Client` was version 1.0.2 (as was the release notes) and `Abstractions` was version 1.1.0.
    - Both are now version 1.1.1.
- No functional changes.

## Version 1.0.2

- Add `ActorMessageId`, `TransactionId`, and `MetertingPointId` to `StartOrchestrationInstanceV1`.

## Version 1.0.1

- No functional changes.

## Version 1.0.0

- Moved `BusinessValidation` to NuGet package `ProcessManager.Components.Abstractions`.

## Version 0.27.1

- No functional changes.

## Version 0.27.0

- Update Nuget package `Grpc.Tools`

## Version 0.26.2

- Update NuGet package properties `RepositoryUrl`, `PackageReleaseNotes` and `PackageDescription`.

## Version 0.26.1

- Update `EnqueueActorMessagesV1` with `BuildServiceBusMessageSubject()` method.

## Version 0.26.0

- Throw exception in `EnqueueActorMessagesV1.ParseData<TData>()` if parsing incorrect `DataType`.
- Throw exception in `NotifyOrchestrationInstanceV1.ParseData<TNotifyData>()` if parsing incorrect `DataType`.
- Throw exception in `StartOrchestrationInstanceV1.ParseInput<TInputData>()` if parsing incorrect `InputType`.

## Version 0.25.1

- No functional changes.

## Version 0.25.0

- Add `IBusinessValidatedDto` and `ValidationErrorDto` used to support business validation.

## Version 0.24.0

- Add `INotifyDataDto` type constraint to `NotifyOrchestrationInstanceV1`

## Version 0.23.0

- Added `IdempotencyKey` to all orchestration instance DTO types:
    - `OrchestrationInstanceDto`
    - `IOrchestrationInstanceTypedDto<out TInputParameterDto>`
    - `OrchestrationInstanceTypedDto`
    - `OrchestrationInstanceTypedDto<TInputParameterDto>`
- Implemented `GetOrchestrationInstanceByIdempotencyKeyQuery` and extended `IProcessManagerClient` to support it.

## Version 0.22.0

- Update `EnqueueActorMessagesV1`, `NotifyOrchestrationInstanceV1` and `StartOrchestrationInstanceV1` with strongly typed formats.
- Update `EnqueueActorMessagesV1`, `NotifyOrchestrationInstanceV1` and `StartOrchestrationInstanceV1` with methods to set and get data.

## Version 0.21.0

- Updated code documenation (XML comments).
- Refactored implementation of custom queries support.
- Implemented interface `IOrchestrationInstanceTypedDto<out TInputParameterDto>` and added it to `OrchestrationInstanceTypedDto<TInputParameterDto>`.

## Version 0.20.0

- Renamed `MessageCommand` property `MessageId` to `IdempotencyKey`.
    - The Process Manager will use the `IdempotencyKey` to handle idempotency for commands initiated using messages.

## Version 0.19.0

- Add `NotifyOrchestrationInstanceAsync` to `IProcessManagerMessageClient`.
- Add `NotifyOrchestrationInstanceEvent` and `NotifyOrchestrationInstanceEvent<TData>` requests.
    - Add `INotifyDataDto` marker interface for `NotifyOrchestrationInstanceEvent<TData>`.
- Add `NotifyOrchestrationInstanceV1` and `NotifyOrchestrationInstanceDataV1` protobuf models.
- Rename `StartOrchestrationV1` to `StartOrchestrationInstanceV1`.
- Add `IOrchestrationInstanceRequest` interface as a base interface for all orchestration instance requests.

## Version 0.18.0

- Add version suffix to service bus messages names:
    - `EnqueueActorMessagesV1`
    - `StartOrchestrationV1`
- Refactor `EnqueueActorMessagesV1` and `StartOrchestrationV1` service bus message fields.

## Version 0.17.0

- Add `EnqueueActorMessages`.

## Version 0.16.0

- Renamed `enum` types to use singular naming to follow [Microsoft naming guidelines for naming enumerations](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-classes-structs-and-interfaces#naming-enumerations).

## Version 0.15.3

- No functional changes.

## Version 0.15.2

- Internal refactoring.

## Version 0.15.1

- No functional changes.

## Version 0.15.0

- Do not allow empty string in values of the following options:
    - `ProcessManagerHttpClientsOptions`
    - `ProcessManagerServiceBusClientOptions`

## Version 0.14.6

- Internal refactoring.

## Version 0.14.5

- Internal refactoring.

## Version 0.14.4

- Add actor id to start orchestration messages.

## Version 0.14.3

- No functional changes.

## Version 0.14.2

- No functional changes.

## Version 0.14.1

- Rename service bus client options.

## Version 0.14.0

- Support start/schedule orchestration instance with no input parameter.
- Support get orchestration instance when started with no input.

## Version 0.13.1

- Update dependencies.

## Version 0.13.0

- Move the last method from the specific client `INotifyAggregatedMeasureDataClientV1` to the general client `IProcessManagerClient`

## Version 0.12.1

- Update version on reusable workflows.

## Version 0.12.0

- Move methods from the specific client `INotifyAggregatedMeasureDataClientV1` to the general client `IProcessManagerClient`

## Version 0.11.1

- No functional changes, moving code to another repository

## Version 0.11.0

- Extend framework to require 'OperatingIdentity' when initiating commands (start, schedule, cancel).

## Version 0.10.0

- Added 'RequestCalculatedDataClientV1'

## Version 0.9.3

- Updated 'NotifyAggregatedMeasureDataInputV1' with new required 'UserId' parameter/property.

## Version 0.9.2

- Updated DTO types.

## Version 0.9.1

- Add documentation to several domain and DTO types.
- Remove nullable from places where we always expect a value.

## Version 0.9.0

- Walking skeleton for working with BRS_023_027.

## Version 0.0.1

- Empty release.
