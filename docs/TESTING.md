# Testing

## How to run the tests

### Automated run (recommended)

```bash
# 1. fresh database
mysql -u admin -p -e "DROP DATABASE IF EXISTS campgear;"
mysql -u admin -p < query.sql
mysql -u admin -p campgear -e "DELETE FROM detail_peminjaman; DELETE FROM peminjaman;"

# 2. start the API (build first)
dotnet build
ASPNETCORE_ENVIRONMENT=Development dotnet bin/Debug/net10.0/CampGear-Backend.dll \
  --urls http://localhost:5162 &

# 3. run the regression suite
bash docs/regression.sh
```

The script prints one `PASS`/`FAIL` line per check and a total.
It expects a clean database — rerunning it without a reset will produce different counts.

### Manual run

1. Load the database: `mysql -u admin -p < query.sql`
2. Start the API: `dotnet run`
3. Open `CampGear-Backend.http` in VS Code (REST Client) **or** Swagger UI at
   <http://localhost:5162/swagger>.
4. Run `AUTH - login as ...` first, then paste each token into
   `@adminToken`, `@petugasToken`, `@peminjamToken` at the top of the `.http` file
   (Swagger: click **Authorize**).

### Last automated result

`docs/regression.sh` on MariaDB 13.0, freshly loaded database, .NET 10:

```
TOTAL: 80  PASS: 80  FAIL: 0
```

Build: `0 Warning(s) 0 Error(s)`.

## Test cases

| ID | Scenario | Test data | Expected result | Actual result | Status |
|---|---|---|---|---|---|
| TC-01 | Login as admin | `081200000001` / `admin123` | 200, token + `role: "Admin"` | 200, token + role Admin | PASS |
| TC-02 | Login with formatted phone | `0812-0000-0003` / `peminjam123` | 200 (hyphens removed) | 200, role Peminjam | PASS |
| TC-03 | Login wrong password | `081200000001` / `wrong` | 401, generic message | 401 `Phone number or password is incorrect` | PASS |
| TC-04 | Login unknown phone | `089999` / `wrong` | 401, **same** message as TC-03 | 401, identical body | PASS |
| TC-05 | Access without token | `GET /alat` | 401 | 401 | PASS |
| TC-06 | Peminjam opens admin endpoint | `GET /users` as Peminjam | 403 | 403 | PASS |
| TC-07 | Peminjam opens another user's loan | `GET /peminjaman/{other}` as Peminjam | 403 | 403 | PASS |
| TC-08 | Add user (Admin) | `{ idRole:3, nama:"Test User", password:"test1234", noTelepon:"0812-9999 8888" }` | 200, phone saved as `081299998888`, password hidden | 200, phone normalised, no hash in list | PASS |
| TC-09 | Duplicate phone/unique rules | create same `noTelepon`, duplicate kategori, duplicate `kodeInventaris` | 409 each | 409 each | PASS |
| TC-10 | Validation error | `POST /kategori {"nama":""}` | 400 with field errors | 400 `{"errors":{"Nama":["nama is required"]}}` | PASS |
| TC-11 | Add alat + unit | alat `Tenda Test` in kategori 1, unit `TEST-001`, status `baik` | 200, unit starts as `tersedia` | 200, `tersedia` | PASS |
| TC-12 | Delete referenced data | `DELETE /alat/1`, `DELETE /kategori/1`, `DELETE /users/3` | 409 for each | 409 for each | PASS |
| TC-13 | Borrow equipment | Peminjam: 2× Tenda Dome 4P + 1× Headlamp, 2026-10-21 → 2026-10-24 (3 days) | 200, `total_biaya = 324000` | total 324000, status `diajukan` | PASS |
| TC-14 | Approve + handover | `PATCH /peminjaman/{id}/approve`, `/borrow` as Petugas | 200, status `disetujui` → `dipinjam`, 3 units assigned | 200; units `TND-DM4-001`, `TND-DM4-002`, `HDL-LED-001`; stock drops | PASS |
| TC-15 | Approve twice | approve same loan again | 409 | 409 `Only peminjaman with status 'diajukan' can be approved` | PASS |
| TC-16 | Return with fine and damaged units | return 2026-10-26 (late 2 days), kondisi `baik`, `rusak_ringan`, `hilang` | `denda = 2×10000 + 0 + 25000 + 250000 = 295000`, loan `selesai`, units `tersedia/tersedia/tidak_tersedia` | exactly 295000; kondisi stored 0 / 25000 / 250000; unit states correct | PASS |
| TC-17 | Return without fine | return on the due date, all `baik` | `keterlambatan = 0`, `denda = 0` | 0 / 0.00 | PASS |
| TC-18 | Return twice | repeat return on a finished loan | 409 | 409 `Only peminjaman with status 'dipinjam' can be returned` | PASS |
| TC-19 | Return a unit not in the loan | `units:[{idUnitAlat: 999}]` | 400 | 400 `does not belong to this peminjaman` | PASS |
| TC-20 | Reports with date range | `GET /laporan/peminjaman?from=2026-10-01&to=2026-10-31` | rows for October only, `totalBiaya` = sum | 2–3 rows, total 454000 in the sample run | PASS |
| TC-21 | Report CSV download | `...&format=csv` | `text/csv`, header row + rows | CSV with 7 columns | PASS |
| TC-22 | Activity log | `GET /log-aktivitas` after the flow | login/logout/CRUD/approve/handover/return rows | 8 rows for the full flow, trigger rows included | PASS |
| TC-23 | Pagination cap | `GET /alat?pageSize=1000` | `pageSize` becomes 100 | 100 | PASS |
| TC-24 | Monitor due/overdue | loan due today, then `status=terlambat` | due loan appears in the default list; overdue filter correct | correct | PASS |

