# ProcessManager.Orchestrations.Abstractions Release Notes

## Version 2.2.5

- Changed List to IReadOnlyCollection type in `MissingMeasurementsLogOnDemandCalculation/V1/Model/CalculationInputV1.cs` to be more generic.

## Version 2.2.4

- Update `EnqueueMissingMeasurementsLogHttpV1` to contain a idempotency key.
- Rename `DateWithMeteringPointIds` to `DateWithMeteringPointId` in `EnqueueMissingMeasurementsLogHttpV1`.

## Version 2.2.3

- Update `EnqueueMissingMeasurementsLogHttpV1` to contain 1 metering point id for each date.

## Version 2.2.2

- No functional changes.

## Version 2.2.1

- Update `EnqueueMissingMeasurementsLogHttpV1` to contain a list of dates with metering point ids.

## Version 2.2.0

- Add initial `EnqueueMissingMeasurementsLogHttpV1` for BRS-045.

## Version 2.1.6

- Update `RequestCalculatedEnergyTimeSeriesRejectedV1` removed obsolete `OriginalMessageId`.
- Update `RequestCalculatedWholesaleServicesRejectedV1` removed obsolete `OriginalMessageId`.

## Version 2.1.5

- Delete unused `EnqueueActorMessagesForMeteringPointV1` and `ReceiversWithMeasureDataV1` for `ElectricalHeatingCalculation`.

## Version 2.1.4

- Update `EnqueueCalculatedMeasurementsHttpV1` to contain a list of `ReceiversWithMeasurements`.

## Version 2.1.3

- Rename `EnqueueMeasureDataSyncV1` to `EnqueueCalculatedMeasurementsHttpV1`

## Version 2.1.2

- Renamed `StartCalculationCommandV1` to `StartNetConsumptionCalculationCommandV1`.

## Version 2.1.1

- Add `EnqueueMeasureDataSyncV1` for BRS-021

## Version 2.1.0

- Removed obsolete types `CalculationQuery` and `CalculationQueryResult`.

## Version 2.0.1

- Added `GridAreaCode` to `ForwardMeteredDataAcceptedV1`.

## Version 2.0.0

- Upgraded to .NET 9

## Version 1.20.1

- Added `MeteringPointId` to `ForwardMeteredDataRejectedV1`.

## Version 1.20.0

- Add `EnqueueActorMessagesForMeteringPointV1` for BRS-021 Electrical Heating calculation.
- Add `ReceiversWithMeasureDataV1` for BRS-021 Electrical Heating calculation.

## Version 1.19.1

- No functional changes.

## Version 1.19.0

- Delete `ForwardedByActorNumber` and `ForwardedByActorRole` from `ForwardMeteredDataRejectedV1`.

## Version 1.18.3

- No functional changes.

## Version 1.18.2

- No functional changes.

## Version 1.18.1

- Use `IEnqueueAcceptedDataDto`, `IEnqueueRejectedDataDto` and `IEnqueueDataDto` marker interfaces.

## Version 1.18.0

- Add custom query `CalculationByIdQueryV1`.

## Version 1.17.2

- No functional changes.

## Version 1.17.1

- Add `ForwardedForActorRole`, to `ForwardMeteredDataRejectedV1`.

## Version 1.17.0

- Add custom query `CalculationsQueryV1` and related types.

## Version 1.16.0

- Add `InternalUse` MeteringPointType to ValueObject `MeteringPointType.cs`.

## Version 1.15.0

- Update NuGet packages.

## Version 1.14.0

- Removed `OriginalTransactionId` from `ForwardMeteredDataAcceptedV1`.

## Version 1.13.0

- Add BRS-045 Missing Measurements Log On Demand Calculation command.

## Version 1.12.0

- Add BRS-045 Missing Measurements Log Calculation command.

## Version 1.11.2

