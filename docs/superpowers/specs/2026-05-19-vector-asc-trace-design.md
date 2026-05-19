# Vector ASC Trace Design

## Goal

Add a hardware-independent CanHub package that can read and write Vector-style `.asc` CAN trace files and map supported CAN/CAN FD messages to CanHub frame models.

## Sources

- Vector Logging File Formats Overview: confirms `.asc` is the ASCII frame logging format supported by CANoe/CANalyzer.
  https://support.vector.com/sys_attachment.do?sys_id=a94857572b39329034d2f47ebe91bf2b
- CANalyzer manual excerpts and public examples: ASC files declare `base hex` or `base dec`; following message lines use the Trace window format; non-message lines may be ignored.
- CANedge Vector ASC converter examples: provide concrete Classic CAN and CAN FD output samples.
  https://canlogger.csselectronics.com/tools-docs/converters_txt/converters/asc/index.html
- python-can ASCReader/ASCWriter: public compatibility implementation for common CANoe/CANalyzer ASC variants.
  https://python-can.readthedocs.io/en/main/_modules/can/io/asc.html

## Package Shape

Create `src/CanHub.Trace.VectorAsc/CanHub.Trace.VectorAsc.csproj`.

The package depends only on `CanHub.Abstractions`. It does not belong in `CanHub.Abstractions` because file I/O and Vector trace syntax are not core frame contracts. It does not belong in `CanHub.Core` because it is offline trace serialization, not adapter discovery, leasing, or runtime coordination.

Create `tests/CanHub.Trace.VectorAsc.Tests/CanHub.Trace.VectorAsc.Tests.csproj` with MSTest v4.

## Supported Input Scope

Reader support:

- Header lines: `date ...`, `base hex timestamps absolute`, `base dec timestamps relative`, `internal events logged`, `no internal events logged`.
- Trigger blocks: `Begin Triggerblock ...`, `Begin TriggerBlock ...`, `End Triggerblock`, `End TriggerBlock`.
- Classic CAN data: `<time> <channel> <id> Rx|Tx d <dlc> <bytes...>`.
- Classic CAN remote: `<time> <channel> <id> Rx|Tx r <dlc>`.
- Classic CAN error: `<time> <channel> ErrorFrame`.
- CAN FD data: `<time> CANFD <channel> Rx|Tx <id> [symbolic-name] <brs> <esi> <dlc> <data-length> <bytes...> ...`.
- CAN FD flags: parse trailing `flags` when present and use bits 12, 13, and 14 as compatibility hints for FD, BRS, and ESI.
- Standard and extended identifiers. Extended identifiers use the Vector `x` suffix, for example `1ABCDEFFx`.
- `base hex` and `base dec` for IDs, DLC, and data bytes.
- Absolute timestamps as offsets from the trigger block start. Relative timestamps as deltas from the previous message when converting to absolute event time.

Unsupported input is skipped by default and exposed through diagnostics:

- LIN, FlexRay, Ethernet, J1939TP, statistic rows, chip status rows, comments.
- Symbol decoding and database signal information.
- Multiple trigger blocks beyond sequential parsing semantics.
- Vendor-specific trailing CAN FD timing fields beyond retaining parse compatibility.
- FD-capable devices may label classic-length rows as `CANFD`; the reader should prefer the explicit `CANFD` token but emit diagnostics when the trailing flags contradict BRS/ESI or FD semantics.

## Output Scope

Writer support:

- Always emits Vector-compatible header lines:
  - `date <local-time>`
  - `base hex  timestamps absolute`
  - `internal events logged`
  - `Begin Triggerblock <start-time>`
  - `0.000000 Start of measurement`
  - frame lines
  - `End TriggerBlock`
- Emits Classic CAN data and remote frames using Trace-style rows.
- Emits CAN FD data frames using a conservative CANoe-style CANFD row with BRS, ESI, lower-case hex DLC, data length, payload bytes, CANoe-style high flags, and zero-valued trailing timing/status fields.
- Omits CAN FD symbolic names on export by default because CANoe replay can reject unknown symbolic names even when the numeric frame is otherwise valid.
- Emits error frames as `ErrorFrame`.
- Formats header timestamps to millisecond precision for CANoe/CANalyzer compatibility.

## Public API

Core public types:

- `VectorAscFile`: parsed file metadata plus frame records.
- `VectorAscFrame`: one trace frame record with timestamp, channel, direction, frame, observation metadata, and raw line number.
- `VectorAscReadOptions`: strict mode and timestamp handling options.
- `VectorAscWriteOptions`: base, timestamp format, start time, channel fallback.
- `VectorAscDiagnostic`: line-numbered warnings for skipped or malformed rows.
- `VectorAscReader`: static file/string/TextReader read helpers.
- `VectorAscWriter`: static file/string/TextWriter write helpers.
- `VectorAscCanHubConversion`: helpers between `VectorAscFrame` and `CanFrameEvent`.

Use `CanFrame` for payload and flags. Use `CanFrameEvent` conversion for timeline-oriented consumers, while keeping `VectorAscFrame` as the more faithful trace DTO because ASC carries channel, textual direction, and file line metadata.

## Error Handling

Default parsing is tolerant: unsupported rows are skipped with diagnostics. Strict parsing throws `FormatException` for malformed supported rows and unsupported message-like rows.

Writers throw `ArgumentOutOfRangeException` or `ArgumentException` for impossible ASC output, such as negative channels or non-transmittable frame kinds where no output format is supported.

## Testing

Use TDD. Cover:

- Header parsing and base/timestamp options.
- Classic data, remote, extended ID, and error rows.
- CAN FD BRS/ESI/DLC/data-length rows, including symbolic-name variants.
- Tolerant diagnostics and strict-mode failures.
- Writer output for Classic and CAN FD rows.
- Round trip from writer output back through reader.
- Conversion to and from `CanFrameEvent`.

## Demo Validation

Before formal API work, use a temporary ignored demo/probe to validate sample `.asc` lines and output shape. Delete the probe after validation so it is not committed with the package.
