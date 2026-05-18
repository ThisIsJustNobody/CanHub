# Vector Bus Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add opt-in Vector channel recovery that closes and reopens the same Vector XL port after selected bus/native faults.

**Architecture:** Keep recovery coordination in `VectorChannelLeaseEntry`, because multiple `VectorBus` sessions share one native port. Split port close/reopen primitives from permanent disposal so recovery can reuse the same `VectorChannelPort` and original `CanOpenContext`. Recreate `VectorReceiveLoop` after recovery because a stopped receive loop is intentionally single-use.

**Tech Stack:** .NET 10, C# latest, MSTest v4, Vector XL Driver wrapper, existing CanHub adapter/test projects.

---

### Task 1: Vector Recovery Unit Tests

**Files:**
- Modify: `tests/CanHub.Adapter.Vector.Tests/VectorLifecycleTests.cs`

- [x] **Step 1: Add failing ResetOnFault test**

Create a synthetic open `VectorChannelLeaseEntry` with `CanRecoveryOptions.ResetOnFault(restartDelay: TimeSpan.Zero)` and a fake lifecycle. Publish `CanStatusCode.BusOff` through `HandleFaultStatus`. Assert `BusOff`, `Recovering`, `Recovered`; one close; one reopen; and `entry.IsOpen`.

- [x] **Step 2: Add failing ReopenWithBackoff test**

Use `CanRecoveryOptions.ReopenWithBackoff(restartDelay: TimeSpan.Zero, maxAttempts: 3, maxBackoffDelay: TimeSpan.Zero)` and a fake lifecycle that fails two opens then succeeds. Assert `Recovered.Count == 3`.

- [x] **Step 3: Add failing NativeReceiveFault test**

Publish a `CanStatusKind.Bus` / `CanStatusCode.NativeDriverError` status with recovery configured for `CanRecoveryTrigger.NativeReceiveFault`. Assert recovery runs.

- [x] **Step 4: Verify red**

Run:

```powershell
dotnet test tests\CanHub.Adapter.Vector.Tests\CanHub.Adapter.Vector.Tests.csproj --filter "FullyQualifiedName~VectorLifecycleTests"
```

Expected: compile failure for missing Vector recovery seam/entry point.

### Task 2: Vector Channel Recovery Implementation

**Files:**
- Create: `src/CanHub.Adapter.Vector/Internal/VectorChannelOpenSpec.cs`
- Create: `src/CanHub.Adapter.Vector/Internal/IVectorChannelLifecycle.cs`
- Create: `src/CanHub.Adapter.Vector/Internal/VectorNativeChannelLifecycle.cs`
- Modify: `src/CanHub.Adapter.Vector/Internal/VectorChannelPort.cs`
- Modify: `src/CanHub.Adapter.Vector/Internal/VectorChannelLeaseEntry.cs`
- Modify: `src/CanHub.Adapter.Vector/Internal/VectorReceiveLoop.cs`
- Modify: `src/CanHub.Adapter.Vector/VectorAdapterProvider.cs`
- Modify: `src/CanHub.Adapter.Vector/VectorBus.cs`

- [x] **Step 1: Add port recovery primitives**

Add internal `CloseForRecovery(Action<CanStatusEvent>)` and `ReopenForRecovery(CanOpenContext, CancellationToken)` methods to `VectorChannelPort`. These close/reopen the native port without setting the permanent disposed state.

- [x] **Step 2: Add lifecycle seam**

Add `IVectorChannelLifecycle` and production `VectorNativeChannelLifecycle` that delegate to the new port recovery primitives. Tests use a fake lifecycle and reflection to mark the synthetic port open/closed.

- [x] **Step 3: Move recovery coordination into lease**

Store original `VectorChannelOpenSpec`, `CanRecoveryOptions`, and lifecycle on `VectorChannelLeaseEntry`. Add `HandleFaultStatus`, `ConfigureRecovery`, `IsRecovering`, reset/backoff recovery logic, and receive-loop recreation after reopen.

- [x] **Step 4: Route receive-loop statuses through recovery**

Create receive loops with `HandleFaultStatus` as the status sink. Keep non-fault statuses report-only.

