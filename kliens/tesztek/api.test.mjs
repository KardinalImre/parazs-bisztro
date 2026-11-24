import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { DatabaseSync } from 'node:sqlite';
import { after, before, test } from 'node:test';
import { tesztkornyezetLetrehozasa } from './tesztszerver.mjs';

let szerver;
before(async () => { szerver = await tesztkornyezetLetrehozasa(); });
after(async () => { await szerver?.bezaras(); });

const minta = (felulirasok = {}) => ({
  keresAzonosito: randomUUID(), vendegNeve: 'Teszt Vendég', telefonszam: '+36 30 555 0100',
  megjegyzes: 'Próbarendelés', tetelek: [{ etelAzonosito: 1, mennyiseg: 2 }, { etelAzonosito: 9, mennyiseg: 1 }], ...felulirasok,
});
const leadas = adatok => szerver.keres('/rendelesek', { modszer: 'POST', adatok });
const modositasKuldese = (utvonal, adatok) => szerver.keres('/szemelyzet' + utvonal, { modszer: 'PATCH', adatok, szemelyzet: true });

test('API-01 · A feltöltött étlap négy kategóriát és tíz ételt ad vissza', async () => {
  const { kod, torzs } = await szerver.keres('/etlap');
  assert.equal(kod, 200);
  assert.deepEqual(torzs.kategoriak.map(tetel => tetel.nev), ['Pizzák', 'Főételek', 'Desszertek', 'Italok']);
  assert.equal(torzs.etelek.length, 10);
  assert.equal(torzs.etelek[0].ar, 4290);
});

test('API-02 · Rendelés mentése, szerveroldali ár és követés személyes adatok nélkül', async () => {
  const eredmeny = await leadas(minta({ vegosszeg: 1 }));
  assert.equal(eredmeny.kod, 201);
  assert.equal(eredmeny.torzs.vegosszeg, 9870);
  assert.equal(eredmeny.torzs.allapot, 'beerkezett');
  assert.equal(eredmeny.hely, '/api/rendelesek/' + eredmeny.torzs.azonosito);
  const kovetes = await szerver.keres('/rendelesek/' + eredmeny.torzs.azonosito);
  assert.deepEqual(kovetes.torzs, eredmeny.torzs);
  assert.equal(kovetes.torzs.tetelek[0].egysegar, 4290);
  for (const mezo of ['vendegNeve', 'telefonszam', 'megjegyzes', 'keresAzonosito']) assert.ok(!(mezo in kovetes.torzs));
  assert.equal((await szerver.keres('/rendelesek/' + randomUUID())).kod, 404);
});

test('API-03 · Hibás bevitel és hibás JSON elutasítása', async () => {
  const esetek = [
    minta({ tetelek: [] }), minta({ telefonszam: 'abcdefgh' }), minta({ vendegNeve: ' ' }),
    minta({ tetelek: [{ etelAzonosito: 1, mennyiseg: 21 }] }),
    minta({ tetelek: [{ etelAzonosito: 1, mennyiseg: 1 }, { etelAzonosito: 1, mennyiseg: 1 }] }),
    minta({ tetelek: [null] }), minta({ keresAzonosito: '00000000-0000-0000-0000-000000000000' }),
  ];
  for (const adatok of esetek) assert.equal((await leadas(adatok)).kod, 400);
  const hibas = await fetch(szerver.cim + '/api/rendelesek', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{',
  });
  assert.equal(hibas.status, 400);
  assert.match((await hibas.json()).hiba, /A kérés érvénytelen/);
});

test('API-04 · Többszöri egyidejű küldés egyetlen rendelést hoz létre', async () => {
  const keres = minta();
  const eredmenyek = await Promise.all(Array.from({ length: 5 }, () => leadas(keres)));
  assert.deepEqual(eredmenyek.map(eredmeny => eredmeny.kod).sort(), [200, 200, 200, 200, 201]);
  assert.equal(new Set(eredmenyek.map(eredmeny => eredmeny.torzs.azonosito)).size, 1);
  assert.equal((await leadas({ ...keres, megjegyzes: 'Módosított adatok' })).kod, 409);
});

test('API-05 · Személyzeti védelem és rendelési állapotátmenetek', async () => {
  assert.equal((await szerver.keres('/szemelyzet/rendelesek')).kod, 401);
  assert.equal((await szerver.keres('/szemelyzet/etlap')).kod, 401);
  const rendeles = (await leadas(minta())).torzs;
  const utvonal = '/rendelesek/' + rendeles.azonosito;
  assert.equal((await szerver.keres('/szemelyzet' + utvonal, { modszer: 'PATCH', adatok: { allapot: 'keszul' } })).kod, 401);
  assert.equal((await modositasKuldese(utvonal, { allapot: 'atveheto' })).kod, 409);
  assert.equal((await modositasKuldese(utvonal, { allapot: 'unknown' })).kod, 400);
  for (const allapot of ['keszul', 'atveheto', 'atadva', 'atadva']) {
    const modositas = await modositasKuldese(utvonal, { allapot });
    assert.equal(modositas.kod, 200);
    assert.equal(modositas.torzs.allapot, allapot);
    assert.equal(modositas.torzs.telefonszam, '+36 30 555 0100');
  }
  assert.equal((await modositasKuldese(utvonal, { allapot: 'torolve' })).kod, 409);
  assert.equal((await szerver.keres(utvonal)).torzs.allapot, 'atadva');
});

