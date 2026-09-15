# Chapter 14 property-taxonomy generated-SQL delta proof

Date: 2026-09-12

## Verdict and scope

`PROPERTY_TAXONOMY_SQL_PLAN_DELTA_VERIFICATION_READY_FOR_REVIEW`

At branch `perf/property-taxonomy-sql-plan-delta`, source commit
`a79bc467aa766e6ce673a2840a83e59e34e7f8ab`, the settled four-root property
taxonomy changes exactly the eight expected complete-Listing root commands. The
other 25 production commands and all 80 typed parameter records remain exact
against the accepted Chapter 10F SQL artifacts. The existing measured workflow
passes all 198 EXPLAIN executions and current plan gates.

This is a bounded delta acceptance record, not a new performance baseline. It
does not replace or modify `docs/benchmarks/chapter-10f/evidence/`, and no
`baseline export` command was run. `CH13-PERF-01` remains outside this work.

## Disposable environment and migration/profile identity

- Target: newly created local container
  `realestate-queryreview-postgres16-14k`, database identity redacted.
- Image/version: `postgres:16-alpine`, PostgreSQL `16.14`.
- Safety: loopback-only disposable database name, tool ownership verification,
  `--confirm-disposable`, exact container name, and Docker auto-remove enabled.
- Migration history: exactly 21 rows, from
  `20260610042853_AddListingTables` through
  `20260904023937_AddCommercialAndLandPropertyTaxonomy`.
- EF model: `No changes have been made to the model since the last migration.`
- Profile: unchanged `chapter-10f-v2`; 100,000 listings and 200,000
  translations; 50,000 Apartment and 50,000 House listings; no Commercial or
  Land roots were seeded.
- Profile verification: 61/61 established invariants, 8/8 strong-Active checks,
  7/7 coordinate/root-ownership checks, and 8/8 locked result identities.
- Cleanup: the exact container was stopped after verification and auto-removed;
  a filtered `docker ps -a` check returned no remaining task target.

Independent SQL/catalog checks after profile creation returned:

| Check | Result |
|---|---:|
| `ListingCommercialDetails` rows | 0 |
| `ListingLandDetails` rows | 0 |
| Commercial child/root mismatches | 0 |
| Land child/root mismatches | 0 |

Both tables contain a non-null `uuid` `ListingId` shared primary key and one
non-null `varchar(50)` subtype column with database default `Unknown`. Each
foreign key references `Listings(Id)` with `ON DELETE CASCADE`. Catalog output
contained only the implicit unique primary-key index for each empty dependent;
no secondary subtype index exists or was required for this proof.

## Accepted baseline protection and trust anchors

The immutable accepted evidence directory contained 69 files before this run.
Its manifest still has:

- Git blob: `051b93a9b19dbdbcac6a3411afa4636b3b191ad2`;
- raw SHA-256:
  `d6dac6f58245f7ecd65b626ca1c3b85df2a2d39808a350e8b1536b82779f5a17`;
- 68 manifest-referenced canonical artifacts: 68 exact, 0 mismatches;
- complete 69-file path/raw-hash inventory SHA-256:
  `5237fef672c2c57f55e4ee4ff22f5af97fc49a09e7ad116e8b02c48d2c9d0cd1`.

The same count, hashes, Git status, and inventory hash were reproduced after
verification. The accepted baseline has no tracked or untracked changes. The
historical trust anchor is
`chapter-13k3-final-generated-sql-freeze-proof.md`, especially its immutable
manifest identity, normalized-LF-only comparison policy, 33-command matrix,
80-parameter contract, and locked result hash
`7f74f991bf29b6f3ad24d48f2e8e13ecf9f375ea6f9eb0da8f18204c528bfb36`.

## Production capture and parameter identity

The current repository paths produced exactly 33 commands and 80 typed
parameter records. Each current parameter line was compared ordinally with its
accepted command artifact after only CRLF/CR-to-LF normalization. The 33
per-command parameter groups were exact, accounting for all 80 records; names,
CLR types, `DbType`, Npgsql types, nullability, and exact values did not change.

