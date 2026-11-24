import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { execFile } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { test } from 'node:test';
import { tesztkornyezetLetrehozasa, szemelyzetiKulcs } from './tesztszerver.mjs';

const futtat = promisify(execFile);
const projekt = fileURLToPath(new URL('../../szemelyzet-wpf-tesztek/Parazs.Szemelyzet.Tesztek.csproj', import.meta.url));

test('WPF-01 · A WPF API-kliens valódi rendelést, ételt és ügyfélszolgálati jegyet kezel', async () => {
  const szerver = await tesztkornyezetLetrehozasa();
  try {
    const rendeles = await szerver.keres('/rendelesek', {
      modszer: 'POST', adatok: {
        keresAzonosito: randomUUID(), vendegNeve: 'WPF Próba', telefonszam: '06305550100',
        tetelek: [{ etelAzonosito: 1, mennyiseg: 1 }],
      },
    });
    assert.equal(rendeles.kod, 201);
    const jegy = await szerver.keres('/ugyfelszolgalat/jegyek', {
      modszer: 'POST', adatok: {
        keresAzonosito: randomUUID(), targy: 'WPF ügy', email: 'wpf@example.com',
        uzenet: 'Tesztüzenet a WPF-klienshez.', rendelesAzonosito: rendeles.torzs.azonosito,
      },
    });
    assert.equal(jegy.kod, 201);
    const { stdout } = await futtat('dotnet', [
      'run', '--project', projekt, '--', szerver.cim, rendeles.torzs.azonosito, jegy.torzs.azonosito,
    ], { env: { ...process.env, PARAZS_TESZT_KULCS: szemelyzetiKulcs }, timeout: 60000 });
    assert.match(stdout, /WPF-kliens API-műveletei sikeresek/);
    assert.equal((await szerver.keres('/ugyfelszolgalat/jegyek/' + jegy.torzs.azonosito)).torzs.allapot, 'lezart');
  } finally {
    await szerver.bezaras();
  }
});

