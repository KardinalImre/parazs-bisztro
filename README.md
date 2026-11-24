# Parázs Bisztró

Magyar nyelvű éttermi rendelőrendszer személyes átvételhez. A vendégek mobilról és asztali böngészőből rendelhetnek. A személyzet böngészőben vagy a natív Windowsos WPF-alkalmazásban kezelheti a rendeléseket, az étlap elérhetőségét és az ügyfélszolgálati jegyeket.

## A webes alkalmazás indítása

Szükséges a **.NET 10 SDK** és a **Node.js 22.12+ vagy 24.x**. Első alkalommal a függőségek letöltéséhez internetkapcsolat kell. A projekt mappájában futtasd:

```sh
npm --prefix kliens ci
npm --prefix kliens run epites
dotnet run --project szerver/Parazs.csproj --no-launch-profile
```

- [Étlap és rendelés](http://127.0.0.1:5080)
- [Vendégprofil és demópénztárca](http://127.0.0.1:5080/profil)
- [Ügyfélszolgálat](http://127.0.0.1:5080/ugyfelszolgalat)
- [Böngészős személyzeti felület](http://127.0.0.1:5080/szemelyzet)

A szerver első indításkor feltölti a mintaétlapot. A személyzeti kulcs az indítóterminálban jelenik meg. A szerver a **Ctrl+C** billentyűkkel állítható le.

## Windowsos személyzeti alkalmazás

A [GitHub-kiadásból](https://github.com/KardinalImre/parazs-bisztro/releases/tag/v1.0.0) töltsd le a **Parazs-Szemelyzet-win-x64.msi** fájlt. A telepítő a WPF-alkalmazást és a hozzá tartozó .NET futtatókörnyezetet tartalmazza. Telepítés után a Start menüben keresd a **Parázs Bisztró – Személyzet** nevet.

Indítsd el a webszervert, majd a WPF-ablakba írd be a szerver címét és a terminálban megjelenő személyzeti kulcsot. Forrásból Windowson így indítható:

```sh
dotnet run --project szemelyzet-wpf/Parazs.Szemelyzet.csproj
```

A telepítő elkészítése:

```sh
dotnet build telepito/Parazs.Szemelyzet.Telepito.wixproj -c Release
```

Az MSI a `telepito/bin/Release/` mappában jelenik meg. A telepítő jelenlegi kiadása digitálisan aláíratlan.

## Bemutató

1. Keresd meg az étlapon a Margheritát, tedd a kosárba és nyisd meg az átvételi űrlapot.
2. Próbáld ki a **PARAZS10** kódot legalább 3 000 Ft-os kosárral. Az **ELVITEL500** kód 4 000 Ft értékű kosártól 500 Ft kedvezményt ad.
3. Profil létrehozása után tölts fel próbaösszeget, és válassz demópénztárcás fizetést. A tárca fiktív egyenleget használ.
4. A WPF-ben állítsd a rendelést átvehetőre. A vendég az étlap követési szakaszában ellenőrizheti az állapotot, a saját rendeléseit pedig a profilján látja.
5. Küldj ügyfélszolgálati próbaüzenetet, válaszold meg a WPF-ben, majd kérd le a választ a kapott ügyazonosítóval.

## Beadandó anyagok

- [Műszaki és felhasználói dokumentáció](dokumentacio/DOKUMENTACIO.md)
- [Adatbázismodell](dokumentacio/ADATBAZIS.svg) és [SQLite-export](adatbazis/adatbazis-export.sql)
- [Teszteredmények és újrafuttatás](dokumentacio/TESZTEREDMENYEK.md)
- [Trello-tábla](https://trello.com/b/5ABCPIms/)

A csapat neve és a tényleges munkamegosztás a műszaki dokumentációban tölthető ki.

