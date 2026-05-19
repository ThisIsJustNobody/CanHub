# Vector ASC Trace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a new `CanHub.Trace.VectorAsc` package that reads and writes Vector-style `.asc` CAN/CAN FD trace files.

**Architecture:** Keep the trace package independent from adapters and Core; it depends only on `CanHub.Abstractions`. Use a faithful `VectorAscFrame` DTO for file records, then provide conversion helpers for `CanFrameEvent`.

**Tech Stack:** .NET 10, C# latest, MSTest v4, CanHub `CanFrame` and `CanFrameEvent`.

---

### Task 1: Add Project Skeleton

**Files:**
- Create: `src/CanHub.Trace.VectorAsc/CanHub.Trace.VectorAsc.csproj`
- Create: `src/CanHub.Trace.VectorAsc/README.md`
- Create: `src/CanHub.Trace.VectorAsc/README.zh-CN.md`
- Create: `tests/CanHub.Trace.VectorAsc.Tests/CanHub.Trace.VectorAsc.Tests.csproj`
- Modify: `CanHub.slnx`

- [ ] **Step 1: Write the failing project build expectation**

Run: `dotnet build src/CanHub.Trace.VectorAsc/CanHub.Trace.VectorAsc.csproj`

Expected: FAIL because the project file does not exist.

- [ ] **Step 2: Create the project and test project**

Create a library project using the same package metadata conventions as existing CanHub packages. Reference `..\CanHub.Abstractions\CanHub.Abstractions.csproj`. Add MSTest v4 packages in the test project and reference the new library.

- [ ] **Step 3: Add both projects to `CanHub.slnx`**

Add the library under `/src/` and the tests under `/tests/`.

- [ ] **Step 4: Verify build**

Run: `dotnet build src/CanHub.Trace.VectorAsc/CanHub.Trace.VectorAsc.csproj`

Expected: PASS.

### Task 2: Demo Validate ASC Shapes

**Files:**
- Create: `samples/VectorAscDemo/VectorAscDemo.csproj`
- Create: `samples/VectorAscDemo/Program.cs`

- [ ] **Step 1: Create a small console demo**

The demo should contain representative Classic CAN, extended ID, remote, error, and CAN FD lines as embedded strings. It should print tokenized fields and a sample generated output block.

- [ ] **Step 2: Run demo**

Run: `dotnet run --project samples/VectorAscDemo/VectorAscDemo.csproj`

Expected: console output shows the parsed token positions and generated ASC lines.

### Task 3: Reader Metadata and Diagnostics

**Files:**
- Create: `src/CanHub.Trace.VectorAsc/VectorAscFile.cs`
- Create: `src/CanHub.Trace.VectorAsc/VectorAscDiagnostic.cs`
- Create: `src/CanHub.Trace.VectorAsc/VectorAscReadOptions.cs`
- Create: `src/CanHub.Trace.VectorAsc/VectorAscReader.cs`
- Test: `tests/CanHub.Trace.VectorAsc.Tests/VectorAscReaderHeaderTests.cs`

- [ ] **Step 1: Write failing tests**

Tests should assert that `date`, `base`, `timestamps`, `internal events logged`, and trigger block start are parsed into `VectorAscFile`.

- [ ] **Step 2: Verify red**

Run: `dotnet test tests/CanHub.Trace.VectorAsc.Tests/CanHub.Trace.VectorAsc.Tests.csproj --filter "FullyQualifiedName~VectorAscReaderHeaderTests"`

Expected: FAIL because reader types do not exist.

- [ ] **Step 3: Implement metadata parsing**

Implement tolerant header parsing with invariant culture for numeric fields and local-compatible date parsing.

- [ ] **Step 4: Verify green**

Run the same filtered test and expect PASS.

### Task 4: Reader Classic CAN Rows

**Files:**
- Create: `src/CanHub.Trace.VectorAsc/VectorAscFrame.cs`
- Modify: `src/CanHub.Trace.VectorAsc/VectorAscReader.cs`
- Test: `tests/CanHub.Trace.VectorAsc.Tests/VectorAscReaderClassicTests.cs`

- [ ] **Step 1: Write failing tests**

Cover Classic data, remote, extended ID suffix `x`, and `ErrorFrame`.

