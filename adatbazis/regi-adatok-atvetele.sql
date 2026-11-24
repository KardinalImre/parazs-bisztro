-- Az előző kiadás tábláiból, egyetlen tranzakción belül.
DELETE FROM etelek;
DELETE FROM kategoriak;

INSERT INTO kategoriak (azonosito, nev, sorrend)
SELECT id, name, sort_order FROM regi_categories;

INSERT INTO etelek (azonosito, kategoria_azonosito, nev, leiras, ar, elerheto)
SELECT id, category_id, name, description, price, available FROM regi_dishes;

INSERT INTO rendelesek (azonosito, keres_azonosito, keres_lenyomat, vendeg_neve,
    telefonszam, megjegyzes, allapot, vegosszeg, letrehozva)
SELECT id, request_id, request_hash, customer_name, phone, note,
    CASE status
        WHEN 'received' THEN 'beerkezett'
        WHEN 'preparing' THEN 'keszul'
        WHEN 'ready' THEN 'atveheto'
        WHEN 'completed' THEN 'atadva'
        WHEN 'cancelled' THEN 'torolve'
    END,
    total, created_at
FROM regi_orders;

INSERT INTO rendelesi_tetelek (rendeles_azonosito, etel_azonosito, etel_neve, egysegar, mennyiseg)
SELECT order_id, dish_id, dish_name, unit_price, quantity FROM regi_order_items;

DROP TABLE regi_order_items;
DROP TABLE regi_orders;
DROP TABLE regi_dishes;
DROP TABLE regi_categories;

