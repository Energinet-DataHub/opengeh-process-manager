# ProcessManager.Orchestrations.Abstractions Release Notes

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
