# Parázs Bisztró — teszteredmények

**Futtatás dátuma:** 2026. november 24.  
**Környezet:** Windows 10 x64, .NET SDK 10.0.401, Node.js 24.18.1, Microsoft Edge 158.0.4370.33, Playwright 1.65.1.  
**Eredmény:** 8 API-integrációs teszt és 2 böngészős végponttól végpontig tartó teszt sikeres. Sikertelen vagy kihagyott teszt 0.

## API-integrációs tesztek

A csomagolt [API-tesztek](../kliens/tesztek/api.test.mjs) külön ASP.NET Core-szervert indítanak, ideiglenes SQLite-adatbázist használnak, és HTTP-n ellenőrzik a válaszokat.

- **API-01 · Étlap — sikeres.** Négy kategória, tíz étel, ellenőrzött mintaár és 200-as válasz.
- **API-02 · Rendelés és követés — sikeres.** Az új rendelés 201-et és követési URL-t ad. A szerver 9 870 Ft-ot számít a két pizzából és egy limonádéból. A nyilvános válasz személyes adat nélküli rendelési összesítést ad; ismeretlen kódra 404 érkezik.
- **API-03 · Validáció — sikeres.** Hibás név, telefonszám, azonosító, mennyiség, hiányos tétel, ismételt tétel, üres kosár és hibás JSON esetén 400 érkezik.
- **API-04 · Ismételt kérés — sikeres.** Öt egyidejű, azonos kérés egy rendelési azonosítót eredményez. Az egyik válasz 201, a másik négy 200. Azonos kérésazonosító módosított tartalommal 409-et ad.
- **API-05 · Jogosultság és állapot — sikeres.** A védett erőforrások kulcs nélkül 401-et adnak. Az engedélyezett sorrend végigjárható; átugrott és végállapotból indított átmenetre 409 érkezik.
- **API-06 · Elérhetőség és tranzakció — sikeres.** Az elrejtett étel eltűnik az étlapról. A vele leadott rendelés 409-et ad, a mentett rendelések darabszáma változatlan marad. Visszakapcsolás után az étel ismét rendelhető.
- **API-07 · Tartós mentés — sikeres.** Szerver-újraindítás után a korábbi rendelés és a módosított elérhetőség megmarad.

- **API-08 · Átállás a korábbi sémáról — sikeres.** A régi rendelés az eredeti követési kóddal, egységárral és tételekkel lekérhető. Az elrejtett étel állapota megmarad, az azonos tartalmú rendelési kérésre 200 érkezik, és az adatok újraindítás után is elérhetők.

## Böngészős tesztek

A [Playwright-forgatókönyvek](../kliens/tesztek/bongeszo.test.mjs) a lefordított React-klienssel és a C# szerverrel futnak.

- **UI-01 · Asztali rendelés — sikeres.** 1440 × 1000 képponton betöltött az étlap és a fotó, a vendég leadta a rendelést, a személyzet átvehetőre állította, a vendég pedig újratöltés után lekérte az új állapotot. A régi személyzeti cím átirányítása is működött.
- **UI-02 · Mobil rendelés — sikeres.** 390 × 844 képpontos érintéses emulációban működött a kategóriaszűrés, a korábbi változatban mentett kosár beolvasása, a mennyiségmódosítás és a beküldés. Két tiramisu végösszege 3 580 Ft. A felület a megadott szélességen vízszintes görgetés nélkül használható.

## SQL-export és fordítás

- **SQL-visszaállítás — sikeres.** Az export üres SQLite 3.50.4 adatbázisba betölthető volt. Eredmény 4 kategória, 10 étel, 0 rendelés és 0 rendelési tétel. A `PRAGMA integrity_check` eredménye `ok`, a `PRAGMA foreign_key_check` üres eredményt adott.
- **C# fordítás — sikeres.** 0 hiba és 0 figyelmeztetés.
- **React-kliens összeállítása — sikeres.** A Vite a statikus állományokat a szerver `wwwroot` könyvtárába írta.

## Ismételt futtatás

A projekt gyökérkönyvtárában futtasd:

```sh
npm --prefix kliens ci
npm --prefix kliens run epites
dotnet build szerver/Parazs.csproj
npm --prefix kliens run teszt:api
```

A böngészős teszthez egyszer telepítsd a Playwright Chromiumot, majd indítsd a tesztet:

```sh
cd kliens
npx playwright install chromium
npm run teszt:bongeszo
```

Telepített Edge használatához PowerShellből állítsd a `PARAZS_BONGESZO` változót `msedge` értékre, majd futtasd az `npm run teszt:bongeszo` parancsot. A mobilteszt emulált nézetet használ. A tesztek a helyi bemutató működését vizsgálják.

