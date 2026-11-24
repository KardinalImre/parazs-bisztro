import assert from 'node:assert/strict';
import { after, before, test } from 'node:test';
import { chromium } from 'playwright';
import { tesztkornyezetLetrehozasa, szemelyzetiKulcs } from './tesztszerver.mjs';

let szerver;
let bongeszo;
before(async () => {
  szerver = await tesztkornyezetLetrehozasa();
  bongeszo = await chromium.launch({ channel: process.env.PARAZS_BONGESZO || undefined });
});
after(async () => {
  await bongeszo?.close();
  await szerver?.bezaras();
});

test('UI-01 · Asztali rendelés, személyzeti átállítás és vendégoldali követés', async () => {
  const page = await bongeszo.newPage({ viewport: { width: 1440, height: 1000 } });
  const szemelyzet = await bongeszo.newPage();
  page.setDefaultTimeout(8000);
  szemelyzet.setDefaultTimeout(8000);
  try {
    const kivetelek = [];
    page.on('pageerror', hiba => kivetelek.push(hiba.message));
    await page.goto(szerver.cim);
    await page.getByRole('heading', { name: 'Parázs szalámis pizza' }).waitFor();
    assert.ok(await page.locator('.nyitokep img').evaluate(image => image.complete && image.naturalWidth > 0));
    await page.getByRole('button', { name: 'Parázs szalámis pizza kosárba' }).click();
    await page.getByRole('button', { name: 'Tovább az adatokhoz' }).click();
    await page.getByLabel('Név').fill('Böngészős Teszt');
    await page.getByLabel('Telefonszám').fill('+36 30 555 0100');
    await page.getByRole('button', { name: 'Rendelés leadása' }).click();
    await page.getByText('Megkaptuk a rendelésed.').waitFor();
    const azonosito = await page.locator('.visszaigazolas code').textContent();
    assert.match(azonosito, /^[0-9a-f-]{36}$/);

    await szemelyzet.goto(szerver.cim + '/admin');
    assert.equal(new URL(szemelyzet.url()).pathname, '/szemelyzet');
    await szemelyzet.getByLabel('Személyzeti kulcs').fill(szemelyzetiKulcs);
    await szemelyzet.getByRole('button', { name: 'Belépés' }).click();
    const rendeles = szemelyzet.locator('.szemelyzeti-rendeles').filter({ hasText: azonosito });
    await rendeles.getByRole('button', { name: 'Elkészítés indítása' }).click();
    await rendeles.getByRole('button', { name: 'Átvehetőre állítás' }).click();
    await rendeles.getByText('Átvehető', { exact: true }).waitFor();

    await page.reload();
    assert.equal(await page.getByLabel('Követési kód').inputValue(), azonosito);
    await page.getByRole('button', { name: 'Állapot lekérése' }).click();
    await page.locator('.kovetett').getByText('Átvehető', { exact: true }).waitFor();
    assert.deepEqual(kivetelek, []);
    if (process.env.PARAZS_KEPERNYOKEPEK)
      await page.screenshot({ path: process.env.PARAZS_KEPERNYOKEPEK + '/desktop.png', fullPage: true });
  } finally {
    await Promise.all([page.close(), szemelyzet.close()]);
  }
});

test('UI-02 · Mobilos étlap, kosár megőrzése és rendelés 390 px szélességen', async () => {
  const page = await bongeszo.newPage({
    viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true,
  });
  page.setDefaultTimeout(8000);
  try {
    await page.goto(szerver.cim);
    const kereso = page.getByRole('searchbox', { name: 'Keresés az étlapon' });
    await kereso.fill('csokolade');
    await page.getByRole('heading', { name: 'Csokoládétorta' }).waitFor();
    assert.equal(await page.locator('.etel').count(), 1);
    await kereso.fill('');
    await page.getByRole('button', { name: 'Desszertek', exact: true }).click();
    assert.equal(await page.locator('.etel').count(), 2);
    await page.getByRole('button', { name: 'Házi tiramisu kosárba' }).click();
    const mentettKosar = await page.evaluate(() => {
      const kosar = JSON.parse(localStorage.getItem('parazs.cart'));
      localStorage.setItem('parazs.cart', JSON.stringify([{ id: 7, quantity: 1 }]));
      return kosar;
    });
    assert.deepEqual(mentettKosar, [{ azonosito: 7, mennyiseg: 1 }]);
    await page.reload();
    await page.getByRole('link', { name: 'Kosár (1)' }).click();
    const kosar = page.locator('#kosar');
    await kosar.getByText('Házi tiramisu', { exact: true }).waitFor();
    await kosar.getByRole('button', { name: 'Házi tiramisu növelése' }).click();
    await kosar.getByRole('button', { name: 'Tovább az adatokhoz' }).click();
    await kosar.getByLabel('Név').fill('Mobil Teszt');
    await kosar.getByLabel('Telefonszám').fill('06305550100');
    await kosar.getByRole('button', { name: 'Rendelés leadása' }).click();
    await kosar.getByText('Megkaptuk a rendelésed.').waitFor();
    assert.match(await kosar.locator('.visszaigazolas').textContent(), /3\s?580 Ft/);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    if (process.env.PARAZS_KEPERNYOKEPEK)
      await page.screenshot({ path: process.env.PARAZS_KEPERNYOKEPEK + '/mobile.png', fullPage: true });
  } finally {
    await page.close();
  }
});


