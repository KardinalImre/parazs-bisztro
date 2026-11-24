# Parázs Bisztró — műszaki és felhasználói dokumentáció

## Cél

A rendszer az éttermi elviteles előrendelést és a rendelésekkel kapcsolatos kommunikációt kezeli. A fogások és az elérhetőségük adatbázisban vannak. A személyzet a saját felületéről vezeti végig a rendelést az elkészítéstől az átadásig, és válaszolhat a vendégeknek.

## Vendégoldal

1. Az étlapon név és leírás szerint lehet keresni, ékezetek nélkül is. A találatok kategória szerint szűrhetők.
2. A kosár megmutatja a fogások darabszámát, egységárát és részösszegét. Egy fogásból legfeljebb 20 adag, rendelésenként legfeljebb 30 különböző fogás választható. Minden sor külön törölhető.
3. Az átvételi űrlap megnyitásakor az API az aktuális árak és elérhetőség alapján ad vissza végösszeget. A megváltozott kosarat vagy kuponkódot újból ellenőrizni kell. Leadáskor a szerver véglegesíti az összeget.
4. A bejelentkezett vendég a névvel és telefonszámmal előtöltött űrlapot kapja. A rendelés a saját profiljában is megjelenik. A vendégrendelések követési kóddal ellenőrizhetők.
5. A vendég fizethet átvételkor, vagy bejelentkezve használhatja a demópénztárcát. A próbaegyenleg 1 000, 5 000 vagy 10 000 Ft-tal tölthető fel. A jóváírás és a fizetés a helyi demó adatbázisában történik.
6. Az ügyfélszolgálati oldalon e-mail-címmel és opcionálisan rendelési kóddal lehet üzenetet beküldeni. A kapott ügyazonosítóval ugyanitt olvasható a válasz.

A böngésző a kosár tételeinek azonosítóit és darabszámát, az utolsó rendelési kódot és az utolsó ügyazonosítót őrzi meg. A profil hét napra szóló HttpOnly, SameSite=Strict sütit kap. A szerver a munkamenet-token lenyomatát tárolja, a jelszavakat az ASP.NET Core Identity `PasswordHasher` osztályával hasheli.

## Személyzeti munka

A böngészős nézetet a `/szemelyzet` címen, a natív klienst a Start menü **Parázs Bisztró – Személyzet** parancsával lehet megnyitni. Mindkettő a szerver indításakor megjelenő személyzeti kulccsal használható.

- **Rendelések:** a legutóbbi 100 rendelés, vendégadatok, fogások, fizetési mód és végösszeg. A sorrend **Beérkezett → Készül → Átvehető → Átadva**. Átadás előtt a rendelés törölhető, ami demópénztárcás fizetésnél egyszeri jóváírást eredményez.
- **Étel-elérhetőség:** az elfogyott étel elrejthető; visszakapcsolás után megint rendelhető.
- **Ügyfélszolgálat:** a legutóbbi 100 jegy, az aktív ügyekkel elöl. Megválaszolható és lezárható.
- **Frissítés:** a kliens lekéri az új rendeléseket, a jegyeket és az étlapot. A WPF-kliens mentés után is frissíti a listákat.

## Komponensek és műszaki feltételek

- **React 19 és Vite** a magyar, reszponzív vendég- és webes személyzeti nézethez. A `kliens/forras/api.js` közös JSON-klienst ad.
- **ASP.NET Core 10 és C#** a REST-erőforrásokhoz, validációhoz, árkalkulációhoz és jogosultságokhoz. A saját C#-metódusok angol nevűek; a megjelenített szövegek, a REST-címek és a JSON-mezők magyarok.
- **SQLite** helyi fájlalapú adattár, paraméterezett SQL-parancsokkal és engedélyezett idegen kulcsokkal. A rendelés fejléce, tételei, a kupon kedvezménye és a tárcaterhelés egy tranzakcióban mentődik.
- **WPF, .NET 10 és WiX** a Windows x64 személyzeti alkalmazáshoz és MSI-telepítőhöz. A WPF JSON-on keresztül éri el a szervert. Helyi HTTP vagy HTTPS alapcímet fogad el, a kulcsot a kliens memóriájában tartja.
- Szerverindításhoz .NET 10 SDK, Node.js 22.12+ vagy 24.x, npm és korszerű böngésző kell. A [README](../README.md) tartalmazza a parancsokat.

A `PARAZS_ADATBAZIS` másik SQLite-fájlra mutathat. A `PARAZS_SZEMELYZETI_KULCS` 16–128 szóköz nélküli, látható ASCII karakterből állhat. Enélkül a szerver friss kulcsot generál. A szerver alapértelmezésben a `127.0.0.1:5080` címen érhető el. Telefonos vizsgabemutatóhoz megbízható helyi hálózaton a `-- --urls http://0.0.0.0:5080` kapcsolóval indítható. A telefonról a számítógép helyi IP-címét kell megnyitni.