| Command identity | Parameters | Historical normalized SHA-256 | Current normalized SHA-256 | Body |
|---|---:|---|---|---|
| `N1-01-filtered-count` | 0 | `71d892484ec51372fa196a1c61ba1f6d4922e85cd1b6de54c86bdba554db148f` | `71d892484ec51372fa196a1c61ba1f6d4922e85cd1b6de54c86bdba554db148f` | exact |
| `N1-02-page-root` | 2 | `6ac635d49b34479191afb7d9ce1be6a3e9a19b43861c94327c2fb4988d9e665c` | `b6dae805d9a2592ec92cdf7718c3a3b16a5f71c4b87b57e4620a2f8f76b75198` | expected delta |
| `N1-03-translation-split` | 1 | `b37c0c307121df1df5d6119dcb576d278beff0451d32811844ed2a90e75c5403` | `b37c0c307121df1df5d6119dcb576d278beff0451d32811844ed2a90e75c5403` | exact |
| `N1-04-image-split` | 1 | `b48092b5fd5ce53853d5be89a99c577b6f3dce478f221f797001532a3673ad1a` | `b48092b5fd5ce53853d5be89a99c577b6f3dce478f221f797001532a3673ad1a` | exact |
| `P1-01-filtered-count` | 1 | `7e7cbca5ef4d4bc0c972a3614cdc0298f25d98c6df920a98b6d6d819823d9154` | `7e7cbca5ef4d4bc0c972a3614cdc0298f25d98c6df920a98b6d6d819823d9154` | exact |
| `P1-02-page-root` | 3 | `0d4484d8900eafef50333afcbd3cd737de67ecebec433251089c8703f00a5afa` | `a3a4cdd0c595c014025aeadf160a8038dc4fb1704a006bd9e800549515a14b2f` | expected delta |
| `P1-03-translation-split` | 1 | `e4ccdc85713fd6d6fcee8d49b45bf969ebcc1c85bf56fc8225fea6ff68f1a653` | `e4ccdc85713fd6d6fcee8d49b45bf969ebcc1c85bf56fc8225fea6ff68f1a653` | exact |
| `P1-04-image-split` | 1 | `8b83fea33c68b4053ea5fd026ec2577976114dbce781f2aed6c27d601b8fd922` | `8b83fea33c68b4053ea5fd026ec2577976114dbce781f2aed6c27d601b8fd922` | exact |
| `P2-01-filtered-count` | 1 | `b845629ded9b79684afceb9f1da29e85881a0e25fbc778adbe0a76b08e46fa97` | `b845629ded9b79684afceb9f1da29e85881a0e25fbc778adbe0a76b08e46fa97` | exact |
| `P2-02-page-root` | 3 | `401516a17717d27d71166aa6771f56c919991b77a1fda8f9dbd8b133e3f4b0de` | `ac220a9528f7356020291c8d1613ed8182cb999e03a6fcbbda83ba0195374e82` | expected delta |
| `P2-03-translation-split` | 1 | `0020227ffae96e4c3acf6abe79403d4cc95c5b42121f7ce297ce9e97c9b12165` | `0020227ffae96e4c3acf6abe79403d4cc95c5b42121f7ce297ce9e97c9b12165` | exact |
| `P2-04-image-split` | 1 | `a1ddac6b9ed5a6d51e923c16e079b21a108a274af2bca337fb797ed0f2c8c89e` | `a1ddac6b9ed5a6d51e923c16e079b21a108a274af2bca337fb797ed0f2c8c89e` | exact |
| `A1-01-agency-existence` | 1 | `6957da296e3df9a17006b00cdf7862ca144caecc6d5c25bf6608e8dd0ab9caa3` | `6957da296e3df9a17006b00cdf7862ca144caecc6d5c25bf6608e8dd0ab9caa3` | exact |
| `A1-02-filtered-count` | 1 | `a506edf16752b908341e0f558d979124d9bd7718758151b7e8bae5bd9dfa363c` | `a506edf16752b908341e0f558d979124d9bd7718758151b7e8bae5bd9dfa363c` | exact |
| `A1-03-page-root` | 3 | `78592696ad80d0326c0244e60ecfea9a3173e3d0b3799e86d5041e369ba5b23f` | `363713fac9da459fb8e53dd4fea0ba1a0de1df26886f151430edd1e97516f836` | expected delta |
| `A1-04-translation-split` | 1 | `42d6909ae55df01699676218496223e7ff1afe65c1e89a9066a44638200bca17` | `42d6909ae55df01699676218496223e7ff1afe65c1e89a9066a44638200bca17` | exact |
| `A1-05-image-split` | 1 | `47118f996169c5eaec50e9daf97184b5a46ed53bd8605adbd1f30b2fd789409b` | `47118f996169c5eaec50e9daf97184b5a46ed53bd8605adbd1f30b2fd789409b` | exact |
| `R1-01-filtered-count` | 4 | `e670819b7f690e0cc3c7bee6160226152431feb1929cd9dd797114e69b6760ed` | `e670819b7f690e0cc3c7bee6160226152431feb1929cd9dd797114e69b6760ed` | exact |
| `R1-02-page-root` | 6 | `303e2dfe836de14043d960fdc9bfeebcba1e00c4253334868475a5576f6c9bdf` | `dc5dd17d148404863f68851cbfd31cd5b4fde40061974344dc3078ce851ddd94` | expected delta |
| `R1-03-translation-split` | 1 | `9fb4ad29408df9ae8a50997191b4796ad1506a7e809b8a094e8af7d533ca3568` | `9fb4ad29408df9ae8a50997191b4796ad1506a7e809b8a094e8af7d533ca3568` | exact |
| `R1-04-image-split` | 1 | `2fc8bea5437b48e10534bb17a20af4515a878545cfa249f447ce457ed7978072` | `2fc8bea5437b48e10534bb17a20af4515a878545cfa249f447ce457ed7978072` | exact |
| `L1-01-filtered-count` | 5 | `b796b29ddd815aa3e46243794b90ed937344f81ef811bcdb0c00b059b3e03bff` | `b796b29ddd815aa3e46243794b90ed937344f81ef811bcdb0c00b059b3e03bff` | exact |
| `L1-02-page-root` | 7 | `10978b46d7ecf7e416914a1272a55bce9c2b0c28c49c29f08bc9a1bbd84dcddc` | `c1044a40ea63f3f22d22095af5db93c3f6a5cf86956f574f840702b8e53d6a22` | expected delta |
| `L1-03-translation-split` | 1 | `c69350705a93687913a84ec6872d74a4416465aef13728833b8474bd40e1cd62` | `c69350705a93687913a84ec6872d74a4416465aef13728833b8474bd40e1cd62` | exact |
| `L1-04-image-split` | 1 | `c1b1626cb8559db294ec739e29eb53339f605b289c5f22907a9d773d2c519814` | `c1b1626cb8559db294ec739e29eb53339f605b289c5f22907a9d773d2c519814` | exact |
| `Q1-01-filtered-count` | 4 | `024b959782407204569ce8f26427d2ee4814c31df854937e8cd0385c1d2bbce7` | `024b959782407204569ce8f26427d2ee4814c31df854937e8cd0385c1d2bbce7` | exact |
| `Q1-02-page-root` | 6 | `aaeef27c03ad826b3040e33436fd3345094cd697cc3eaea46e21d21152eb6a29` | `e18b77b9b47ef4a46f826c2f21d77e8a67a3ccf00b61515daf597ecd99ae75ee` | expected delta |
| `Q1-03-translation-split` | 1 | `a95a01bc39823b3067b0cd51a70ee380ece5dcdcc80eaa9b69708330126845a0` | `a95a01bc39823b3067b0cd51a70ee380ece5dcdcc80eaa9b69708330126845a0` | exact |
| `Q1-04-image-split` | 1 | `6de558d72f3dcac53389ca493ccb931b8a728c34aed9ee518cdfc8aaf2a6ce2e` | `6de558d72f3dcac53389ca493ccb931b8a728c34aed9ee518cdfc8aaf2a6ce2e` | exact |
| `C1-01-comparable-source` | 3 | `c676a6ba3f9752ef1a91862ad0df2051416bd51a4bed37a15b170c4c860f72a4` | `c676a6ba3f9752ef1a91862ad0df2051416bd51a4bed37a15b170c4c860f72a4` | exact |
| `C1-02-comparable-ranked-root` | 14 | `204e9a1c35cbb2deb3b57f078f30be6be81a43ab359f9294a9bc5a5c4c001dc3` | `f2c288ed5b8b592703a929f464d948119b53517a9767fbef972c6cc0422eb215` | expected delta |
| `C1-03-comparable-translation-split` | 1 | `d105d7ed7dc5296a458706fc0e183cff3967c1979747639f6dbed81fff6d7396` | `d105d7ed7dc5296a458706fc0e183cff3967c1979747639f6dbed81fff6d7396` | exact |
| `C1-04-comparable-image-split` | 1 | `8a2bf5088ecfce33e4b295ab559a5bef2c678f320c367e50d49ffac58bd30a73` | `8a2bf5088ecfce33e4b295ab559a5bef2c678f320c367e50d49ffac58bd30a73` | exact |

