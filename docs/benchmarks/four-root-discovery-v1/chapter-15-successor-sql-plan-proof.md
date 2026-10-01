# Chapter 15 successor SQL and plan proof

Status: authoritative PostgreSQL 16 `four-root-discovery-v1` permanent baseline.

## Immutable authority

- Source commit: `673ce09bfebf5bb0784f8ca500de9fc09c671cdc`.
- Production `src/` Git tree: `367529280368366f8216471daff0c2d9e7984ccd`.
- Accepted index disposition SHA-256:
  `dda685aa1e6053424728cf70dfe8a1a9fc2b9956e8de5c7bd24370cb00c01800`.
- Commercial disposition: `NO_INDEX`.
- Land disposition: `NO_INDEX`.
- Applied and source migration inventory: 21.

The source worktree was clean before profile creation, capture, and the final
publication gate. The sealed raw run recorded the same source commit, and the
publisher revalidated the clean current HEAD immediately before publication.

## Authoritative environment

- PostgreSQL: 16.14 on Alpine Linux.
- Image: `postgres:16-alpine`.
- Image ID and repository digest:
  `sha256:57c72fd2a128e416c7fcc499958864df5301e940bca0a56f58fddf30ffc07777`.
- Docker server: 29.8.1.
- Database storage: a new anonymous volume mounted at
  `/var/lib/postgresql/data`.
- Isolation: loopback-only published port, `AutoRemove=true`, no shared or
  persistent development database.

The disposable container and its anonymous volume were removed after all
online catalog checks completed.

## Locked identities and protocol

- Run ID: `four-root-discovery-v1-baseline-20261001T184801Z-673ce09b`.
- Profile SHA-256:
  `7d389dfbecb10fa0f491a58e6f83bda265cee1ea7e664a43b53f6fcc7c838946`.
- Invariant manifest/result SHA-256:
  `bae3243bd1da79993710b3522b2f1e7c1c695152cd8dea25d61d13f9d9331be7`.
- Query-shape manifest SHA-256:
  `fce861c91a31a8e505dd0193cc0baaad5982d5c81fc1454258f2281a7b74a979`.
- Semantic result/order SHA-256:
  `975a9d37ced4e372c0aec370782bf7932101042ebcd263e14a097fad0d4cab19`.
- Totals: 21 shapes, 83 commands, 190 typed parameters, 498 plans, and
  179/179 profile invariants.
- Sampling: one discarded warm-up plus five measured samples for every
  command, in the locked command order.

All normalized SQL identities, typed parameter arrays, result counts, selected
IDs, and order hashes matched the accepted successor manifest. Raw offline
verification passed before publication.

## Plan and catalog acceptance

All 498 plans were present and structurally valid. There were zero capture
anomalies, zero plan switches, zero spills, and no temporary-I/O finding. The
curated evidence retains the command and sequence medians, shared buffers,
execution times, and median plans.

The Q1 permanent-export gate passed across the count and page samples:

- `IX_ListingTranslations_Q_Trigram` was used for both count and page;
- no `Seq Scan` of `ListingTranslations` was present;
- no `Seq Scan` node had a missing or blank structured relation name;
- the filtered-count median was 10.625 ms;
- the first-page aligned sequence median was 12.581 ms;
- no Q1 warm-up or measured plan spilled.

The final PostgreSQL catalog contained only the shared primary-key indexes
`PK_ListingCommercialDetails` and `PK_ListingLandDetails` on the two subtype
detail tables. No CommercialType or LandType secondary index existed.

## Permanent evidence

- Location:
  `docs/benchmarks/four-root-discovery-v1/evidence/postgresql-16/`.
- Files: 169.
- Total bytes: 1,233,047.
- Complete raw-file inventory SHA-256:
  `41472809280fecf6924abd6622e2bad6c32b7e3d0cbe73a33fc88b31e13e44ef`.
- `baseline-measurements.json` raw SHA-256:
  `f781ce5ce18be1c50f49856743fc409f5750e4fcf233a406f7a9464cbd29e8eb`.
- `environment.json` raw SHA-256:
  `eed38f537001fcfe04e807d9bb07a33c33e826f98a9c045ee3c013b59bd4f59d`.
- `baseline-summary.md` raw SHA-256:
  `6d12ec682ead630b66d5221765c6c52ff8fe23f48d7cc10c6ff04b87981a5c56`.

The complete inventory hash uses ordinally sorted `/`-normalized
`relative-path|lowercase-raw-file-sha256` lines joined with LF and a terminal
LF. Permanent offline verification passed as schema 2 for generation
`four-root-discovery-v1`, lane `postgresql-16`. Export-time and offline
credential scans passed with no credential findings.

## Historical immutability

The frozen Chapter 10F evidence was verified independently before and after
publication with identical anchors:

- 69 files;
- inventory SHA-256:
  `5237fef672c2c57f55e4ee4ff22f5af97fc49a09e7ad116e8b02c48d2c9d0cd1`;
- `baseline-measurements.json` raw SHA-256:
  `d6dac6f58245f7ecd65b626ca1c3b85df2a2d39808a350e8b1536b82779f5a17`;
- committed measurements Git blob:
  `051b93a9b19dbdbcac6a3411afa4636b3b191ad2`.

No historical evidence file changed. PostgreSQL 16 is the authoritative
Chapter 15 performance lane represented by this permanent baseline.
