import { useEffect, useRef, useState } from 'react';
import { api, forintban, keresAzonosito } from './api.js';

const tipusNev = { feltoltes: 'Próbafeltöltés', fizetes: 'Rendelés fizetése', jovairas: 'Jóváírás' };

export function Tarca() {
  const [tarca, tarcaBeallitasa] = useState(null);
  const [hiba, hibaBeallitasa] = useState('');
  const [foglalt, foglaltsagBeallitasa] = useState(false);
  const [uzenet, uzenetBeallitasa] = useState('');
  const bekuldes = useRef(null);

  useEffect(() => {
    api('/profil/tarca').then(tarcaBeallitasa).catch(problema => hibaBeallitasa(problema.message));
  }, []);

  async function feltoltes(esemeny) {
    esemeny.preventDefault();
    const osszeg = Number(new FormData(esemeny.currentTarget).get('osszeg'));
    if (bekuldes.current?.osszeg !== osszeg) bekuldes.current = { osszeg, keresAzonosito: keresAzonosito() };
    hibaBeallitasa('');
    uzenetBeallitasa('');
    foglaltsagBeallitasa(true);
    try {
      tarcaBeallitasa(await api('/profil/tarca/feltoltesek', { modszer: 'POST', adatok: bekuldes.current }));
      bekuldes.current = null;
      uzenetBeallitasa('A próbaösszeget jóváírtuk.');
    } catch (problema) {
      hibaBeallitasa(problema.message);
    } finally {
      foglaltsagBeallitasa(false);
    }
  }

  return (
    <section className="profil-elozmenyek">
      <h2>Demópénztárca</h2>
      <div className="szemelyzeti-rendeles">
        <p>Próbaegyenleg a vizsgabemutatóhoz. A feltöltés a demóban történik, bankkártyaadat megadása nélkül.</p>
        {tarca && <p>Elérhető egyenleg <strong>{forintban(tarca.egyenleg)}</strong></p>}
        {hiba && <p className="hiba" role="alert">{hiba}</p>}
        {uzenet && <p className="visszaigazolas" role="status">{uzenet}</p>}
        <form className="tarca-feltoltes" onSubmit={feltoltes}>
          <label>Próbaösszeg
            <select name="osszeg" defaultValue="5000" disabled={foglalt}>
              {[1000, 5000, 10000].map(osszeg => <option key={osszeg} value={osszeg}>{forintban(osszeg)}</option>)}
            </select>
          </label>
          <button className="gomb elsodleges" disabled={foglalt}>Próbafeltöltés</button>
        </form>
        {tarca?.muveletek.length > 0 && <ul className="tarca-muveletek">
          {tarca.muveletek.map((muvelet, sorszam) => (
            <li key={muvelet.letrehozva + '-' + sorszam}>
              <span>{tipusNev[muvelet.tipus]}</span>
              <span>{muvelet.osszeg > 0 ? '+' : ''}{forintban(muvelet.osszeg)}</span>
            </li>
          ))}
        </ul>}
      </div>
    </section>
  );
}

