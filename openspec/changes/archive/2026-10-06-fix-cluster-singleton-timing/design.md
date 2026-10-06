## Context

njord runs as a single-node Akka.NET cluster. Cluster formation uses a manual
`cluster.Join(selfAddress)` registered as the last startup task, after all 13
`WithSingleton` calls. Singleton proxies time out at 30 seconds because the
cluster hasn't formed when they start looking.

FunkArr solves this identically by using `SeedNodes` in `WithClustering()`,
which configures the self-join in HOCON — the cluster starts forming during
actor system creation, before startup tasks execute.

Full analysis: `e2e/SINGLETON-RCA.md`.

## Goals / Non-Goals

**Goals:**
- Eliminate the 30-second ClusterSingletonProxy warnings
- Ensure all singletons (pipeline, enrichment, scheduler, etc.) start before
  ha-njord connects via gRPC
- Match the proven FunkArr cluster formation pattern

**Non-Goals:**
- Multi-node clustering
- Akka.Management / ClusterBootstrap
- Making the remoting port configurable (can follow later)

## Decisions

### 1. SeedNodes over manual Join

Use `WithClustering(new ClusterOptions { SeedNodes = [...] })` instead of
`cluster.Join(selfAddress)` in a `WithActors` callback.

**Why:** SeedNodes configures the join in HOCON, which Akka processes during
actor system creation — before any startup task runs. This eliminates the race
between proxy lookup and cluster formation. FunkArr uses this exact pattern
successfully.

**Alternative:** Moving `cluster.Join()` to an earlier startup task (before
singletons). Rejected because it would still be a startup task, not part of
system creation, and the timing depends on Akka.Hosting's internal execution
order which could change.

### 2. Fixed remoting port 2552

Pin `WithRemoting(new RemoteOptions { HostName = "localhost", Port = 2552 })`
instead of `Port = 0`.

**Why:** `SeedNodes` requires a known address. Ephemeral ports can't be
referenced before the system starts. Port 2552 is the Akka convention and what
FunkArr uses.

**Alternative:** Pick the port after system start and re-configure. Too complex
for a single-node deployment.

### 3. Separate WithActors for shutdown task

Move `AddStreamShutdownTask` from the `WithActors` callback that did
`cluster.Join` into its own `WithActors` callback.

**Why:** The join is removed entirely (handled by SeedNodes), but the shutdown
task still needs a registration point. A separate `WithActors` is the cleanest
way — no new infrastructure needed.

## Risks / Trade-offs

- **[Fixed port conflict]** → Port 2552 could conflict with another Akka
  process on the same machine. Mitigation: njord runs in Docker (isolated
  network) or as the only Akka service on the host. The port is internal
  (not exposed in docker-compose).
- **[Integration test port collision]** → Tests running in parallel could
  collide on port 2552. Mitigation: check if tests use `Port = 0` today
  and whether they configure clustering at all. Most test projects use
  `Akka.Hosting.TestKit` which creates its own actor system with separate
  config. The `E2EFixture` uses `WebApplicationFactory` and may need adjustment.
