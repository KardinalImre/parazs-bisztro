-- SQLite SQL-export. Csak az étlap mintaadatait tartalmazza.
PRAGMA foreign_keys = ON;
SAVEPOINT parazs_export;

CREATE TABLE IF NOT EXISTS kategoriak (
    azonosito INTEGER PRIMARY KEY,
    nev TEXT NOT NULL UNIQUE,
    sorrend INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS etelek (
    azonosito INTEGER PRIMARY KEY,
    kategoria_azonosito INTEGER NOT NULL REFERENCES kategoriak(azonosito),
    nev TEXT NOT NULL UNIQUE,
    leiras TEXT NOT NULL,
    ar INTEGER NOT NULL CHECK (ar BETWEEN 1 AND 1000000),
    elerheto INTEGER NOT NULL DEFAULT 1 CHECK (elerheto IN (0, 1))
);

CREATE TABLE IF NOT EXISTS rendelesek (
    azonosito TEXT PRIMARY KEY NOT NULL,
    keres_azonosito TEXT NOT NULL UNIQUE,
    keres_lenyomat TEXT NOT NULL,
    vendeg_neve TEXT NOT NULL,
    telefonszam TEXT NOT NULL,
    megjegyzes TEXT NOT NULL DEFAULT '',
    allapot TEXT NOT NULL DEFAULT 'beerkezett'
        CHECK (allapot IN ('beerkezett', 'keszul', 'atveheto', 'atadva', 'torolve')),
    vegosszeg INTEGER NOT NULL CHECK (vegosszeg > 0),
    letrehozva TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS rendelesi_tetelek (
    rendeles_azonosito TEXT NOT NULL REFERENCES rendelesek(azonosito),
    etel_azonosito INTEGER NOT NULL REFERENCES etelek(azonosito),
    etel_neve TEXT NOT NULL,
    egysegar INTEGER NOT NULL CHECK (egysegar > 0),
    mennyiseg INTEGER NOT NULL CHECK (mennyiseg BETWEEN 1 AND 20),
    PRIMARY KEY (rendeles_azonosito, etel_azonosito)
);

CREATE INDEX IF NOT EXISTS etelek_kategoria_szerint ON etelek(kategoria_azonosito);
CREATE INDEX IF NOT EXISTS tetelek_etel_szerint ON rendelesi_tetelek(etel_azonosito);
CREATE INDEX IF NOT EXISTS rendelesek_idopont_szerint ON rendelesek(letrehozva DESC);

INSERT OR IGNORE INTO kategoriak (azonosito, nev, sorrend) VALUES
    (1, 'Pizzák', 10), (2, 'Főételek', 20), (3, 'Desszertek', 30), (4, 'Italok', 40);

INSERT OR IGNORE INTO etelek (azonosito, kategoria_azonosito, nev, leiras, ar) VALUES
    (1, 1, 'Parázs szalámis pizza', 'Paradicsomszósz, fior di latte, csípős szalámi és friss bazsalikom.', 4290),
    (2, 1, 'Margherita', 'Paradicsomszósz, fior di latte, bazsalikom és extra szűz olívaolaj.', 3490),
    (3, 1, 'Erdei gombás pizza', 'Fehér alap, pirított erdei gomba, mozzarella és petrezselyem.', 4490),
    (4, 2, 'Sült csirkecomb', 'Citromos sült csirkecomb, rozmaringos burgonya és pecsenyelé.', 4790),
    (5, 2, 'Sütőtökös rizottó', 'Krémes rizottó sült sütőtökkel, zsályával és parmezánnal.', 4090),
    (6, 2, 'Sült céklasaláta', 'Sült cékla, kecskesajt, pirított dió és balzsamecetes öntet.', 3390),
    (7, 3, 'Házi tiramisu', 'Kávéval átitatott piskóta, mascarpone és holland kakaó.', 1790),
    (8, 3, 'Csokoládétorta', 'Étcsokoládés tortaszelet málnaszósszal.', 1890),
    (9, 4, 'Házi limonádé', 'Frissen facsart citrom, menta és szódavíz, 0,4 l.', 1290),
    (10, 4, 'Szénsavas ásványvíz', 'Palackozott ásványvíz, 0,5 l.', 690);

CREATE TABLE IF NOT EXISTS profilok (
    azonosito TEXT PRIMARY KEY NOT NULL,
    nev TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE,
    telefonszam TEXT NOT NULL DEFAULT '',
    jelszo_lenyomat TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS munkamenetek (
    lenyomat TEXT PRIMARY KEY NOT NULL,
    profil_azonosito TEXT NOT NULL REFERENCES profilok(azonosito),
    lejarat INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS munkamenetek_lejarat_szerint ON munkamenetek(lejarat);

CREATE TABLE IF NOT EXISTS rendeles_profilok (
    rendeles_azonosito TEXT PRIMARY KEY NOT NULL REFERENCES rendelesek(azonosito),
    profil_azonosito TEXT NOT NULL REFERENCES profilok(azonosito)
);
CREATE INDEX IF NOT EXISTS rendelesek_profil_szerint ON rendeles_profilok(profil_azonosito);

CREATE TABLE IF NOT EXISTS kuponok (
    kod TEXT PRIMARY KEY NOT NULL,
    tipus TEXT NOT NULL CHECK (tipus IN ('szazalek', 'fix')),
    ertek INTEGER NOT NULL CHECK (ertek > 0 AND (tipus <> 'szazalek' OR ertek < 100)),
    minimum_osszeg INTEGER NOT NULL CHECK (minimum_osszeg >= 0),
    aktiv INTEGER NOT NULL DEFAULT 1 CHECK (aktiv IN (0, 1))
);

CREATE TABLE IF NOT EXISTS rendeles_kedvezmenyek (
    rendeles_azonosito TEXT PRIMARY KEY NOT NULL REFERENCES rendelesek(azonosito),
    kuponkod TEXT NOT NULL REFERENCES kuponok(kod),
    kedvezmeny INTEGER NOT NULL CHECK (kedvezmeny >= 0)
);

INSERT OR IGNORE INTO kuponok (kod, tipus, ertek, minimum_osszeg) VALUES
    ('PARAZS10', 'szazalek', 10, 3000), ('ELVITEL500', 'fix', 500, 4000);

CREATE TABLE IF NOT EXISTS tarca_muveletek (
    azonosito TEXT PRIMARY KEY NOT NULL,
    profil_azonosito TEXT NOT NULL REFERENCES profilok(azonosito),
    tipus TEXT NOT NULL CHECK (tipus IN ('feltoltes', 'fizetes', 'jovairas')),
    osszeg INTEGER NOT NULL CHECK (osszeg <> 0),
    letrehozva TEXT NOT NULL,
    rendeles_azonosito TEXT REFERENCES rendelesek(azonosito),
    keres_azonosito TEXT,
    CHECK ((tipus = 'feltoltes' AND osszeg > 0 AND keres_azonosito IS NOT NULL AND rendeles_azonosito IS NULL)
        OR (tipus = 'fizetes' AND osszeg < 0 AND keres_azonosito IS NULL AND rendeles_azonosito IS NOT NULL)
        OR (tipus = 'jovairas' AND osszeg > 0 AND keres_azonosito IS NULL AND rendeles_azonosito IS NOT NULL)),
    UNIQUE (profil_azonosito, keres_azonosito),
    UNIQUE (rendeles_azonosito, tipus)
);
CREATE INDEX IF NOT EXISTS tarca_muveletek_profil_szerint ON tarca_muveletek(profil_azonosito, letrehozva DESC);

CREATE TABLE IF NOT EXISTS ugyfelszolgalati_jegyek (
    azonosito TEXT PRIMARY KEY NOT NULL,
    keres_azonosito TEXT NOT NULL UNIQUE,
    email TEXT NOT NULL,
    targy TEXT NOT NULL,
    uzenet TEXT NOT NULL,
    rendeles_azonosito TEXT REFERENCES rendelesek(azonosito),
    allapot TEXT NOT NULL DEFAULT 'nyitott' CHECK (allapot IN ('nyitott', 'megvalaszolt', 'lezart')),
    valasz TEXT NOT NULL DEFAULT '',
    letrehozva TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS jegyek_rendeles_szerint ON ugyfelszolgalati_jegyek(rendeles_azonosito);

RELEASE parazs_export;