The exact-body set is therefore 25 commands: seven filtered counts,
`A1-01-agency-existence`, `C1-01-comparable-source`, eight translation child
loads, and eight image child loads. There are no missing or extra identities.

## Bounded normalized delta for the eight roots

For each changed body, an ordinal script removed only the exact four-column
suffix and the two exact join lines shown below. The resulting normalized text
was byte-for-byte equal to its historical artifact. This proves that every
predicate, effective-translation expression, ordering key, limit/offset,
comparable eligibility/ranking expression, and all other projections are
unchanged.

| Identity | Commercial alias | Land alias | Commercial joins | Land joins | New projected columns | Historical after removing delta |
|---|---|---|---:|---:|---:|---|
| `N1-02-page-root` | `l3` | `l4` | 1 | 1 | 4 | exact |
| `P1-02-page-root` | `l3` | `l4` | 1 | 1 | 4 | exact |
| `P2-02-page-root` | `l3` | `l4` | 1 | 1 | 4 | exact |
| `A1-03-page-root` | `l3` | `l4` | 1 | 1 | 4 | exact |
| `R1-02-page-root` | `l3` | `l4` | 1 | 1 | 4 | exact |
| `L1-02-page-root` | `l7` | `l8` | 1 | 1 | 4 | exact |
| `Q1-02-page-root` | `l7` | `l8` | 1 | 1 | 4 | exact |
| `C1-02-comparable-ranked-root` | `l7` | `l8` | 1 | 1 | 4 | exact |

