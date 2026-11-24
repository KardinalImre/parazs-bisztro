# Parázs Bisztró — teszteredmények

**Futtatás:** 2026. november 24., Windows 10 x64, .NET SDK 10.0.401, Node.js 24.18.1, Playwright 1.65.1 és Microsoft Edge.

**Összesítés:** 14 API-integrációs, 4 böngészős és 1 WPF API-kliens teszt sikeres.

## API-integráció

A [tesztkód](../kliens/tesztek/api.test.mjs) önálló ASP.NET Core-tesztszervert és ideiglenes SQLite-adatbázist indít. A tesztkérések a tényleges REST API-ra érkeznek.

- **API-01–03 · Étlap, rendelés és validáció.** Ellenőrzött a tíz mintaétel, a szerveroldali ár, a nyilvános rendelésválasz adatszűrése, valamint a hibás tétel, telefonszám, kosár és JSON elutasítása.
- **API-04–07 · Ismétlés, jogosultság és megőrzés.** Öt párhuzamos beküldésből egy rendelés jött létre. Kulcs nélkül a személyzeti kérések elutasításra kerültek. Működött az állapotátmenet, az étel elrejtése és az újraindítás utáni megőrzés.
- **API-08–09 · Korábbi adatok és ár.** A korábbi adatbázis átvétele megőrizte a tételeket és az eredeti követési kódot. Eltérő, előzetesen jóváhagyott végösszegre 409 érkezett.
- **API-10–11 · Profil és előzmények.** Regisztráció, belépés, adatmódosítás és kilépés működött. Másik profilból a saját rendelés nem jelent meg. Más profil azonos kérésazonosítóval sem kapta meg a rendelést. A lejárt vagy megváltozott profilhoz tartozó rendelési kérés 401-et adott.
- **API-12–13 · Kupon és tárca.** A százalékos kupon a szerveren számolódott, az érvénytelen és a minimum alatti kódot a szerver elutasította. A tárcafeltöltés és rendelés ismétlése egyszeri egyenlegváltozást eredményezett. Elégtelen egyenlegnél a rendelés nem maradt az adatbázisban. Törléskor egyszeri jóváírás történt.
- **API-14 · Ügyfélszolgálat.** A jegy beküldhető, megválaszolható és lezárható. Vendégoldalon az e-mail-cím és a beküldött üzenet nem jelenik meg. A válasz az ügyazonosítóval lekérhető.

## Böngészős forgatókönyvek

A [Playwright-tesztek](../kliens/tesztek/bongeszo.test.mjs) a lefordított React-klienst és a C# szervert használják.

- **UI-01 · Asztali rendelés.** Rendelés leadása, személyzeti állapotváltás és vendégoldali követés.
- **UI-02 · Mobil rendelés.** Ékezetfüggetlen kereső, kategóriaszűrés, régi kosár beolvasása, mennyiségmódosítás és rendelés 390 képpontos szélességen.
- **UI-03 · Profil, tárca és kupon.** Regisztráció, adatmentés, próbafeltöltés, előtöltött átvételi űrlap, kedvezmény és fizetés, valamint korábbi rendelés megjelenítése.
- **UI-04 · Ügyfélszolgálati üzenet.** Jegy beküldése mobilméretben, személyzeti válasz és vendégoldali válaszlekérés.

## WPF és telepítő

- **WPF-01 · Kliens–API integráció.** A [teszt](../kliens/tesztek/wpf-api.test.mjs) a WPF által használt C# HTTP-klienst futtatja. A valódi tesztszerveren ellenőrizte a listákat, a rendelés állapotát, az étel elrejtését, a jegy megválaszolását és lezárását, valamint a hibás kulcsot és a címellenőrzést.
- **WPF és MSI összeállítása.** A WPF-kliens lefordult, a WiX elkészítette a Windows x64 MSI-t. Az MSI adminisztratív kibontása sikerült, a kibontott futtatható állomány lenyomata egyezett a becsomagolt példányéval. A kibontott alkalmazás magyar belépőablaka megnyílt. Az ellenőrzés ideiglenes mappában, adminisztratív kibontással történt.
- **Telepítői megjegyzés.** A WiX ICE91 jelzése a felhasználónkénti telepítési mappára vonatkozik. A csomag felhasználónkénti telepítésre van beállítva és digitálisan aláíratlan.

## SQL-export

A tiszta SQLite-adatbázisba betöltött export 11 táblát, 4 kategóriát, 10 ételt és 2 kupont eredményezett. Az integritás- és idegenkulcs-ellenőrzés hibamentes volt.

## Újrafuttatás

A projekt gyökérkönyvtárából:

```sh
npm --prefix kliens ci
npm --prefix kliens run epites
dotnet build szerver/Parazs.csproj
npm --prefix kliens run teszt:api
npm --prefix kliens run teszt:wpf
cd kliens
npx playwright install chromium
npm run teszt:bongeszo
```

Telepített Edge választásához a `PARAZS_BONGESZO` változó értéke `msedge`. A mobilforgatókönyv böngészős emulációval fut.