test('API-06 · Elrejtett étel nem rendelhető, félbehagyott rendelés nem marad az adatbázisban', async () => {
  const lista = () => szerver.keres('/szemelyzet/rendelesek', { szemelyzet: true });
  const darab = (await lista()).torzs.length;
  assert.equal((await modositasKuldese('/etelek/9', { elerheto: false })).kod, 204);
  const { torzs: etlap } = await szerver.keres('/etlap');
  assert.ok(!etlap.etelek.some(etel => etel.azonosito === 9));
  const { torzs: szemelyzetiEtlap } = await szerver.keres('/szemelyzet/etlap', { szemelyzet: true });
  assert.equal(szemelyzetiEtlap.etelek.find(etel => etel.azonosito === 9).elerheto, false);
  assert.equal((await leadas(minta())).kod, 409);
  assert.equal((await lista()).torzs.length, darab);
  assert.equal((await modositasKuldese('/etelek/9', { elerheto: true })).kod, 204);
  assert.equal((await leadas(minta())).kod, 201);
});

test('API-07 · A rendelés és az étel elérhetősége újraindítás után megmarad', async () => {
  const rendeles = (await leadas(minta())).torzs;
  assert.equal((await modositasKuldese('/etelek/9', { elerheto: false })).kod, 204);
  await szerver.ujrainditas();
  assert.deepEqual((await szerver.keres('/rendelesek/' + rendeles.azonosito)).torzs, rendeles);
  assert.ok(!(await szerver.keres('/etlap')).torzs.etelek.some(etel => etel.azonosito === 9));
});


test('API-08 · A régi adatbázis rendelései, tételei és elérhetősége átkerülnek', async () => {
  const azonosito = randomUUID();
  const adatok = minta({
    vendegNeve: 'Regi Vendeg', telefonszam: '06305550100', megjegyzes: 'Korabbi rendeles',
    tetelek: [{ etelAzonosito: 1, mennyiseg: 2 }],
  });
  const lenyomat = createHash('sha256').update(JSON.stringify({
    name: adatok.vendegNeve, phone: adatok.telefonszam, note: adatok.megjegyzes,
    quantities: { 1: 2 },
  })).digest('hex').toUpperCase();

  const regi = await tesztkornyezetLetrehozasa(eleresiUt => {
    const adatbazis = new DatabaseSync(eleresiUt);
    try {
      adatbazis.exec(readFileSync(new URL('./regi-sema.sql', import.meta.url), 'utf-8'));
      adatbazis.prepare('UPDATE dishes SET available = 0 WHERE id = 9').run();
      adatbazis.prepare(`
        INSERT INTO orders VALUES (?, ?, ?, ?, ?, ?, 'ready', 8000, '2026-11-24T10:00:00Z')
      `).run(azonosito, adatok.keresAzonosito, lenyomat, adatok.vendegNeve, adatok.telefonszam, adatok.megjegyzes);
      adatbazis.prepare('INSERT INTO order_items VALUES (?, 1, ?, 4000, 2)').run(azonosito, 'Korábbi pizza');
    } finally {
      adatbazis.close();
    }
  });
  try {
    const rendeles = await regi.keres('/rendelesek/' + azonosito);
    assert.equal(rendeles.kod, 200);
    assert.equal(rendeles.torzs.allapot, 'atveheto');
    assert.equal(rendeles.torzs.vegosszeg, 8000);
    assert.deepEqual(rendeles.torzs.tetelek, [{ etelAzonosito: 1, etelNeve: 'Korábbi pizza', egysegar: 4000, mennyiseg: 2 }]);
    assert.ok(!(await regi.keres('/etlap')).torzs.etelek.some(etel => etel.azonosito === 9));
    const ujraproba = await regi.keres('/rendelesek', { modszer: 'POST', adatok });
    assert.equal(ujraproba.kod, 200);
    assert.equal(ujraproba.torzs.azonosito, azonosito);
    await regi.ujrainditas();
    assert.deepEqual((await regi.keres('/rendelesek/' + azonosito)).torzs, rendeles.torzs);
  } finally {
    await regi.bezaras();
  }
});

test('API-09 · Az összesítés szerverára ellenőrizhető a leadáskor', async () => {
  const adatok = minta({ tetelek: [{ etelAzonosito: 2, mennyiseg: 2 }] });
  const osszesites = await szerver.keres('/rendelesek/osszesites', { modszer: 'POST', adatok });
  assert.equal(osszesites.kod, 200);
  assert.equal(osszesites.torzs.vegosszeg, 6980);
  assert.equal((await leadas({ ...adatok, vartVegosszeg: 1 })).kod, 409);
  assert.equal((await leadas({ ...adatok, vartVegosszeg: 6980 })).kod, 201);
});