Normalized diff for the first alias class (applies independently and exactly to
N1, P1, P2, A1, and R1):

```diff
- ..., l2."YardAreaSquareMeters"
+ ..., l2."YardAreaSquareMeters", l3."ListingId", l3."CommercialType", l4."ListingId", l4."LandType"
+ LEFT JOIN "ListingCommercialDetails" AS l3 ON l1."Id" = l3."ListingId"
+ LEFT JOIN "ListingLandDetails" AS l4 ON l1."Id" = l4."ListingId"
```

Normalized diff for the second alias class (applies independently and exactly
to L1, Q1, and C1):

```diff
- ..., l6."YardAreaSquareMeters"
+ ..., l6."YardAreaSquareMeters", l7."ListingId", l7."CommercialType", l8."ListingId", l8."LandType"
+ LEFT JOIN "ListingCommercialDetails" AS l7 ON s0."Id" = l7."ListingId"
+ LEFT JOIN "ListingLandDetails" AS l8 ON s0."Id" = l8."ListingId"
```

No unavoidable pre-existing alias changed in this capture. The delta is exactly
two LEFT JOINs and four projected columns per root command—not two projections.
Counts, source lookup, translation/image hydration, paging, and comparable
semantics are unchanged.

## Locked results and order

The current semantic result hash is the accepted
`7f74f991bf29b6f3ad24d48f2e8e13ecf9f375ea6f9eb0da8f18204c528bfb36`.
All eight locked shapes retained their exact totals, item counts, and order:

