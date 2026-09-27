# SOLID and CQRS in Dixels.Portal

This document explains how the backend applies the five SOLID principles and CQRS: **where** each one
shows up in our code, **why** we used it, and **which problem it solved**. Every example points to a
real file under `backend/src`.

- [The layers in one picture](#the-layers-in-one-picture)
- [CQRS](#cqrs-command-query-responsibility-segregation)
- [S — Single Responsibility](#s--single-responsibility-principle)
- [O — Open/Closed](#o--openclosed-principle)
- [L — Liskov Substitution](#l--liskov-substitution-principle)
- [I — Interface Segregation](#i--interface-segregation-principle)
- [D — Dependency Inversion](#d--dependency-inversion-principle)
- [Where we knowingly bend the rules](#where-we-knowingly-bend-the-rules)
- [How to add a new use case](#how-to-add-a-new-use-case)

---

## The layers in one picture

```
HTTP ─► App service (permissions only)
          │  ICommandDispatcher / IQueryDispatcher
          ▼
        Handler (one use case)  ──►  Domain (entities, BookingManager, rules)  ──►  IRepository<T>
          │                                                                             │
          └─ Validator / Operations / Mapper (shared, single-purpose helpers)          EF Core ─► PostgreSQL
```

Dependencies only point **down**. The Domain doesn't know about HTTP or EF Core; EF Core plugs in from outside.

---

## CQRS (Command Query Responsibility Segregation)

**The idea:** a request either **changes** something (a *command*) or **reads** something (a *query*),
never both. Each use case is its own small class.

### Where

| Piece | File | Role |
|---|---|---|
| `ICommand<TResult>`, `IQuery<TResult>` | `Application/Cqrs/` | Marks a request as write or read |
| `ICommandHandler<,>`, `IQueryHandler<,>` | `Application/Cqrs/` | The code that runs one request |
| `ICommandDispatcher`, `IQueryDispatcher` | `Application/Cqrs/` | Find the right handler and run it |
| `AddCqrsHandlers(assembly)` | `Application/Cqrs/CqrsServiceCollectionExtensions.cs` | Registers every handler automatically |
| Commands | `Application/<Feature>/Commands/*.cs` | e.g. `Bookings/Commands/CancelBookingCommand.cs` |
| Queries | `Application/<Feature>/Queries/*.cs` | e.g. `Spaces/Queries/GetSpaceRegistryPageQuery.cs` |
| Safety test | `test/.../Cqrs/HandlerRegistrationTests.cs` | Fails if a command/query has no handler |

Each file holds one request **and** its handler, so a use case is read top to bottom in one place.

### Before and after

**Before**, `BookingAppService` was 218 lines holding every booking use case, their shared helpers and
permission attributes in one class:

```csharp
public class BookingAppService : EstateAppServiceBase, IBookingAppService
{
    // 4 injected dependencies, 10 public use cases, 5 private helpers...
    public async Task<BookingDto> RescheduleAsync(Guid id, RescheduleBookingDto input) { /* 30 lines */ }
    public async Task<BookingDto> CancelAsync(Guid id) { /* ... */ }
    // ...
}
```

**After**, the app service only states *who may call what*, and hands the work to one handler:

```csharp
[Authorize]
public class BookingAppService : PortalAppService, IBookingAppService
{
    public Task<BookingDto> RescheduleAsync(Guid id, RescheduleBookingDto input)
        => _commands.SendAsync(new RescheduleBookingCommand(id, input));

    [Authorize(PortalPermissions.Bookings.ManageAll)]
    public Task<int> GetUpcomingCountAsync(EstateScopeDto input)
        => _queries.QueryAsync(new GetUpcomingBookingCountQuery(input));
}
```

### Why, and what problem it solved

| Problem before | How CQRS fixes it |
|---|---|
| App services grew with every feature (Booking 218 lines, Space 153). Changing "reschedule" meant scrolling past nine other use cases. | One file per use case. `RescheduleBookingCommand.cs` is the whole story of rescheduling. |
| A read could accidentally change data (it had the same repositories and helpers as the writes). | Queries and commands have different interfaces and dispatchers. A reviewer sees at a glance which code may write. |
| Every use case in a service paid for every dependency in its constructor. | Each handler injects only what it uses (`CancelBookingCommandHandler` needs 2 things, not 4). |
| Adding a use case meant editing a big shared class (merge conflicts). | Adding a use case means adding a new file. Existing files stay untouched. |

### Choices we made

- **No MediatR.** It moved to a commercial licence; our dispatcher is about 40 lines
  (`CommandDispatcher.cs`, `QueryDispatcher.cs`) and does exactly what we need.
- **The API did not change.** App services keep their names and method signatures, so ABP's auto API
  still produces the same URLs (`POST /api/app/booking/{id}/cancel`) and the React app needed no changes.
- **Permissions stay on the app service.** ABP checks `[Authorize]` on app services, and it also wraps
  each app-service call in a unit of work (one database transaction), which the handler runs inside.
- **One database.** This is CQRS in the code, not two databases. Reads and writes still use the same
  PostgreSQL tables. That's enough for our size; a separate read model can be added later if needed.

---

## S — Single Responsibility Principle

> A class should have **one reason to change**.

### Where

| Class | Its one job | File |
|---|---|---|
| App services | The HTTP boundary: permissions and routing to a handler | `Application/*/…AppService.cs` |
| Each command/query handler | One use case | `Application/*/Commands`, `…/Queries` |
| `BuildingInputValidator`, `FloorInputValidator`, `SpaceInputValidator`, `SpaceTypeNameValidator` | "Is this form input allowed?" | `Application/<Feature>/…Validator.cs` |
| `*DtoMapper` (`BuildingDtoMapper`, `SpaceDtoMapper`, `BookingDtoMapper`, …) | Turn an entity into what the API returns | `Application/<Feature>/…DtoMapper.cs` |
| `BookingOperations` | Booking steps several handlers share (find, owner check, create one, cancel one) | `Application/Bookings/BookingOperations.cs` |
| `MaintenanceBookingImpact` | Which bookings a blocked window hits (count / cancel) | `Application/Maintenance/MaintenanceBookingImpact.cs` |
| `BookingManager` | Booking business rules (hours, conflicts, self-overlap, blocked time) | `Domain/Bookings/BookingManager.cs` |
| `ConstraintResolver` | Effective rules: Space overrides Floor overrides Building | `Domain/Estate/ConstraintResolver.cs` |
| `EstateOverrideRules` | A floor/space may only narrow its parent's rules | `Domain/Estate/EstateOverrideRules.cs` |
| `MaintenanceScopeResolver` | "Block Floor 3" → which spaces that covers | `Domain/Maintenance/MaintenanceScopeResolver.cs` |
| `*Configuration` (EF) | How one entity maps to a table | `EntityFrameworkCore/EntityFrameworkCore/Configurations/` |

### Why, and what problem it solved

- **Mappers.** Building a `SpaceDto` needs the building, the floor and the resolved rules. When that
  lived inside `SpaceAppService`, a change to *what the screen shows* and a change to *how a space is
  saved* touched the same class. Now display changes go to `SpaceDtoMapper` only.
- **Validators.** Create and update must enforce the same rules (unique name, valid floor, overrides only
  narrow). Before, both paths called one private method in a large service; now both handlers use the
  same validator, so the rule can't drift between "create" and "edit".
- **`MaintenanceBookingImpact`.** The preview ("this will affect 4 bookings") and the real schedule must
  agree on what "affected" means. One class guarantees the number the admin saw is the number that runs.
- **Domain rules in the Domain.** "Space overrides floor overrides building" is a business rule, not a
  screen or database detail, so it's in `ConstraintResolver` and is tested without any web or database code
  (`Domain.Tests`).

---

## O — Open/Closed Principle

> Code should be **open for extension, closed for modification**: add behaviour by adding code, not by
> editing working code.

### Where

| Extension point | Add something by… | Without editing… |
|---|---|---|
| CQRS handlers | Adding a `…Command`/`…Query` file with its handler | The dispatcher or the DI registration (`AddCqrsHandlers` scans for it) |
| Seed data | Adding a class that implements `IDataSeedContributor` (`RoleDataSeedContributor`, `EstateDataSeedContributor`, `OpenIddictDataSeedContributor`) | `PortalDbMigrationService`, which runs *all* contributors it finds |
| Specifications | Adding a new `Specification<T>` and combining with `.And(...)` | Repositories or existing queries (`OverlappingBookingsSpecification`, `OverlappingMaintenanceSpecification`) |
| Auto API | Adding a public method to an app service | Any controller (ABP generates it from `ConventionalControllers.Create` in `PortalWebModule`) |
| EF mappings | Adding an `IEntityTypeConfiguration<T>` class | Other entities' configuration |
| Permissions | Adding a constant + one line in `PortalPermissionDefinitionProvider` | The authorization system |

### Why, and what problem it solved

- **Seeders.** When we added the `employee` role, we wrote `RoleDataSeedContributor` and did not touch the
  migration service. When we removed the demo user, we deleted one file; nothing else needed editing.
- **Specifications.** The "half-open window" overlap rule (09:00–10:00 and 10:00–11:00 don't clash) is
  written once in `OverlappingBookingsSpecification`. The conflict check, the blocked-time preview and the
  "cancel affected bookings" command all reuse it instead of each re-typing the date comparison (and
  getting `<` vs `<=` wrong in one of them).
- **Handlers.** Adding "book on behalf of someone" later means one new command file; no existing handler,
  service or registration code changes.

---

## L — Liskov Substitution Principle

> Anything that uses an abstraction must keep working when you swap in **any** implementation of it.

### Where

| Abstraction | Implementations that are swapped | Proof it works |
|---|---|---|
| `IRepository<T, Guid>` | EF Core on **PostgreSQL** (production) and EF Core on **SQLite** (tests) | The same handlers pass 34 tests on SQLite; `SpaceRegistryTests` also builds the registry query for Npgsql to check it translates |
| `IPortalDbSchemaMigrator` | `EntityFrameworkCorePortalDbSchemaMigrator` (real) and `NullPortalDbSchemaMigrator` (does nothing) | `PortalDbMigrationService` loops over whatever is registered and behaves correctly with either |
| `IDataSeedContributor` | Our three seeders plus ABP's own identity seeder | `IDataSeeder` runs each one the same way; none needs special treatment |
| `Specification<T>` | Both overlap specifications | Any of them can be passed where a `Specification<T>` or `Expression<Func<T,bool>>` is expected |

### Why, and what problem it solved

Tests run on a throwaway SQLite database, so they're fast and need no Postgres server. That only works
because nothing in the Application or Domain layer relies on Postgres-specific behaviour of the repository.
If a handler did, a test would pass on SQLite and fail in production. The registry test catches exactly
that class of mistake.

---

## I — Interface Segregation Principle

> Many small, focused interfaces are better than one big one. No class should depend on methods it doesn't use.

### Where

| Small interface | What it exposes | Instead of… |
|---|---|---|
| `ICommandHandler<TCommand,TResult>` / `IQueryHandler<TQuery,TResult>` | One method: `HandleAsync` | A "BookingService" with ten methods every caller gets |
| `ICommandDispatcher` vs `IQueryDispatcher` | Send a command / run a query | One dispatcher that can do both |
| `IPortalUserContext` | `UserId`, `UserIdOrNull`, `IsAdminAsync()` | Inheriting all of `ApplicationService` just to know who is asking |
| `IBuildingAppService`, `IFloorAppService`, `ISpaceAppService`, … | One interface per feature | One `IEstateAppService` with every estate method |

### Why, and what problem it solved

- **Read-only code gets a read-only tool.** `ProfileLookupAppService` injects only `IQueryDispatcher`, so it
  *cannot* send a command. The compiler enforces "this service never writes".
- **Handlers are not app services.** Before, getting the current user or the admin check meant inheriting a
  base class (`EstateAppServiceBase`) that brought the whole ABP application-service toolbox with it.
  Handlers now ask for exactly the three facts they need through `IPortalUserContext`.
- **Per-feature API interfaces.** The frontend's Spaces page and a future mobile client can depend on
  `ISpaceAppService` alone; a change to bookings doesn't ripple into code that only lists spaces.

---

## D — Dependency Inversion Principle

> High-level code should depend on **abstractions**, not on concrete low-level classes. The low-level
> details plug in from outside.

### Where

| High-level code | Depends on the abstraction | Concrete detail plugged in by DI |
|---|---|---|
| App services | `ICommandDispatcher`, `IQueryDispatcher` | `CommandDispatcher`, `QueryDispatcher` |
| Dispatchers | `ICommandHandler<,>` / `IQueryHandler<,>` | Each handler class |
| Handlers, `BookingManager` | `IRepository<T, Guid>` | ABP's EF Core repository |
| Handlers | `IPortalUserContext`, `IClock`, `IGuidGenerator` | `PortalUserContext`, ABP clock, ABP sequential GUIDs |
| `PortalDbMigrationService` (Domain) | `IPortalDbSchemaMigrator` | `EntityFrameworkCorePortalDbSchemaMigrator` (EF project) |
| `PortalDbMigrationService` | `IDataSeeder` | ABP's seeder, which finds every `IDataSeedContributor` |

### Why, and what problem it solved

- **The Domain doesn't reference EF Core.** `PortalDbMigrationService` lives in the Domain project, which
  is not allowed to know about EF Core. It asks an interface to migrate; the EF project provides the
  implementation. Swapping the database (we moved from SQL Server to PostgreSQL) didn't touch the Domain.
- **Time and IDs are injected.** Handlers read `IClock.Now` instead of `DateTime.Now` and use
  `IGuidGenerator` instead of `Guid.NewGuid()`. Tests can control the clock, and IDs are sequential GUIDs
  that keep database indexes fast.
- **The current user is injected.** Handlers never read the HTTP context. The same handler works from an
  API call, a test, or a future background job.

---

## Where we knowingly bend the rules

Being honest about trade-offs helps the next person decide when to fix them:

- **Entities have public setters** (`booking.EndUtc = ...`). Strict DDD would expose methods like
  `booking.Reschedule(start, end)` that protect their own invariants. Today the rules live in
  `BookingManager` and the handlers; `Booking.Cancel()` is the first step toward richer entities.
- **`Space` stores `BuildingId` as well as `FloorId`.** That's duplicated data (the floor already knows its
  building). It makes queries simpler, but the create/update validators must keep the two consistent.
- **Lifecycle is a string** (`"scheduled"`, `"ended"`) rather than an enum, because the API and the SPA
  already use those strings.
- **Validators expose a static `Apply`.** Copying form fields onto an entity isn't validation; it sits there
  because create and update both need it and it's two lines. If it grows, it gets its own class.

---

## How to add a new use case

Example: "an admin books a space on behalf of another user".

1. **Contract** (Application.Contracts): add the input DTO and a method on `IBookingAppService`.
2. **Command** (Application/Bookings/Commands): create `CreateBookingOnBehalfCommand.cs` with
   `record CreateBookingOnBehalfCommand(...) : ICommand<BookingDto>` and its handler. Reuse
   `BookingOperations` and `BookingManager` for the rules.
3. **App service**: add one line that sends the command, with
   `[Authorize(PortalPermissions.Bookings.ManageAll)]`.
4. **Nothing to register.** `AddCqrsHandlers` finds the handler; `HandlerRegistrationTests` proves it.
5. **Test** it in `EntityFrameworkCore.Tests` through the app service interface.
