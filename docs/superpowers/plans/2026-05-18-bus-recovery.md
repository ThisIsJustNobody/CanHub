# Bus Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add opt-in CAN bus recovery primitives and a first recoverable Virtual adapter path, while keeping existing behavior report-only by default.

**Architecture:** Recovery starts in `CanHub.Abstractions` as stable options and status codes. Virtual gets deterministic fault injection so the shared session semantics can be tested without hardware. Vector and ZLG lifecycle recovery should build on the same close/reopen semantics after the API and coordinator behavior are proven.

**Tech Stack:** .NET 10, C# latest, MSTest v4, existing CanHub adapter/test projects.

---

### Task 1: Public Recovery API

**Files:**
- Create: `src/CanHub.Abstractions/CanRecoveryOptions.cs`
- Modify: `src/CanHub.Abstractions/CanOpenOptions.cs`
- Modify: `src/CanHub.Abstractions/CanStatusEvent.cs`
- Test: `tests/CanHub.Abstractions.Tests/CanOpenOptionsTests.cs`
- Test: `tests/CanHub.Abstractions.Tests/CanStatusEventTests.cs`

- [ ] **Step 1: Write failing tests for recovery defaults**

Add tests that assert `new CanOpenOptions().Recovery` is `CanRecoveryOptions.Disabled`, assigning `null` throws `ArgumentNullException`, and non-disabled defaults are `BusOff`, zero dwell time, 200 ms restart delay, max attempts by mode, and rejecting transmits while recovering.

- [ ] **Step 2: Run tests and verify red**

Run: `dotnet test tests\CanHub.Abstractions.Tests\CanHub.Abstractions.Tests.csproj --filter "FullyQualifiedName~CanOpenOptionsTests"`

Expected: compile failure because `CanRecoveryOptions` and `CanOpenOptions.Recovery` do not exist.

- [ ] **Step 3: Add minimal recovery types**

Add `CanRecoveryOptions`, `CanRecoveryMode`, and `CanRecoveryTrigger` in `CanHub.Abstractions`, and add a null-guarded `Recovery` property to `CanOpenOptions`.

- [ ] **Step 4: Add recovery status codes**

Add `RecoveryFailed = 402` and `RecoverySkipped = 403` to `CanStatusCode`.

- [ ] **Step 5: Run abstraction tests and verify green**

Run: `dotnet test tests\CanHub.Abstractions.Tests\CanHub.Abstractions.Tests.csproj`

Expected: all abstraction tests pass.

### Task 2: Virtual Adapter Recovery State

**Files:**
- Create: `src/CanHub.Adapter.Virtual/VirtualRecoveryOptions.cs`
- Modify: `src/CanHub.Adapter.Virtual/VirtualAdapterProvider.cs`
- Modify: `src/CanHub.Adapter.Virtual/Internal/VirtualBusSession.cs`
- Test: `tests/CanHub.Adapter.Virtual.Tests/VirtualAdapterTests.cs`

- [ ] **Step 1: Write failing tests for disabled recovery fault reporting**

Open a virtual bus with native virtual recovery injection. Subscribe to `StatusChanged`, inject bus-off, and assert the status event is reported while the bus remains open because `CanOpenOptions.Recovery` defaults to disabled.

- [ ] **Step 2: Write failing tests for CloseOnFault**

Open a virtual bus with `Recovery = CanRecoveryOptions.CloseOnFault()` and injected bus-off. Assert `StatusChanged` receives `BusOff`, then `Recovering`, then `Recovered` or terminal close status according to implementation, and `IsOpen` becomes false.

- [ ] **Step 3: Write failing tests for ResetOnFault**

Open a virtual bus with `Recovery = CanRecoveryOptions.ResetOnFault()`, inject bus-off, wait for recovery, then send a normal frame and assert submission is accepted.

- [ ] **Step 4: Run virtual tests and verify red**

Run: `dotnet test tests\CanHub.Adapter.Virtual.Tests\CanHub.Adapter.Virtual.Tests.csproj --filter "FullyQualifiedName~VirtualAdapterTests"`

Expected: compile failure because virtual recovery options and injection do not exist.

- [ ] **Step 5: Implement minimal Virtual recovery control**

Add a virtual native option object that can schedule or expose bus-off injection for tests. Store real status handlers in `VirtualBusSession`, publish `BusOff`, and execute report-only, close, or single reset semantics.

- [ ] **Step 6: Run virtual tests and verify green**

Run: `dotnet test tests\CanHub.Adapter.Virtual.Tests\CanHub.Adapter.Virtual.Tests.csproj`

Expected: all virtual tests pass.

### Task 3: Shared Recovery Semantics

**Files:**
- Modify: `src/CanHub.Adapter.Virtual/Internal/VirtualChannelState.cs`
- Modify: `src/CanHub.Adapter.Virtual/Internal/VirtualBusSession.cs`
- Test: `tests/CanHub.Adapter.Virtual.Tests/VirtualAdapterTests.cs`

- [ ] **Step 1: Write failing shared-session test**

Open two sessions on the same virtual endpoint with recovery enabled. Inject one bus-off at the shared channel state and assert both sessions observe one recovery sequence and both can send after reset.

- [ ] **Step 2: Run shared-session test and verify red**

Run: `dotnet test tests\CanHub.Adapter.Virtual.Tests\CanHub.Adapter.Virtual.Tests.csproj --filter "SameEndpoint"`

Expected: test fails until recovery state is lifted from session to shared channel state.

- [ ] **Step 3: Move recovery coordination to channel state**

Keep per-session handler registration, but ensure bus-off/recovery state and fault injection are coordinated per `VirtualChannelState`.

- [ ] **Step 4: Run virtual tests and verify green**

Run: `dotnet test tests\CanHub.Adapter.Virtual.Tests\CanHub.Adapter.Virtual.Tests.csproj`

Expected: all virtual tests pass.

### Task 4: Package and Baseline Verification

**Files:**
- Modify: `src/CanHub.Abstractions/README.md`
- Modify: `src/CanHub.Abstractions/README.zh-CN.md`
- Modify: `src/CanHub.Adapter.Virtual/README.md`
- Modify: `src/CanHub.Adapter.Virtual/README.zh-CN.md`

- [ ] **Step 1: Add short README notes**

Document that recovery is opt-in, defaults to report-only, and Virtual supports deterministic test injection.

- [ ] **Step 2: Run full non-hardware test suite**

Run these commands serially:

```powershell
dotnet test tests\CanHub.Abstractions.Tests\CanHub.Abstractions.Tests.csproj
dotnet test tests\CanHub.Core.Tests\CanHub.Core.Tests.csproj
dotnet test tests\CanHub.Adapter.Virtual.Tests\CanHub.Adapter.Virtual.Tests.csproj
dotnet test tests\CanHub.Adapter.Vector.Tests\CanHub.Adapter.Vector.Tests.csproj
dotnet test tests\CanHub.Adapter.Zlg.Tests\CanHub.Adapter.Zlg.Tests.csproj
dotnet test tests\CanHub.PackageSmoke.Tests\CanHub.PackageSmoke.Tests.csproj
```

Expected: all pass; hardware tests remain skipped unless environment variables opt in.

### Follow-Up Plan

After the public API and Virtual recovery path are green, write a second plan for adapter lifecycle recovery:

- Vector close/reopen recovery around `VectorChannelLeaseEntry`, `VectorChannelPort`, and `VectorReceiveLoop`.
- ZLG close/reopen recovery around `ZlgDeviceLeaseEntry`, `ZlgChannelLeaseEntry`, and receive-loop routing.
- Manifest capability declarations for close/reopen recovery support and exceptions.
