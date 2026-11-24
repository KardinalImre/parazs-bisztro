# Parázs Bisztró

A Parázs egy magyar nyelvű éttermi rendelőrendszer személyes átvételhez. A vendég az adatbázisból betöltött étlapról választ, összeállítja a kosarát és leadja a rendelést. A személyzet jelzi az elkészítés és az átadás állapotát, valamint átmenetileg elrejtheti az elfogyott fogásokat.

## Indítás

A futtatáshoz **.NET 10 SDK** és **Node.js 22.12+ vagy 24.x** szükséges. Első indításkor internetkapcsolat kell az npm- és NuGet-csomagok letöltéséhez.

Windowson a kibontott mappában nyisd meg az **Inditas.cmd** fájlt, vagy futtasd a következő parancsokat a projekt gyökeréből:

```sh
npm --prefix kliens ci
npm --prefix kliens run epites
dotnet run --project szerver/Parazs.csproj --no-launch-profile
```

- Étlap és rendelés — <http://127.0.0.1:5080>
- Személyzeti felület — <http://127.0.0.1:5080/szemelyzet>

A terminálban megjelenik az induláskor generált személyzeti kulcs. Az adatbázist az alkalmazás automatikusan létrehozza a `szerver/adatok/` könyvtárban, és tíz fogással tölti fel. A szolgáltatás a terminálban a **Ctrl+C** billentyűkkel állítható le.

Frissítéskor a korábbi SQLite-adatbázis átvételét a műszaki dokumentáció ismerteti.

## Beadandó fájlok

- `kliens/` — React-kliens és automatizált tesztek.
- `szerver/` — C# REST API és SQLite adatkezelés.
- [Adatbázismodell-diagram](dokumentacio/ADATBAZIS.svg)
- [SQL-export](adatbazis/adatbazis-export.sql)
- [Műszaki és felhasználói dokumentáció](dokumentacio/DOKUMENTACIO.md)
- [Teszteredmények és futtatás](dokumentacio/TESZTEREDMENYEK.md)

Bemutatóhoz használj fiktív nevet és telefonszámot. A csapat adatai és a tényleges munkamegosztás a műszaki dokumentáció végén tölthetők ki.

