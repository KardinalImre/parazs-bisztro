import { useEffect, useRef, useState } from 'react';
import { api, forintban, eltarol, keresAzonosito, allapotFelirat, tarolt } from './api.js';

const uresEtlap = { kategoriak: [], etelek: [] };
const kosarKulcs = 'parazs.cart';
const rendelesKulcs = 'parazs.lastOrder';
function keresheto(szoveg) {
  return szoveg.toLocaleLowerCase('hu-HU').normalize('NFD').replace(/\p{M}/gu, '');
}

const kovetesiMinta = /^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i;

function MennyisegVezerlo({ nev, adag, foglalt, modositas }) {
  return (
    <div className="mennyiseg">
      <button type="button" aria-label={nev + ' csökkentése'} disabled={foglalt} onClick={() => modositas(-1)}>−</button>
      <span>{adag}</span>
      <button type="button" aria-label={nev + ' növelése'} disabled={foglalt || adag >= 20}
        onClick={() => modositas(1)}>+</button>
    </div>
  );
}

function Kovetes({ rendeles, rendelesBeallitasa, kod, kodBeallitasa }) {
  const [hiba, hibaBeallitasa] = useState('');
  const [foglalt, foglaltsagBeallitasa] = useState(false);

  async function lekeres(esemeny) {
    esemeny.preventDefault();
    const tisztitott = kod.trim().toLowerCase();
    hibaBeallitasa('');
    if (!kovetesiMinta.test(tisztitott)) {
      hibaBeallitasa('Add meg a visszaigazolásban szereplő követési kódot.');
      return;
    }
    foglaltsagBeallitasa(true);
    try {
      rendelesBeallitasa(await api('/rendelesek/' + tisztitott));
    } catch (problema) {
      hibaBeallitasa(problema.message);
    } finally {
      foglaltsagBeallitasa(false);
    }
  }

  return (
    <section className="kovetes" id="kovetes">
      <div>
        <span className="felirat">SZEMÉLYES ÁTVÉTEL</span>
        <h2>Rendelés követése</h2>
        <p>A visszaigazolásban kapott kóddal megnézheted, mikor vehető át a rendelésed.</p>
      </div>
      <div>
        <form onSubmit={lekeres}>
          <label htmlFor="kovetes-kod">Követési kód</label>
          <div className="kovetesi-vezerlok">
            <input id="kovetes-kod" value={kod} onChange={esemeny => kodBeallitasa(esemeny.target.value)}
              maxLength={36} spellCheck="false" autoComplete="off" required />
            <button className="gomb masodlagos" disabled={foglalt}>{foglalt ? 'Betöltés…' : 'Állapot lekérése'}</button>
          </div>
        </form>
        {hiba && <p className="hiba" role="alert">{hiba}</p>}
        {rendeles && rendeles.azonosito === kod.trim().toLowerCase() && (
          <div className="kovetett" role="status">
            <span className={'allapot ' + rendeles.allapot}>{allapotFelirat[rendeles.allapot]}</span>
            <strong>{forintban(rendeles.vegosszeg)}</strong>
            <ul>
              {rendeles.tetelek.map(tetel => <li key={tetel.etelAzonosito}>{tetel.mennyiseg} × {tetel.etelNeve}</li>)}
            </ul>
          </div>
        )}
      </div>
    </section>
  );
}

