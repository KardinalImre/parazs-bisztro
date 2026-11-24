import { useEffect, useState } from 'react';
import { api } from './api.js';
import { jegyAllapot } from './ugyfelszolgalat.jsx';

export function SzemelyzetiJegyek({ kulcs }) {
  const [jegyek, jegyekBeallitasa] = useState([]);
  const [foglalt, foglaltsagBeallitasa] = useState(false);
  const [hiba, hibaBeallitasa] = useState('');

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

  const frissites = () => muvelet(async () => jegyekBeallitasa(await api('/szemelyzet/jegyek', { kulcs })));
  useEffect(() => { void frissites(); }, [kulcs]);

  function modositas(azonosito, allapot, valasz) {
    void muvelet(async () => {
      const friss = await api('/szemelyzet/jegyek/' + azonosito, { modszer: 'PATCH', kulcs, adatok: { allapot, valasz } });
      jegyekBeallitasa(jelenlegi => jelenlegi.map(jegy => jegy.azonosito === azonosito ? friss : jegy));
    });
  }

  return (
    <section aria-label="Ügyfélszolgálati jegyek">
      <button className="gomb masodlagos" onClick={frissites} disabled={foglalt}>Jegyek frissítése</button>
      {hiba && <p className="hiba" role="alert">{hiba}</p>}
      {!foglalt && !hiba && !jegyek.length && <p>Még nincs ügyfélszolgálati üzenet.</p>}
      <div className="szemelyzeti-rendelesek ugyfel-valasz">
        {jegyek.map(jegy => (
          <article className="szemelyzeti-rendeles" key={jegy.azonosito}>
            <h2>{jegy.targy}</h2>
            <p>{jegyAllapot[jegy.allapot]} · {jegy.email}</p>
            {jegy.rendelesAzonosito && <p>Rendelés <code>{jegy.rendelesAzonosito}</code></p>}
            <p className="ugyfel-uzenet">{jegy.uzenet}</p>
            {jegy.allapot === 'lezart' ? <p className="ugyfel-uzenet">{jegy.valasz}</p> : (
              <form onSubmit={esemeny => {
                esemeny.preventDefault();
                modositas(jegy.azonosito, 'megvalaszolt', new FormData(esemeny.currentTarget).get('valasz'));
              }}>
                <label>Válasz<textarea name="valasz" rows={3} required minLength={2} maxLength={2000} defaultValue={jegy.valasz} /></label>
                <div className="szemelyzeti-muveletek">
                  <button className="gomb elsodleges" disabled={foglalt}>Válasz mentése</button>
                  <button className="gomb masodlagos" type="button" disabled={foglalt}
                    onClick={() => modositas(jegy.azonosito, 'lezart')}>Ügy lezárása</button>
                </div>
              </form>
            )}
          </article>
        ))}
      </div>
    </section>
  );
}
