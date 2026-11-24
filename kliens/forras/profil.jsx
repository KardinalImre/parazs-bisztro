import { useEffect, useState } from 'react';
import { api } from './api.js';

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
      {profil ? (
        <form className="szemelyzeti-belepes" onSubmit={mentes}>
          <h2>Profiladatok</h2>
          <p className="halvany">{profil.email}</p>
          <label>Név<input name="nev" autoComplete="name" defaultValue={profil.nev} required minLength={2} maxLength={80} /></label>
          <label>Telefonszám<input name="telefonszam" type="tel" autoComplete="tel" defaultValue={profil.telefonszam} maxLength={25} /></label>
          <button className="gomb elsodleges" disabled={foglalt}>Adatok mentése</button>
        </form>
      ) : (
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

