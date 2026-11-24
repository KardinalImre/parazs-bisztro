import { useState } from 'react';
import { api, forintban, allapotFelirat } from './api.js';

const kovetkezoAllapot = { beerkezett: 'keszul', keszul: 'atveheto', atveheto: 'atadva' };
const kovetkezoFelirat = { beerkezett: 'Elkészítés indítása', keszul: 'Átvehetőre állítás', atveheto: 'Átadás rögzítése' };
const idopontFormazo = new Intl.DateTimeFormat('hu-HU', { dateStyle: 'short', timeStyle: 'short' });

export function Szemelyzet() {
  const [kulcs, kulcsBeallitasa] = useState('');
  const [rendelesek, rendelesekBeallitasa] = useState([]);
  const [etlap, etlapBeallitasa] = useState(null);
  const [foglalt, foglaltsagBeallitasa] = useState(false);
  const [hiba, hibaBeallitasa] = useState('');
  const [ful, fulBeallitasa] = useState('rendelesek');

  async function muvelet(feladat) {
    foglaltsagBeallitasa(true);
    hibaBeallitasa('');
    try {
      await feladat();
    } catch (problema) {
      hibaBeallitasa(problema.message);
    } finally {
      foglaltsagBeallitasa(false);
    }
  }

  async function frissites(hozzaferesiKulcs) {
    const beallitasok = { kulcs: hozzaferesiKulcs };
    const [ujRendelesek, ujEtlap] = await Promise.all([api('/szemelyzet/rendelesek', beallitasok), api('/szemelyzet/etlap', beallitasok)]);
    rendelesekBeallitasa(ujRendelesek);
    etlapBeallitasa(ujEtlap);
  }

  function belepes(esemeny) {
    esemeny.preventDefault();
    const hozzaferesiKulcs = new FormData(esemeny.currentTarget).get('kulcs').trim();
    void muvelet(async () => { await frissites(hozzaferesiKulcs); kulcsBeallitasa(hozzaferesiKulcs); });
  }

  function kilepes() {
    kulcsBeallitasa('');
    rendelesekBeallitasa([]);
    etlapBeallitasa(null);
    hibaBeallitasa('');
  }

  function allapotModositasa(azonosito, allapot) {
    void muvelet(async () => {
      const frissitett = await api('/szemelyzet/rendelesek/' + azonosito, { modszer: 'PATCH', kulcs, adatok: { allapot } });
      rendelesekBeallitasa(jelenlegi => jelenlegi.map(rendeles => rendeles.azonosito === azonosito ? frissitett : rendeles));
    });
  }

  function elerhetosegValtasa(etel) {
    const elerheto = !etel.elerheto;
    void muvelet(async () => {
      await api('/szemelyzet/etelek/' + etel.azonosito, { modszer: 'PATCH', kulcs, adatok: { elerheto } });
      etlapBeallitasa(jelenlegi => ({
        ...jelenlegi, etelek: jelenlegi.etelek.map(tetel => tetel.azonosito === etel.azonosito ? { ...tetel, elerheto } : tetel),
      }));
    });
  }

  return (
    <main className="keret szemelyzet">
      <div className="etlap-fejlec">
        <div><span className="felirat">PARÁZS BISZTRÓ</span><h1>Személyzeti felület</h1></div>
        {kulcs && <button className="gomb masodlagos" disabled={foglalt} onClick={kilepes}>Kilépés</button>}
      </div>
      {hiba && <p className="hiba" role="alert">{hiba}</p>}
      {!kulcs ? (
        <form className="szemelyzeti-belepes" onSubmit={belepes}>
          <label>Személyzeti kulcs<input type="password" name="kulcs" autoComplete="off" required /></label>
          <button className="gomb elsodleges" disabled={foglalt}>{foglalt ? 'Ellenőrzés…' : 'Belépés'}</button>
        </form>
      ) : (
        <>
          <div className="szemelyzeti-eszkoztar">
            <div className="szurok">
              <button aria-pressed={ful === 'rendelesek'} onClick={() => fulBeallitasa('rendelesek')}>Rendelések</button>
              <button aria-pressed={ful === 'etlap'} onClick={() => fulBeallitasa('etlap')}>Étel elérhetősége</button>
            </div>
            <button className="gomb masodlagos" onClick={() => muvelet(() => frissites(kulcs))} disabled={foglalt}>Frissítés</button>
          </div>
          {ful === 'rendelesek' ? (
            <div className="szemelyzeti-rendelesek">
              {!rendelesek.length && <p>Még nem érkezett rendelés.</p>}
              {rendelesek.map(rendeles => (
                <article className="szemelyzeti-rendeles" key={rendeles.azonosito}>
                  <div className="kosar-fejlec">
                    <h2>{rendeles.vendegNeve}</h2><span className={'allapot ' + rendeles.allapot}>{allapotFelirat[rendeles.allapot]}</span>
                  </div>
                  <p className="halvany">{idopontFormazo.format(new Date(rendeles.letrehozva))} · {rendeles.telefonszam}</p>
                  <code className="halvany">{rendeles.azonosito}</code>
                  <ul>{rendeles.tetelek.map(tetel => <li key={tetel.etelAzonosito}>{tetel.mennyiseg} × {tetel.etelNeve}</li>)}</ul>
                  {rendeles.megjegyzes && <p>Megjegyzés — {rendeles.megjegyzes}</p>}
                  <strong>{forintban(rendeles.vegosszeg)}</strong>
                  {kovetkezoAllapot[rendeles.allapot] && (
                    <div className="szemelyzeti-muveletek">
                      <button className="gomb elsodleges" disabled={foglalt}
                        onClick={() => allapotModositasa(rendeles.azonosito, kovetkezoAllapot[rendeles.allapot])}>{kovetkezoFelirat[rendeles.allapot]}</button>
                      <button className="gomb masodlagos" disabled={foglalt}
                        onClick={() => allapotModositasa(rendeles.azonosito, 'torolve')}>Rendelés törlése</button>
                    </div>
                  )}
                </article>
              ))}
            </div>
          ) : (
            <ul className="elerhetoseg">
              {etlap.etelek.map(etel => <li key={etel.azonosito}>
                <div><strong>{etel.nev}</strong><span>{etel.elerheto ? 'Rendelhető' : 'Nem rendelhető'}</span></div>
                <button className="gomb masodlagos" disabled={foglalt}
                  aria-label={etel.nev + (etel.elerheto ? ' elrejtése' : ' visszakapcsolása')}
                  onClick={() => elerhetosegValtasa(etel)}>{etel.elerheto ? 'Elrejtés' : 'Visszakapcsolás'}</button>
              </li>)}
            </ul>
          )}
        </>
      )}
    </main>
  );
}