| Shape | Total | Items | Ordered ordinal summary |
|---|---:|---:|---|
| N1 | 70,000 | 20 | 3,031 through 3,012 descending |
| P1 | 23,334 | 20 | 69,961 descending by 3,000 through 12,961 |
| P2 | 23,334 | 20 | 68,998 descending by 3,000 through 11,998 |
| A1 | 350 | 20 | 3,001, then 69,801 descending by 1,000 through 51,801 |
| R1 | 1,050 | 20 | 69,844 descending by 1,000 through 50,844 |
| L1 | 140 | 20 | 1,140 through 1,121 descending |
| Q1 | 120 | 20 | 2,120 through 2,101 descending |
| C1 | 30 eligible | 6 | 3,003; 3,002; 3,005; 3,004; 3,006; 3,007 |

## Plan and performance gate

Raw run `chapter-10f-v2-baseline-20260912T211140Z-a79bc467` completed 198/198
EXPLAIN executions: 33 warm-up and 165 measured (five measured rounds for each
command). Structural plan and row-count validation passed. There were zero plan
switches, zero anomalies, zero spills, and zero samples with temp read/write
blocks. Result identity and order remained locked.

Widened-root medians:

| Command | Planning ms | Execution ms | Shared blocks | Temp blocks | Root plan width | Actual rows |
|---|---:|---:|---:|---:|---:|---:|
| N1 root | 0.787 | 62.341 | 3,536 | 0 | 1,120 | 20 |
| P1 root | 0.722 | 45.683 | 3,550 | 0 | 1,120 | 20 |
| P2 root | 0.774 | 45.158 | 3,550 | 0 | 1,120 | 20 |
| A1 root | 0.785 | 1.876 | 602 | 0 | 1,120 | 20 |
| R1 root | 0.876 | 39.953 | 3,536 | 0 | 1,120 | 20 |
| L1 root | 3.701 | 12.565 | 1,519 | 0 | 1,120 | 20 |
| Q1 root | 4.069 | 11.038 | 1,263 | 0 | 1,120 | 20 |
| C1 ranked root | 4.183 | 60.596 | 21,229 | 0 | 1,220 | 6 |

Measured aligned sequence medians:

| Sequence | Execution ms | Shared blocks | Temp blocks | Spill |
|---|---:|---:|---:|---|
| N1 first page | 62.933 | 3,830 | 0 | no |
| P1 first page | 46.314 | 3,832 | 0 | no |
| P2 first page | 46.393 | 3,852 | 0 | no |
| A1 first page | 2.642 | 884 | 0 | no |
| A1 endpoint supplementary | 3.883 | 1,388 | 0 | no |
| R1 first page | 41.595 | 3,838 | 0 | no |
| L1 first page | 13.376 | 1,813 | 0 | no |
| Q1 first page | 11.517 | 1,557 | 0 | no |
| C1 candidate page | 60.950 | 21,318 | 0 | no |
| C1 endpoint supplementary | 61.219 | 21,327 | 0 | no |

The Q1 gate passed: filtered-count median `10.863 ms`, aligned first-page median
`11.517 ms`, no spill, and the count/page plans retained
`IX_ListingTranslations_Q_Trigram`. The observations are below the active 250
ms verification thresholds and also below the accepted export-era timing and
buffer limits; no export was performed. Sequential scans of the two empty new
dependent tables are acceptable and no index-use claim is made for them.

## Query-affecting source freeze

The following SHA-256/Git-blob pairs identify the current query and profile
inputs. A later closeout can verify these paths directly without comparing the
whole Git tree (which will include this proof document):

