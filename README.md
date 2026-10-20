# CampGear Backend

Equipment lending (camping gear rental) REST API for the UKK RPL 2026 case study.
ASP.NET Core Web API (.NET 10) + Dapper + MySQL/MariaDB, JWT authentication, Swagger UI.

## Requirements

- .NET SDK 10
- MySQL 8 or MariaDB 10.4+
- (optional) VS Code REST Client extension for `CampGear-Backend.http`

## Setup

1. Create the database (drops nothing, but run it on an empty `campgear` schema):

```bash
mysql -u admin -p < query.sql
```

`query.sql` creates the schema, seed data, functions, trigger and stored procedures.
The two `CALL` examples at the bottom insert one loan; delete it if you want a clean dataset:

```sql
DELETE FROM detail_peminjaman; DELETE FROM peminjaman;
```

2. Check the connection string and secrets in `appsettings.json`:

| Key | Meaning |
|---|---|
| `Database:connection` | MySQL connection string |
| `Cors:AllowedOrigins` | Frontend origins allowed by CORS |
| `Jwt:Secret` / `Issuer` / `Audience` / `ExpiryMinutes` | JWT signing and lifetime (120 minutes) |
| `Denda:TerlambatPerHari` | Late fine per day per loan (10000) |
| `Denda:RusakRingan` / `RusakBerat` / `Hilang` | Per-unit condition fine (25000 / 100000 / 250000) |

3. Run:

```bash
dotnet run
```

Swagger UI: <http://localhost:5162/swagger>
Frontend guide: [`docs/FRONTEND_API.md`](docs/FRONTEND_API.md)
(Run with the `http` launch profile, or accept the HTTPS redirect.)

## Test accounts

Login uses **phone number + password**.

| Role | Phone number | Password |
|---|---|---|
| Admin | 081200000001 | `admin123` |
| Petugas | 081200000002 | `petugas123` |
| Peminjam | 081200000003 | `peminjam123` |

Passwords are stored as BCrypt hashes (cost 11). The phone number is normalised
(spaces and hyphens removed) on save and on login, so `0812-0000-0003` also works.

## How to use the API

1. `POST /api/v1/auth/login` with a test account, copy `token`.
2. In Swagger click **Authorize** and paste the token (no `Bearer ` prefix), or send
   `Authorization: Bearer <token>` from Postman.
3. Use `CampGear-Backend.http` for the full happy path (login, create loan, approve,
   handover, return with fines, reports).

## Endpoint summary

| Module | Endpoints | Roles |
|---|---|---|
| Auth | `POST /auth/login`, `POST /auth/logout` | public, any token |
| Users | `GET/POST /users`, `GET/PUT/DELETE /users/{id}` | Admin |
| Kategori | `GET /kategori`, `GET /kategori/{id}`, `POST/PUT/DELETE /kategori` | any token (write: Admin) |
| Alat | `GET /alat`, `GET /alat/{id}`, `POST/PUT/DELETE /alat` | any token (write: Admin) |
| Unit alat | `GET /unit-alat`, `GET /unit-alat/{id}`, `POST/PUT/DELETE /unit-alat` | Admin, Petugas (write: Admin) |
| Peminjaman | `GET /peminjaman`, `GET /peminjaman/mine`, `GET /peminjaman/{id}`, `POST /peminjaman`, `PUT/DELETE /peminjaman/{id}`, `PATCH /peminjaman/{id}/approve`, `/reject`, `/borrow`, `GET /peminjaman/monitor` | see Swagger |
| Pengembalian | `GET /pengembalian`, `GET /pengembalian/{id}`, `POST /pengembalian` | see Swagger |
| Log aktivitas | `GET /log-aktivitas` | Admin |
| Laporan | `GET /laporan/peminjaman`, `GET /laporan/pengembalian` (`format=json\|csv`) | Admin, Petugas |

All list endpoints accept `page` and `pageSize` (default 20, maximum 100).

## Business rules

- **Stock is never stored.** Available stock = `unit_alat` rows with
  `status_ketersediaan = 'tersedia'` and `status_barang <> 'rusak_berat'`.
- **Loan total** = sum(`harga_sewa` × `jumlah` × days), days = `DATEDIFF(end, start)`,
  minimum 1 (same-day = 1). Computed by `fn_hitung_lama_sewa`.
- **Status flow**: `diajukan` → `disetujui` → `dipinjam` → `selesai`; `diajukan` → `ditolak`.
  `id_petugas` is taken from the JWT when approving/rejecting.
- **Handover** picks the physical units, sets them to `dipinjam` and the loan to `dipinjam`.
- **Return**: late days = `max(0, DATEDIFF(tanggal_kembali, due date))`,
  late fine = days × `Denda:TerlambatPerHari`.
  `pengembalian.denda` = late fine + sum of condition fines.
  Units: `baik`/`rusak_ringan` → `tersedia`; `rusak_berat`/`hilang` → `tidak_tersedia`.
- **Logout**: JWT is stateless, so logout only writes `log_aktivitas` and the client
  discards the token. No blacklist is implemented (future work).
- Every multi-table action runs in one Dapper transaction
  (`BeginTransactionAsync` / `CommitAsync`, `RollbackAsync` in `catch`).

## Project structure

```
Controllers/   endpoint groups per module (static MapX() extension, like StatusBarangController)
Services/      business rules, validation, Dapper SQL, transactions
Models/        entities, request DTOs, response DTOs
Middleware/    ExceptionMiddleware (consistent error response)
Program.cs     DI, JWT, Swagger, middleware pipeline
query.sql      final database deliverable
CampGear-Backend.http  manual test collection
docs/          documentation, test cases, report
```

## Documentation

- [`docs/ANALYSIS_AND_DESIGN.md`](docs/ANALYSIS_AND_DESIGN.md) — requirements, ERD, use cases, flowcharts
- [`docs/MODULES.md`](docs/MODULES.md) — Input / Process / Output per module
- [`docs/TESTING.md`](docs/TESTING.md) — test case table, screenshots to capture
- [`docs/DEBUGGING_AND_REPORT.md`](docs/DEBUGGING_AND_REPORT.md) — bugs found, fixes, evaluation
- [`docs/FRONTEND_API.md`](docs/FRONTEND_API.md) — **frontend integration guide** (auth, CORS, every endpoint, client examples)
- [`docs/openapi.json`](docs/openapi.json) — OpenAPI spec for codegen/Postman import