- [x] **Step 5: Publish status for error frame observations**

When classic error frames or CAN FD RX_ERROR frames are observed, continue broadcasting the error frame and also publish a `Bus/NativeDriverError` status so `CanRecoveryTrigger.NativeReceiveFault` can trigger recovery.

- [x] **Step 6: Reject sends while not open or recovering**

Make `VectorBus.SendAsync` return `CanTransmitSubmissionStatus.NotStarted` when the shared entry is closed or recovering. Publish/route transmit native errors through `HandleFaultStatus`.

- [x] **Step 7: Verify green**

Run:

```powershell
dotnet test tests\CanHub.Adapter.Vector.Tests\CanHub.Adapter.Vector.Tests.csproj --filter "FullyQualifiedName~VectorLifecycleTests"
```

Expected: all Vector lifecycle tests pass.

### Task 3: Single-Channel Vector/ZLG Hardware Recovery

**Files:**
- Modify: `tests/CanHub.Adapter.Zlg.Tests/Support/ZlgCanHubHardwareTestBase.cs`
- Modify: `tests/CanHub.Adapter.Zlg.Tests/Hardware/ZlgBus2TerminationHardwareTests.cs`
- Modify: `src/CanHub.Adapter.Vector/README.md`
- Modify: `src/CanHub.Adapter.Vector/README.zh-CN.md`

- [x] **Step 1: Add hardware helper overload**

Allow `OpenVectorAsync` to pass `CanRecoveryOptions`.

- [x] **Step 2: Add opt-in hardware test**

Use `CANHUB_TEST_VECTOR=1` and `CANHUB_TEST_ZLG=1`. Open Vector `VN5610A` device 0 channel 2 and ZLG device 0/1 bus2 on the unterminated bus. Send a classic CAN frame from Vector and assert a recovery status appears.

- [x] **Step 3: Document Vector recovery and hardware topology**

Document `CANHUB_TEST_VECTOR_DEVICE=VN5610A`, `CANHUB_TEST_VECTOR_DEVICE_INDEX=0`, `CANHUB_TEST_VECTOR_CHANNEL_INDEX=2`, and the single-Vector-channel bus2 topology.

### Task 4: Verification and Commit

**Files:**
- Commit all Vector recovery changes.

- [x] **Step 1: Run serial tests**

Run:

```powershell
dotnet test tests\CanHub.Abstractions.Tests\CanHub.Abstractions.Tests.csproj
dotnet test tests\CanHub.Core.Tests\CanHub.Core.Tests.csproj
dotnet test tests\CanHub.Adapter.Virtual.Tests\CanHub.Adapter.Virtual.Tests.csproj
dotnet test tests\CanHub.Adapter.Vector.Tests\CanHub.Adapter.Vector.Tests.csproj
dotnet test tests\CanHub.Adapter.Zlg.Tests\CanHub.Adapter.Zlg.Tests.csproj
dotnet test tests\CanHub.PackageSmoke.Tests\CanHub.PackageSmoke.Tests.csproj
```

Expected: all non-hardware tests pass; hardware tests skip unless environment variables opt in.

- [x] **Step 2: Run the Vector/ZLG hardware recovery test**

Run with:

```powershell
$env:CANHUB_TEST_VECTOR = "1"
$env:CANHUB_TEST_VECTOR_DEVICE = "VN5610A"
$env:CANHUB_TEST_VECTOR_DEVICE_INDEX = "0"
$env:CANHUB_TEST_VECTOR_CHANNEL_INDEX = "2"
$env:CANHUB_TEST_ZLG = "1"
$env:CANHUB_TEST_ZLG_DEVICE0 = "0"
$env:CANHUB_TEST_ZLG_DEVICE1 = "1"
$env:CANHUB_TEST_ZLG_BUS2_CHANNEL = "1"
dotnet test tests\CanHub.Adapter.Zlg.Tests\CanHub.Adapter.Zlg.Tests.csproj --filter "FullyQualifiedName~Bus2_VectorCh2_UnterminatedBusFault_TriggersRecoveryAttempt"
```

- [x] **Step 3: Commit**

Commit with message `feat: add vector channel recovery`.