test('UI-03 · Vendégprofil, előtöltött rendelési adatok és rendelési előzmények', async () => {
  const oldal = await bongeszo.newPage();
  oldal.setDefaultTimeout(8000);
  try {
    await oldal.goto(szerver.cim + '/profil');
    await oldal.getByRole('button', { name: 'Új profilt kérek' }).click();
    await oldal.getByLabel('Név').fill('Profil Próba');
    await oldal.getByLabel('E-mail-cím').fill('bongeszo@example.com');
    await oldal.getByLabel('Jelszó', { exact: true }).fill('Bongeszo-jelszo-902');
    await oldal.getByRole('button', { name: 'Regisztráció', exact: true }).click();
    await oldal.getByLabel('Telefonszám').fill('06305550100');
    await oldal.getByRole('button', { name: 'Adatok mentése' }).click();
    await oldal.getByText('A profiladatokat mentettük.').waitFor();
    await oldal.getByRole('button', { name: 'Próbafeltöltés' }).click();
    await oldal.getByText('A próbaösszeget jóváírtuk.').waitFor();
    await oldal.getByRole('navigation').getByRole('link', { name: 'Étlap' }).click();
    await oldal.getByRole('button', { name: 'Margherita kosárba' }).click();
    await oldal.getByRole('button', { name: 'Tovább az adatokhoz' }).click();
    assert.equal(await oldal.getByLabel('Név').inputValue(), 'Profil Próba');
    assert.equal(await oldal.getByLabel('Telefonszám').inputValue(), '06305550100');
    await oldal.getByLabel('Fizetési mód').selectOption('tarca');
    await oldal.getByLabel('Kuponkód').fill('parazs10');
    await oldal.getByRole('button', { name: 'Végösszeg ellenőrzése' }).click();
    await oldal.getByText(/PARAZS10 kedvezmény/).waitFor();
    await oldal.getByRole('button', { name: 'Rendelés leadása' }).click();
    await oldal.getByText('Megkaptuk a rendelésed.').waitFor();
    await oldal.getByText(/Demópénztárcával fizetve/).waitFor();
    await oldal.getByRole('navigation').getByRole('link', { name: 'Profil' }).click();
    await oldal.getByRole('heading', { name: 'Korábbi rendeléseim' }).waitFor();
    await oldal.getByText('1 × Margherita', { exact: true }).waitFor();
  } finally {
    await oldal.close();
  }
});

test('UI-04 · Ügyfélszolgálati üzenet és válasz megjelenítése', async () => {
  const vendeg = await bongeszo.newPage({ viewport: { width: 390, height: 844 } });
  const szemelyzet = await bongeszo.newPage();
  vendeg.setDefaultTimeout(8000);
  szemelyzet.setDefaultTimeout(8000);
  try {
    await vendeg.goto(szerver.cim + '/ugyfelszolgalat');
    await vendeg.getByLabel('E-mail-cím').fill('ugyfel@example.com');
    await vendeg.getByLabel('Tárgy').fill('Átvételi kérdés');
    await vendeg.getByLabel('Üzenet', { exact: true }).fill('Meddig lehet átvenni a rendelést?');
    await vendeg.getByRole('button', { name: 'Üzenet elküldése' }).click();
    await vendeg.getByRole('heading', { name: 'Átvételi kérdés' }).waitFor();
    assert.match(await vendeg.getByLabel('Ügyazonosító').inputValue(), /^[0-9a-f-]{36}$/);
    await szemelyzet.goto(szerver.cim + '/szemelyzet');
    await szemelyzet.getByLabel('Személyzeti kulcs').fill(szemelyzetiKulcs);
    await szemelyzet.getByRole('button', { name: 'Belépés' }).click();
    await szemelyzet.getByRole('button', { name: 'Ügyfélszolgálat', exact: true }).click();
    await szemelyzet.getByLabel('Válasz', { exact: true }).fill('A pultnál várjuk.');
    await szemelyzet.getByRole('button', { name: 'Válasz mentése' }).click();
    await szemelyzet.getByText(/Megválaszolva/).waitFor();
    await vendeg.getByRole('button', { name: 'Válasz lekérése' }).click();
    await vendeg.getByText('A pultnál várjuk.', { exact: true }).waitFor();
  } finally {
    await Promise.all([vendeg.close(), szemelyzet.close()]);
  }
});
