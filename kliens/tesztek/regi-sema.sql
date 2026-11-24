-- SQLite SQL-export. Csak az étlap mintaadatait tartalmazza.
PRAGMA foreign_keys = ON;
BEGIN IMMEDIATE;

CREATE TABLE IF NOT EXISTS categories (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    sort_order INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS dishes (
    id INTEGER PRIMARY KEY,
    category_id INTEGER NOT NULL REFERENCES categories(id),
    name TEXT NOT NULL UNIQUE,
    description TEXT NOT NULL,
    price INTEGER NOT NULL CHECK (price BETWEEN 1 AND 1000000),
    available INTEGER NOT NULL DEFAULT 1 CHECK (available IN (0, 1))
);

CREATE TABLE IF NOT EXISTS orders (
    id TEXT PRIMARY KEY NOT NULL,
    request_id TEXT NOT NULL UNIQUE,
    request_hash TEXT NOT NULL,
    customer_name TEXT NOT NULL,
    phone TEXT NOT NULL,
    note TEXT NOT NULL DEFAULT '',
    status TEXT NOT NULL DEFAULT 'received'
        CHECK (status IN ('received', 'preparing', 'ready', 'completed', 'cancelled')),
    total INTEGER NOT NULL CHECK (total > 0),
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS order_items (
    order_id TEXT NOT NULL REFERENCES orders(id),
    dish_id INTEGER NOT NULL REFERENCES dishes(id),
    dish_name TEXT NOT NULL,
    unit_price INTEGER NOT NULL CHECK (unit_price > 0),
    quantity INTEGER NOT NULL CHECK (quantity BETWEEN 1 AND 20),
    PRIMARY KEY (order_id, dish_id)
);

CREATE INDEX IF NOT EXISTS dishes_by_category ON dishes(category_id);
CREATE INDEX IF NOT EXISTS order_items_by_dish ON order_items(dish_id);
CREATE INDEX IF NOT EXISTS orders_by_creation ON orders(created_at DESC);

INSERT OR IGNORE INTO categories (id, name, sort_order) VALUES
    (1, 'Pizzák', 10), (2, 'Főételek', 20), (3, 'Desszertek', 30), (4, 'Italok', 40);

INSERT OR IGNORE INTO dishes (id, category_id, name, description, price) VALUES
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

COMMIT;

