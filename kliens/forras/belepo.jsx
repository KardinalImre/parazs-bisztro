import { createRoot } from 'react-dom/client';
import { Rendeles } from './rendeles.jsx';
import { Profil } from './profil.jsx';
import { Szemelyzet } from './szemelyzet.jsx';
import { Ugyfelszolgalat } from './ugyfelszolgalat.jsx';
import './stilus.css';

function Alkalmazas() {
  const utvonal = location.pathname.replace(/\/$/, '');
  const szemelyzet = utvonal === '/szemelyzet';
  const profil = utvonal === '/profil';
  const ugyfelszolgalat = utvonal === '/ugyfelszolgalat';
  return (
    <>
      <header className="fejlec">
        <div className="keret fejlec-belso">
          <a className="marka" href="/" aria-label="Parázs Bisztró kezdőlap">
            <span className="markajel" aria-hidden="true">✦</span>
            <span>PARÁZS<small>BISZTRÓ</small></span>
          </a>
          <nav aria-label="Fő navigáció">
            {szemelyzet || profil || ugyfelszolgalat ? <a href="/">Étlap</a> : <>
              <a href="#etlap">Étlap</a>
              <a href="#kovetes">Rendelés követése</a>
              <a href="/profil">Profil</a>
            </>}
          </nav>
        </div>
      </header>
      {szemelyzet ? <Szemelyzet /> : profil ? <Profil /> : ugyfelszolgalat ? <Ugyfelszolgalat /> : <Rendeles />}
      <footer className="keret lablec">
        <span>Parázs Bisztró · Vizsgaprojekt</span>
        {!ugyfelszolgalat && <a href="/ugyfelszolgalat">Ügyfélszolgálat</a>}
        {!szemelyzet && <a href="/szemelyzet">Személyzeti felület</a>}
      </footer>
    </>
  );
}

createRoot(document.getElementById('root')).render(<Alkalmazas />);

