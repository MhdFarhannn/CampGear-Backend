# CampGear API — Frontend Integration Guide

Everything a client needs: base URL, auth, CORS, every endpoint, request/response
shapes, status codes, enums, and copy-paste code examples.
Machine-readable spec: [`openapi.json`](openapi.json).

---

## 1. Base URL and environment

| Environment | Base URL |
|---|---|
| Local (default) | `http://localhost:5162` |
| HTTPS profile | `https://localhost:7185` |
| Swagger UI | `http://localhost:5162/swagger` |

All endpoints are prefixed with `/api/v1`.

```env
VITE_API_URL=http://localhost:5162/api/v1
```

Backslashes are **not** used: paths are literal, e.g. `GET /api/v1/alat?page=1`.

---

## 2. CORS

Configured in `Program.cs` from `appsettings.json` → `Cors:AllowedOrigins`.

Allowed origins by default:

```
http://localhost:3000    (React / Next.js)
http://localhost:5173    (Vite)
http://localhost:8080
http://localhost:4200    (Angular)
http://127.0.0.1:3000  http://127.0.0.1:5173  http://127.0.0.1:8080  http://127.0.0.1:4200
```

- `AllowAnyHeader`, `AllowAnyMethod` (needed for `Authorization` and `PATCH`).
- Credentials are **not** required (the JWT travels in the header, not a cookie).
- To use another origin, add it to `Cors:AllowedOrigins` and restart the API.
  Requests from unlisted origins get no `Access-Control-Allow-Origin` header.

---

## 3. Authentication

### 3.1 Login

`POST /api/v1/auth/login` — **public**

```json
{ "noTelepon": "081200000001", "password": "admin123" }
```

`noTelepon` is normalised server-side (spaces and hyphens are removed), so
`0812-0000-0003` works too.

**200 OK**

```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-10-20T09:47:16.082Z",
  "user": { "id": 1, "nama": "Admin CampGear", "role": "Admin" }
}
```

**401 Unauthorized** — wrong phone **or** wrong password (deliberately identical):

```json
{ "title": "Unauthorized", "status": 401, "detail": "Phone number or password is incorrect", "traceId": "..." }
```

### 3.2 Using the token

Send on every protected request:

```
Authorization: Bearer <token>
```

- Lifetime: **120 minutes** (`Jwt:ExpiryMinutes`). `ClockSkew` is 0, so treat
  `expiresAt` as hard.
- Claims: user id, name, role. The API always takes the user id from the token,
  never from the body — a client cannot act as another user.
- Role values are exactly `Admin`, `Petugas`, `Peminjam`.

### 3.3 Logout

`POST /api/v1/auth/logout` — **requires token**

- **200 OK**, empty body.
- JWT is stateless: the server only writes an activity log entry. The client
  **must delete the token** from storage and navigate to the login screen.
- The old token technically stays valid until it expires (no blacklist).

### 3.4 Recommended client flow

```
1. Login -> store { token, expiresAt, user } (localStorage/sessionStorage)
2. On app start: if now > expiresAt -> clear and go to login
3. HTTP interceptor: attach Authorization: Bearer <token>
4. On 401 -> clear session and redirect to login
5. On 403 -> keep session, show "You do not have access"
6. Logout -> POST /auth/logout (best effort), clear storage, redirect
```

---

## 4. Conventions

### 4.1 Pagination

Every list endpoint accepts `page` (1-based, default 1) and `pageSize`
(default 20, **maximum 100** — larger values are silently capped).

```json
{ "items": [ ... ], "page": 1, "pageSize": 20, "total": 42 }
```

### 4.2 Dates

- Request dates: `"YYYY-MM-DD"` (e.g. `"2026-10-21"`).
- Response dates: `"YYYY-MM-DD"`; timestamps are ISO-8601 with offset
  (`"2026-10-20T12:39:54+07:00"`).
- Money: JSON number with 2 decimals (e.g. `324000.00`).

### 4.3 Error format

Every failure returns a problem body:

```json
{ "title": "Conflict", "status": 409, "detail": "Kategori already exists", "traceId": "0HN..." }
```

| Status | When | Client action |
|---|---|---|
| 400 | Bad body, missing date, bad id, invalid range | show `detail` next to the form |
| 401 | Missing/expired/invalid token, wrong credentials | logout and redirect to login |
| 403 | Valid token, wrong role, or another user's data | show "no access" |
| 404 | Resource does not exist | show not-found state |
| 409 | Wrong state (approve twice), duplicate (phone/kategori/kode), still referenced | show `detail` |
| 500 | Unexpected | show generic error, log `traceId` |

