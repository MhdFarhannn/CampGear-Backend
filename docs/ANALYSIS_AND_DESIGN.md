# CampGear — Analysis and Design

## 1. Requirements

### Actors

| Actor | Description |
|---|---|
| Admin | Manages master data, users, loans and returns; reads the activity log |
| Petugas | Approves/rejects requests, hands over gear, monitors returns, prints reports |
| Peminjam | Browses gear, submits loan requests, returns gear |

### Functional requirements

| ID | Requirement | Actor |
|---|---|---|
| F-01 | Login with phone number + password, receive a JWT | All |
| F-02 | Logout (activity log entry, client discards token) | All |
| F-03 | CRUD users, passwords hashed with BCrypt, phone number normalised | Admin |
| F-04 | CRUD kategori | Admin |
| F-05 | CRUD alat and their physical units (`unit_alat`) | Admin |
| F-06 | View the equipment list with category, price and available stock | Peminjam |
| F-07 | Submit a loan request (multiple alat, quantity, dates), total cost computed | Peminjam |
| F-08 | View own loans only | Peminjam |
| F-09 | Approve/reject a request (`diajukan` → `disetujui`/`ditolak`), officer taken from JWT | Petugas |
| F-10 | Hand over the gear (`disetujui` → `dipinjam`), assign physical units | Petugas |
| F-11 | Monitor returns (borrowed, due today, overdue) | Petugas |
| F-12 | Return gear with condition per unit and automatic fines | Peminjam, Petugas |
| F-13 | Print loan and return reports by date range (JSON or CSV) | Petugas |
| F-14 | View the activity log | Admin |
| F-15 | Admin can view and manage all loans and returns | Admin |

### Non-functional requirements

| ID | Requirement |
|---|---|
| N-01 | Dapper parameterised queries only, no string concatenation |
| N-02 | async/await for all database access, no `.Result` / `.Wait()` |
| N-03 | Passwords never returned in a response and never logged |
| N-04 | Role-based access: 401 for missing/invalid token, 403 for wrong role |
| N-05 | Pagination on every list endpoint, maximum page size 100 |
| N-06 | Composed queries (JOIN / subquery / aggregation), no query inside a loop |
| N-07 | Consistent error body for every failure |
| N-08 | Swagger UI with a JWT Authorize button |
| N-09 | Multi-table actions are atomic (one transaction) |
| N-10 | Fine rates configurable in `appsettings.json` |

## 2. ERD

```mermaid
erDiagram
    roles ||--o{ users : "id_role"
    jenis_kelamin ||--o{ users : "id_jenis_kelamin"
    kategori ||--o{ alat : "id_kategori"
    alat ||--o{ unit_alat : "id_alat"
    status_barang ||--o{ unit_alat : "id_status_barang"
    status_ketersediaan ||--o{ unit_alat : "id_status_ketersediaan"
    status_peminjaman ||--o{ peminjaman : "id_status_peminjaman"
    users ||--o{ peminjaman : "id_peminjam"
    users ||--o{ peminjaman : "id_petugas"
    peminjaman ||--o{ detail_peminjaman : "id_peminjaman"
    alat ||--o{ detail_peminjaman : "id_alat"
    detail_peminjaman ||--o{ detail_unit_peminjaman : "id_detail_peminjaman"
    unit_alat ||--o{ detail_unit_peminjaman : "id_unit_alat"
    peminjaman ||--o| pengembalian : "id_peminjaman"
    pengembalian ||--o{ detail_pengembalian : "id_pengembalian"
    unit_alat ||--o{ detail_pengembalian : "id_unit_alat"
    status_kondisi_kembali ||--o{ detail_pengembalian : "id_status_kondisi_kembali"
    users ||--o{ log_aktivitas : "id_user"
```

Key notes:

- `users` has two relationships to `peminjaman` (`id_peminjam`, `id_petugas`).
- `pengembalian` has `id_peminjaman UNIQUE` → one return header per loan.
- `detail_unit_peminjaman` records which physical unit was handed over for each loan line.
- Available stock is derived, never stored.

## 3. Use cases

```mermaid
flowchart LR
    A[Admin] --> A1[Login / Logout]
    A --> A2[Manage users]
    A --> A3[Manage kategori]
    A --> A4[Manage alat + unit]
    A --> A5[Manage peminjaman]
    A --> A6[Manage pengembalian]
    A --> A7[View activity log]

    P[Petugas] --> P0[Login / Logout]
    P --> P1[Approve / reject loan]
    P --> P2[Hand over gear]
    P --> P3[Monitor returns]
    P --> P4[Print reports]

    U[Peminjam] --> U0[Login / Logout]
    U --> U1[View gear + stock]
    U --> U2[Submit loan request]
    U --> U3[Return gear]
```

