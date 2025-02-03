# ProcessManager.Orchestrations.Abstractions Release Notes

## Version 0.18.3

- Add `Resolution` to `RequestCalculatedWholesaleServicesAcceptedV1`.

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
