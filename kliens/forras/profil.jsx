import { useEffect, useState } from 'react';
import { api, allapotFelirat, forintban } from './api.js';
import { Tarca } from './tarca.jsx';

function Elozmenyek({ azonosito }) {
  const [rendelesek, rendelesekBeallitasa] = useState(null);
  const [hiba, hibaBeallitasa] = useState('');
  useEffect(() => {
    let aktiv = true;
    api('/profil/rendelesek').then(valasz => { if (aktiv) rendelesekBeallitasa(valasz); })
      .catch(problema => { if (aktiv) hibaBeallitasa(problema.message); });
    return () => { aktiv = false; };
  }, [azonosito]);
  return (
    <section className="profil-elozmenyek">
      <h2>Korábbi rendeléseim</h2>
      {hiba && <p className="hiba" role="alert">{hiba}</p>}
      {!hiba && rendelesek === null && <p role="status">Betöltés…</p>}
      {rendelesek?.length === 0 && <p>Még nincs ehhez a profilhoz tartozó rendelés.</p>}
      {rendelesek?.length > 0 && <div className="szemelyzeti-rendelesek">
        {rendelesek.map(rendeles => <article key={rendeles.azonosito} className="szemelyzeti-rendeles">
          <div className="kosar-fejlec">
            <strong>{new Date(rendeles.letrehozva).toLocaleString('hu-HU')}</strong>
            <span className={'allapot ' + rendeles.allapot}>{allapotFelirat[rendeles.allapot]}</span>
          </div>
          <ul>{rendeles.tetelek.map(tetel => <li key={tetel.etelAzonosito}>{tetel.mennyiseg} × {tetel.etelNeve}</li>)}</ul>
          <p><strong>{forintban(rendeles.vegosszeg)}</strong></p>
          <a href={'/?rendeles=' + rendeles.azonosito + '#kovetes'}>Rendelés követése</a>
        </article>)}
      </div>}
    </section>
  );
}

export function Profil() {
  const [profil, profilBeallitasa] = useState(null);
  const [regisztracio, regisztracioBeallitasa] = useState(false);
  const [foglalt, foglaltsagBeallitasa] = useState(true);
  const [hiba, hibaBeallitasa] = useState('');
  const [uzenet, uzenetBeallitasa] = useState('');

  useEffect(() => {
    api('/profil').then(valasz => profilBeallitasa(valasz.profil))
      .catch(problema => hibaBeallitasa(problema.message))
      .finally(() => foglaltsagBeallitasa(false));
  }, []);

  async function muvelet(feladat) {
    hibaBeallitasa('');
    uzenetBeallitasa('');
    foglaltsagBeallitasa(true);
    try {
      await feladat();
    } catch (problema) {
      hibaBeallitasa(problema.message);
    } finally {
      foglaltsagBeallitasa(false);
    }
  }

  function belepes(esemeny) {
    esemeny.preventDefault();
    const adatok = Object.fromEntries(new FormData(esemeny.currentTarget));
    const utvonal = regisztracio ? '/profil/regisztracio' : '/profil/belepes';
    void muvelet(async () => profilBeallitasa(await api(utvonal, { modszer: 'POST', adatok })));
  }

  function mentes(esemeny) {
    esemeny.preventDefault();
    const adatok = Object.fromEntries(new FormData(esemeny.currentTarget));
    void muvelet(async () => {
      profilBeallitasa(await api('/profil', { modszer: 'PATCH', adatok }));
      uzenetBeallitasa('A profiladatokat mentettük.');
    });
  }

  function kilepes() {
    void muvelet(async () => {
      await api('/profil/munkamenet', { modszer: 'DELETE' });
      profilBeallitasa(null);
      regisztracioBeallitasa(false);
    });
  }

  return (
    <main className="keret szemelyzet">
      <div className="etlap-fejlec">
        <div><span className="felirat">PARÁZS BISZTRÓ</span><h1>Vendégprofil</h1></div>
        {profil && <button className="gomb masodlagos" disabled={foglalt} onClick={kilepes}>Kilépés</button>}
      </div>
      {hiba && <p className="hiba" role="alert">{hiba}</p>}
      {uzenet && <p className="visszaigazolas" role="status">{uzenet}</p>}
      {profil ? (<>

        <form className="szemelyzeti-belepes" onSubmit={mentes}>
          <h2>Profiladatok</h2>
          <p className="halvany">{profil.email}</p>
          <label>Név<input name="nev" autoComplete="name" defaultValue={profil.nev} required minLength={2} maxLength={80} /></label>
          <label>Telefonszám<input name="telefonszam" type="tel" autoComplete="tel" defaultValue={profil.telefonszam} maxLength={25} /></label>
          <button className="gomb elsodleges" disabled={foglalt}>Adatok mentése</button>
        </form>
        <Tarca key={'tarca-' + profil.azonosito} />
        <Elozmenyek key={profil.azonosito} azonosito={profil.azonosito} />
      </>) : (
        <form className="szemelyzeti-belepes" onSubmit={belepes}>
          <h2>{regisztracio ? 'Profil létrehozása' : 'Bejelentkezés'}</h2>
          {regisztracio && <label>Név<input name="nev" autoComplete="name" required minLength={2} maxLength={80} /></label>}
          <label>E-mail-cím<input name="email" type="email" autoComplete="email" maxLength={254} required /></label>
          <label>Jelszó<input name="jelszo" type="password" autoComplete={regisztracio ? 'new-password' : 'current-password'}
            required minLength={10} maxLength={128} /></label>
          <button className="gomb elsodleges" disabled={foglalt}>{regisztracio ? 'Regisztráció' : 'Bejelentkezés'}</button>
          <button className="gomb masodlagos" type="button" disabled={foglalt}
            onClick={() => { regisztracioBeallitasa(!regisztracio); hibaBeallitasa(''); }}>
            {regisztracio ? 'Már van profilom' : 'Új profilt kérek'}
          </button>
        </form>
      )}
    </main>
  );
}

