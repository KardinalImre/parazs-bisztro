import { createRoot } from 'react-dom/client';
import { Rendeles } from './rendeles.jsx';
import { Profil } from './profil.jsx';
import { Szemelyzet } from './szemelyzet.jsx';
import './stilus.css';

function Alkalmazas() {
  const utvonal = location.pathname.replace(/\/$/, '');
  const szemelyzet = utvonal === '/szemelyzet';
  const profil = utvonal === '/profil';
  return (
    <>
      <header className="fejlec">
        <div className="keret fejlec-belso">
          <a className="marka" href="/" aria-label="Parázs Bisztró kezdőlap">
            <span className="markajel" aria-hidden="true">✦</span>
            <span>PARÁZS<small>BISZTRÓ</small></span>
          </a>
          <nav aria-label="Fő navigáció">
            {szemelyzet || profil ? <a href="/">Étlap</a> : <>
              <a href="#etlap">Étlap</a>
              <a href="#kovetes">Rendelés követése</a>
              <a href="/profil">Profil</a>
            </>}
          </nav>
        </div>
      </header>
      {szemelyzet ? <Szemelyzet /> : profil ? <Profil /> : <Rendeles />}
      <footer className="keret lablec">
        <span>Parázs Bisztró · Vizsgaprojekt</span>
        {!szemelyzet && <a href="/szemelyzet">Személyzeti felület</a>}
      </footer>
    </>
  );
}

createRoot(document.getElementById('root')).render(<Alkalmazas />);

