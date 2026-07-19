# Chapter 10F production SQL capture matrix

Logical run ID: `chapter-10f-v1-production-sql`

The capture runner first verifies all 61 `chapter-10f-v1` profile invariants. It then constructs a tool-local `RealEstateDbContext` with `ProductionCommandCaptureInterceptor`, instantiates the committed `ListingRepository` and `AgencyRepository`, and calls their public methods directly. No production LINQ expression is copied into the tool.

All paged shapes use page 1 and page size 20. The comparable limit is 6.

| Shape | Exact repository input | Expected result | Required command roles |
|---|---|---|---|
| N1 | `lang=en`, newest, no filters | total 70,000; 20 items | filtered-count, page-root, translation-split, image-split |
| P1 | `lang=en`, `Currency=EUR`, price ascending | total 23,334; 20 items | filtered-count, page-root, translation-split, image-split |
| P2 | `lang=en`, `Currency=EUR`, price descending | total 23,334; 20 items | filtered-count, page-root, translation-split, image-split |
| A1 | agency `20000000-0000-0000-0000-000000000001`, `lang=en`, newest | agency exists; total 350; 20 items | agency-existence, filtered-count, page-root, translation-split, image-split |
| R1 | `lang=en`, area 80-89, rooms 2-3, newest | total 1,050; 20 items | filtered-count, page-root, translation-split, image-split |
| L1 | `lang=en`, `AuditCity10F`, `AuditMunicipality10F`, `AuditNeighborhood10F` | total 140; 20 items | filtered-count, page-root, translation-split, image-split |
| Q1 | `lang=en`, `q=needle10f`, newest | total 120; 20 items | filtered-count, page-root, translation-split, image-split |
| C1 | source `40000000-0000-0000-0000-000000000bb9`, `lang=en`, limit 6 | source found; eligible fixed pool 30; 6 items | comparable-source, comparable-ranked-root, comparable-translation-split, comparable-image-split |

`GetFilteredReadOnlyAsync` always executes its filtered count before loading a page. P2 therefore emits a duplicate EUR count command even though later plan evidence may reuse the structurally identical P1 count. The capture records every command actually produced rather than suppressing it.

The expected C1 order is:

```text
40000000-0000-0000-0000-000000000bbb  # ordinal 3003
40000000-0000-0000-0000-000000000bba  # ordinal 3002
40000000-0000-0000-0000-000000000bbd  # ordinal 3005
40000000-0000-0000-0000-000000000bbc  # ordinal 3004
40000000-0000-0000-0000-000000000bbe  # ordinal 3006
40000000-0000-0000-0000-000000000bbf  # ordinal 3007
```

## Capture contract

Every captured command records:

- stable logical run ID, shape ID, shape-local sequence, and command role;
- complete generated command text without normalization or truncation;
- command type;
- every parameter name;
- runtime CLR type;
- `DbType`;
- Npgsql type and PostgreSQL data type name when provided by Npgsql;
- `IsNullable` and whether the exact value is null;
- exact invariant-culture value, with defensive credential-name redaction.

The JSON artifact does not contain the connection string, username, or password. It is written only beneath the operating-system temporary directory, never beneath the repository.

## Validation gates

The command fails unless:

- all eight logical shapes and all 33 production commands are present;
- each role occurs exactly once per shape, including A1 agency existence and all comparable roles;
- every returned total and page size matches this matrix;
- C1 IDs match the exact six-row order above;
- captured parameters contain CLR, `DbType`, Npgsql type, nullability, and exact-value metadata;
- every public listing command contains the Active predicate;
- every paged/limited root and child command contains server-side deterministic ordering and `LIMIT`;
- P1 and P2 contain the required price direction;
- Q1 applies `ILIKE` only to Title, City, Municipality, and Neighborhood, never Description or AddressLine;
- comparable translation/image split SQL applies the ranked `LIMIT` before joining aggregate children.

These checks validate production-generated SQL and results. They do not run `EXPLAIN`, measure performance, capture medians, inspect server settings, or experiment with indexes.