- Update `ForwardMeteredDataAcceptedV1` to support a list of receivers.
- Rename `ForwardMeteredDataAcceptedV1.AcceptedEnergyOberservation` to `AcceptedMeteredData`.
- Add `MeteredDataReceiverV1` model.

## Version 1.11.1

- Rename `OriginalBusinessReason` to `BusinessReason` in `ForwardMeteredDataRejectedV1`.

## Version 1.11.0

- Add `OriginalBusinessReason` to `ForwardMeteredDataInputV1`.

## Version 1.10.2

- Update NuGet packages.

## Version 1.10.1

- No functional changes.

## Version 1.10.0

- Rename `MessageId` to `ActorMessageId`, remove `AuthenticatedActorId` and reorder properties on `ForwardMeteredDataInputV1`.
- Remove most properties from `ForwardMeteredDataRejectedV1`, since the correct properties aren't determined yet.

## Version 1.9.0

- Remove internal process: Migrate Calculations From Wholesale.

## Version 1.8.0

- Add BRS-021 Net Consumption (group 6) command.

## Version 1.7.0

- Add BRS-021 Capacity Settlement command.

## Version 1.6.0

- Implemented specific events for notify. Previously we used one generic event implementation and used magic strings as input for the event name.

## Version 1.5.0

- Rename `StartForwardMeteredDataCommandV1` to `ForwardMeteredDataCommandV1`.
- Rename `MeteredDataForMeteringPointMessageInputV1` to `ForwardMeteredDataInputV1`.
- Rename `MeteredDataForMeteringPointAcceptedV1` to `ForwardMeteredDataAcceptedV1` and update properties.
- Rename `MeteredDataForMeteringPointRejectedV1` to `ForwardMeteredDataRejectedV1`.
- Rename `MeteredDataForMeteringPointMessagesEnqueuedNotifyEventsV1` to `ForwardMeteredDataNotifyEventsV1`.
- Rename `MarketActorRecipient` to `MarketActorRecipientV1`.
- Use `ActorNumber` value object in `MarketActorRecipient` and rename `ActorId` to `ActorNumber`.
- Move `AcceptedEnergyObservation` to `ForwardMeteredDataAcceptedV1.AcceptedEnergyObservation`.

## Version 1.4.1

- Update NuGet packages.

## Version 1.4.0

- Add `MigrateCalculationsFromWholesaleCustomStateV1`.

## Version 1.3.3

- Update NuGet packages.

## Version 1.3.2

- Update NuGet packages.

## Version 1.3.1

- No functional changes.

## Version 1.3.0

- `CalculationQuery` now takes `IReadOnlyCollection<OrchestrationInstanceLifecycleState>?` instead of `OrchestrationINstanceLifecycleState?`.
- `CalculationQuery` now takes an additional optional parameter `Instant? scheduledAtOrLater` to filter on scheduled calculations.

## Version 1.2.1

- Add `InternalProcesses/V1/MigrateCalculationsFromWholesale` internal process.
    - Add `MigrateCalculationsFromWholesaleUniqueName`.
    - Add `MigrateCalculationsFromWholesaleCommandV1`.

## Version 1.2.0

**Breaking changes:**

- Update `ActorRequestQuery` with `CreatedByActorNumber` and `CreatedByActorRole` instead of `CreatedByActorId`.
- Update to `ProcessManager.Abstractions` package version 1.2.0, which includes the following breaking changes:
    - Change `ActorId` to `ActorNumber` and `ActorRole` on `ActorIdentityDto`. This is a breaking change for all commands on `IProcessManagerMessageClient`.
    - Change `ActorId` to `ActorNumber` and `ActorRole` on `UserIdentityDto`. This is a breaking change for all commands and queries on `IProcessManagerClient`.
    - Change `OrchestrationStartedByActorId` to `OrchestrationStartedByActor` with `ActorNumber` and `ActorRole` on `EnqueueActorMessagesV1`.
    - Change `StartedByActorId` to `StartedByActor` with `ActorNumber` and `ActorRole` on `StartOrchestrationInstanceV1`.