- [ ] **Step 2: Verify red**

Run the filtered Classic reader tests and expect FAIL because frame row parsing is missing.

- [ ] **Step 3: Implement Classic row parsing**

Map `Rx` to receive, `Tx` to transmit, ASC channels to zero-based `CanFrameEvent.ChannelIndex`, and create `CanFrame` values.

- [ ] **Step 4: Verify green**

Run the filtered Classic reader tests and expect PASS.

### Task 5: Reader CAN FD Rows

**Files:**
- Modify: `src/CanHub.Trace.VectorAsc/VectorAscReader.cs`
- Test: `tests/CanHub.Trace.VectorAsc.Tests/VectorAscReaderCanFdTests.cs`

- [ ] **Step 1: Write failing tests**

Cover CAN FD data with and without symbolic names, BRS, ESI, DLC/data-length consistency, trailing flags bits 12/13/14, and 64-byte payloads.

- [ ] **Step 2: Verify red**

Run filtered CAN FD tests and expect FAIL because CAN FD row parsing is missing.

- [ ] **Step 3: Implement CAN FD parsing**

Parse `CANFD <channel> <dir> <id> [symbolic] <brs> <esi> <dlc> <data-length> <bytes...>`. Skip trailing timing/status fields after payload bytes, but inspect a present flags field for FD/BRS/ESI consistency diagnostics.

- [ ] **Step 4: Verify green**

Run filtered CAN FD tests and expect PASS.

### Task 6: Writer

**Files:**
- Create: `src/CanHub.Trace.VectorAsc/VectorAscWriteOptions.cs`
- Create: `src/CanHub.Trace.VectorAsc/VectorAscWriter.cs`
- Test: `tests/CanHub.Trace.VectorAsc.Tests/VectorAscWriterTests.cs`

- [ ] **Step 1: Write failing tests**

Assert header shape, Classic data/remote/error output, CAN FD BRS/ESI output, millisecond header timestamp precision, and round-trip readability.

- [ ] **Step 2: Verify red**

Run filtered writer tests and expect FAIL because writer types do not exist.

- [ ] **Step 3: Implement writer**

Emit `base hex  timestamps absolute`, `internal events logged`, trigger block, start-of-measurement event, frames, and `End TriggerBlock`.

- [ ] **Step 4: Verify green**

Run filtered writer tests and expect PASS.

### Task 7: CanHub Conversion Helpers

**Files:**
- Create: `src/CanHub.Trace.VectorAsc/VectorAscCanHubConversion.cs`
- Test: `tests/CanHub.Trace.VectorAsc.Tests/VectorAscCanHubConversionTests.cs`

- [ ] **Step 1: Write failing tests**

Assert conversion from `CanFrameEvent` to `VectorAscFrame` preserves frame, direction, channel, timestamp, and sequence, and conversion back recreates receive/transmit event shapes.

- [ ] **Step 2: Verify red**

Run filtered conversion tests and expect FAIL because conversion helpers do not exist.

- [ ] **Step 3: Implement conversion helpers**

Use `CanFrameEvent.CreateReceived`, `CreateTransmitted`, and `CreateTransmitSubmission` according to direction and observation kind.

- [ ] **Step 4: Verify green**

Run filtered conversion tests and expect PASS.

### Task 8: Documentation and Verification

**Files:**
- Modify: `README.md`
- Modify: `README.zh-CN.md`
- Modify: `src/CanHub.Trace.VectorAsc/README.md`
- Modify: `src/CanHub.Trace.VectorAsc/README.zh-CN.md`

- [ ] **Step 1: Update docs**

Document install, read, write, supported scope, and unsupported rows diagnostics.

- [ ] **Step 2: Run package tests**

Run: `dotnet test tests/CanHub.Trace.VectorAsc.Tests/CanHub.Trace.VectorAsc.Tests.csproj`

Expected: PASS.

- [ ] **Step 3: Run baseline tests**

Run the four baseline test projects from AGENTS.md plus the new test project, sequentially with `-m:1`.

Expected: PASS for existing tests; hardware-gated Vector tests may remain skipped.

- [ ] **Step 4: Build solution**

Run: `dotnet build CanHub.slnx -m:1`

Expected: PASS.
