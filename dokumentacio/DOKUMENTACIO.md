# Parázs Bisztró — műszaki és felhasználói dokumentáció

## Cél és használati kör

Az előrendelők telefonos diktálás nélkül állíthatják össze az elviteles rendelésüket. A személyzet egy helyen látja a tételeket, az átvételi adatokat és a rendelés állapotát. A vendég a visszaigazolásban kapott kóddal ellenőrizheti, hogy elkészült-e az étel. Az átvétel és a fizetés az étteremben történik.

A felület magyar nyelvű. Mindkét nézet reszponzív webalkalmazás, így ugyanaz a webes kliens használható telefonon és asztali böngészőben.

## A vendég folyamata

1. Az **Étlap** szakaszban válassz kategóriát, majd a **Kosárba** gombbal adj hozzá ételeket.
2. A plusz és mínusz gombokkal állítsd a mennyiséget. Egy ételből legfeljebb 20 adag rendelhető, egy kosárban legfeljebb 30 különböző étel lehet. Mobilon a **Kosár** hivatkozás közvetlenül a kosárhoz ugrik.
3. Válaszd a **Tovább az adatokhoz** gombot. Adj meg egy nevet, egy telefonszámot és igény esetén megjegyzést.
4. A **Rendelés leadása** után a visszaigazolás mutatja a végösszeget és a követési kódot. Érdemes megőrizni a kódot.
5. A **Rendelés követése** szakaszban az **Állapot lekérése** gomb az aktuális állapotot és a tételeket mutatja.

A böngésző a kosár tételeinek azonosítóit és mennyiségeit, illetve az utolsó követési kódot tárolja a `localStorage`-ban. A név, a telefonszám és a megjegyzés nem kerül tartós böngészőtárba.

## A személyzet folyamata

1. Nyisd meg a `/szemelyzet` oldalt, és írd be a szerverhez tartozó személyzeti kulcsot.
2. A **Rendelések** lapon a legutóbbi 100 rendelés jelenik meg. A **Frissítés** lekéri az új rendeléseket és az aktuális étlapot.
3. A szokásos folyamat **Beérkezett → Készül → Átvehető → Átadva**. Az átadásig a rendelés törölhető. Az **Átadva** és a **Törölve** végállapotok. A törlés az állapotot módosítja, a rendelés előzménye megmarad.
4. Az **Étel elérhetősége** lapon az **Elrejtés** gomb kiveszi az ételt a vendégek kínálatából. A **Visszakapcsolás** újra rendelhetővé teszi.
5. A **Kilépés** törli a kliens memóriájából a kulcsot és a betöltött személyzeti adatokat.

## Komponensek

- **React 19 és Vite** — az `Alkalmazas` választ a vendég- és személyzeti nézet között. A `Rendeles` kezeli az étlapot és a rendelést, a `Szemelyzet` a személyzeti folyamatokat. A közös `api.js` küldi a JSON-kéréseket és kezeli a hálózati hibák megjelenítéséhez szükséges üzeneteket. A stílusok átrendezik az étlapot és a kosarat kisebb képernyőn.
- **ASP.NET Core 10, C#** — a `Program.cs` regisztrálja a REST-végpontokat, kiszolgálja a lefordított React-felületet és ellenőrzi a személyzeti kulcsot. Az `RendelesiSzabalyok.cs` ellenőrzi a rendelési adatokat és az állapotátmeneteket. Az `EttermiAdattar.cs` végzi az SQL-műveleteket. A `Modellek.cs` külön típust tart fenn a nyilvános és a személyzeti rendelésnézethez.
- **SQLite** — a `Microsoft.Data.Sqlite` adatszolgáltató paraméterezett SQL-parancsokat használ. Az idegen kulcsok ellenőrzése aktív; a WAL mód a párhuzamos olvasást segíti.

A React-kliens HTTP/JSON-kéréseket küld a C# API-nak. Az API ellenőrzi a bemenetet, végrehajtja az adatbázis-műveletet, majd HTTP státuszkóddal és megfelelő adattartalommal válaszol. A kliens lefordítása után a kliens és az API azonos eredetről kerül kiszolgálásra.

## REST-végpontok

- **GET `/api/etlap`** — kategóriák és rendelhető ételek. Válasz 200.
- **POST `/api/rendelesek`** — rendelés leadása. Új rendelés 201 és `Location` fejléc. Azonos kérés újraküldése 200, módosított tartalom ugyanazzal a kérésazonosítóval 409.
- **GET `/api/rendelesek/{azonosito}`** — rendelési állapot, tételek és ár személyes adatok nélkül. Válasz 200 vagy 404.
- **GET `/api/szemelyzet/rendelesek`** — a legutóbbi rendelések átvételi adatokkal. Válasz 200.
- **PATCH `/api/szemelyzet/rendelesek/{azonosito}`** — állapotváltás, például `{"allapot":"keszul"}`. Válasz 200, ismeretlen állapotnál 400, nem létező rendelésnél 404, tiltott átmenetnél 409.
- **GET `/api/szemelyzet/etlap`** — a teljes étlap. Válasz 200.
- **PATCH `/api/szemelyzet/etelek/{azonosito}`** — elérhetőség állítása `{"elerheto":false}` vagy `{"elerheto":true}` adattal. Válasz 204, hiányzó mezőnél 400, ismeretlen ételnél 404.