### 4.4 Field names

The API uses **camelCase** JSON (`hargaSewa`, `noTelepon`, `idKategori`) but
Indonesian domain names. Status values are fixed lowercase strings.

---

## 5. Enums / fixed values

| Field | Values |
|---|---|
| `role` | `Admin`, `Petugas`, `Peminjam` |
| `status` (peminjaman) | `diajukan`, `disetujui`, `ditolak`, `dipinjam`, `selesai` |
| `statusCondisi` / `statusKondisi` | `baik`, `rusak_ringan`, `rusak_berat`, `hilang` |
| `statusBarang` (unit_alat) | `baik`, `rusak_ringan`, `rusak_berat` |
| `statusKetersediaan` (unit_alat) | `tersedia`, `dipinjam`, `tidak_tersedia` |
| `jenisKelamin` | `Laki-laki`, `Perempuan` |
| `monitor status` filter | `jatuh_tempo`, `terlambat`, or empty |
| `format` (laporan) | `json` (default), `csv` |

Seed ids: roles 1=Admin 2=Petugas 3=Peminjam · status_peminjaman 1=diajukan
2=disetujui 3=ditolak 4=dipinjam 5=selesai · status_barang 1=baik 2=rusak_ringan
3=rusak_berat · status_ketersediaan 1=tersedia 2=dipinjam 3=tidak_tersedia ·
status_kondisi_kembali 1=baik 2=rusak_ringan 3=rusak_berat 4=hilang ·
jenis_kelamin 1=Laki-laki 2=Perempuan.

---

## 6. Endpoints

### 6.1 Auth

| Method | Path | Auth | Body | Response |
|---|---|---|---|---|
| POST | `/auth/login` | public | `LoginRequest` | `LoginResponse` |
| POST | `/auth/logout` | any token | — | empty |

### 6.2 Users — Admin only

| Method | Path | Query | Body / Response |
|---|---|---|---|
| GET | `/users` | `page`, `pageSize`, `search`, `idRole` | paged `UserResponse` |
| GET | `/users/{id}` | — | `UserResponse` |
| POST | `/users` | — | `UserRequest` → empty |
| PUT | `/users/{id}` | — | `UserRequest` → empty |
| DELETE | `/users/{id}` | — | empty · `409` if used by loans/logs |

`UserRequest` — `password` is **required on create**, optional on update
(send `null`/omit to keep the current password). Never returned.

```json
{ "idRole": 3, "idJenisKelamin": 1, "nama": "Andi", "password": "rahasia123", "noTelepon": "0812-7777-6666", "alamat": "Bandung" }
```

`UserResponse`:

```json
{ "id": 4, "nama": "Andi", "noTelepon": "081277776666", "alamat": "Bandung",
  "idRole": 3, "role": "Peminjam", "idJenisKelamin": 1, "jenisKelamin": "Laki-laki",
  "createdAt": "2026-10-20T12:46:48+07:00" }
```

### 6.3 Kategori

| Method | Path | Auth | Body |
|---|---|---|---|
| GET | `/kategori` | any token | — (not paged, small list) |
| GET | `/kategori/{id}` | any token | — |
| POST | `/kategori` | Admin | `{ "nama": "Tenda" }` |
| PUT | `/kategori/{id}` | Admin | `{ "nama": "Tenda" }` |
| DELETE | `/kategori/{id}` | Admin | — · `409` if used by alat |

Response: `{ "id": 1, "nama": "Tenda" }`.

### 6.4 Alat

| Method | Path | Auth | Query / Body |
|---|---|---|---|
| GET | `/alat` | any token | `page`, `pageSize`, `search`, `idKategori` |
| GET | `/alat/{id}` | any token | — |
| POST | `/alat` | Admin | `AlatRequest` |
| PUT | `/alat/{id}` | Admin | `AlatRequest` |
| DELETE | `/alat/{id}` | Admin | `409` if units or history exist |

```json
// AlatRequest
{ "idKategori": 1, "nama": "Tenda Dome 4P", "deskripsi": "Kapasitas 4 orang",
  "hargaSewa": 50000, "imagePath": null }
```

```json
// AlatResponse — stokTersedia is computed live, never stored
{ "id": 1, "idKategori": 1, "kategori": "Tenda", "nama": "Tenda Dome 4P",
  "deskripsi": "Kapasitas 4 orang", "hargaSewa": 50000.00, "imagePath": null,
  "stokTersedia": 3, "createdAt": "2026-10-20T12:22:27+07:00" }
```

