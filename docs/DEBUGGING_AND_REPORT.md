# Debugging notes and evaluation report

## 1. Errors encountered and how they were fixed

### 1.1 Project did not compile
- **Error**: `CS0234: The type or namespace name 'Services' does not exist in the namespace 'Katalog'`
- **Cause**: `Controllers/StatusBarangControllers.cs` still used `using Katalog.Services;` and `namespace Katalog.Controller`.
- **Fix**: changed to `CampGearBackend.Services` and `namespace CampGearBackend.Controllers` (two lines, no logic change).

### 1.2 Swashbuckle and .NET 10 OpenAPI conflict
- **Error**: `CS0234: 'Models' does not exist in the namespace 'Microsoft.OpenApi'`.
- **Cause**: `Microsoft.AspNetCore.OpenApi 10.x` depends on `Microsoft.OpenApi 2.x`, where the `Models` namespace was removed; Swashbuckle 6.6.2 expects `Microsoft.OpenApi 1.6`.
- **Fix**: switched to `Swashbuckle.AspNetCore 10.3.1` and used the new API:
  ```csharp
  options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
  {
      [new OpenApiSecuritySchemeReference("Bearer", document, null)] = new List<string>()
  });
  ```

### 1.3 `DateOnly` was sent to MySQL as `10/21/2026`
- **Error**: `Incorrect date value: '10/21/2026' for column tanggal_peminjaman`.
- **Cause**: `MySql.Data` formats `DateOnly` with the current culture; the server expects `yyyy-MM-dd`.
- **Fix**: pass `date.ToDateTime(TimeOnly.MinValue)` to both Dapper parameters and `MySqlParameter`. Date filters in SQL use `@From` / `DATE_ADD(@To, INTERVAL 1 DAY)`.

### 1.4 `LAST_INSERT_ID()` returned 0
- **Cause**: `SELECT LAST_INSERT_ID()` on a pooled connection without the insert's command does not reliably return the id.
- **Fix**: used the insert's own command:
  ```csharp
  await cmd.ExecuteNonQueryAsync();
  idPeminjaman = (uint)cmd.LastInsertedId;
  ```
  Details are then inserted with `... SELECT @IdPeminjaman, a.id, ...` using that id.

### 1.5 Stored procedure parameter names
- **Error**: `Parameter 'p_id_peminjaman' not found in the collection`.
- **Cause**: `MySql.Data` looks up procedure parameters by their exact name; `p_idpeminjaman` and `p_id_peminjaman` are different.
- **Fix**: parameter names must exactly match the procedure definition (with underscores).

### 1.6 False "already returned" / "this unit was already returned"
- **Cause**: MySQL/MariaDB applies consistent reads to the procedure's internal `SELECT COUNT(*)` and cannot see the row inserted by an earlier call in the same, not-yet-committed transaction.
- **Fix**: the "already returned" rule and the loan status check live in the service, where the loan is locked with `SELECT ... FOR UPDATE`. The procedure only handles one unit.

### 1.7 Cannot `UPDATE peminjaman` inside the procedure
- **Error**: table `peminjaman` is "already used" by the statement.
- **Fix**: the service sets the loan status to `selesai` after all units are processed, inside the same transaction. The procedure keeps the per-unit fine and unit-status work.

### 1.8 `Column 'id_status_barang' cannot be null` for `hilang`
- **Cause**: `status_barang` has no `hilang` row, so `SELECT id FROM status_barang WHERE nama = 'hilang'` returned `NULL`.
- **Fix**: `hilang` sets `id_status_barang` to the id of `rusak_berat` (and `tidak_tersedia`). `detail_pengembalian` still records `hilang` and its 250000 fine.

### 1.9 Empty reports for a single day
- **Cause**: `DateOnly` was passed as a parameter that the provider converted to a datetime, so `BETWEEN @From AND @To` did not cover the day.
- **Fix**: pass `DateTime` with `00:00:00` and use an exclusive upper bound:
  `>= @From AND < DATE_ADD(@To, INTERVAL 1 DAY)`. Same approach for the activity log.

### 1.10 Controller bodies with `DateOnly` and `[Required]`
- **Issue**: `DateOnly?` with `[Required]` is not reliably validated in JSON bodies.
- **Fix**: service validation returns `400` for a missing date or an end date before the start date (mirrors `chk_tanggal_peminjaman` in the schema).

### 1.11 CSV not readable in Excel
- **Issue**: UTF-8 bytes without a charset hint show garbled Indonesian characters.
- **Fix**: return `text/csv; charset=utf-8`.

### 1.12 Return by another borrower returned 404 instead of 403
- **Symptom** (found during the final regression run): a Peminjam returning someone
  else's loan got `404 Peminjaman not found` instead of `403`, which leaks the
  existence of a loan.
- **Cause**: the ownership check ran after `SELECT ... FOR UPDATE` inside the
  transaction; the lock/read path made the owner row come back empty on the first
  attempt, so the handler fell into the "not found" branch before the ownership test.
- **Fix**: ownership (and existence) is now checked **first** on a separate
  connection before the transaction starts, so the order is deterministic:
  `404` when the loan does not exist, `403` when it belongs to another borrower,
  then `409` for the wrong status. Same shape as `GET /peminjaman/{id}`.

### 1.13 Controller refactor broke error status codes
- **Symptom** after moving from controllers to endpoint groups: every business error
  returned `500` (21 regression failures) and body validation stopped working
  (empty `nama` accepted).
