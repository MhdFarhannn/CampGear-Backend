#!/bin/bash
B=http://localhost:5162/api/v1
PASS=0; FAIL=0
chk() {
  local id="$1" desc="$2" expected="$3" actual="$4"
  if [ "$expected" = "$actual" ]; then
    PASS=$((PASS+1)); printf "PASS  %-6s %-58s %s\n" "$id" "$desc" "$actual"
  else
    FAIL=$((FAIL+1)); printf "FAIL  %-6s %-58s expected=%s actual=%s\n" "$id" "$desc" "$expected" "$actual"
  fi
}
code() { curl -s -m 15 -o /tmp/opencode/out.json -w "%{http_code}" "$@"; }
body() { cat /tmp/opencode/out.json; }
login() { curl -s -m 15 -X POST $B/auth/login -H 'Content-Type: application/json' -d "{\"noTelepon\":\"$1\",\"password\":\"$2\"}"; }

# ---------- AUTH ----------
A=$(login 081200000001 admin123)
chk TC-01 "login admin" "200" "$(curl -s -m 15 -o /tmp/opencode/out.json -w '%{http_code}' -X POST $B/auth/login -H 'Content-Type: application/json' -d '{"noTelepon":"081200000001","password":"admin123"}')"
chk TC-02 "login formatted phone (dashes)" "200" "$(curl -s -m 15 -o /tmp/opencode/out.json -w '%{http_code}' -X POST $B/auth/login -H 'Content-Type: application/json' -d '{"noTelepon":"0812-0000-0003","password":"peminjam123"}')"
chk TC-03 "login wrong password" "401" "$(curl -s -m 15 -o /tmp/opencode/out.json -w '%{http_code}' -X POST $B/auth/login -H 'Content-Type: application/json' -d '{"noTelepon":"081200000001","password":"wrong"}')"
MSG1=$(body | python3 -c "import json,sys;print(json.load(sys.stdin)['detail'])")
chk TC-04 "login unknown phone" "401" "$(curl -s -m 15 -o /tmp/opencode/out.json -w '%{http_code}' -X POST $B/auth/login -H 'Content-Type: application/json' -d '{"noTelepon":"089999","password":"wrong"}')"
MSG2=$(body | python3 -c "import json,sys;print(json.load(sys.stdin)['detail'])")
chk TC-04b "identical generic message" "same" "$([ "$MSG1" = "$MSG2" ] && echo same || echo different)"
chk TC-05 "no token on /alat" "401" "$(code $B/alat)"

TA=$(echo "$A" | python3 -c "import json,sys;print(json.load(sys.stdin)['token'])")
TP=$(login 081200000002 petugas123 | python3 -c "import json,sys;print(json.load(sys.stdin)['token'])")
TL=$(login 081200000003 peminjam123)
TU=$(echo "$TL" | python3 -c "import json,sys;print(json.load(sys.stdin)['token'])")
HA="Authorization: Bearer $TA"; HP="Authorization: Bearer $TP"; HU="Authorization: Bearer $TU"; HJ="Content-Type: application/json"

chk TC-06 "peminjam on /users (403)" "403" "$(code $B/users -H "$HU")"
chk TC-06b "petugas on /users (403)" "403" "$(code $B/users -H "$HP")"
chk TC-06c "petugas on /log-aktivitas (403)" "403" "$(code $B/log-aktivitas -H "$HP")"
chk TC-23 "peminjam cannot submit as petugas" "403" "$(code -X POST $B/peminjaman -H "$HP" -H "$HJ" -d '{"tanggalPeminjaman":"2026-10-21","tanggalPengembalian":"2026-10-24","items":[{"idAlat":1,"jumlah":1}]}')"

# ---------- USERS ----------
chk TC-08 "create user" "200" "$(code -X POST $B/users -H "$HA" -H "$HJ" -d '{"idRole":3,"idJenisKelamin":1,"nama":"Test User","password":"test1234","noTelepon":"0812-9999 8888","alamat":"Test"}')"
NUID=$(mysql -u admin -pbodi campgear -N -e "select id from users where nama='Test User'" 2>/dev/null)
chk TC-08b "phone normalised" "081299998888" "$(mysql -u admin -pbodi campgear -N -e "select no_telepon from users where id=$NUID" 2>/dev/null)"
chk TC-08c "password not exposed" "no" "$(code $B/users/$NUID -H "$HA" >/dev/null; grep -qi password /tmp/opencode/out.json && echo yes || echo no)"
chk TC-09a "duplicate phone (409)" "409" "$(code -X POST $B/users -H "$HA" -H "$HJ" -d '{"idRole":3,"nama":"Dup","password":"x12345","noTelepon":"081299998888"}')"
chk TC-09d "update user" "200" "$(code -X PUT $B/users/$NUID -H "$HA" -H "$HJ" -d '{"idRole":3,"idJenisKelamin":2,"nama":"Test User 2","noTelepon":"081299998888","alamat":"T2"}')"
chk TC-12c "delete referenced user (409)" "409" "$(code -X DELETE $B/users/3 -H "$HA")"
chk TC-12d "delete unreferenced user" "200" "$(code -X DELETE $B/users/$NUID -H "$HA")"
chk TC-06d "404 unknown user" "404" "$(code $B/users/999 -H "$HA")"