A `/api/szemelyzet/` végpontokhoz `Authorization: Bearer <személyzeti kulcs>` fejléc szükséges. Érvénytelen vagy hiányzó kulccsal a válasz 401. JSON törzs küldéséhez az `application/json` típust kell használni.

Példa a rendelési kérés törzsére:

```json
{
  "keresAzonosito": "c0a801d5-481d-4dd8-b026-9b377cd2ff3d",
  "vendegNeve": "Minta Vendég",
  "telefonszam": "+36 30 555 0100",
  "megjegyzes": "",
  "tetelek": [{ "etelAzonosito": 1, "mennyiseg": 2 }, { "etelAzonosito": 9, "mennyiseg": 1 }]
}
```

## Adatmodell és működési szabályok

- A `kategoriak` kategóriáit a `etelek` fogásai hivatkozzák. A `etelek` tárolja a nevet, a leírást, az egész forintban megadott árat és az elérhetőséget.
- Az `rendelesek` tartalmazza a külön generált követési azonosítót, az ismételt küldéshez használt kérésazonosítót és tartalomlenyomatot, az átvételi adatokat, az állapotot, a végösszeget és az UTC időbélyeget.
- Az `rendelesi_tetelek` összeköti a rendelést az ételekkel. Az étel neve és az egységára a leadáskor kerül a tételbe, így későbbi étlapváltozás után is az eredeti adatok jelennek meg.
- Leadáskor a szerver a rendelhető ételek aktuális árából számítja az összeget. A rendelési fej és a tételek egy tranzakcióban kerülnek mentésre.
- A kliens azonos űrlaptartalommal újrapróbált beküldéskor újrahasználja a kérésazonosítót. Az egyedi adatbáziskulcs és a tranzakció biztosítja, hogy a párhuzamosan beérkező azonos kérésekből egy rendelés legyen.

A táblák mezőit és kapcsolatait az [adatbázismodell-diagram](ADATBAZIS.svg) mutatja. Az [SQL-export](../adatbazis/adatbazis-export.sql) a sémát és a mintaétlapot tartalmazza. Egy új adatbázisba az SQLite parancssorából is betölthető:

```text
sqlite3 parazs.sqlite3
sqlite> .read adatbazis/adatbazis-export.sql
sqlite> .tables
sqlite> .quit
```

A fenti példát a projekt gyökerében futtasd. Az export önálló visszaállításához SQLite parancssor kell. Az alkalmazás induláskor a beépített .NET-adatszolgáltatóval tölti be ugyanezt az exportot, amihez külön SQLite-program nem szükséges.

## Frissítés a korábbi kiadásról

A magyarított kiadás az első indításkor tranzakcióban átveszi az angol nevű táblákból az étlapot, a rendeléseket, a tételeket és az állapotokat. A korábbi követési kódok tovább használhatók. Ha a régi adatbázis a projekt mappájában maradt a `server/data/parazs.sqlite3` útvonalon, a szerver felismeri. Ha máshova bontottad ki a csomagot, állítsd le a régi szervert, másold az adatbázist a `szerver/adatok/parazs.sqlite3` helyre, vagy add meg az adatbázis másolatának teljes útvonalát a `PARAZS_ADATBAZIS` változóban.

A korábbi `/admin` böngészőcím átirányít a `/szemelyzet` oldalra. Saját API-kliens esetén a fenti magyar végpontokra és JSON-mezőkre kell átállni. A böngésző az előző kiadásból megmaradt kosarat és követési kódot is beolvassa azonos webcímen.

## Műszaki feltételek és beállítások

- .NET 10 SDK, Node.js 22.12+ vagy 24.x, npm, valamint korszerű böngésző.
- Alapértelmezett cím `http://127.0.0.1:5080`. A fejlesztői Vite-szerverhez futtasd a szerver mellett az `npm --prefix kliens run fejlesztes` parancsot. A Vite a `/api` kéréseket az 5080-as portra továbbítja.
- A `PARAZS_ADATBAZIS` környezeti változóval másik SQLite-fájl adható meg, lehetőleg abszolút elérési úttal.
- A `PARAZS_SZEMELYZETI_KULCS` változóval tartós kulcs állítható be, 16–128 szóköz nélküli, látható ASCII karakterrel. Enélkül a program minden induláskor új kulcsot ír a terminálra.
- Telefonos bemutatóhoz megbízható helyi hálózaton futtasd a `dotnet run --project szerver/Parazs.csproj --no-launch-profile -- --urls http://0.0.0.0:5080` parancsot. A telefonon a gazdagép helyi IP-címét és az 5080-as portot nyisd meg. Ehhez a tűzfalnak engednie kell a helyi kapcsolatot.
- A csomag vizsgabemutatóra, fiktív adatokhoz készült. Éles használat előtt HTTPS, személyenkénti hozzáférés, mentési eljárás és az ügyféladatok megőrzési szabályai szükségesek.

## Projektadatok a leadáshoz

- Intézmény és osztály — [kitöltendő]
- A 2–3 fős csapat tagjai — [kitöltendő]
- Tényleges munkamegosztás és közös ellenőrzés — [kitöltendő]
- Leadás dátuma — [kitöltendő]

A fejléc pizzafotója a projekthez készült generált illusztráció.

## Technológiai források

- [ASP.NET Core Minimal API — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0)
- [Microsoft.Data.Sqlite — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/)
- [Vite — összeállítási és futtatási követelmények](https://vite.dev/guide/)