### 6.5 Unit alat

| Method | Path | Auth | Query / Body |
|---|---|---|---|
| GET | `/unit-alat` | any token | `page`, `pageSize`, `idAlat`, `idStatusKetersediaan` |
| GET | `/unit-alat/{id}` | any token | — |
| POST | `/unit-alat` | Admin | `{ "idAlat": 1, "kodeInventaris": "TND-DM4-004", "idStatusBarang": 1 }` |
| PUT | `/unit-alat/{id}` | Admin | `{ "kodeInventaris": "...", "idStatusBarang": 1, "idStatusKetersediaan": 1 }` |
| DELETE | `/unit-alat/{id}` | Admin | — |

New units always start as `tersedia`. A unit that is currently `dipinjam`
cannot be edited (`409`).

```json
{ "id": 1, "idAlat": 1, "namaAlat": "Tenda Dome 4P", "idStatusKetersediaan": 1,
  "statusKetersediaan": "tersedia", "kodeInventaris": "TND-DM4-001",
  "idStatusBarang": 1, "statusBarang": "baik", "createdAt": "..." }
```

### 6.6 Peminjaman

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/peminjaman` | **Peminjam** | submit request, status `diajukan` |
| GET | `/peminjaman/mine` | **Peminjam** | own loans, `page`, `pageSize`, `status` |
| GET | `/peminjaman` | Admin, Petugas | `page`, `pageSize`, `status`, `idPeminjam`, `from`, `to` |
| GET | `/peminjaman/monitor` | Admin, Petugas | `page`, `pageSize`, `status` = `jatuh_tempo`/`terlambat` |
| GET | `/peminjaman/{id}` | any (Peminjam: own) | detail |
| PUT | `/peminjaman/{id}` | Admin | only `diajukan` |
| DELETE | `/peminjaman/{id}` | Admin | only `diajukan`/`ditolak` |
| PATCH | `/peminjaman/{id}/approve` | Admin, Petugas | `diajukan` → `disetujui` |
| PATCH | `/peminjaman/{id}/reject` | Admin, Petugas | `{ "catatan": "optional" }` |
| PATCH | `/peminjaman/{id}/borrow` | Admin, Petugas | `disetujui` → `dipinjam`, assigns units |

```json
// POST /peminjaman (Peminjam)
{
  "tanggalPeminjaman": "2026-10-21",
  "tanggalPengembalian": "2026-10-24",
  "items": [ { "idAlat": 1, "jumlah": 2 }, { "idAlat": 6, "jumlah": 1 } ]
}
```

Response: **200 OK**, empty body. Then reload `GET /peminjaman/mine`.

Rules: end ≥ start; each `idAlat` once per request; `jumlah` 1–1000.

```json
// PeminjamanResponse
{
  "id": 1, "idPeminjam": 3, "peminjam": "Peminjam CampGear",
  "idPetugas": 2, "petugas": "Petugas CampGear", "status": "dipinjam",
  "tanggalPeminjaman": "2026-10-21", "tanggalPengembalian": "2026-10-24",
  "totalBiaya": 324000.00, "createdAt": "2026-10-20T12:39:54+07:00",
  "details": [
    { "id": 1, "idAlat": 1, "alat": "Tenda Dome 4P", "jumlah": 2,
      "hargaSewaSaatTransaksi": 50000.00,
      "idUnitAlat": [1, 2], "kodeInventaris": ["TND-DM4-001", "TND-DM4-002"] },
    { "id": 2, "idAlat": 6, "alat": "Headlamp LED", "jumlah": 1,
      "hargaSewaSaatTransaksi": 8000.00,
      "idUnitAlat": [11], "kodeInventaris": ["HDL-LED-001"] }
  ]
}
```

`idUnitAlat` and `kodeInventaris` are empty until the handover; the indexes match
(`idUnitAlat[i]` ↔ `kodeInventaris[i]`). `totalBiaya` =
Σ `hargaSewa` × `jumlah` × days, days = `max(1, end − start)`.

**Monitor** items (for the "due/overdue" screen):

```json
{ "id": 1, "peminjam": "Peminjam CampGear", "noTelepon": "081200000003",
  "tanggalPeminjaman": "2026-10-21", "tanggalPengembalian": "2026-10-24",
  "hariTerlambat": 0, "totalBiaya": 324000.00 }