UC-01 Login · UC-02 Logout · UC-03 Manage user · UC-04 Manage kategori ·
UC-05 Manage alat/unit · UC-06 View gear · UC-07 Submit loan · UC-08 Approve/reject ·
UC-09 Handover · UC-10 Monitor returns · UC-11 Return with fine · UC-12 Reports ·
UC-13 Activity log.

## 4. Flowcharts

### 4.1 Login (BCrypt + JWT)

```mermaid
flowchart TD
    A[POST /auth/login] --> B[Validate request]
    B -->|invalid| B1[400]
    B --> C[Normalise no_telepon]
    C --> D[SELECT user JOIN roles WHERE no_telepon]
    D --> E{found?}
    E -->|no| F[401 generic message]
    E -->|yes| G[BCrypt.Verify password]
    G -->|false| F
    G -->|true| H[Create claims: id, name, role]
    H --> I[Sign JWT HS256, expires in 120 min]
    I --> J[INSERT log_aktivitas Login]
    J --> K[200 token + user]
```

No user enumeration: wrong phone and wrong password return the same body.

### 4.2 Loan: submit → approve → handover

```mermaid
flowchart TD
    A[POST /peminjaman as Peminjam] --> B[id from JWT claim]
    B --> C[Validate dates and duplicate alat]
    C --> D[BEGIN TRANSACTION]
    D --> E[INSERT peminjaman status diajukan]
    E --> F[Per item: INSERT detail_peminjaman<br>with current harga_sewa]
    F --> G[Compute total with fn_hitung_lama_sewa]
    G --> H[UPDATE total_biaya]
    H --> I[COMMIT + log]
    I --> J[PATCH approve as Petugas]
    J --> K[UPDATE only WHERE status = diajukan<br>set id_petugas from JWT]
    K -->|0 rows| L[409 wrong state]
    K --> M[PATCH borrow]
    M --> N[BEGIN TRANSACTION<br>lock loan FOR UPDATE]
    N --> O[Per detail: pick available units<br>LIMIT jumlah FOR UPDATE]
    O --> P{enough units?}
    P -->|no| Q[ROLLBACK 409]
    P -->|yes| R[INSERT detail_unit_peminjaman<br>set units dipinjam]
    R --> S[Set loan dipinjam, COMMIT + log]
```

Stock is checked at submission (fast feedback) and locked at handover
(`FOR UPDATE`), which is the authoritative gate.

### 4.3 Return and fine calculation

```mermaid
flowchart TD
    A[POST /pengembalian] --> B[Load loan FOR UPDATE]
    B -->|not found| C[404]
    B --> D{status = dipinjam?}
    D -->|no| E[409]
    D --> F{Peminjam calls?}
    F -->|yes, not owner| G[403]
    F --> H{units belong to loan?}
    H -->|no| I[400]
    H --> J[BEGIN TRANSACTION]
    J --> K[Per unit: CALL sp_proses_pengembalian]
    K --> L[late days = max 0, DATEDIFF kembali - due]
    L --> M[denda = late x rate + sum denda_kondisi]
    M --> N[detail_pengembalian + update unit_alat]
    N --> O[UPDATE loan = selesai, COMMIT + log]
```

Fine table:

| Rule | Config key | Column |
|---|---|---|
| Late per day | `Denda:TerlambatPerHari` = 10000 | `pengembalian.denda` |
| baik | 0 | `detail_pengembalian.denda_kondisi` |
| rusak_ringan | 25000 | same |
| rusak_berat | 100000 | same |
| hilang | 250000 | same |

## 5. Database objects added to `query.sql`

| Object | Type | Purpose |
|---|---|---|
| `fn_hitung_lama_sewa` | function | rental days, minimum 1 |
| `fn_hitung_denda_terlambat` | function | `max(0, days late) × daily rate` |
| `sp_ajukan_peminjaman` | procedure | validate one item and insert header + detail atomically |
| `sp_proses_pengembalian` | procedure | per-unit return: fines, unit status |
| `trg_peminjaman_status_log` | trigger | write `log_aktivitas` on status change |
| `uq_users_no_telepon` | constraint | unique phone number (login key) |
| `idx_peminjaman_tanggal`, `idx_log_aktivitas_waktu` | index | date-range reports and log filter |

Schema changes are additive only. Table and column names follow `query.sql`.

## 6. Design decisions

| Decision | Reason |
|---|---|
| Endpoint groups per module | same style as `StatusBarangController`; auth via `.RequireAuthorization(p => p.RequireRole(...))` |
| Dapper + explicit SQL | exam requirement, easy to explain |
| `DefaultTypeMap.MatchNamesWithUnderscores = true` | maps `harga_sewa` → `HargaSewa` without aliases |
| `IDbTransaction` in services | one place per flow (submit, handover, return, delete) |
| CSV for reports | printable, opens in Excel, no PDF library dependency |
| Fine rates in `appsettings.json` | change without touching code or DB |
