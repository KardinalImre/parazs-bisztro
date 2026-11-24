export const allapotFelirat = {
  beerkezett: 'Beérkezett', keszul: 'Készül', atveheto: 'Átvehető',
  atadva: 'Átadva', torolve: 'Törölve',
};

const forint = new Intl.NumberFormat('hu-HU');
export const forintban = adag => forint.format(adag) + ' Ft';

export async function api(utvonal, { modszer = 'GET', adatok, kulcs } = {}) {
  const fejlecek = {};
  if (adatok !== undefined) fejlecek['Content-Type'] = 'application/json';
  if (kulcs) fejlecek.Authorization = 'Bearer ' + kulcs;
  const valasz = await fetch('/api' + utvonal, {
    method: modszer, headers: fejlecek, body: adatok === undefined ? undefined : JSON.stringify(adatok),
  });
  if (valasz.status === 204) return null;
  const eredmeny = await valasz.json().catch(() => null);
  if (!valasz.ok) throw new Error(eredmeny?.hiba || 'A kérés sikertelen (' + valasz.status + ').');
  if (eredmeny === null) throw new Error('A szerver nem adott értelmezhető választ.');
  return eredmeny;
}

export function tarolt(nev, alapertelmezett) {
  try { return JSON.parse(localStorage.getItem(nev)) ?? alapertelmezett; }
  catch { return alapertelmezett; }
}

export function eltarol(nev, ertek) {
  try { localStorage.setItem(nev, JSON.stringify(ertek)); }
  catch { /* Tiltott tárhely esetén az oldal továbbra is működik. */ }
}

export function keresAzonosito() {
  const bajtok = crypto.getRandomValues(new Uint8Array(16));
  bajtok[6] = (bajtok[6] & 15) | 64;
  bajtok[8] = (bajtok[8] & 63) | 128;
  const hexadecimalis = Array.from(bajtok, ertek => ertek.toString(16).padStart(2, '0')).join('');
  return [hexadecimalis.slice(0, 8), hexadecimalis.slice(8, 12), hexadecimalis.slice(12, 16), hexadecimalis.slice(16, 20), hexadecimalis.slice(20)].join('-');
}