| Path | SHA-256 | Git blob |
|---|---|---|
| `src/RealEstate.Infrastructure/Persistence/Repositories/ListingRepository.cs` | `bea302e79470bd5de00c5a382f602ee3246ce056b4422caf66a1b16d08a19975` | `906a3a00957d8504c8bfe93b5e378ce8e24f3512` |
| `src/RealEstate.Infrastructure/Persistence/Repositories/AgencyRepository.cs` | `eac9fdc055518f797ed41af710fad6eee773341e35b86378e01b7c7484411c7f` | `2e437f422558f9eebc4255dce5c87898c406aff9` |
| `src/RealEstate.Application/Listings/Repositories/IListingRepository.cs` | `b132829c0234f1819ee5026f15999814e8e1e50c973663cf3e7fe25b76eaa038` | `22b77be4cd055981db07f94a19fe086239541300` |
| `src/RealEstate.Application/Agencies/Repositories/IAgencyRepository.cs` | `259de554b719fd675bd34b8da1e84182960c4535af376863cada67179985dd1b` | `edab2bdbb7ce57d6f45b2c8de9801ad4b7768e16` |
| `src/RealEstate.Infrastructure/Persistence/Configurations/ListingConfiguration.cs` | `31aaaaab350166375578fda8347c3483d31a1c89a39e536a4389d7ea3dcd5143` | `8c7193e406f14263a7f9c02e400946d5cbea0e10` |
| `src/RealEstate.Infrastructure/Persistence/Configurations/ListingCommercialDetailsConfiguration.cs` | `2fd61d92ec7774253302ac1e79fa631b1dbbb7187bc21130a205676b0d45f85b` | `d4ea2230059cbce076c5bddb424396bcc339f433` |
| `src/RealEstate.Infrastructure/Persistence/Configurations/ListingLandDetailsConfiguration.cs` | `21c5779c7245a6e64f10becf6a1d2fd9eef8a9c6ccde3db1abe502af91d6e254` | `4f39fc420df5034cf8a49c2449934494bfcc63f0` |
| `tools/RealEstate.QueryReview/QueryShapeDefinitions.cs` | `a7344e1404d342b6431bbaae963f0a888112494abfa741eaa852b0df4c52c769` | `c651cef33ef239a86b8c467c2510b1af5ba0ce0d` |
| `tools/RealEstate.QueryReview/ProductionCommandCaptureInterceptor.cs` | `1b2574134afdc370f16bab0ca018f7757f740c6d7ef6ad2eea610a22c484953a` | `d18660b4c4cc7527ea50c359f0e065f58c13635a` |
| `tools/RealEstate.QueryReview/DeterministicProfileSeeder.cs` | `64fe05b32ff2af4f2b8fe4946acc45ad6044b801abda178b361a2d500778c5ae` | `8751b85abd540dbd746cfedd25069af8a6a87750` |

## Verification summary

- `dotnet restore`: initial unconstrained .NET 10/MSBuild worker fan-out failed
  without diagnostics; the task-owned workers were drained and the same restore
  completed successfully with single-node/no-reuse settings.
- QueryReview restore: up to date under the same bounded settings.
- solution Release build: PASS, 0 warnings, 0 errors.
- QueryReview Release build: PASS, 0 warnings, 0 errors.
- fresh profile create and independent profile verify: PASS.
- production SQL capture: PASS, 33 commands and 80 typed parameters.
- raw baseline run/offline verification: PASS, 198 plans and Q1 PASS.
- EF pending-model check: PASS, no pending model changes.
- complete Release test suite: PASS, 2,183 passed, 0 failed, 0 skipped.
- QueryReview/profile/baseline source diff: none.
- production/test/migration/API/EF snapshot diff: none.
- accepted Chapter 10F baseline diff: none.
- `baseline export`: not run.

Final verdict: the property-taxonomy read-contract widening is the exact intended
SQL delta and remains inside the existing performance envelope. No stop
condition was triggered.