## Version 1.1.0

- Add optional `createdByActorId` to filter to `ActorRequestQuery` for BRS-026/028.

## Version 1.0.4

- Update dependent NuGet package.

## Version 1.0.3

- Update dependent NuGet package.

## Version 1.0.2

- Update dependent NuGet package.

## Version 1.0.1

- Update dependent NuGet package.

## Version 1.0.0

- Update dependent NuGet package.

## Version 0.20.0

- Moved all types from namespace `ValueObjects` to new NuGet package `Energinet.DataHub.ProcessManager.Components.ValueObjects`
- Add reference to new NuGet package `Energinet.DataHub.ProcessManager.Components.ValueObjects`

## Version 0.19.0

- Add protobuf contract `CalculationEnqueueCompletedV1`

## Version 0.18.4

- Update NuGet package properties `RepositoryUrl`, `PackageReleaseNotes` and `PackageDescription`.

## Version 0.18.3

- Add `Resolution` to `RequestCalculatedWholesaleServicesAcceptedV1`.
- Add `RequestCalculatedWholesaleServicesAcceptedV1.AcceptedChargeType` type for `RequestCalculatedWholesaleServicesAcceptedV1.ChargeTypes`.

## Version 0.18.2

- Update `RequestCalculatedEnergyTimeSeriesRejectedV1` with missing properties.
- Update `RequestCalculatedWholesaleServicesRejectedV1` with missing properties.

## Version 0.18.1

- Update `RequestCalculatedEnergyTimeSeriesInputV1` with "requested by actor" properties.
- Update `RequestCalculatedEnergyTimeSeriesAcceptedV1` with "requested by actor" properties.
- Update `RequestCalculatedWholesaleServicesInputV1` with "requested by actor" and message/transaction id properties.
- Implement `RequestCalculatedWholesaleServicesAcceptedV1` with correct properties.

## Version 0.18.0

- Add `ActorNumber` and `ActorRole` to `MeteredDataForMeteringPointMessageInputV1`.

## Version 0.17.4

- Rename `NotifyEnqueueFinishedV1` to `CalculationEnqueueActorMessagesCompletedNotifyEventV1`.
- Rename `CalculatedDataForCalculationTypeV1` to `CalculationEnqueueActorMessagesV1`.

## Version 0.17.3

- Updated `MeteredDataForMeteringPointAcceptedV1`
- Add `MeteredDataForMeteringPointMessagesEnqueuedNotifyEventsV1`

## Version 0.17.2

- Update `RequestCalculatedWholesaleServicesInputV1` model with `IBusinessValidatedDto` interface.
- Rename `RequestCalculatedWholesaleServicesInputV1.ChargeTypeInputV1` to `RequestCalculatedWholesaleServicesInputV1.ChargeTypeInput`.
- Update `RequestCalculatedWholesaleServicesRejectedV1` properties to be a list of validation errors.

## Version 0.17.1

- Update `RequestCalculatedEnergyTimeSeriesInputV1` with the required input.
- Update `RequestCalculatedEnergyTimeSeriesAcceptedV1` with the correct properties.

## Version 0.17.0

- Add shared namespace for `BRS_026`/`BRS_028` types, so they now live in `BRS_026_028.BRS_026` and `BRS_026_028.BRS_028` namespaces.

## Version 0.16.1

- Add `NotifyEnqueueFinishedV1`.

## Version 0.16.0

- Add `SettlementMethod` and `SettlementVersion` DataHub types.
- Update `RequestCalculatedEnergyTimeSeriesInputV1` model with `IBusinessValidatedDto` interface.
- Update `RequestCalculatedEnergyTimeSeriesRejectedV1` properties to be a list of validation errors.

## Version 0.15.2

- Update dependent NuGet package.

## Version 0.15.1

