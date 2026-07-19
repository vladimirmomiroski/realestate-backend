# Chapter 10F query-review tool

`RealEstate.QueryReview` is a standalone, opt-in .NET 10 console tool. It is not part of `RealEstate.slnx` and does not run during the normal build or test suite.

Every command verifies that an explicitly supplied connection string targets a database whose name starts with `realestate_queryreview`, connects through `RealEstateDbContext` and Npgsql, confirms the connected database identity, and accepts only PostgreSQL major version 16.

The target database must already exist and must be disposable. The explicit acknowledgement flag is mandatory:

```powershell
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -- doctor `
  --connection-string "Host=localhost;Port=5432;Database=realestate_queryreview_local;Username=postgres;Password=<password>" `
  --confirm-disposable
```

Create the deterministic profile only in a fresh disposable database. The command applies the existing committed EF migrations and then inserts and transactionally verifies the profile described in [profile.md](profile.md):

```powershell
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -- profile create `
  --connection-string "Host=localhost;Port=5432;Database=realestate_queryreview_local;Username=postgres;Password=<password>" `
  --confirm-disposable
```

Verify an existing profile separately with SELECT-only checks. This command does not apply migrations or change data:

```powershell
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -- profile verify `
  --connection-string "Host=localhost;Port=5432;Database=realestate_queryreview_local;Username=postgres;Password=<password>" `
  --confirm-disposable
```

Capture the complete EF/Npgsql SQL emitted by direct calls to the committed repositories:

```powershell
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -- capture-sql `
  --connection-string "Host=localhost;Port=5432;Database=realestate_queryreview_local;Username=postgres;Password=<password>" `
  --confirm-disposable
```

`capture-sql` requires an existing profile and verifies all 61 invariants before it constructs a separate tool-local `RealEstateDbContext` with the command interceptor. It directly invokes the committed `ListingRepository` for every listing shape and the committed `AgencyRepository.ExistsAsync` for the public agency pre-check. The fixed inputs, expected results, command roles, and validations are recorded in [query-matrix.md](query-matrix.md).

The stable logical run ID is `chapter-10f-v1-production-sql`. Complete SQL, typed parameter metadata, exact parameter values, result IDs, and logical roles are written to:

```text
<OS temp>/realestate-queryreview/chapter-10f-v1-production-sql/captured-commands.json
```

Connection strings and credentials are never written. Parameter names that indicate passwords, credentials, secrets, or tokens are redacted defensively.

The tool does not read a connection string from application settings or environment variables and does not print the supplied connection string. Future generated output defaults to the operating system temporary directory under `realestate-queryreview`, outside the repository; the current commands print that path but create no output there.

This checkpoint does not run `EXPLAIN`, capture server settings, take benchmark timings or medians, or create, drop, or evaluate indexes. It creates no migration; `profile create` only applies migrations already committed in Infrastructure.
