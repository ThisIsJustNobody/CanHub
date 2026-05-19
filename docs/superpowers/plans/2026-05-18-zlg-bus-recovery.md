# ZLG Bus Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add opt-in ZLG channel recovery that reacts to selected bus/native fault statuses by closing the channel handle and reopening it with the original open configuration.

**Architecture:** Keep recovery coordination on `ZlgChannelLeaseEntry`, because multiple `ZlgBus` sessions share one lease. Extract the native channel open/close primitive behind an internal lifecycle seam so unit tests can prove reset/backoff behavior without hardware. Hardware tests remain opt-in and validate the user's two-bus ZLG setup.

**Tech Stack:** .NET 10, C# latest, MSTest v4, CanHub Abstractions/Core/ZLG adapter.

---

### Task 1: ZLG Channel Recovery Unit Tests

**Files:**
- Modify: `tests/CanHub.Adapter.Zlg.Tests/ZlgLifecycleTests.cs`

- [ ] **Step 1: Write failing reset recovery test**

Add a synthetic `ZlgChannelLeaseEntry` with `CanRecoveryOptions.ResetOnFault(restartDelay: TimeSpan.Zero)` and a fake lifecycle that records close/open calls. Publish a `CanStatusCode.BusOff` status through the new recovery entry point and assert the event sequence is `BusOff`, `Recovering`, `Recovered`, the old handle was closed, one new handle was opened, and `IsOpen` is true.

- [ ] **Step 2: Write failing backoff retry test**

Use `CanRecoveryOptions.ReopenWithBackoff(triggers: CanRecoveryTrigger.BusOff, restartDelay: TimeSpan.Zero, maxAttempts: 3, maxBackoffDelay: TimeSpan.Zero)`. Configure the fake lifecycle to fail two opens and succeed on the third. Assert `Recovered.Count == 3`.

- [ ] **Step 3: Run ZLG lifecycle tests and verify red**

Run:

```powershell
dotnet test tests\CanHub.Adapter.Zlg.Tests\CanHub.Adapter.Zlg.Tests.csproj --filter "FullyQualifiedName~ZlgLifecycleTests"
```

Expected: compile failure because `ZlgChannelLeaseEntry` has no recovery entry point or injectable lifecycle seam.

### Task 2: ZLG Channel Recovery Implementation

**Files:**
- Create: `src/CanHub.Adapter.Zlg/Internal/ZlgChannelOpenSpec.cs`
- Create: `src/CanHub.Adapter.Zlg/Internal/IZlgChannelLifecycle.cs`
- Create: `src/CanHub.Adapter.Zlg/Internal/ZlgNativeChannelLifecycle.cs`
- Modify: `src/CanHub.Adapter.Zlg/Internal/ZlgChannelLeaseEntry.cs`
- Modify: `src/CanHub.Adapter.Zlg/Internal/ZlgDeviceLeaseEntry.cs`
- Modify: `src/CanHub.Adapter.Zlg/ZlgAdapterProvider.cs`
- Modify: `src/CanHub.Adapter.Zlg/ZlgBus.cs`

- [ ] **Step 1: Add channel lifecycle seam**

Move the existing channel init/start/clear sequence into `ZlgNativeChannelLifecycle.OpenChannel`. Move `ZCAN_ResetCAN` close handling into `ZlgNativeChannelLifecycle.CloseChannel`.

- [ ] **Step 2: Store original open context on the lease**

Pass a `ZlgChannelOpenSpec`, `CanRecoveryOptions`, and lifecycle instance into `ZlgChannelLeaseEntry` so recovery can re-run the same open parameters.

- [ ] **Step 3: Implement recovery coordination**

Add `HandleFaultStatus(CanStatusEvent statusEvent)` to publish the fault status and trigger recovery when `CanRecoveryOptions.Triggers` matches. `CloseOnFault` closes the channel and publishes `Disconnected`; `ResetOnFault` attempts one reopen; `ReopenWithBackoff` retries up to `MaxAttempts`.

- [ ] **Step 4: Reject sends while closed or recovering**

Make `ZlgBus.SendAsync` return `CanTransmitSubmissionStatus.NotStarted` when the shared entry is closed or recovering.

- [ ] **Step 5: Run ZLG lifecycle tests and verify green**

Run:

```powershell
dotnet test tests\CanHub.Adapter.Zlg.Tests\CanHub.Adapter.Zlg.Tests.csproj --filter "FullyQualifiedName~ZlgLifecycleTests"
```

Expected: all ZLG lifecycle tests pass.

### Task 3: ZLG Fault Status Mapping and Hardware Hooks

**Files:**
- Modify: `src/CanHub.Adapter.Zlg/Internal/ZlgFrameConverter.cs`
- Modify: `src/CanHub.Adapter.Zlg/Internal/ZlgDeviceLeaseEntry.cs`
- Modify: `tests/CanHub.Adapter.Zlg.Tests/ZlgFrameConverterTests.cs`
- Modify: `tests/CanHub.Adapter.Zlg.Tests/Hardware/ZlgBus2TerminationHardwareTests.cs`
- Modify: `tests/CanHub.Adapter.Zlg.Tests/Support/ZlgCanHubHardwareTestBase.cs`

- [ ] **Step 1: Add failing bus-off status mapping test**

Assert a merged ZLG error object with `ZlgNodeState.BusOff` maps to `CanStatusCode.BusOff`.

- [ ] **Step 2: Route merged error statuses through recovery**

Change `ZlgDeviceLeaseEntry.DispatchMergedObject` so merged error objects call `entry.HandleFaultStatus(status)` instead of only publishing status.

- [ ] **Step 3: Add opt-in hardware recovery tests**

Add tests for the user's ZLG setup:
- one-node terminated bus causes No ACK or bus error and keeps the bus usable after recovery,
- unterminated bus with two nodes reports bit/native bus errors and attempts recovery.

These tests must remain gated by `CANHUB_TEST_ZLG`.

### Task 4: Verification and Commit

**Files:**
- Modify: `src/CanHub.Adapter.Zlg/README.md`
- Modify: `src/CanHub.Adapter.Zlg/README.zh-CN.md`

- [ ] **Step 1: Document ZLG recovery policy**

Document opt-in recovery, default disabled behavior, and the hardware environment variables for the two-bus ZLG validation setup.

- [ ] **Step 2: Run serial non-hardware tests**

Run:

```powershell
dotnet test tests\CanHub.Abstractions.Tests\CanHub.Abstractions.Tests.csproj
dotnet test tests\CanHub.Core.Tests\CanHub.Core.Tests.csproj
dotnet test tests\CanHub.Adapter.Virtual.Tests\CanHub.Adapter.Virtual.Tests.csproj
dotnet test tests\CanHub.Adapter.Zlg.Tests\CanHub.Adapter.Zlg.Tests.csproj
dotnet test tests\CanHub.PackageSmoke.Tests\CanHub.PackageSmoke.Tests.csproj
```

Expected: all pass; hardware tests are skipped unless environment variables opt in.

- [ ] **Step 3: Commit ZLG recovery slice**

Commit the ZLG recovery implementation separately from any later Vector work.