- **Cause**: the `StatusBarangController` style catches every exception and always
  returns 500. `AppException(404/409/...)` was swallowed there, and DataAnnotations
  are not evaluated for minimal-API parameters.
- **Fix**: added `Problem.Fault(ex)` (maps `AppException` to its status, else 500)
  used in every `catch`, and an explicit `string.IsNullOrWhiteSpace(nama)` guard in
  `KategoriService`. Regression suite back to 80/80.

### 1.14 `MapStatusBarang()` was never mapped
- **Symptom**: registering it in `Program.cs` crashed startup with
  `Failure to infer one or more parameters` (`StatusBarang statusBarang` is a class,
  so minimal APIs treat it as the body and `service` becomes ambiguous).
- **Fix**: left `MapStatusBarang` unregistered (the example file stays untouched);
  `GET /api/v1/status*` lookup data is not used by the frontend. It can be enabled
  later by binding the body explicitly with `[FromBody]` inside the group.

### 1.15 Alat create/update could not receive a file
- **Cause**: JSON endpoints only accept `imagePath` as text, and DataAnnotations are
  not evaluated on `[FromForm]` classes bound manually from a minimal-API handler.
- **Fix**: `POST/PUT /alat` read `multipart/form-data` via `HttpRequest.ReadFormAsync()`,
  a new `ImageService` validates size/type/extensions, generates a GUID file name and
  saves into `wwwroot/uploads/alat/`, `app.UseStaticFiles()` serves the files, and
  `AlatService.Validate()` performs the field checks (400 with a clear message).
  Uploads are limited to 2 MB and jpg/jpeg/png/webp.

### 1.16 Uploading an image through the browser
- **Pitfall**: setting `Content-Type: multipart/form-data` manually in `fetch`
  drops the boundary, so the request body cannot be parsed (empty response).
- **Rule**: send the `FormData` object without a `Content-Type` header — documented
  in `docs/FRONTEND_API.md` §6.4.1 with fetch/Axios examples.

### 1.17 Compiler warnings in `JwtService` / `Env`
- **Warnings**: `CS8601`/`CS8604` (null reference assignments in `JwtService`) and
  `CS8618` (non-nullable `Env.Value`).
- **Fix**: fallback values (`?? string.Empty`, `?? "120"`) and `= null!` for the
  configuration holder. Final build: `0 Warning(s) 0 Error(s)`.

## 2. Evaluation report

### 2.1 Features that work well

- Login with phone number + BCrypt, generic 401 message, JWT with id/name/role, expiry 120 minutes.
- Role protection with 401/403 on every endpoint; borrower data is always scoped to the JWT id.
- Full loan lifecycle: submit → approve/reject → handover with physical unit assignment → return with fines.
- Fines computed exactly from `appsettings.json` (late 10000/day; 0 / 25000 / 100000 / 250000 per condition).
- Transactions for every multi-table action; rollback verified by provoking the stock error.
- Activity log from both the services and the trigger.
- Reports (JSON + Excel-friendly CSV) and paged lists on every list endpoint.
- Swagger UI with an Authorize button and a runnable `.http` collection.

### 2.2 Known limitations / unfixed issues

| # | Limitation | Impact | Possible fix |
|---|---|---|---|
| L-1 | Logout has no token blacklist | A stolen token stays valid until expiry | store `jti` in a revoked table and check it in `OnTokenValidated` |
| L-2 | Stock is only re-verified at handover, not reserved at submission | Two requests can both be `diajukan` and the second handover fails with 409 | reserve units at approval |
| L-3 | `hilang` is mapped to `rusak_berat` in `unit_alat` | Unit history cannot distinguish lost from heavily damaged | add `hilang` to `status_barang` (schema change, needs approval) |
| L-4 | Reports are CSV, not PDF | Printing uses Excel/browser print | add a PDF renderer |
| L-5 | Alat images are not resized or thumbnailed | large uploads slow the catalog | add image resizing |
| L-6 | `users.no_telepon` is now `NOT NULL UNIQUE` | Very old seed rows without a phone number would be rejected | intended: the phone number is the login key |
| L-7 | Fine rates are appsettings-only | DB-side calls need the rates passed as parameters | acceptable PDO (procedure parameters) |
| L-8 | No refresh token | Users log in again after 2 hours | add refresh tokens |
| L-9 | No xUnit project | Regression is a shell suite (`docs/regression.sh`) that drives the real HTTP API | add xUnit tests with a throwaway schema |

### 2.3 Next development plans

1. Reserve units when a loan is approved, release them on rejection.
2. Token revocation table for real logout.
3. PDF reports with the CampGear letterhead.
4. Integration tests with a throwaway MySQL schema.
5. Optional client app (Android/web) using this API.
6. Dashboard counts (loans per month, most rented alat, revenue).

## 3. Summary of files delivered

| File | Content |
|---|---|
| `query.sql` | Final database: schema, seed data, 2 functions, 2 procedures, 1 trigger, indexes, COMMIT/ROLLBACK examples |
| `Program.cs` | DI, JWT, Swagger + Bearer, middleware |
| `Controllers/*` | 9 endpoint-group controllers (`MapX` extensions) |
| `Services/*` | business logic, SQL, transactions |
| `Models/*` | entities and DTOs |
| `Middleware/ExceptionMiddleware.cs` | consistent error responses |
| `appsettings.json` | connection string, JWT, fine rates |
| `CampGear-Backend.http` | runnable test collection |
| `README.md`, `docs/*` | setup, design, modules, testing, frontend API guide, OpenAPI JSON, this report |
