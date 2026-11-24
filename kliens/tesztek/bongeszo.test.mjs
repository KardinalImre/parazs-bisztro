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