```

### 6.7 Pengembalian

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/pengembalian` | Peminjam (own), Petugas, Admin | returns all units of the loan |
| GET | `/pengembalian` | Admin, Petugas | `page`, `pageSize`, `from`, `to` |
| GET | `/pengembalian/{id}` | any (Peminjam: own) | detail with per-unit fines |

```json
// POST /pengembalian
{
  "idPeminjaman": 1,
  "tanggalKembali": "2026-10-26",
  "catatan": "telat 2 hari",
  "units": [
    { "idUnitAlat": 1,  "statusKondisi": "baik" },
    { "idUnitAlat": 2,  "statusKondisi": "rusak_ringan", "catatan": "sobek" },
    { "idUnitAlat": 11, "statusKondisi": "hilang" }
  ]
}
```

Get the `idUnitAlat` values straight from `GET /peminjaman/{id}`:
`details[].idUnitAlat` (parallel to `details[].kodeInventaris`, which is for display).
Only units that are actually handed over appear there, so the return form should
iterate `details[].idUnitAlat` and let the user choose a condition per unit.

Response: **200 OK**, empty body.

```json
// PengembalianResponse
{ "id": 1, "idPeminjaman": 1, "peminjam": "Peminjam CampGear",
  "tanggalKembali": "2026-10-26", "keterlambatanPengembalian": 2,
  "denda": 295000.00, "catatan": "telat 2 hari",
  "details": [
    { "idUnitAlat": 1, "kodeInventaris": "TND-DM4-001", "alat": "Tenda Dome 4P",
      "statusKondisi": "baik", "dendaKondisi": 0.00, "catatan": null }
  ] }
```

Fine rules (configured in `appsettings.json` → `Denda`):

| Kondisi | Fine per unit |
|---|---|
| baik | 0 |
| rusak_ringan | 25 000 |
| rusak_berat | 100 000 |
| hilang | 250 000 |

Late fine = `keterlambatanPengembalian` × 10 000. `denda` = late + all unit fines.
Unit state after return: `baik`/`rusak_ringan` → `tersedia`;
`rusak_berat`/`hilang` → `tidak_tersedia`.

### 6.8 Log aktivitas — Admin

`GET /api/v1/log-aktivitas` — `page`, `pageSize`, `idUser`, `from`, `to`

```json
{ "id": 8, "idUser": 2, "namaUser": "Petugas CampGear",
  "aktivitas": "Menyetujui peminjaman #1", "waktu": "2026-10-20T12:44:55+07:00" }
```

### 6.9 Laporan — Admin, Petugas

| Method | Path | Query |
|---|---|---|
| GET | `/laporan/peminjaman` | `from`, `to`, `format=json\|csv` |
| GET | `/laporan/pengembalian` | `from`, `to`, `format=json\|csv` |

Default range = current month. `to < from` → `400`.

```json
// /laporan/peminjaman
{ "from": "2026-10-01", "to": "2026-10-31", "totalBiaya": 336000.00,
  "items": [ { "id": 1, "peminjam": "Peminjam CampGear", "petugas": "Petugas CampGear",
    "status": "selesai", "tanggalPeminjaman": "2026-10-21",
    "tanggalPengembalian": "2026-10-24", "totalBiaya": 324000.00, "jumlahAlat": 3 } ] }
```

```json
// /laporan/pengembalian
{ "from": "2026-10-01", "to": "2026-10-31", "totalDenda": 295000.00,
  "items": [ { "id": 1, "idPeminjaman": 1, "peminjam": "Peminjam CampGear",
    "tanggalKembali": "2026-10-26", "keterlambatanPengembalian": 2,
    "denda": 295000.00, "dendaKondisi": 275000.00 } ] }
```

`format=csv` returns `text/csv; charset=utf-8` (semicolon separator, header row)
as a file download — useful for the print/export button.

---

## 7. Screen-by-screen mapping

### Login screen
`POST /auth/login` → store token, redirect by `user.role`:
`Admin` → dashboard, `Petugas` → staff dashboard, `Peminjam` → catalog.

### Peminjam — catalog
`GET /alat?page&pageSize&search&idKategori` + `GET /kategori`.
Show `nama`, `kategori`, `hargaSewa`, `stokTersedia`. Disable the borrow button
when `stokTersedia === 0`.

### Peminjam — borrow form
Cart of `{ idAlat, jumlah }` → `POST /peminjaman`.
Client-side check: `tanggalPengembalian >= tanggalPeminjaman`.

### Peminjam — my loans
`GET /peminjaman/mine?status=` → badge with `status`.
For `dipinjam`, show a "Return" button → return form.

