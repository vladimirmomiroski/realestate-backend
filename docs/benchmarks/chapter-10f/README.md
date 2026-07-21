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

The tool does not read a connection string from application settings or environment variables and does not print the supplied connection string. Generated output defaults to the operating system temporary directory under `realestate-queryreview`, outside the repository.

## Raw baseline plan capture

`baseline run` is the opt-in Chapter 10F.2A command. It requires the name of the running disposable PostgreSQL 16 Docker container so the raw environment artifact can include the image identity and resource limits:

```powershell
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -- baseline run `
  --connection-string "Host=localhost;Port=5432;Database=realestate_queryreview_local;Username=postgres;Password=<password>" `
  --confirm-disposable `
  --container-name realestate-queryreview-postgres16
```

The command:

1. verifies all 61 deterministic profile invariants;
2. runs one `VACUUM (ANALYZE)` before measurement;
3. captures Git, .NET/runtime, host, Docker, PostgreSQL settings, extensions, table statistics, relation sizes, and index definitions without credentials;
4. invokes the committed repositories and validates all 33 commands, the current 152 typed parameters, expected result counts/page sizes, and comparable order;
5. retains original typed parameter values only in memory and replays every captured SELECT through `EXPLAIN (ANALYZE, BUFFERS, SETTINGS, SUMMARY, FORMAT JSON)`;
6. runs one complete warm-up round and five complete measured rounds in fixed command order;
7. validates stable SQL, parameter, command-role, result, top-level row-count, and structural-plan hashes;
8. writes exactly 198 raw plan JSON files and scans every output file for connection credentials.

Raw output is written only to:

```text
<OS temp>/realestate-queryreview/chapter-10f-v1-baseline-<UTC>-<commit>/
```

The directory contains `manifest.json`, `captured-commands.json`, `environment-raw.json`, and six raw plan files beneath each of the 33 command-key directories. It is intentionally outside the repository.

## Offline baseline verification

`baseline verify` reads an existing 10F.2A raw-run directory without accepting a connection string and without opening a database connection:

```powershell
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -- baseline verify `
  --run-directory "C:\Users\User\AppData\Local\Temp\realestate-queryreview\chapter-10f-v1-baseline-<UTC>-<commit>"
```

The run directory must be absolute and outside the repository. The verifier requires the fixed 33-command order and exactly six plans per command, recomputes SQL, parameter, result, structural-plan, row, and raw-plan hashes, rejects missing or additional raw plans, and recursively extracts PostgreSQL timing, buffer, spill, scan, join, sort, index, rows-removed, and memory evidence.

Warm-up plans remain auditable but are excluded from medians. Each command median uses measured runs 1-5 only, sorts by the unrounded value and then original run number, and selects the third value. Page and comparable sequences are summed within each original run before the five aligned totals are median-selected.

The Q1 gate fails when the filtered-count execution median exceeds 250 ms, the aligned first-page sequence execution median exceeds 250 ms, or any Q1 warm-up or measured plan spills. Equality at 250 ms passes. A failure records the evidence and stops for owner review; it does not authorize an index or a broader search implementation.

The verifier writes `measurements-raw.json` and a temporary `curated` evidence directory inside the existing OS-temp raw run. It preserves credential scanning. It does not rerun EXPLAIN, connect to PostgreSQL, export evidence into the repository, create or evaluate indexes, add migrations, or alter production code.

## Permanent baseline evidence export

Export is a separate, explicit offline command. It accepts only an absolute verified raw-run directory outside the repository and requires the permanent-export acknowledgement:

```powershell
dotnet run --project tools/RealEstate.QueryReview/RealEstate.QueryReview.csproj -- baseline export `
  --run-directory "C:\Users\User\AppData\Local\Temp\realestate-queryreview\chapter-10f-v1-baseline-<UTC>-<commit>" `
  --confirm-evidence-export
```

The exporter reruns the complete offline verifier before writing anything. It rejects incomplete or extra commands/plans, artifact identity or hash drift, a failing Q1 gate, anomalies, credential findings, relative or repository-contained raw runs, connection/database options, missing confirmation, and arbitrary destination options.

The destination is fixed to `docs/benchmarks/chapter-10f/evidence/`. A successful export contains exactly:

```text
environment.json
baseline-measurements.json
baseline-summary.md
sql/<33 command-key>.sql
baseline-plans/<33 command-key>.json
```

Each permanent plan is the execution-median plan selected by verified measurements. Warm-up and nonmedian plans, raw manifests, logs, and connection information are never exported. The exporter recomputes source and destination SHA-256 hashes, validates the exact file set, and scans every permanent file for credential material before reporting success.