function Kosar({ sorok, vegosszeg, modositas, foglalt, elkuld, hiba, visszaigazolas, profil }) {
  const [atvetel, atvetelBeallitasa] = useState(false);
  const nevMezo = useRef(null);
  const [osszesites, osszesitesBeallitasa] = useState(null);
  const [osszesitesFoglalt, osszesitesFoglaltsaga] = useState(false);
  const [osszesitesHiba, osszesitesHibaBeallitasa] = useState('');
  const tartalom = JSON.stringify(sorok.map(({ azonosito, mennyiseg }) => ({ etelAzonosito: azonosito, mennyiseg })));
  const ervenyesOsszesites = osszesites?.tartalom === tartalom ? osszesites : null;

  async function osszesitesLekerese() {
    osszesitesFoglaltsaga(true);
    osszesitesHibaBeallitasa('');
    try {
      const valasz = await api('/rendelesek/osszesites', {
        modszer: 'POST', adatok: { tetelek: JSON.parse(tartalom) },
      });
      osszesitesBeallitasa({ ...valasz, tartalom });
      atvetelBeallitasa(true);
    } catch (problema) {
      osszesitesHibaBeallitasa(problema.message);
    } finally {
      osszesitesFoglaltsaga(false);
    }
  }

  useEffect(() => {
    if (atvetel) nevMezo.current?.focus();
  }, [atvetel]);

  return (
    <aside className="kosar" id="kosar" aria-labelledby="kosar-cim">
      <div className="kosar-fejlec">
        <h2 id="kosar-cim">Kosarad</h2>
        <span className="cimke">Elvitel</span>
      </div>
      {visszaigazolas && (
        <div className="visszaigazolas" role="status">
          <strong>Megkaptuk a rendelésed.</strong>
          <span>{forintban(visszaigazolas.vegosszeg)} · Fizetés átvételkor</span>
          <span>Követési kód</span>
          <code>{visszaigazolas.azonosito}</code>
          <a href="#kovetes">Rendelés követése</a>
        </div>
      )}
      {sorok.length ? (
        <>
          <ul className="kosar-sorok">
            {sorok.map(({ etel, azonosito, mennyiseg }) => (
              <li key={azonosito}>
                <div>
                  <strong>{etel?.nev || 'Már nem rendelhető tétel'}</strong>
                  <span>{etel ? mennyiseg + ' × ' + forintban(etel.ar) : 'Töröld a kosárból.'}</span>
                  {etel && <strong className="kosar-reszosszeg">{forintban(etel.ar * mennyiseg)}</strong>}
                </div>
                <div className="kosar-muveletek">
                {etel && <MennyisegVezerlo nev={etel.nev} adag={mennyiseg} foglalt={foglalt}
                  modositas={elteres => modositas(azonosito, elteres)} />}
                  <button type="button" className="egyszeru" disabled={foglalt}
                    aria-label={etel ? etel.nev + ' eltávolítása' : 'Elérhetetlen tétel eltávolítása'}
                    onClick={() => modositas(azonosito, -mennyiseg)}>Törlés</button>
                </div>
              </li>
            ))}
          </ul>
          <div className="vegosszeg"><span>Összesen</span><strong>{forintban(ervenyesOsszesites?.vegosszeg ?? vegosszeg)}</strong></div>
          {atvetel ? (
            <form className="atveteli-urlap" onSubmit={async esemeny => {
              esemeny.preventDefault();
              if (!ervenyesOsszesites) return;
              if (await elkuld(new FormData(esemeny.currentTarget), ervenyesOsszesites.vegosszeg)) {
                atvetelBeallitasa(false);
                osszesitesBeallitasa(null);
              }
              else osszesitesBeallitasa(null);
            }}>
              <fieldset disabled={foglalt}>
                <legend>Átvételi adatok</legend>
                <label>Név<input ref={nevMezo} name="vendegNeve" autoComplete="name" defaultValue={profil?.nev} required minLength={2} maxLength={80} /></label>
                <label>Telefonszám<input name="telefonszam" type="tel" autoComplete="tel" defaultValue={profil?.telefonszam} required minLength={7} maxLength={25} /></label>
                <label>Megjegyzés <span className="halvany">(opcionális)</span>
                  <textarea name="megjegyzes" rows={2} maxLength={300} /></label>
                {!ervenyesOsszesites && <button type="button" className="gomb masodlagos teljes"
                  disabled={osszesitesFoglalt || sorok.some(sor => !sor.etel)}
                  onClick={osszesitesLekerese}>Végösszeg ellenőrzése</button>}
                <button className="gomb elsodleges teljes" disabled={!ervenyesOsszesites || osszesitesFoglalt || sorok.some(sor => !sor.etel)}>
                  {foglalt ? 'Rendelés küldése…' : 'Rendelés leadása'}
                </button>
              </fieldset>
            </form>
          ) : (
            <button className="gomb elsodleges teljes" disabled={foglalt || osszesitesFoglalt || sorok.some(sor => !sor.etel)}
              onClick={osszesitesLekerese}>{osszesitesFoglalt ? 'Összesítés…' : 'Tovább az adatokhoz'}</button>
          )}
          <p className="kosar-megjegyzes">Személyes átvétel, fizetés a helyszínen.</p>
        </>
      ) : (
        <div className="ures-kosar"><strong>{visszaigazolas ? 'Új rendelés' : 'Még üres a kosarad'}</strong>
          <p>Válassz az étlapról, és add a kosaradhoz, amit megkóstolnál.</p></div>
      )}
      {osszesitesHiba && <p className="hiba" role="alert">{osszesitesHiba}</p>}
      {hiba && <p className="hiba" role="alert">{hiba}</p>}
    </aside>
  );
}

