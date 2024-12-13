# ProcessManager.Client Release Notes

## Version 0.14.2

- No functional change

## Version 0.14.1

- Rename service bus client options

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
