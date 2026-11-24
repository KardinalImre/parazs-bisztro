import { createRoot } from 'react-dom/client';
import { Rendeles } from './rendeles.jsx';
import { Szemelyzet } from './szemelyzet.jsx';
import './stilus.css';

function Alkalmazas() {
  const szemelyzet = location.pathname.replace(/\/$/, '') === '/szemelyzet';
  return (
    <>
      <header className="fejlec">
        <div className="keret fejlec-belso">
          <a className="marka" href="/" aria-label="Parázs Bisztró kezdőlap">
            <span className="markajel" aria-hidden="true">✦</span>
            <span>PARÁZS<small>BISZTRÓ</small></span>
          </a>
          <nav aria-label="Fő navigáció">
            {szemelyzet ? <a href="/">Étlap</a> : <>
              <a href="#etlap">Étlap</a>
              <a href="#kovetes">Rendelés követése</a>
            </>}
          </nav>
        </div>
      </header>
      {szemelyzet ? <Szemelyzet /> : <Rendeles />}
      <footer className="keret lablec">
        <span>Parázs Bisztró · Vizsgaprojekt</span>
        {!szemelyzet && <a href="/szemelyzet">Személyzeti felület</a>}
      </footer>
    </>
  );
}

createRoot(document.getElementById('root')).render(<Alkalmazas />);

