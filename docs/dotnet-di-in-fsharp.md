# .NET DI in This F# Backend

This document explains how dependency injection (DI) is wired in the API and how services are resolved at runtime.

## Mental Model

The app uses the built-in ASP.NET Core container:

1. `Program.fs` creates a `WebApplicationBuilder`.
2. `AppStartup` functions register services into `builder.Services`.
3. `builder.Build()` creates the root service provider.
4. Requests and hosted services resolve dependencies from that provider.

In this project, most registrations are singleton.

## Registration Entry Point

`Program.fs` wires DI through startup helpers:

- `addDatabase`
- `addAuthentication`
- `addSummaryBackgroundProcessing`
- `addIndexBackgroundProcessing`
- `addApplicationServices`
- `addBackgroundJobsWorker`

This keeps registration logic centralized in `Startup/AppStartup.fs`.

## Service Graph (Current)

```text
NpgsqlDataSource (Singleton)
  -> DbSession (Singleton)
      -> IBackgroundMaintenanceService (Singleton, implementation: BackgroundMaintenanceService)
          -> BackgroundJobsService.Worker (HostedService)

SummaryUpdateQueue (Singleton) ----\
                                    -> IBackgroundJobPublisher (Singleton, implementation: QueueBackedBackgroundJobPublisher)
IndexUpdateQueue (Singleton) ------/

IBackgroundJobPublisher
  -> resolved in request handlers (e.g. DiaryHandlers.save)
  -> used by DiaryService.saveDiary to enqueue background work

JwtConfig (Singleton)
  -> resolved in AuthHandlers.login
```

## Where Resolution Happens

Two patterns are used.

### 1) Constructor injection

`BackgroundJobsService.Worker` takes dependencies in its constructor:

- `IBackgroundMaintenanceService`
- `SummaryUpdateQueue`
- `IndexUpdateQueue`
- `ILogger<Worker>`

ASP.NET Core creates the worker and injects these automatically when `AddHostedService<Worker>()` is registered.

### 2) Request-time resolution (`RequestServices`)

Falco handlers resolve dependencies from `HttpContext.RequestServices`:

- `HandlerContext.dbSession` resolves `DbSession`
- `DiaryHandlers.save` resolves `IBackgroundJobPublisher`
- `AuthHandlers.login` resolves `JwtConfig`

This is a service-locator style. It works, though constructor injection is usually preferred when framework shape allows it.

## Why `Func<IServiceProvider, ...>` Is Used

`AppStartup.addApplicationServices` uses factory registrations:

- `AddSingleton<IBackgroundJobPublisher>(Func<IServiceProvider, ...>)`
- `AddSingleton<IBackgroundMaintenanceService>(Func<IServiceProvider, ...>)`

The factory gets `IServiceProvider`, then pulls prerequisites with `GetRequiredService`.
Use this when construction needs custom wiring rather than a direct concrete type registration.

## Lifetime Notes

### Singleton

One instance for the whole app lifetime.

In this codebase:

- queues are singleton (shared producer/consumer state)
- background publisher and maintenance service are singleton
- `DbSession` is singleton

### Scoped

One instance per HTTP request. Not currently used for core services here.

### Transient

New instance each resolve. Not currently used for core services here.

## Is Singleton `DbSession` Safe Here?

Yes, with current implementation.

`DbSession` does not hold a single long-lived open `NpgsqlConnection`. It opens a fresh connection per operation in `WithConnection`/`WithTransaction`, and disposes it after use. That makes singleton `DbSession` effectively stateless and safe to share.

## Request and Background Flow

### HTTP flow (save diary)

1. Handler gets `DbSession` and `IBackgroundJobPublisher`.
2. `DiaryService.saveDiary` writes diary data in a transaction.
3. Service enqueues summary/index jobs through publisher.
4. Handler returns quickly; heavy work is deferred.

### Background flow

1. `Worker` reads queue items.
2. `Worker` calls `IBackgroundMaintenanceService` to compute summaries and index/todo updates.
3. Periodic sweep loops repair missing/stale rows.

This design keeps request latency low while preserving eventual consistency.

## Why DI for `IBackgroundJobPublisher` Helps

Using `IBackgroundJobPublisher` as a DI contract gives practical benefits in this codebase:

- Decouples `DiaryService.saveDiary` from queue internals (`Channel`, debounce, and worker scheduling).
- Makes implementation swappable (in-memory queue now, external broker later) by changing registration, not service code.
- Improves testing: tests can inject a fake publisher and assert enqueue behavior without running hosted workers.
- Keeps side effects explicit at the service boundary (`save` triggers async follow-up work) instead of hidden static calls.
- Allows background pipeline changes (batching, retries, dead-letter strategy) with minimal impact on handlers/services.
- Preserves fast request latency by enqueueing quickly and letting worker loops process heavy summary/index updates.

## Practical Rules for New Code

- Register new app-level services in `AppStartup`.
- Prefer explicit interfaces for cross-layer collaborators.
- Keep queues/dispatchers singleton when they coordinate shared background state.
- Use `DbSession.WithTransaction` for multi-write consistency.
- Avoid resolving services from deep utility functions; keep resolution near request or startup boundaries.