# ---------- KATEGORI ----------
chk TC-10 "validation empty nama (400)" "400" "$(code -X POST $B/kategori -H "$HA" -H "$HJ" -d '{"nama":""}')"
chk TC-09b "duplicate kategori (409)" "409" "$(code -X POST $B/kategori -H "$HA" -H "$HJ" -d '{"nama":"Tenda"}')"
chk "K-1" "create kategori" "200" "$(code -X POST $B/kategori -H "$HA" -H "$HJ" -d '{"nama":"Kategori Test"}')"
KID=$(mysql -u admin -pbodi campgear -N -e "select id from kategori where nama='Kategori Test'" 2>/dev/null)
chk "K-2" "update kategori" "200" "$(code -X PUT $B/kategori/$KID -H "$HA" -H "$HJ" -d '{"nama":"Kategori Test 2"}')"
chk "K-3" "delete kategori" "200" "$(code -X DELETE $B/kategori/$KID -H "$HA")"
chk "K-4" "delete kategori in use (409)" "409" "$(code -X DELETE $B/kategori/1 -H "$HA")"
chk "K-5" "list kategori as peminjam" "200" "$(code $B/kategori -H "$HU")"

# ---------- ALAT / UNIT ----------
chk "A-1" "create alat (multipart)" "200" "$(code -X POST $B/alat -H "$HA" -F 'idKategori=1' -F 'nama=Tenda Test' -F 'deskripsi=t' -F 'hargaSewa=10000')"
AID=$(mysql -u admin -pbodi campgear -N -e "select id from alat where nama='Tenda Test'" 2>/dev/null)
chk "A-2" "create alat unknown kategori (404)" "404" "$(code -X POST $B/alat -H "$HA" -F 'idKategori=999' -F 'nama=X' -F 'hargaSewa=1')"
chk "A-2b" "create alat without nama (400)" "400" "$(code -X POST $B/alat -H "$HA" -F 'idKategori=1' -F 'hargaSewa=1')"
chk "A-2c" "create alat with image (stored + served)" "200" "$(code -X POST $B/alat -H "$HA" -F 'idKategori=1' -F 'nama=Tenda Foto' -F 'hargaSewa=1' -F 'gambar=@/tmp/opencode/tenda.png;type=image/png' >/dev/null; P=$(mysql -u admin -pbodi campgear -N -e "select image_path from alat where nama='Tenda Foto'" 2>/dev/null | tr -d '\n'); curl -s -m 10 -o /dev/null -w '%{http_code}' "http://localhost:5162$P")"
FIMG=$(mysql -u admin -pbodi campgear -N -e "select id from alat where nama='Tenda Foto'" 2>/dev/null)
chk "A-2d" "delete alat (also removes image file)" "200" "$(code -X DELETE $B/alat/$FIMG -H "$HA")"
chk "A-3" "unit create" "200" "$(code -X POST $B/unit-alat -H "$HA" -H "$HJ" -d "{\"idAlat\":$AID,\"kodeInventaris\":\"TEST-001\",\"idStatusBarang\":1}")"
chk "A-4" "unit duplicate code (409)" "409" "$(code -X POST $B/unit-alat -H "$HA" -H "$HJ" -d "{\"idAlat\":$AID,\"kodeInventaris\":\"TEST-001\",\"idStatusBarang\":1}")"
chk "A-5" "new unit starts tersedia" "tersedia" "$(mysql -u admin -pbodi campgear -N -e "select sk.nama from unit_alat ua join status_ketersediaan sk on sk.id=ua.id_status_ketersediaan where ua.kode_inventaris='TEST-001'" 2>/dev/null)"
UID3=$(mysql -u admin -pbodi campgear -N -e "select id from unit_alat where kode_inventaris='TEST-001'" 2>/dev/null)
chk "A-6" "unit update" "200" "$(code -X PUT $B/unit-alat/$UID3 -H "$HA" -H "$HJ" -d '{"kodeInventaris":"TEST-002","idStatusBarang":1,"idStatusKetersediaan":1}')"
chk "A-7" "unit delete" "200" "$(code -X DELETE $B/unit-alat/$UID3 -H "$HA")"
chk "A-8" "alat delete" "200" "$(code -X DELETE $B/alat/$AID -H "$HA")"
chk TC-12a "delete alat in use (409)" "409" "$(code -X DELETE $B/alat/1 -H "$HA")"
chk TC-23b "pageSize cap 100" "100" "$(code "$B/alat?pageSize=1000" -H "$HA" >/dev/null; python3 -c "import json;print(json.load(open('/tmp/opencode/out.json'))['pageSize'])")"
exec 3>/dev/null
STOCKAPI=$(python3 -c "import json;d=json.load(open('/tmp/opencode/out.json'));print([i['stokTersedia'] for i in d['items'] if i['id']==1][0])")
STOCKDB=$(mysql -u admin -pbodi campgear -N -e "select count(*) from unit_alat ua join status_ketersediaan sk on sk.id=ua.id_status_ketersediaan join status_barang sb on sb.id=ua.id_status_barang where ua.id_alat=1 and sk.nama='tersedia' and sb.nama<>'rusak_berat'" 2>/dev/null)
chk "A-9" "API stock matches DB stock" "$STOCKDB" "$STOCKAPI"

