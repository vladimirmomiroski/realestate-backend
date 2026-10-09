# Task 15H subtype-index disposition

Status: final measured disposition for the PostgreSQL 16 `four-root-discovery-v1` candidate experiment. This record selects no production subtype index.

## Accepted comparison identity

- Unindexed bundle: `../unindexed/cfdde3290bcbde6edf1a95fdcc49f494ff9e84118239dd2403daaafc13a635d4/`.
- Accepted source lineage: `ae89a343a3c41a9c1e7f6f5c4f00fe4d86f805c3`.
- PostgreSQL: 16.14; `postgres:16-alpine`; image ID `sha256:57c72fd2a128e416c7fcc499958864df5301e940bca0a56f58fddf30ffc07777`.
- Profile: 179 invariants; profile SHA-256 `7d389dfbecb10fa0f491a58e6f83bda265cee1ea7e664a43b53f6fcc7c838946`; invariant manifest/result SHA-256 `bae3243bd1da79993710b3522b2f1e7c1c695152cd8dea25d61d13f9d9331be7`.
- Query contract: 21 shapes, 83 commands, 190 parameters, 498 plans; shape-manifest SHA-256 `fce861c91a31a8e505dd0193cc0baaad5982d5c81fc1454258f2281a7b74a979`; aggregate result/order SHA-256 `975a9d37ced4e372c0aec370782bf7932101042ebcd263e14a097fad0d4cab19`.

## Accepted candidate bundles and reproduced gates

| Candidate | Corrected 205-file bundle SHA-256 | G1 | G2 | G3 | G4 | G5 | G6 | Qualifies |
|---|---|---|---|---|---|---|---|---|
| Commercial known-subtypes partial covering | `ffb72a234d1046863f4c084fb2a2da4c687c36ee289df40a94084d750c21b2de` | PASS | PASS | **FAIL** | **FAIL** | PASS | PASS | No |
| Commercial subtypes full covering | `43d7d5f534e13edd524d10760d979274d369f900424d90f47fdde20247f4fe0f` | PASS | PASS | **FAIL** | PASS | PASS | PASS | No |
| Land known-subtypes partial covering | `75a81289d025525188849557aee5d0f20c69a081969e364f08a99ff0978b3490` | PASS | PASS | **FAIL** | **FAIL** | PASS | PASS | No |
| Land subtypes full covering | `bc60ff5272f3c1b59640f255b0dfae6048c4de0b802cfdc8e591fc56a2aa1079` | PASS | PASS | **FAIL** | PASS | PASS | PASS | No |

The locked rule is conjunctive: a candidate qualifies only when every gate G1 through G6 passes. Gate 3 additionally requires each command in the declared filtered-count, shallow-page-root, and deep-page-root trio to achieve both at least 25% median shared-block reduction and at least 20% median execution-time reduction.

All four candidates fail Gate 3. Their qualifying-command shared-block reductions are only 1.446%–3.163%. Correctness and intended-index-use gates pass, and some execution-time medians improve, but those facts cannot override the mandatory whole-command shared-block threshold.

The decision-integrity negative proves that a candidate with `[G1 PASS, G2 PASS, G3 FAIL, G4 PASS, G5 PASS, G6 PASS]` is not selectable. Selection is `all(G1..G6)`, not any-gate or best-effort ranking.

## Final table dispositions

- **Commercial: `NO_INDEX`.** Neither Commercial candidate passes every locked gate.
- **Land: `NO_INDEX`.** Neither Land candidate passes every locked gate.
- **Combined-winner confirmation: not eligible.** There is no independently passing Commercial candidate and no independently passing Land candidate.
- **Task 15I: formally skipped.** No production EF index configuration, migration, or placeholder migration is required.
- **Production migration inventory: 21.** No schema or model-snapshot change is authorized by this disposition.

`NO_INDEX` is an intentional, successful measured outcome. It may be revisited only if the dataset, query shapes, PostgreSQL/runtime behavior, or other material operating conditions change enough to justify a new predeclared experiment. The accepted thresholds are not weakened after observing these results.