- Updated `MeteredDataForMeteringPointRejectedV1` to use DataHubTypes.

## Version 0.15.0

- Removed `ProcessManager.Components` from `ProcessManager.Orchestrations.Abstractions` dependencies.

## Version 0.14.0

- Add `DataHubTypes` derived types to be shared with consumer.

## Version 0.13.0

- Update dependent NuGet package.

## Version 0.12.2

- Add `RequestCalculatedEnergyTimeSeriesNotifyEventsV1` and `RequestCalculatedWholesaleServicesNotifyEventsV1`

## Version 0.12.1

- Add `CalculatedDataForCalculationTypeV1`

## Version 0.12.0

- Add accept model (RSM-012) for BRS 21 named `MeteredDataForMeteringPointAcceptedV1`

## Version 0.11.0

- Add rejected model (RSM-009) for BRS 21 named `MeteredDataForMeteringPointRejectedV1`

## Version 0.10.0

- Moved types used for custom querying BRS 026 + 028 to a shared namespace:
    - `ActorRequestQuery`
    - `IActorRequestQueryResult`
    - `RequestCalculatedEnergyTimeSeriesResult`
    - `RequestCalculatedWholesaleServicesResult`

## Version 0.9.0

- Refactored all `OrchestrationDescriptionUniqueNameDto` implementations to use an implementation with a shared const `Name` and a readonly property per version.

## Version 0.8.0

- Refactored implementation of custom queries support.
- Added types for querying data spanning BRS 026 + 028:
    - `ActorRequestQuery`
    - `IActorRequestQueryResult`
    - `RequestCalculatedEnergyTimeSeriesResult`
    - `RequestCalculatedWholesaleServicesResult`

## Version 0.7.0

- Dependent NuGet packages updated

## Version 0.6.3

- No functional changes.

## Version 0.6.2

- Rename `MeteredDataForMeasurementPointMessageInputV1` to `MeteredDataForMeteredPointMessageInputV1`

## Version 0.6.1

- No functional changes.

## Version 0.6.0

- Add rejected model for `RequestCalculatedEnergyTimeSeriesRejectedV1`
- Add rejected model for `RequestCalculatedWholesaleServicesRejectedV1`

## Version 0.5.0

- Renamed `enum` types to use singular naming to follow [Microsoft naming guidelines for naming enumerations](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-classes-structs-and-interfaces#naming-enumerations).

## Version 0.4.3

- No functional changes.

## Version 0.4.2

- No functional changes.

## Version 0.4.1

- No functional changes.

## Version 0.4.0

- Rename `StartRequestCalculatedEnergyTimeSeriesCommandV1` to `RequestCalculatedEnergyTimeSeriesCommandV1`

## Version 0.3.0

- Do not allow empty string in values of the following options:
    - `ProcessManagerTopicOptions`

## Version 0.2.7

- Internal refactoring.

## Version 0.2.6

- Internal refactoring.

## Version 0.2.5

- Add actor id to start orchestration messages.

## Version 0.2.4

- Update (implement) correct input for starting BRS-026 and BRS-028 orchestrations.

## Version 0.2.3

- No functional changes.

## Version 0.2.2

- Change `StartForwardMeteredDataCommandV1` to a `MessageCommand<MeteredDataForMeasurementPointMessageInputV1>`

## Version 0.2.1

- Updated `Brs_021_ForwardMeteredData_V1` unique name to `Brs_021_ForwardMeteredData`

## Version 0.2.0

- Implemented `Brs_021_ForwardMeteredData_V1`
- Implemented `StartForwardMeteredDataCommandV1`

## Version 0.1.0

- Support start/schedule orchestration instance with no input parameter.
- Support get orchestration instance when started with no input.
- Implemented `Brs_021_ElectricalHeatingCalculation_V1`
- Implemented `StartElectricalHeatingCalculationCommandV1`

## Version 0.0.1

- First release.