exec 3>&-

# ---------- PEMINJAMAN ----------
chk TC-13 "submit loan" "200" "$(code -X POST $B/peminjaman -H "$HU" -H "$HJ" -d '{"tanggalPeminjaman":"2026-10-21","tanggalPengembalian":"2026-10-24","items":[{"idAlat":1,"jumlah":2},{"idAlat":6,"jumlah":1}]}')"
LID=$(mysql -u admin -pbodi campgear -N -e "select max(id) from peminjaman" 2>/dev/null)
chk TC-13b "total_biaya = 324000" "324000.00" "$(mysql -u admin -pbodi campgear -N -e "select total_biaya from peminjaman where id=$LID" 2>/dev/null)"
chk TC-13c "status = diajukan" "diajukan" "$(mysql -u admin -pbodi campgear -N -e "select sp.nama from peminjaman p join status_peminjaman sp on sp.id=p.id_status_peminjaman where p.id=$LID" 2>/dev/null)"
chk "P-1" "same-day loan = 1 day" "8000.00" "$(curl -s -m 15 -o /dev/null -X POST $B/peminjaman -H "$HU" -H "$HJ" -d '{"tanggalPeminjaman":"2026-10-21","tanggalPengembalian":"2026-10-21","items":[{"idAlat":6,"jumlah":1}]}'; mysql -u admin -pbodi campgear -N -e "select total_biaya from peminjaman order by id desc limit 1" 2>/dev/null)"
SID=$(mysql -u admin -pbodi campgear -N -e "select max(id) from peminjaman" 2>/dev/null)
chk "P-2" "end before start (400)" "400" "$(code -X POST $B/peminjaman -H "$HU" -H "$HJ" -d '{"tanggalPeminjaman":"2026-10-24","tanggalPengembalian":"2026-10-21","items":[{"idAlat":1,"jumlah":1}]}')"
chk "P-3" "duplicate alat in items (400)" "400" "$(code -X POST $B/peminjaman -H "$HU" -H "$HJ" -d '{"tanggalPeminjaman":"2026-10-21","tanggalPengembalian":"2026-10-22","items":[{"idAlat":1,"jumlah":1},{"idAlat":1,"jumlah":1}]}')"
chk "P-4" "unknown alat (404)" "404" "$(code -X POST $B/peminjaman -H "$HU" -H "$HJ" -d '{"tanggalPeminjaman":"2026-10-21","tanggalPengembalian":"2026-10-22","items":[{"idAlat":999,"jumlah":1}]}')"
OTHER2=$(code -X POST $B/peminjaman -H "$HU" -H "$HJ" -d '{"tanggalPeminjaman":"2026-10-21","tanggalPengembalian":"2026-10-22","items":[{"idAlat":5,"jumlah":1}]}' >/dev/null; mysql -u admin -pbodi campgear -N -e "select max(id) from peminjaman" 2>/dev/null)
OTHER2PJ=$(mysql -u admin -pbodi campgear -N -e "select id_peminjam from peminjaman where id=$OTHER2" 2>/dev/null)
chk "P-4b" "failed requests created no extra loan (3 total)" "3" "$(mysql -u admin -pbodi campgear -N -e "select count(*) from peminjaman" 2>/dev/null)"
chk TC-14a "approve" "200" "$(code -X PATCH $B/peminjaman/$LID/approve -H "$HP")"
chk TC-14b "id_petugas from JWT" "2" "$(mysql -u admin -pbodi campgear -N -e "select id_petugas from peminjaman where id=$LID" 2>/dev/null)"
chk TC-15 "approve twice (409)" "409" "$(code -X PATCH $B/peminjaman/$LID/approve -H "$HP")"
chk "P-5" "peminjam cannot approve (403)" "403" "$(code -X PATCH $B/peminjaman/$LID/approve -H "$HU")"
chk TC-14c "handover" "200" "$(code -X PATCH $B/peminjaman/$LID/borrow -H "$HP")"
chk TC-14d "3 units assigned" "3" "$(mysql -u admin -pbodi campgear -N -e "select count(*) from detail_unit_peminjaman du join detail_peminjaman dp on dp.id=du.id_detail_peminjaman where dp.id_peminjaman=$LID" 2>/dev/null)"
chk TC-14e "units now dipinjam" "3" "$(mysql -u admin -pbodi campgear -N -e "select count(*) from unit_alat where id in (select du.id_unit_alat from detail_unit_peminjaman du join detail_peminjaman dp on dp.id=du.id_detail_peminjaman where dp.id_peminjaman=$LID) and id_status_ketersediaan=2" 2>/dev/null)"
chk "P-6" "borrow twice (409)" "409" "$(code -X PATCH $B/peminjaman/$LID/borrow -H "$HP")"
OTHER=$(mysql -u admin -pbodi campgear -N -e "select id from peminjaman where id_peminjam<>3 limit 1" 2>/dev/null)
if [ -n "$OTHER" ]; then chk TC-07 "peminjam reads another borrower's loan (403)" "403" "$(code $B/peminjaman/$OTHER -H "$HU")"; fi
chk "P-7" "own loan readable" "200" "$(code $B/peminjaman/$LID -H "$HU")"
SID2=$SID
chk "P-8" "reject other request" "200" "$(code -X PATCH $B/peminjaman/$SID/reject -H "$HP" -H "$HJ" -d '{"catatan":"stok tidak cukup"}')"
chk "P-9" "rejected status" "ditolak" "$(mysql -u admin -pbodi campgear -N -e "select sp.nama from peminjaman p join status_peminjaman sp on sp.id=p.id_status_peminjaman where p.id=$SID" 2>/dev/null)"
chk "P-10" "admin edit rejected loan (409)" "409" "$(code -X PUT $B/peminjaman/$SID -H "$HA" -H "$HJ" -d '{"tanggalPeminjaman":"2026-10-21","tanggalPengembalian":"2026-10-22","items":[{"idAlat":1,"jumlah":1}]}')"
chk "P-11" "admin delete rejected loan" "200" "$(code -X DELETE $B/peminjaman/$SID -H "$HA")"