export function Rendeles() {
  const [etlap, etlapBeallitasa] = useState(uresEtlap);
  const [profil, profilBeallitasa] = useState(null);
  const [etlapHiba, etlapHibaBeallitasa] = useState('');
  const [betolt, betoltesBeallitasa] = useState(true);
  const [kategoria, kategoriaBeallitasa] = useState(0);
  const [kereses, keresesBeallitasa] = useState('');
  const [kosar, kosarBeallitasa] = useState(() => {
    const mentett = tarolt(kosarKulcs, []);
    if (!Array.isArray(mentett)) return [];
    return mentett.map(sor => ({
      // Az előző kiadás kosara angol mezőneveket használt.
      azonosito: sor?.azonosito ?? sor?.id,
      mennyiseg: sor?.mennyiseg ?? sor?.quantity,
    })).filter(sor => Number.isInteger(sor.azonosito) &&
      Number.isInteger(sor.mennyiseg) && sor.mennyiseg > 0 && sor.mennyiseg <= 20);
  });
  const [visszaigazolas, visszaigazolasBeallitasa] = useState(null);
  const [kovetett, kovetettBeallitasa] = useState(null);
  const [kod, kodBeallitasa] = useState(() => String(new URLSearchParams(location.search).get('rendeles') || tarolt(rendelesKulcs, '')));
  const [rendelesHiba, rendelesHibaBeallitasa] = useState('');
  const [foglalt, foglaltsagBeallitasa] = useState(false);
  const bekuldes = useRef(null);
  const folyamatban = useRef(false);

  async function etlapBetoltese() {
    betoltesBeallitasa(true);
    etlapHibaBeallitasa('');
    try { etlapBeallitasa(await api('/etlap')); }
    catch (hiba) { etlapHibaBeallitasa(hiba.message); }
    finally { betoltesBeallitasa(false); }
  }

  useEffect(() => {
    void etlapBetoltese();
    api('/profil').then(valasz => profilBeallitasa(valasz.profil)).catch(() => {});
  }, []);
  useEffect(() => { eltarol(kosarKulcs, kosar); }, [kosar]);

  const azonositoSzerint = new Map(etlap.etelek.map(etel => [etel.azonosito, etel]));
  const sorok = kosar.map(sor => ({ ...sor, etel: azonositoSzerint.get(sor.azonosito) }));
  const vegosszeg = sorok.reduce((osszeg, sor) => osszeg + (sor.etel?.ar ?? 0) * sor.mennyiseg, 0);
  const darab = kosar.reduce((osszeg, sor) => osszeg + sor.mennyiseg, 0);
  const szavak = keresheto(kereses.trim()).split(/\s+/).filter(Boolean);
  const talalatok = etlap.etelek.filter(etel => {
    const szoveg = keresheto(etel.nev + ' ' + etel.leiras);
    return (!kategoria || etel.kategoriaAzonosito === kategoria)
      && szavak.every(szo => szoveg.includes(szo));
  });

  function modositas(azonosito, elteres) {
    if (folyamatban.current) return;
    rendelesHibaBeallitasa('');
    kosarBeallitasa(jelenlegi => {
      const meglevo = jelenlegi.find(sor => sor.azonosito === azonosito);
      const mennyiseg = Math.min(20, Math.max(0, (meglevo?.mennyiseg ?? 0) + elteres));
      if (!mennyiseg) return jelenlegi.filter(sor => sor.azonosito !== azonosito);
      if (!meglevo) return jelenlegi.length < 30 ? [...jelenlegi, { azonosito, mennyiseg }] : jelenlegi;
      return jelenlegi.map(sor => sor.azonosito === azonosito ? { azonosito, mennyiseg } : sor);
    });
  }

  async function elkuld(urlap, vartVegosszeg) {
    if (folyamatban.current || !sorok.length || sorok.some(sor => !sor.etel)) return false;
    folyamatban.current = true;
    foglaltsagBeallitasa(true);
    rendelesHibaBeallitasa('');
    const adatok = {
      vendegNeve: urlap.get('vendegNeve'), telefonszam: urlap.get('telefonszam'), megjegyzes: urlap.get('megjegyzes'),
      tetelek: sorok.map(({ azonosito, mennyiseg }) => ({ etelAzonosito: azonosito, mennyiseg })),
    };
    const lenyomat = JSON.stringify(adatok);
    if (bekuldes.current?.lenyomat !== lenyomat)
      bekuldes.current = { lenyomat, azonosito: keresAzonosito() };
    try {
      const rendeles = await api('/rendelesek', { modszer: 'POST', adatok: { ...adatok, vartVegosszeg, keresAzonosito: bekuldes.current.azonosito } });
      visszaigazolasBeallitasa(rendeles);
      kovetettBeallitasa(rendeles);
      kodBeallitasa(rendeles.azonosito);
      eltarol(rendelesKulcs, rendeles.azonosito);
      kosarBeallitasa([]);
      bekuldes.current = null;
      return true;
    } catch (hiba) {
      rendelesHibaBeallitasa(hiba.message);
      void etlapBetoltese();
      return false;
    } finally {
      folyamatban.current = false;
      foglaltsagBeallitasa(false);
    }
  }

  return (
    <main className="keret vendegfelulet">
      <section className="nyitokep">
        <div className="nyitoszoveg">
          <span className="felirat">PARÁZS BISZTRÓ</span>
          <h1>Frissen a<br />kemencéből<span>.</span></h1>
          <p>Kemencés pizzák, bisztrófogások és házi desszertek. Rendelj előre, és vedd át személyesen.</p>
          <a className="gomb elsodleges" href="#etlap">Megnézem az étlapot</a>
        </div>
        <img src="/pizza.jpg" alt="Kemencében sült pizza csípős szalámival és friss bazsalikommal"
          width="1536" height="1024" fetchPriority="high" />
      </section>

      <div className="rendelesi-felulet">
        <section id="etlap" className="etlap" aria-labelledby="etlap-cim">
          <div className="etlap-fejlec">
            <div><span className="felirat">RENDELÉS ELVITELRE</span><h2 id="etlap-cim">Étlap</h2></div>
            <a className="gomb masodlagos kosar-ugras" href="#kosar">Kosár ({darab})</a>
          </div>
          <label className="etlap-kereso">Keresés az étlapon
            <input type="search" value={kereses} onChange={esemeny => keresesBeallitasa(esemeny.target.value)}
              placeholder="Például pizza vagy bazsalikom" maxLength={100} />
          </label>
          <div className="szurok" aria-label="Ételkategóriák">
            {[{ azonosito: 0, nev: 'Minden' }, ...etlap.kategoriak].map(tetel => (
              <button key={tetel.azonosito} type="button" aria-pressed={kategoria === tetel.azonosito}
                onClick={() => kategoriaBeallitasa(tetel.azonosito)}>{tetel.nev}</button>
            ))}
          </div>
          {betolt && <p role="status">Az étlap betöltése…</p>}
          {etlapHiba && <p className="hiba" role="alert">{etlapHiba} <button className="egyszeru" onClick={etlapBetoltese}>Újrapróbálom</button></p>}
          {!betolt && !etlapHiba && (
            <div className="etel-racs">
              {talalatok.map(etel => {
                const mennyiseg = kosar.find(sor => sor.azonosito === etel.azonosito)?.mennyiseg ?? 0;
                return (
                  <article className="etel" key={etel.azonosito}>
                    <span className="etel-kategoria">{etlap.kategoriak.find(tetel => tetel.azonosito === etel.kategoriaAzonosito)?.nev}</span>
                    <h3>{etel.nev}</h3>
                    <p>{etel.leiras}</p>
                    <div className="etel-muvelet">
                      <strong>{forintban(etel.ar)}</strong>
                      {mennyiseg ? <MennyisegVezerlo nev={etel.nev} adag={mennyiseg} foglalt={foglalt}
                        modositas={elteres => modositas(etel.azonosito, elteres)} />
                        : <button className="hozzaadas" type="button" disabled={foglalt || kosar.length >= 30}
                          aria-label={etel.nev + ' kosárba'} onClick={() => modositas(etel.azonosito, 1)}>
                          <span aria-hidden="true">+</span> Kosárba
                        </button>}
                    </div>
                  </article>
                );
              })}
              {!talalatok.length && <p role="status">Nincs a szűrésnek megfelelő rendelhető étel.</p>}
            </div>
          )}
        </section>
        <Kosar sorok={sorok} vegosszeg={vegosszeg} modositas={modositas} foglalt={foglalt || betolt}
          elkuld={elkuld} hiba={rendelesHiba} visszaigazolas={visszaigazolas} profil={profil} />
      </div>
      <Kovetes rendeles={kovetett} rendelesBeallitasa={kovetettBeallitasa} kod={kod} kodBeallitasa={kodBeallitasa} />
    </main>
  );
}

