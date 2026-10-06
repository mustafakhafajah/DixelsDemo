# How the backend is built (ABP layers and SOLID)

This page describes the backend as it is today: which ABP layer holds what, and where the five SOLID principles
show up in the code. Every example points to a real file under `backend/src`.

> **History.** The backend once had its own CQRS layer (commands, queries, handlers and dispatchers under
> `Application/Cqrs`). It was removed in favour of ABP's standard application services, so there are no
> `ICommandDispatcher`, `IQueryDispatcher`, `*Command` or `*Query` classes any more. Text that mentions them is
> out of date.

- [The layers in one picture](#the-layers-in-one-picture)
- [What lives in each project](#what-lives-in-each-project)
- [S: Single Responsibility](#s-single-responsibility)
- [O: Open/Closed](#o-openclosed)
- [L: Liskov Substitution](#l-liskov-substitution)
- [I: Interface Segregation](#i-interface-segregation)
- [D: Dependency Inversion](#d-dependency-inversion)
- [Where we knowingly bend the rules](#where-we-knowingly-bend-the-rules)
- [How to add a new feature](#how-to-add-a-new-feature)

---

## The layers in one picture

```
HTTP ─► Controller (HttpApi)          route + HTTP verb only, forwards to the app service interface
          │
          ▼
        App service (Application)     [Authorize] permissions, loads data, maps to DTOs, sends emails
          │
          ▼
        Domain                        entities, domain services (*Manager), rules, specifications
          │
          ▼
        IRepository<T, Guid>  ──►  EF Core (EntityFrameworkCore)  ──►  PostgreSQL (SQLite in tests)
```

Dependencies only point **down**. The Domain doesn't know about HTTP or EF Core; EF Core plugs in from outside.
This is ABP's standard layered startup template, and the code stays inside its projects and folders.

---

## What lives in each project

| Project | What it holds | Examples |
|---|---|---|
| `Dixels.Portal.Domain.Shared` | Constants, enums, error codes, localization resources. No logic. | `Bookings/BookingStatus.cs`, `PortalDomainErrorCodes.cs`, `Localization/` |
| `Dixels.Portal.Domain` | Entities (aggregate roots), domain services (`*Manager`), pure rule classes, specifications, data seeders. | `Bookings/Booking.cs`, `Bookings/BookingManager.cs`, `Estate/ConstraintResolver.cs`, `Identity/RoleDataSeedContributor.cs` |
| `Dixels.Portal.Application.Contracts` | App service interfaces, DTOs, permission names: what the outside world sees. | `Bookings/IBookingAppService.cs`, `Permissions/PortalPermissions.cs` |
| `Dixels.Portal.Application` | App services (one per feature), Mapperly mappers, emails, background jobs, event handlers. | `Bookings/BookingAppService.cs`, `PortalApplicationMappers.cs`, `Bookings/BookingReminderJob.cs` |
| `Dixels.Portal.EntityFrameworkCore` | `PortalDbContext`, one `IEntityTypeConfiguration<T>` per entity, migrations. | `EntityFrameworkCore/Configurations/BookingConfiguration.cs` |
| `Dixels.Portal.HttpApi` | Hand-written API controllers. ABP's auto API controllers are **off**, so these are the API. | `Controllers/Bookings/BookingController.cs` (`/api/app/bookings`) |
| `Dixels.Portal.Web` | The host: OpenIddict sign-in server, ABP's MVC pages (sign-in, account, admin), Swagger, CORS, configuration. | `PortalWebModule.cs`, `Program.cs` |
| `Dixels.Portal.DbMigrator` | Console app that applies migrations and runs every data seeder. | `DbMigratorHostedService.cs` |

### App services

Every feature has one app service that inherits an ABP base class:

| App service | Base class | Why that base |
|---|---|---|
| `BuildingAppService`, `FloorAppService`, `SpaceAppService`, `SpaceTypeAppService` | ABP's `CrudAppService<...>` | Create / read / update / delete with paging; each overrides what needs rules (names in several languages, override checks) |
| `BookingAppService`, `MaintenanceWindowAppService`, `MyProfileAppService`, `ProfilePictureAppService`, `UserDirectoryAppService`, `LanguagePreferenceAppService` | `PortalAppService` (ABP's `ApplicationService` with the portal's localization resource) | The operations are not plain CRUD (series, conflict checks, blocking time, pictures) |
| `PortalProfileAppService` | ABP's own `ProfileAppService` | Replaces ABP's profile service to add field-level validation; ABP still does the work |

ABP wraps each app-service call in a unit of work (one database transaction) and checks the `[Authorize(...)]`
permission attributes before the method runs. Checks that depend on the data (for example "is this your own
booking?") use ABP's `AuthorizationService` inside the method.

### Controllers

The controllers in `Dixels.Portal.HttpApi` implement the same interface as the app service they forward to
(`BookingController : IBookingAppService`), so the compiler keeps them in step. They only choose the route and HTTP
verb (`[Route("api/app/bookings")]`, `[HttpPatch("{id}")]`). Methods that should not be on the API are marked
`[NonAction]` (for example `BuildingController.DeleteAsync`, because deleting a building would leave floors and
bookings behind). `PortalWebModule` notes that auto API controllers are deliberately not used.

---

## S: Single Responsibility

> A class should have **one reason to change**.

| Class | Its one job | File |
|---|---|---|
| Controllers | HTTP routing only | `HttpApi/Controllers/*/…Controller.cs` |
| App services | One feature's use cases: permissions, loading, mapping to DTOs | `Application/<Feature>/…AppService.cs` |
| `*Manager` domain services | Rules that need the database (unique names, a floor belongs to its building, booking conflicts) | `Domain/Buildings/BuildingManager.cs`, `FloorManager`, `SpaceManager`, `SpaceTypeManager`, `Domain/Bookings/BookingManager.cs` |
| `ConstraintResolver` | Effective rules: Space overrides Floor overrides Building | `Domain/Estate/ConstraintResolver.cs` |
| `EstateOverrideRules` | A floor or space may only narrow its parent's rules, never widen them | `Domain/Estate/EstateOverrideRules.cs` |
| `BuildingCalendar` | Closed days and holidays in the building's own time zone | `Domain/Estate/BuildingCalendar.cs` |
| `TimeWindowLifecycle` | Scheduled / in progress / ended / cancelled, for bookings and blocked time alike | `Domain/Estate/TimeWindowLifecycle.cs` |
| `MaintenanceScopeResolver` | "Block Floor 3" → which spaces that covers | `Domain/Maintenance/MaintenanceScopeResolver.cs` |
| `BookingOverlap` | Turns PostgreSQL's "overlapping booking" constraint error into the normal friendly conflict message | `Domain/Bookings/BookingOverlap.cs` |
| `PortalApplicationMappers` | Entity → DTO copying (Mapperly, used through ABP's `ObjectMapper`) | `Application/PortalApplicationMappers.cs` |
| `BookingNotifier` | Booking emails and queuing the reminder | `Application/Bookings/BookingNotifier.cs` |
| `BookingEmails`, `WelcomeEmails` | The text and layout of each email | `Application/Bookings/BookingEmails.cs`, `Application/Users/WelcomeEmails.cs` |
| `SpaceQueryExtensions` | The space list's filters, as reusable query pieces | `Application/Spaces/SpaceQueryExtensions.cs` |
| `*Configuration` (EF) | How one entity maps to a table | `EntityFrameworkCore/EntityFrameworkCore/Configurations/` |

### Why it helps

- **Business rules sit in the Domain.** "Space overrides floor overrides building" and "an override may only
  narrow" are business rules, not screen or database details, so they are small static classes in the Domain and
  are tested without any web or database code (`test/Dixels.Portal.Domain.Tests/Estate/`).
- **One way to make a booking.** `Booking`'s constructor is `internal`; new bookings come from
  `BookingManager.CreateAsync`, which runs the rules first. No app service can skip them.
- **Emails are apart from bookings.** `BookingAppService` saves the booking and calls `BookingNotifier`. What an
  email says changes in `BookingEmails` only, and a broken email is logged and never stops the booking.
- **Mapping is apart from logic.** Mapperly mappers copy the simple fields. Fields that need other tables or the
  reader's language (names, counts, resolved rules) are ignored in the mapper and filled in by the app service, so
  the mapper never touches the database.

---

## O: Open/Closed

> Code should be **open for extension, closed for modification**: add behaviour by adding code, not by editing
> working code.

| Extension point | Add something by… | Without editing… |
|---|---|---|
| Blocked-time scopes | A new class implementing `IMaintenanceScopeStrategy` (today: space, floor and building strategies in `Domain/Maintenance/Scopes/`) | `MaintenanceScopeResolver`, which takes every registered strategy from DI |
| Seed data | A class implementing ABP's `IDataSeedContributor` (`EstateDataSeedContributor`, `RoleDataSeedContributor`, `BookingPermissionsSplitDataSeedContributor`, `OpenIddictDataSeedContributor`) | `PortalDbMigrationService`, which asks ABP's `IDataSeeder` to run all of them |
| Specifications | A new ABP `Specification<T>` | Repositories or existing queries (`OverlappingBookingsSpecification`, `OverlappingMaintenanceSpecification`) |
| Background work | A class deriving from ABP's `AsyncBackgroundJob<TArgs>` (`BookingReminderJob`) | ABP's job manager |
| Reacting to ABP events | A class implementing `ILocalEventHandler<T>` (`WelcomeEmailHandler` for new users) | ABP's identity module, which raises the event |
| EF mappings | An `IEntityTypeConfiguration<T>` class, applied in `PortalDbContext.OnModelCreating` | Other entities' configuration |
| Permissions | A constant in `PortalPermissions` and one line in `PortalPermissionDefinitionProvider` | ABP's authorization system |
| Error → HTTP status | An entry in `PortalDomainErrorCodes.StatusCodes` | `PortalWebModule.ConfigureErrorStatusCodes`, which maps them all |

### Why it helps

- **Seeders.** The `employee` role came with `RoleDataSeedContributor`; splitting `Bookings.ManageAll` into
  separate permissions came with `BookingPermissionsSplitDataSeedContributor`. The migration service didn't change.
- **Specifications.** The half-open overlap rule (09:00–10:00 and 10:00–11:00 don't clash) is written once in
  `OverlappingBookingsSpecification` and reused wherever bookings are checked for clashes, instead of each place
  re-typing the date comparison.
- **Scopes.** A new kind of scope is one new strategy class; the resolver and the app service stay as they are.

---

## L: Liskov Substitution

> Anything that uses an abstraction must keep working when you swap in **any** implementation of it.

| Abstraction | Implementations that are swapped | Proof it works |
|---|---|---|
| `IRepository<T, Guid>` | EF Core on **PostgreSQL** (production) and EF Core on **SQLite** (tests) | The app services are tested on SQLite (`EntityFrameworkCore.Tests`); `SpaceRegistryTests` also builds the space list query for Npgsql to check it translates |
| `IPortalDbSchemaMigrator` | `EntityFrameworkCorePortalDbSchemaMigrator` (real) and `NullPortalDbSchemaMigrator` (does nothing) | `PortalDbMigrationService` loops over whatever is registered |
| `IMaintenanceScopeStrategy` | Space, floor and building strategies | `MaintenanceScopeResolver` calls any of them the same way (`ScopeStrategyTests`) |
| `IDataSeedContributor` | Our four seeders plus ABP's own identity seeder | ABP's `IDataSeeder` runs each one the same way |
| `Specification<T>` | Both overlap specifications | Either can be passed where a `Specification<T>` or `Expression<Func<T,bool>>` is expected |

### Why it helps

Tests run on a throwaway SQLite database, so they are fast and need no PostgreSQL server (which is also why CI
needs no database). That only works because nothing in the Application or Domain layer relies on
PostgreSQL-specific behaviour of the repository. The one deliberate exception is the database's exclusion
constraint that stops two confirmed bookings overlapping on one space; `BookingOverlap` handles its error, and the
normal conflict check in `BookingManager` runs first on both databases.

---

## I: Interface Segregation

> Many small, focused interfaces are better than one big one. No class should depend on methods it doesn't use.

| Small interface | What it exposes | Instead of… |
|---|---|---|
| `IBuildingAppService`, `IFloorAppService`, `ISpaceAppService`, `IBookingAppService`, … | One interface per feature | One "estate service" with every method |
| `IMaintenanceScopeStrategy` | One property and one method | A resolver that knows every kind of scope |
| `IAbpCliRunner` | One method: create the first migration and run the DbMigrator | `PortalDbMigrationService` starting `cmd.exe` / `bash` itself |
| `IPortalDbSchemaMigrator` | One method: `MigrateAsync` | The Domain knowing about EF Core |

### Why it helps

- **Per-feature API interfaces.** Each controller implements exactly one interface, and a change to bookings
  doesn't touch code that only lists spaces.
- **Small seams for testing.** `PortalDbMigrationService` only sees `IAbpCliRunner`, so it can be tested without
  launching a process.

---

## D: Dependency Inversion

> High-level code should depend on **abstractions**, not on concrete low-level classes. The low-level details
> plug in from outside.

| High-level code | Depends on the abstraction | Concrete detail plugged in by DI |
|---|---|---|
| Controllers | `IBookingAppService`, `ISpaceAppService`, … | The app service classes |
| App services, `*Manager` | `IRepository<T, Guid>` | ABP's EF Core repository |
| App services | `IIdentityUserRepository`, `AuthorizationService`, `CurrentUser`, `Clock`, `GuidGenerator` | ABP's implementations (provided through `ApplicationService` / `DomainService`) |
| `BookingNotifier`, `WelcomeEmailHandler` | `IEmailSender`, `IBackgroundJobManager`, `IAppUrlProvider` | ABP's MailKit sender, ABP's background jobs, `App:SelfUrl` / `App:ClientUrl` from configuration |
| `PortalDbMigrationService` (Domain) | `IPortalDbSchemaMigrator`, `IDataSeeder` | `EntityFrameworkCorePortalDbSchemaMigrator` (EF project), ABP's seeder |

### Why it helps

- **The Domain doesn't reference EF Core.** `PortalDbMigrationService` lives in the Domain project and asks an
  interface to migrate; the EF project provides the implementation.
- **Time and IDs are injected.** Code reads ABP's `Clock.Now` instead of `DateTime.Now` and uses
  `GuidGenerator.Create()` instead of `Guid.NewGuid()`. Tests can control the clock, and IDs are sequential GUIDs
  that keep database indexes fast.
- **Email goes through ABP's sender.** Swapping Microsoft 365 for smtp4dev is a settings change
  (`Abp.Mailing.*`), not a code change. See [DEPLOYMENT.md](DEPLOYMENT.md) and [docker.md](docker.md).

---

## Where we knowingly bend the rules

- **Entities have public setters** (`booking.EndUtc = ...`). Strict DDD would expose methods like
  `booking.Reschedule(start, end)`. Today the rules live in `BookingManager` and the app services; `Booking.Cancel()`
  and the `internal` constructor are the first steps toward richer entities.
- **`Space` stores `BuildingId` as well as `FloorId`.** That's duplicated data (the floor already knows its
  building). It makes queries simpler, and `SpaceManager` keeps the two consistent.
- **The booking and blocked-time app services are large** (`BookingAppService` is the biggest file in the
  Application project). They keep one feature's use cases together; private helpers keep each method short.
- **Multi-lingual names are our own small version of ABP's pattern** (`Domain/Localization/MultiLingualObjects.cs`),
  because ABP's `Volo.Abp.MultiLingualObjects` package is not published on NuGet.

---

## How to add a new feature

Example: "an admin books a space on behalf of another user".

1. **Contract** (`Application.Contracts/Bookings`): add the input DTO and a method on `IBookingAppService`. If it
   needs a new permission, add a constant in `PortalPermissions` and define it in
   `PortalPermissionDefinitionProvider`.
2. **Rules** (`Domain/Bookings`): reuse `BookingManager.CreateAsync`; add a rule there if the new case needs one.
3. **App service** (`Application/Bookings/BookingAppService.cs`): implement the method with
   `[Authorize(PortalPermissions.Bookings.…)]`, call the manager, save through the repository, call
   `BookingNotifier`, map to the DTO.
4. **Controller** (`HttpApi/Controllers/Bookings`): the controller implements the same interface, so the build fails
   until you add the method with its route and HTTP verb.
5. **Test** it in `EntityFrameworkCore.Tests` through the app service interface (SQLite, no server needed).
