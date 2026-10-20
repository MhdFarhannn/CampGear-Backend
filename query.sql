CREATE DATABASE IF NOT EXISTS campgear;

USE campgear;

CREATE TABLE roles (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nama VARCHAR(30) NOT NULL UNIQUE
) ENGINE=InnoDB;

CREATE TABLE status_barang (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nama VARCHAR(30) NOT NULL UNIQUE
) ENGINE=InnoDB;

CREATE TABLE status_peminjaman (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nama VARCHAR(30) NOT NULL UNIQUE
) ENGINE=InnoDB;

CREATE TABLE status_kondisi_kembali (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nama VARCHAR(30) NOT NULL UNIQUE
) ENGINE=InnoDB;

CREATE TABLE status_ketersediaan (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nama VARCHAR(30) NOT NULL UNIQUE
) ENGINE=InnoDB;

CREATE TABLE jenis_kelamin (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nama VARCHAR(50) NOT NULL UNIQUE
) ENGINE=InnoDB;

CREATE TABLE users (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    id_role INT UNSIGNED NOT NULL,
    id_jenis_kelamin INT UNSIGNED NULL,
    nama VARCHAR(100) NOT NULL,
    password VARCHAR(255) NOT NULL,
    no_telepon VARCHAR(20) NOT NULL,
    alamat TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT uq_users_no_telepon
        UNIQUE (no_telepon),

    CONSTRAINT fk_users_roles
        FOREIGN KEY (id_role)
        REFERENCES roles(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_users_jenis_kelamin
        FOREIGN KEY (id_jenis_kelamin)
        REFERENCES jenis_kelamin(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE kategori (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    nama VARCHAR(100) NOT NULL UNIQUE
) ENGINE=InnoDB;

CREATE TABLE alat (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    id_kategori INT UNSIGNED NOT NULL,
    nama VARCHAR(150) NOT NULL,
    deskripsi TEXT,
    harga_sewa DECIMAL(15,2) UNSIGNED NOT NULL DEFAULT 0,
    image_path VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_alat_kategori
        FOREIGN KEY (id_kategori)
        REFERENCES kategori(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE unit_alat (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    id_alat INT UNSIGNED NOT NULL,
    id_status_ketersediaan INT UNSIGNED NOT NULL,
    kode_inventaris VARCHAR(50) NOT NULL UNIQUE,
    id_status_barang INT UNSIGNED NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_unit_alat_alat
        FOREIGN KEY (id_alat)
        REFERENCES alat(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_unit_alat_status_barang
        FOREIGN KEY (id_status_barang)
        REFERENCES status_barang(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_unit_alat_status_ketersediaan
        FOREIGN KEY (id_status_ketersediaan)
        REFERENCES status_ketersediaan(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE peminjaman (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    id_peminjam INT UNSIGNED NOT NULL,
    id_petugas INT UNSIGNED NULL,
    id_status_peminjaman INT UNSIGNED NOT NULL,
    tanggal_peminjaman DATE NOT NULL,
    tanggal_pengembalian DATE NOT NULL,
    total_biaya DECIMAL(15,2) UNSIGNED NOT NULL DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_peminjaman_peminjam
        FOREIGN KEY (id_peminjam)
        REFERENCES users(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_peminjaman_petugas
        FOREIGN KEY (id_petugas)
        REFERENCES users(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_peminjaman_status
        FOREIGN KEY (id_status_peminjaman)
        REFERENCES status_peminjaman(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT chk_tanggal_peminjaman
        CHECK (tanggal_pengembalian >= tanggal_peminjaman)
) ENGINE=InnoDB;

CREATE TABLE detail_peminjaman (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    id_peminjaman INT UNSIGNED NOT NULL,
    id_alat INT UNSIGNED NOT NULL,
    jumlah INT UNSIGNED NOT NULL,
    harga_sewa_saat_transaksi DECIMAL(15,2) UNSIGNED NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_detail_peminjaman_peminjaman
        FOREIGN KEY (id_peminjaman)
        REFERENCES peminjaman(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_detail_peminjaman_alat
        FOREIGN KEY (id_alat)
        REFERENCES alat(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT chk_jumlah_peminjaman
        CHECK (jumlah > 0)
) ENGINE=InnoDB;

CREATE TABLE detail_unit_peminjaman (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    id_detail_peminjaman INT UNSIGNED NOT NULL,
    id_unit_alat INT UNSIGNED NOT NULL,

    CONSTRAINT fk_detail_unit_peminjaman_detail
        FOREIGN KEY (id_detail_peminjaman)
        REFERENCES detail_peminjaman(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_detail_unit_peminjaman_unit
        FOREIGN KEY (id_unit_alat)
        REFERENCES unit_alat(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT uq_detail_unit_peminjaman
        UNIQUE (id_detail_peminjaman, id_unit_alat)
) ENGINE=InnoDB;

CREATE TABLE pengembalian (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    id_peminjaman INT UNSIGNED NOT NULL UNIQUE,
    tanggal_kembali DATE NOT NULL,
    keterlambatan_pengembalian INT UNSIGNED NOT NULL DEFAULT 0,
    denda DECIMAL(15,2) UNSIGNED NOT NULL DEFAULT 0,
    catatan TEXT,

    CONSTRAINT fk_pengembalian_peminjaman
        FOREIGN KEY (id_peminjaman)
        REFERENCES peminjaman(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE detail_pengembalian (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    id_pengembalian INT UNSIGNED NOT NULL,
    id_unit_alat INT UNSIGNED NOT NULL,
    id_status_kondisi_kembali INT UNSIGNED NOT NULL,
    denda_kondisi DECIMAL(15,2) UNSIGNED NOT NULL DEFAULT 0,
    catatan TEXT,

    CONSTRAINT fk_detail_pengembalian_pengembalian
        FOREIGN KEY (id_pengembalian)
        REFERENCES pengembalian(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_detail_pengembalian_unit
        FOREIGN KEY (id_unit_alat)
        REFERENCES unit_alat(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_detail_pengembalian_kondisi
        FOREIGN KEY (id_status_kondisi_kembali)
        REFERENCES status_kondisi_kembali(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT uq_detail_pengembalian_unit
        UNIQUE (id_pengembalian, id_unit_alat)
) ENGINE=InnoDB;

CREATE TABLE log_aktivitas (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    id_user INT UNSIGNED NOT NULL,
    aktivitas TEXT NOT NULL,
    waktu TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_log_aktivitas_user
        FOREIGN KEY (id_user)
        REFERENCES users(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE INDEX idx_peminjaman_peminjam
    ON peminjaman (id_peminjam);

CREATE INDEX idx_peminjaman_petugas
    ON peminjaman (id_petugas);

CREATE INDEX idx_peminjaman_status
    ON peminjaman (id_status_peminjaman);

-- supports loan report and monitoring by date range
CREATE INDEX idx_peminjaman_tanggal
    ON peminjaman (tanggal_peminjaman);

CREATE INDEX idx_detail_peminjaman_alat
    ON detail_peminjaman (id_alat);

CREATE INDEX idx_unit_alat_alat
    ON unit_alat (id_alat);

CREATE INDEX idx_log_aktivitas_user
    ON log_aktivitas (id_user);

-- supports activity log list filtered by date range
CREATE INDEX idx_log_aktivitas_waktu
    ON log_aktivitas (waktu);

INSERT INTO roles (nama) VALUES
('Admin'),
('Petugas'),
('Peminjam');

INSERT INTO status_barang (nama) VALUES
('baik'),
('rusak_ringan'),
('rusak_berat');

INSERT INTO status_peminjaman (nama) VALUES
('diajukan'),
('disetujui'),
('ditolak'),
('dipinjam'),
('selesai');

INSERT INTO status_kondisi_kembali (nama) VALUES
('baik'),
('rusak_ringan'),
('rusak_berat'),
('hilang');

INSERT INTO status_ketersediaan (nama) VALUES
('tersedia'),
('dipinjam'),
('tidak_tersedia');

INSERT INTO jenis_kelamin (nama) VALUES
('Laki-laki'),
('Perempuan');

INSERT INTO users (id_role, id_jenis_kelamin, nama, password, no_telepon, alamat) VALUES
(1, 1, 'Admin CampGear', '$2a$11$x13vjVTeU3DxXriGDWOgV.3BJRhTWS3/GzpxUIxNH5nw04U0oqt1y', '081200000001', 'Jl. Merdeka No. 1, Bandung'),
(2, 1, 'Petugas CampGear', '$2a$11$2DMFDDTeX5Jp9sEUEixNuOjuHEXoBBtrSWjj0YLRvcLwxCMB.DsWa', '081200000002', 'Jl. Merdeka No. 2, Bandung'),
(3, 2, 'Peminjam CampGear', '$2a$11$Ln8bWV.lnFGC0ca885aYeOFAImxuZ4nhnQF4nNixXDC17Giij09wi', '081200000003', 'Jl. Merdeka No. 3, Bandung');

INSERT INTO kategori (nama) VALUES
('Tenda'),
('Tas & Carrier'),
('Alat Masak'),
('Penerangan');

INSERT INTO alat (id_kategori, nama, deskripsi, harga_sewa, image_path) VALUES
(1, 'Tenda Dome 4P', 'Tenda dome kapasitas 4 orang, tahan hujan.', 50000.00, NULL),
(1, 'Tenda Ultralight 2P', 'Tenda ringan kapasitas 2 orang.', 35000.00, NULL),
(2, 'Carrier 60L', 'Tas carrier 60 liter dengan rain cover.', 30000.00, NULL),
(3, 'Kompor Portable', 'Kompor kamping gas portable.', 15000.00, NULL),
(3, 'Nesting Cookware Set', 'Set alat masak kamping 2 orang.', 12000.00, NULL),
(4, 'Headlamp LED', 'Lampu kepala LED dengan baterai.', 8000.00, NULL);

INSERT INTO unit_alat (id_alat, id_status_ketersediaan, kode_inventaris, id_status_barang) VALUES
(1, 1, 'TND-DM4-001', 1),
(1, 1, 'TND-DM4-002', 1),
(1, 1, 'TND-DM4-003', 2),
(2, 1, 'TND-UL2-001', 1),
(2, 1, 'TND-UL2-002', 1),
(3, 1, 'CRR-60L-001', 1),
(3, 1, 'CRR-60L-002', 1),
(4, 1, 'KMP-PRT-001', 1),
(4, 1, 'KMP-PRT-002', 1),
(5, 1, 'CKW-SET-001', 1),
(6, 1, 'HDL-LED-001', 1),
(6, 1, 'HDL-LED-002', 1),
(6, 1, 'HDL-LED-003', 1);

DELIMITER $$

-- rented days, same-day loan counts as 1 day
CREATE FUNCTION fn_hitung_lama_sewa(
    p_tanggal_peminjaman DATE,
    p_tanggal_pengembalian DATE
) RETURNS INT UNSIGNED
DETERMINISTIC
BEGIN
    DECLARE v_hari INT;

    SET v_hari = DATEDIFF(p_tanggal_pengembalian, p_tanggal_peminjaman);

    IF v_hari < 1 THEN
        SET v_hari = 1;
    END IF;

    RETURN v_hari;
END$$

-- late fine = days late x daily rate (rate comes from appsettings.json), minimum 0
CREATE FUNCTION fn_hitung_denda_terlambat(
    p_tanggal_kembali DATE,
    p_tanggal_seharusnya DATE,
    p_denda_per_hari DECIMAL(15,2)
) RETURNS DECIMAL(15,2)
DETERMINISTIC
BEGIN
    DECLARE v_hari INT;

    SET v_hari = DATEDIFF(p_tanggal_kembali, p_tanggal_seharusnya);

    IF v_hari < 0 THEN
        SET v_hari = 0;
    END IF;

    RETURN v_hari * p_denda_per_hari;
END$$

-- automatic activity log when a loan status changes
CREATE TRIGGER trg_peminjaman_status_log
AFTER UPDATE ON peminjaman
FOR EACH ROW
BEGIN
    IF OLD.id_status_peminjaman <> NEW.id_status_peminjaman THEN
        INSERT INTO log_aktivitas (id_user, aktivitas)
        VALUES (COALESCE(NEW.id_petugas, NEW.id_peminjam),
                CONCAT('Status peminjaman #', NEW.id, ' diubah menjadi ',
                       (SELECT nama FROM status_peminjaman WHERE id = NEW.id_status_peminjaman)));
    END IF;
END$$

-- one item per call, the caller wraps every item of a request in one transaction
CREATE PROCEDURE sp_ajukan_peminjaman(
    IN p_id_peminjam INT UNSIGNED,
    IN p_tanggal_peminjaman DATE,
    IN p_tanggal_pengembalian DATE,
    IN p_id_alat INT UNSIGNED,
    IN p_jumlah INT UNSIGNED,
    OUT p_id_peminjaman INT UNSIGNED,
    OUT p_total_biaya DECIMAL(15,2)
)
BEGIN
    DECLARE v_harga_sewa DECIMAL(15,2);
    DECLARE v_stok INT UNSIGNED;
    DECLARE v_status_diajukan INT UNSIGNED;
    DECLARE v_lama_sewa INT UNSIGNED;
    DECLARE v_total_biaya DECIMAL(15,2);

    IF p_jumlah = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'jumlah must be greater than 0';
    END IF;

    IF p_tanggal_pengembalian < p_tanggal_peminjaman THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'tanggal_pengembalian must not be before tanggal_peminjaman';
    END IF;

    SELECT id INTO v_status_diajukan
    FROM status_peminjaman
    WHERE nama = 'diajukan';

    SELECT harga_sewa INTO v_harga_sewa
    FROM alat
    WHERE id = p_id_alat;

    IF v_harga_sewa IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'alat not found';
    END IF;

    SELECT COUNT(*) INTO v_stok
    FROM unit_alat ua
    JOIN status_ketersediaan sk ON sk.id = ua.id_status_ketersediaan
    JOIN status_barang sb ON sb.id = ua.id_status_barang
    WHERE ua.id_alat = p_id_alat
      AND sk.nama = 'tersedia'
      AND sb.nama <> 'rusak_berat';

    IF v_stok < p_jumlah THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'insufficient stock for alat';
    END IF;

    SET v_lama_sewa = fn_hitung_lama_sewa(p_tanggal_peminjaman, p_tanggal_pengembalian);
    SET v_total_biaya = v_harga_sewa * p_jumlah * v_lama_sewa;

    INSERT INTO peminjaman (id_peminjam, id_status_peminjaman, tanggal_peminjaman, tanggal_pengembalian, total_biaya)
    VALUES (p_id_peminjam, v_status_diajukan, p_tanggal_peminjaman, p_tanggal_pengembalian, v_total_biaya);

    SET p_id_peminjaman = LAST_INSERT_ID();
    SET p_total_biaya = v_total_biaya;

    INSERT INTO detail_peminjaman (id_peminjaman, id_alat, jumlah, harga_sewa_saat_transaksi)
    VALUES (p_id_peminjaman, p_id_alat, p_jumlah, v_harga_sewa);
END$$

-- one unit per call; the caller wraps all returned units of one loan in one
-- transaction and sets the loan status to 'selesai' itself
CREATE PROCEDURE sp_proses_pengembalian(
    IN p_id_peminjaman INT UNSIGNED,
    IN p_tanggal_kembali DATE,
    IN p_catatan TEXT,
    IN p_id_unit_alat INT UNSIGNED,
    IN p_status_kondisi VARCHAR(30),
    IN p_catatan_unit TEXT,
    IN p_denda_per_hari DECIMAL(15,2),
    IN p_denda_baik DECIMAL(15,2),
    IN p_denda_rusak_ringan DECIMAL(15,2),
    IN p_denda_rusak_berat DECIMAL(15,2),
    IN p_denda_hilang DECIMAL(15,2),
    OUT p_id_pengembalian INT UNSIGNED,
    OUT p_total_denda DECIMAL(15,2)
)
BEGIN
    DECLARE v_tanggal_seharusnya DATE;
    DECLARE v_status_peminjaman INT UNSIGNED;
    DECLARE v_unit_dipinjam INT UNSIGNED;
    DECLARE v_status_kondisi INT UNSIGNED;
    DECLARE v_status_barang INT UNSIGNED;
    DECLARE v_status_tersedia INT UNSIGNED;
    DECLARE v_status_tidak_tersedia INT UNSIGNED;
    DECLARE v_unit_target INT UNSIGNED;
    DECLARE v_kondisi VARCHAR(30);
    DECLARE v_denda_kondisi DECIMAL(15,2);
    DECLARE v_denda_kondisi_total DECIMAL(15,2);
    DECLARE v_denda_terlambat DECIMAL(15,2);

    SELECT tanggal_pengembalian, id_status_peminjaman INTO v_tanggal_seharusnya, v_status_peminjaman
    FROM peminjaman
    WHERE id = p_id_peminjaman;

    IF v_tanggal_seharusnya IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'peminjaman not found';
    END IF;

    IF (SELECT nama FROM status_peminjaman WHERE id = v_status_peminjaman) <> 'dipinjam' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'peminjaman is not in dipinjam status';
    END IF;

    IF p_id_unit_alat IS NULL THEN
        SELECT du.id_unit_alat INTO v_unit_target
        FROM detail_unit_peminjaman du
        JOIN detail_peminjaman dp ON dp.id = du.id_detail_peminjaman
        WHERE dp.id_peminjaman = p_id_peminjaman
        LIMIT 1;

        SET v_kondisi = 'hilang';
    ELSE
        SET v_unit_target = p_id_unit_alat;
        SET v_kondisi = LOWER(TRIM(p_status_kondisi));
    END IF;

    IF v_unit_target IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'unit_alat does not belong to this peminjaman';
    END IF;

    SELECT COUNT(*) INTO v_unit_dipinjam
    FROM detail_unit_peminjaman du
    JOIN detail_peminjaman dp ON dp.id = du.id_detail_peminjaman
    WHERE dp.id_peminjaman = p_id_peminjaman
      AND du.id_unit_alat = v_unit_target;

    IF v_unit_dipinjam = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'unit_alat does not belong to this peminjaman';
    END IF;

    SELECT id INTO v_status_kondisi
    FROM status_kondisi_kembali
    WHERE nama = v_kondisi;

    IF v_status_kondisi IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'invalid status_kondisi_kembali';
    END IF;

    SET v_denda_kondisi = CASE v_kondisi
        WHEN 'baik' THEN p_denda_baik
        WHEN 'rusak_ringan' THEN p_denda_rusak_ringan
        WHEN 'rusak_berat' THEN p_denda_rusak_berat
        WHEN 'hilang' THEN p_denda_hilang
        ELSE 0
    END;

    SELECT COALESCE(SUM(dp.denda_kondisi), 0) INTO v_denda_kondisi_total
    FROM detail_pengembalian dp
    JOIN pengembalian p ON p.id = dp.id_pengembalian
    WHERE p.id_peminjaman = p_id_peminjaman;

    SET v_denda_kondisi_total = v_denda_kondisi_total + v_denda_kondisi;
    SET v_denda_terlambat = fn_hitung_denda_terlambat(p_tanggal_kembali, v_tanggal_seharusnya, p_denda_per_hari);

    INSERT INTO pengembalian (id_peminjaman, tanggal_kembali, keterlambatan_pengembalian, denda, catatan)
    VALUES (p_id_peminjaman, p_tanggal_kembali,
            GREATEST(0, DATEDIFF(p_tanggal_kembali, v_tanggal_seharusnya)),
            v_denda_terlambat + v_denda_kondisi_total,
            p_catatan)
    ON DUPLICATE KEY UPDATE
        tanggal_kembali = p_tanggal_kembali,
        keterlambatan_pengembalian = GREATEST(0, DATEDIFF(p_tanggal_kembali, v_tanggal_seharusnya)),
        denda = v_denda_terlambat + v_denda_kondisi_total,
        catatan = catatan;

    SELECT id INTO p_id_pengembalian
    FROM pengembalian
    WHERE id_peminjaman = p_id_peminjaman;

    INSERT INTO detail_pengembalian (id_pengembalian, id_unit_alat, id_status_kondisi_kembali, denda_kondisi, catatan)
    VALUES (p_id_pengembalian, v_unit_target, v_status_kondisi, v_denda_kondisi, p_catatan_unit)
    ON DUPLICATE KEY UPDATE
        id_status_kondisi_kembali = v_status_kondisi,
        denda_kondisi = v_denda_kondisi,
        catatan = p_catatan_unit;

    IF v_kondisi = 'hilang' THEN
        SELECT id INTO v_status_barang FROM status_barang WHERE nama = 'rusak_berat';
    ELSE
        SELECT id INTO v_status_barang FROM status_barang WHERE nama = v_kondisi;
    END IF;

    SELECT id INTO v_status_tersedia
    FROM status_ketersediaan
    WHERE nama = 'tersedia';

    SELECT id INTO v_status_tidak_tersedia
    FROM status_ketersediaan
    WHERE nama = 'tidak_tersedia';

    IF v_kondisi IN ('rusak_berat', 'hilang') THEN
        UPDATE unit_alat
        SET id_status_barang = v_status_barang,
            id_status_ketersediaan = v_status_tidak_tersedia
        WHERE id = v_unit_target;
    ELSE
        UPDATE unit_alat
        SET id_status_barang = v_status_barang,
            id_status_ketersediaan = v_status_tersedia
        WHERE id = v_unit_target;
    END IF;

    SET p_total_denda = v_denda_terlambat + v_denda_kondisi_total;
END$$

DELIMITER ;

-- ============================================================
-- EXAMPLES: explicit COMMIT and ROLLBACK
-- ============================================================

-- ROLLBACK example: every unit of alat 1 is borrowed twice, the second
-- call reports insufficient stock and ROLLBACK discards both requests
START TRANSACTION;

CALL sp_ajukan_peminjaman(3, '2026-11-01', '2026-11-03', 1, 3, @id1, @total1);

CALL sp_ajukan_peminjaman(3, '2026-11-01', '2026-11-03', 1, 3, @id2, @total2);

ROLLBACK;

-- COMMIT example: a valid request is stored permanently
START TRANSACTION;

CALL sp_ajukan_peminjaman(3, '2026-11-01', '2026-11-03', 1, 2, @id3, @total3);

COMMIT;