## Negative-path matrix (HTTP status coverage)

| Status | Endpoint used in the test |
|---|---|
| 400 | `POST /kategori` with empty name; return with a foreign unit |
| 401 | `GET /alat` without token; wrong password |
| 403 | Peminjam on `/users`; Peminjam reading another loan; Peminjam approving a loan |
| 404 | `GET /alat/999`; `GET /peminjaman/999`; unknown `idAlat` in a loan |
| 409 | Approve twice; duplicate kategori/phone/unit code; deleting referenced data; return twice |
| 500 | not triggered in normal use; middleware returns a generic body and logs the stack trace server-side |

## Screenshots to capture

Take these from Swagger UI or Postman. Number them `01`–`24` in the report.

**Database / design**
1. ERD diagram (`docs/ANALYSIS_AND_DESIGN.md` section 2)
2. `SHOW TABLES` / `query.sql` open in the editor with functions, trigger and procedures visible
3. Result of `SELECT * FROM log_aktivitas` showing the trigger rows
4. `sp_ajukan_peminjaman` / `sp_proses_pengembalian` source (`SHOW CREATE PROCEDURE`)

**Auth**
5. Swagger **Authorize** dialog with a pasted token
6. Login success response (`token`, `expiresAt`, `user`)
7. Login failure response (generic message)
8. 403 from Peminjam calling `/users`, and 401 without a token

**Admin**
9. `POST /users` success + `GET /users` list (no password field)
10. `POST /kategori`, `PUT`, `DELETE` success, plus the 409 duplicate
11. `POST /alat` + `GET /alat` showing `stokTersedia`
12. `POST /unit-alat` + `GET /unit-alat?idAlat=1`
13. Delete blocked with 409 (alat/kategori/user referenced)

**Peminjam**
14. `GET /alat` list with category, price, stock
15. `POST /peminjaman` success and `GET /peminjaman/mine` with total 324000
16. Loan detail showing assigned `kodeInventaris` after handover
17. `POST /pengembalian` with the three kondisi values

**Petugas**
18. `PATCH /approve` and `PATCH /borrow` success (and the 409 on re-approve)
19. `GET /peminjaman/monitor` with the due/overdue filter
20. `GET /laporan/peminjaman` JSON with total
21. CSV report downloaded/opened in Excel (print preview)

**Return result in the database**
22. `SELECT * FROM pengembalian` (late = 2, denda = 295000)
23. `SELECT * FROM detail_pengembalian` (0 / 25000 / 250000)
24. `SELECT * FROM unit_alat` showing `baik/tersedia`, `rusak_ringan/tersedia`, `rusak_berat/tidak_tersedia`

## Postman / .http collection

`CampGear-Backend.http` contains the whole flow in order. Run the three login
requests first, paste the tokens into the variables at the top, then run the rest.
It also includes negative tests: wrong password, 403 for Peminjam on `/users`,
409 duplicate kategori, 409 delete referenced alat, 401 without a token.