### Peminjam — return form
`GET /peminjaman/{id}` for the loan, `GET /unit-alat?idAlat=` to resolve unit ids
by `kodeInventaris`, choose a condition per unit, then `POST /pengembalian`.
Preview the expected fine client-side with the table in 6.7.

### Petugas — approvals
`GET /peminjaman?status=diajukan` → `PATCH /{id}/approve` or `/{id}/reject`.
After approval: `PATCH /{id}/borrow` for the handover.

### Petugas — return monitoring
`GET /peminjaman/monitor` (all active) or `?status=terlambat`.
Highlight rows with `hariTerlambat > 0` and show `noTelepon`.

### Petugas — reports
`GET /laporan/peminjaman?from&to` and `GET /laporan/pengembalian?from&to`;
"Print" → open `...&format=csv` in a new tab, or print the rendered table.

### Admin — master data
`/users`, `/kategori`, `/alat`, `/unit-alat` CRUD screens.
Show `detail` from the error body on `409`.

### Admin — activity log
`GET /log-aktivitas?page&pageSize&idUser&from&to`.

---

## 8. Client examples

### fetch (vanilla / React)

```js
const API = "http://localhost:5162/api/v1";

export async function api(path, { method = "GET", body, token, query } = {}) {
  const url = new URL(API + path);
  Object.entries(query ?? {}).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== "") url.searchParams.set(k, v);
  });

  const res = await fetch(url, {
    method,
    headers: {
      ...(body ? { "Content-Type": "application/json" } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });

  if (res.status === 204 || res.headers.get("content-length") === "0") return null;

  const data = await res.json().catch(() => null);
  if (!res.ok) throw Object.assign(new Error(data?.detail ?? "Request failed"), { status: res.status, data });
  return data;
}
```

```js
// login
const session = await api("/auth/login", {
  method: "POST",
  body: { noTelepon: "081200000003", password: "peminjam123" },
});
localStorage.setItem("campgear", JSON.stringify(session));

// catalog
const { items, total } = await api("/alat", { token, query: { page: 1, pageSize: 20 } });

// submit loan
await api("/peminjaman", {
  method: "POST", token,
  body: {
    tanggalPeminjaman: "2026-10-21",
    tanggalPengembalian: "2026-10-24",
    items: [{ idAlat: 1, jumlah: 2 }],
  },
});
```

### Axios

```js
import axios from "axios";

export const http = axios.create({ baseURL: "http://localhost:5162/api/v1" });

http.interceptors.request.use((config) => {
  const token = JSON.parse(localStorage.getItem("campgear") ?? "null")?.token;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

http.interceptors.response.use(
  (r) => r,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem("campgear");
      location.href = "/login";
    }
    return Promise.reject(error);
  }
);
```

### Flutter / Dart

```dart
final res = await http.post(
  Uri.parse('http://localhost:5162/api/v1/auth/login'),
  headers: {'Content-Type': 'application/json'},
  body: jsonEncode({'noTelepon': '081200000003', 'password': 'peminjam123'}),
);
// headers for later calls: {'Authorization': 'Bearer $token'}
```

Note: Android emulator reaches the host at `http://10.0.2.2:5162`.

### Downloading a CSV report

```js
const res = await fetch(`${API}/laporan/peminjaman?from=${from}&to=${to}&format=csv`, {
  headers: { Authorization: `Bearer ${token}` },
});
const blob = await res.blob();
const link = document.createElement("a");
link.href = URL.createObjectURL(blob);
link.download = `laporan-${from}-${to}.csv`;
link.click();
```

---

## 9. UI reference — test accounts

| Role | Phone | Password |
|---|---|---|
| Admin | 081200000001 | `admin123` |
| Petugas | 081200000002 | `petugas123` |
| Peminjam | 081200000003 | `peminjam123` |

## 10. Known gaps the frontend must handle

1. **Logout does not invalidate the token.** Treat it as a local logout.
2. **No refresh token.** After 120 minutes a request returns 401 → login again.
3. **No image upload.** `imagePath` is a free text URL; the client should fall back
   to a placeholder when it is `null`.
4. **Delete is restricted.** Users/alats/units with history return `409`; the UI
   should hide/disable the delete action instead of retrying.
5. **Stock is checked again at handover.** A loan can be approved while another
   request takes the units; `/borrow` then returns `409`.
6. **A loan must be returned in full.** `POST /pengembalian` takes every unit of the
   loan; partial returns are not supported.
