import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const szerverMappa = fileURLToPath(new URL('../../szerver/', import.meta.url));
export const szemelyzetiKulcs = 'parazs-local-test-szemelyzet-key';

export async function tesztkornyezetLetrehozasa(elokeszites) {
  const mappa = await mkdtemp(join(tmpdir(), 'parazs-test-'));
  const adatbazis = join(mappa, 'test.sqlite3');
  let folyamat;

  async function leallitas() {
    if (!folyamat || folyamat.exitCode !== null || folyamat.signalCode !== null || !folyamat.pid) return;
    const leallt = once(folyamat, 'exit');
    folyamat.kill();
    await leallt;
  }

  async function inditas() {
    folyamat = spawn('dotnet', ['bin/Debug/net10.0/Parazs.dll', '--urls', 'http://127.0.0.1:0'], {
      cwd: szerverMappa,
      env: { ...process.env, PARAZS_ADATBAZIS: adatbazis, PARAZS_SZEMELYZETI_KULCS: szemelyzetiKulcs },
    });
    return new Promise((resolve, reject) => {
      let kimenet = '';
      const idozito = setTimeout(() => befejezes(new Error('A tesztszerver nem indult el.\n' + kimenet)), 15000);

      function befejezes(hiba, url) {
        clearTimeout(idozito);
        folyamat.off('exit', kilepesKezelo);
        folyamat.off('error', hibaKezelo);
        folyamat.stdout.off('data', adatKezelo);
        folyamat.stderr.off('data', adatKezelo);
        if (hiba) reject(hiba);
        else resolve(url);
      }
      function kilepesKezelo() { befejezes(new Error('A tesztszerver leállt.\n' + kimenet)); }
      function hibaKezelo(hiba) { befejezes(hiba); }
      function adatKezelo(reszlet) {
        kimenet += reszlet;
        const url = /Kiszolgálás címe: (http:\/\/127\.0\.0\.1:\d+)/.exec(kimenet)?.[1];
        if (url) befejezes(null, url);
      }
      folyamat.on('error', hibaKezelo);
      folyamat.on('exit', kilepesKezelo);
      folyamat.stdout.on('data', adatKezelo);
      folyamat.stderr.on('data', adatKezelo);
    });
  }

  const tesztkornyezet = {
    cim: '',
    async ujrainditas() { await leallitas(); this.cim = await inditas(); },
    async bezaras() { await leallitas(); await rm(mappa, { recursive: true, force: true }); },
    async keres(utvonal, { modszer = 'GET', adatok, szemelyzet = false } = {}) {
      const headers = {};
      if (szemelyzet) headers.Authorization = 'Bearer ' + szemelyzetiKulcs;
      if (adatok !== undefined) headers['Content-Type'] = 'application/json';
      const response = await fetch(this.cim + '/api' + utvonal, {
        method: modszer, headers, body: adatok === undefined ? undefined : JSON.stringify(adatok),
      });
      const body = await response.json().catch(() => null);
      return { kod: response.status, torzs: body, hely: response.headers.get('location') };
    },
  };
  try {
    await elokeszites?.(adatbazis);
    tesztkornyezet.cim = await inditas();
    return tesztkornyezet;
  } catch (hiba) {
    await tesztkornyezet.bezaras();
    throw hiba;
  }
}