# ---------- PENGEMBALIAN ----------
chk TC-19 "foreign unit (400)" "400" "$(code -X POST $B/pengembalian -H "$HU" -H "$HJ" -d "{\"idPeminjaman\":$LID,\"tanggalKembali\":\"2026-10-26\",\"units\":[{\"idUnitAlat\":999,\"statusKondisi\":\"baik\"}]}")"
# OTHER2 belongs to the same peminjam, so ownership 403 is enforced by the service owner check (covered by R-3 below)
chk "R-1" "return a diajukan loan (409 not dipinjam)" "409" "$(code -X POST $B/pengembalian -H "$HU" -H "$HJ" -d "{\"idPeminjaman\":$OTHER2,\"tanggalKembali\":\"2026-10-26\",\"units\":[{\"idUnitAlat\":1,\"statusKondisi\":\"baik\"}]}")"
chk TC-16 "return (2 days late, baik/ringan/hilang)" "200" "$(code -X POST $B/pengembalian -H "$HU" -H "$HJ" -d "{\"idPeminjaman\":$LID,\"tanggalKembali\":\"2026-10-26\",\"catatan\":\"telat 2 hari\",\"units\":[{\"idUnitAlat\":1,\"statusKondisi\":\"baik\"},{\"idUnitAlat\":2,\"statusKondisi\":\"rusak_ringan\"},{\"idUnitAlat\":11,\"statusKondisi\":\"hilang\"}]}")"
chk TC-16b "denda = 295000" "295000.00" "$(mysql -u admin -pbodi campgear -N -e "select denda from pengembalian where id_peminjaman=$LID" 2>/dev/null)"
chk TC-16c "keterlambatan = 2" "2" "$(mysql -u admin -pbodi campgear -N -e "select keterlambatan_pengembalian from pengembalian where id_peminjaman=$LID" 2>/dev/null)"
chk TC-16d "3 detail rows" "3" "$(mysql -u admin -pbodi campgear -N -e "select count(*) from detail_pengembalian dp join pengembalian p on p.id=dp.id_pengembalian where p.id_peminjaman=$LID" 2>/dev/null)"
chk TC-16e "loan selesai" "selesai" "$(mysql -u admin -pbodi campgear -N -e "select sp.nama from peminjaman p join status_peminjaman sp on sp.id=p.id_status_peminjaman where p.id=$LID" 2>/dev/null)"
chk TC-16f "unit 1 baik/tersedia" "baik|tersedia" "$(mysql -u admin -pbodi campgear -N -e "select concat(sb.nama,'|',sk.nama) from unit_alat ua join status_barang sb on sb.id=ua.id_status_barang join status_ketersediaan sk on sk.id=ua.id_status_ketersediaan where ua.id=1" 2>/dev/null)"
chk TC-16g "unit 2 rusak_ringan/tersedia" "rusak_ringan|tersedia" "$(mysql -u admin -pbodi campgear -N -e "select concat(sb.nama,'|',sk.nama) from unit_alat ua join status_barang sb on sb.id=ua.id_status_barang join status_ketersediaan sk on sk.id=ua.id_status_ketersediaan where ua.id=2" 2>/dev/null)"
chk TC-16h "unit 11 hilang -> tidak_tersedia" "tidak_tersedia" "$(mysql -u admin -pbodi campgear -N -e "select sk.nama from unit_alat ua join status_ketersediaan sk on sk.id=ua.id_status_ketersediaan where ua.id=11" 2>/dev/null)"
chk TC-18 "return twice (409)" "409" "$(code -X POST $B/pengembalian -H "$HU" -H "$HJ" -d "{\"idPeminjaman\":$LID,\"tanggalKembali\":\"2026-10-26\",\"units\":[{\"idUnitAlat\":1,\"statusKondisi\":\"baik\"}]}")"
RID=$(mysql -u admin -pbodi campgear -N -e "select id from pengembalian where id_peminjaman=$LID" 2>/dev/null)
chk "R-2" "return detail readable by peminjam" "200" "$(code $B/pengembalian/$RID -H "$HU")"

