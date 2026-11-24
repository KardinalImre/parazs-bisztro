import { useEffect, useRef, useState } from 'react';
import { api, eltarol, keresAzonosito, tarolt } from './api.js';

const utvonal = '/ugyfelszolgalat/jegyek';
const jegyKulcs = 'parazs.utolsoJegy';
const kodMinta = /^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i;
export const jegyAllapot = { nyitott: 'Beérkezett', megvalaszolt: 'Megválaszolva', lezart: 'Lezárva' };

export function Ugyfelszolgalat() {
  const [kod, kodBeallitasa] = useState(() => String(tarolt(jegyKulcs, '')));
  const [jegy, jegyBeallitasa] = useState(null);
  const [email, emailBeallitasa] = useState('');
  const [foglalt, foglaltsagBeallitasa] = useState(false);
  const [hiba, hibaBeallitasa] = useState('');
  const bekuldes = useRef(null);

  useEffect(() => {
    api('/profil').then(valasz => emailBeallitasa(jelenlegi => jelenlegi || valasz.profil?.email || '')).catch(() => {});
  }, []);

  async function muvelet(feladat) {
    hibaBeallitasa('');
    foglaltsagBeallitasa(true);
    try {
      await feladat();
    } catch (problema) {
      hibaBeallitasa(problema.message);
    } finally {
      foglaltsagBeallitasa(false);
    }
  }

  function bekuld(esemeny) {
    esemeny.preventDefault();
    const urlap = esemeny.currentTarget;
    const adatok = Object.fromEntries(new FormData(urlap));
    adatok.rendelesAzonosito = adatok.rendelesAzonosito.trim() || null;
    const lenyomat = JSON.stringify(adatok);
    if (bekuldes.current?.lenyomat !== lenyomat) bekuldes.current = { lenyomat, azonosito: keresAzonosito() };
    void muvelet(async () => {
      const eredmeny = await api(utvonal, { modszer: 'POST', adatok: { ...adatok, keresAzonosito: bekuldes.current.azonosito } });
      jegyBeallitasa(eredmeny);
      kodBeallitasa(eredmeny.azonosito);
      eltarol(jegyKulcs, eredmeny.azonosito);
      urlap.reset();
      bekuldes.current = null;
    });
  }

  function lekeres(esemeny) {
    esemeny.preventDefault();
    const tisztitott = kod.trim().toLowerCase();
    if (!kodMinta.test(tisztitott)) {
      hibaBeallitasa('Adj meg érvényes ügyazonosítót.');
      return;
    }
    void muvelet(async () => jegyBeallitasa(await api(utvonal + '/' + tisztitott)));
  }

  return (
    <main className="keret szemelyzet">
      <div className="etlap-fejlec"><h1>Ügyfélszolgálat</h1></div>
      {hiba && <p className="hiba" role="alert">{hiba}</p>}
      <div className="ugyfel-racs">
        <form className="szemelyzeti-belepes" onSubmit={bekuld}>
          <h2>Új üzenet</h2>
          <label>E-mail-cím<input name="email" autoComplete="email" type="email" value={email}
            onChange={esemeny => emailBeallitasa(esemeny.target.value)} required maxLength={254} /></label>
          <label>Tárgy<input name="targy" required minLength={3} maxLength={100} /></label>
          <label>Rendelési követési kód <span className="halvany">(opcionális)</span>
            <input name="rendelesAzonosito" maxLength={36} autoComplete="off" />
          </label>
          <label>Üzenet<textarea name="uzenet" required minLength={10} maxLength={2000} rows={5} /></label>
          <button className="gomb elsodleges" disabled={foglalt}>Üzenet elküldése</button>
        </form>
        <div>
          <form className="szemelyzeti-belepes" onSubmit={lekeres}>
            <h2>Válasz keresése</h2>
            <label>Ügyazonosító<input value={kod} onChange={esemeny => kodBeallitasa(esemeny.target.value)}
              required maxLength={36} autoComplete="off" /></label>
            <button className="gomb masodlagos" disabled={foglalt}>Válasz lekérése</button>
          </form>
          {jegy?.azonosito === kod.trim().toLowerCase() && (
            <div className="szemelyzeti-rendeles ugyfel-valasz" role="status">
              <h2>{jegy.targy}</h2>
              <p>{jegyAllapot[jegy.allapot]}</p>
              <code>{jegy.azonosito}</code>
              <p>{jegy.valasz || 'Őrizd meg az ügyazonosítót. Ezzel később lekérheted a választ.'}</p>
            </div>
          )}
        </div>
      </div>
    </main>
  );
}