A csomag fiktív adatokkal történő vizsgabemutatóra készült. Éles használathoz HTTPS, személyenkénti személyzeti jogosultság, adatmegőrzési és mentési eljárás szükséges.

## REST-végpontok

- **GET `/api/etlap`** — rendelhető ételek és kategóriák.
- **POST `/api/rendelesek/osszesites`** — tételek és opcionális kupon alapján részösszeg, kedvezmény és végösszeg.
- **POST `/api/rendelesek`** — rendelés mentése. Új kérés 201, azonos tartalom újraküldése 200. Eltérő végösszegre vagy eltérő tartalmú ismétlésre 409, hibás kuponra vagy elégtelen tárcaegyenlegre 422 érkezik.
- **GET `/api/rendelesek/{azonosito}`** — állapot, tételek és fizetés személyes adatok nélkül.
- **GET, PATCH `/api/profil`** — aktuális profil és a név, telefonszám módosítása.
- **POST `/api/profil/regisztracio`**, **POST `/api/profil/belepes`**, **DELETE `/api/profil/munkamenet`** — fiók és munkamenet.
- **GET `/api/profil/rendelesek`** — a bejelentkezett vendég saját rendelései.
- **GET `/api/profil/tarca`**, **POST `/api/profil/tarca/feltoltesek`** — próbaegyenleg, tranzakciók és feltöltés.
- **POST `/api/ugyfelszolgalat/jegyek`**, **GET `/api/ugyfelszolgalat/jegyek/{azonosito}`** — jegyküldés és követés.
- **GET `/api/szemelyzet/rendelesek`**, **PATCH `/api/szemelyzet/rendelesek/{azonosito}`** — a rendelések megtekintése és állapotuk módosítása.
- **GET `/api/szemelyzet/etlap`**, **PATCH `/api/szemelyzet/etelek/{azonosito}`** — teljes étlap és elérhetőség.
- **GET `/api/szemelyzet/jegyek`**, **PATCH `/api/szemelyzet/jegyek/{azonosito}`** — jegyek, válasz és lezárás.

A személyzeti erőforrások `Authorization: Bearer <személyzeti kulcs>` fejlécet kérnek. Az azonosításhoz szükséges profilvégpontok bejelentkezés nélkül 401-et adnak. Az ügyazonosító és a rendeléskövetési kód véletlenszerű UUID.

Példa a rendelési kérésre:

```json
{
  "keresAzonosito": "c0a801d5-481d-4dd8-b026-9b377cd2ff3d",
  "vendegNeve": "Minta Vendég",
  "telefonszam": "+36 30 555 0100",
  "tetelek": [{ "etelAzonosito": 1, "mennyiseg": 1 }],
  "kuponkod": "PARAZS10",
  "vartVegosszeg": 3861,
  "fizetesiMod": "atvetel"
}
```

## Adatbázis

Az [adatbázisdiagram](ADATBAZIS.svg) bemutatja az étlap, a rendelések, a profilok, a munkamenetek, a kuponok, a pénztárcanapló és az ügyfélszolgálat tizenegy tábláját. A [visszatölthető SQL-export](../adatbazis/adatbazis-export.sql) sémát, tíz mintaételt és két mintakupont tartalmaz.

```text
sqlite3 parazs.sqlite3
sqlite> .read adatbazis/adatbazis-export.sql
sqlite> .tables
sqlite> .quit
```

Az alkalmazás a beépített SQLite-adatszolgáltatójával automatikusan futtatja ugyanezt az exportot. Régi SQLite-fájl használatakor add meg a fájl másolatát a `PARAZS_ADATBAZIS` változóban. A korai, angol táblanevű sémát a szerver tranzakcióban frissíti. A korábbi rendelések és követési kódjaik megmaradnak. A profil előzményei a bejelentkezve leadott rendelésekből épülnek fel.

## Projektadatok a leadáshoz

- Intézmény és osztály — [kitöltendő]
- A 2–3 fős csapat tagjai — [kitöltendő]
- Tényleges munkamegosztás és közös ellenőrzés — [kitöltendő]
- Leadás dátuma — [kitöltendő]

A nyitóoldal ételfotója a projekthez készült generált illusztráció.

## Technológiai források

- [ASP.NET Core Minimal API](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis)
- [Microsoft.Data.Sqlite](https://learn.microsoft.com/dotnet/standard/data/sqlite/)
- [Vite útmutató](https://vite.dev/guide/)
- [.NET Desktop SDK és WPF](https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props-desktop)
- [WiX Toolset](https://docs.firegiant.com/wix/using-wix/)