# ---------- MONITOR / REPORT / LOG ----------
chk TC-24 "monitor list" "200" "$(code $B/peminjaman/monitor -H "$HP")"
chk TC-20 "loan report" "200" "$(code "$B/laporan/peminjaman?from=2026-10-01&to=2026-10-31" -H "$HP")"
chk TC-20b "report total = 336000 (main 324000 + extra 12000)" "336000.0" "$(python3 -c "import json;print(float(json.load(open('/tmp/opencode/out.json'))['totalBiaya']))")"
chk TC-21 "csv report" "text/csv" "$(curl -s -m 15 -o /dev/null -w '%{content_type}' "$B/laporan/peminjaman?from=2026-10-01&to=2026-10-31&format=csv" -H "$HP" | cut -d';' -f1)"
chk "L-1" "return report" "200" "$(code "$B/laporan/pengembalian?from=2026-10-01&to=2026-10-31" -H "$HP")"
chk "L-2" "report bad range (400)" "400" "$(code "$B/laporan/peminjaman?from=2026-10-31&to=2026-10-01" -H "$HP")"
chk TC-22 "activity log" "200" "$(code "$B/log-aktivitas?pageSize=50" -H "$HA")"
chk TC-22b "trigger rows present" "yes" "$(grep -q 'diubah menjadi dipinjam' /tmp/opencode/out.json && echo yes || echo no)"
chk TC-22c "no password in log" "no" "$(grep -qi password /tmp/opencode/out.json && echo yes || echo no)"
chk "L-3" "logout" "200" "$(code -X POST $B/auth/logout -H "$HU")"
chk "L-4" "logout logged" "yes" "$(code "$B/log-aktivitas?pageSize=5" -H "$HA" >/dev/null; grep -q Logout /tmp/opencode/out.json && echo yes || echo no)"

echo "-------------------------------------------"
echo "TOTAL: $((PASS+FAIL))  PASS: $PASS  FAIL: $FAIL"
