using Microsoft.Data.Sqlite;

namespace Parazs;

public sealed partial class EttermiAdattar(string eleresiUt)
{
    private readonly string kapcsolatiKarakterlanc = new SqliteConnectionStringBuilder
    {
        DataSource = eleresiUt, ForeignKeys = true, DefaultTimeout = 5
    }.ToString();

    public void Initialize(string sqlMappa)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(eleresiUt)!);
        using var adatbazis = OpenConnection();
        using var naplo = CreateCommand(adatbazis, null, "PRAGMA journal_mode = WAL");
        naplo.ExecuteScalar();
        using var tranzakcio = adatbazis.BeginTransaction();
        using var ellenorzes = CreateCommand(adatbazis, tranzakcio,
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'categories'");
        var regiSema = ellenorzes.ExecuteScalar() is not null;
        if (regiSema)
            ExecuteSqlFile(adatbazis, tranzakcio, Path.Combine(sqlMappa, "regi-nevek.sql"));
        ExecuteSqlFile(adatbazis, tranzakcio, Path.Combine(sqlMappa, "adatbazis-export.sql"));
        if (regiSema)
            ExecuteSqlFile(adatbazis, tranzakcio, Path.Combine(sqlMappa, "regi-adatok-atvetele.sql"));
        tranzakcio.Commit();
    }

    public Etlap GetMenu(bool elfogyottakkal = false)
    {
        using var adatbazis = OpenConnection();
        using var tranzakcio = adatbazis.BeginTransaction(deferred: true);
        using var kategoriaParancs = CreateCommand(adatbazis, tranzakcio, "SELECT azonosito, nev FROM kategoriak ORDER BY sorrend, azonosito");
        var kategoriak = new List<Kategoria>();
        using (var sorok = kategoriaParancs.ExecuteReader())
            while (sorok.Read()) kategoriak.Add(new(sorok.GetInt32(0), sorok.GetString(1)));

        using var etelParancs = CreateCommand(adatbazis, tranzakcio, """
            SELECT azonosito, kategoria_azonosito, nev, leiras, ar, elerheto FROM etelek
            WHERE elerheto = 1 OR $reszletes ORDER BY azonosito
            """, ("$reszletes", elfogyottakkal));
        var etelek = new List<Etel>();
        using (var sorok = etelParancs.ExecuteReader())
            while (sorok.Read())
                etelek.Add(new(sorok.GetInt32(0), sorok.GetInt32(1), sorok.GetString(2),
                    sorok.GetString(3), sorok.GetInt32(4), sorok.GetBoolean(5)));
        tranzakcio.Commit();
        return new(kategoriak, etelek);
    }

    public void SetDishAvailability(int etelAzonosito, bool? elerheto)
    {
        if (elerheto is null) throw new KeresHiba(400, "Az elérhetőség megadása kötelező.");
        using var adatbazis = OpenConnection();
        using var modositas = CreateCommand(adatbazis, null, "UPDATE etelek SET elerheto = $elerheto WHERE azonosito = $azonosito",
            ("$elerheto", elerheto.Value), ("$azonosito", etelAzonosito));
        if (modositas.ExecuteNonQuery() == 0) throw new KeresHiba(404, "Az étel nem található.");
    }

    public KosarOsszesites QuoteCart(OsszesitesBemenet bemenet)
    {
        var mennyisegek = RendelesiSzabalyok.ValidateItems(bemenet.Tetelek);
        using var adatbazis = OpenConnection();
        using var tranzakcio = adatbazis.BeginTransaction(deferred: true);
        var tetelek = ReadAvailableItems(adatbazis, tranzakcio, mennyisegek);
        var osszesites = CalculateQuote(adatbazis, tranzakcio, tetelek, RendelesiSzabalyok.NormalizeCouponCode(bemenet.Kuponkod));
        tranzakcio.Commit();
        return osszesites;
    }

    public (RendelesOsszesito Rendeles, bool Letrejott) CreateOrder(RendelesBemenet bemenet, Guid? profilAzonosito)
    {
        var keres = RendelesiSzabalyok.ValidateOrder(bemenet);
        using var adatbazis = OpenConnection();
        using var tranzakcio = adatbazis.BeginTransaction();
        var meglevo = FindOrderRequest(adatbazis, tranzakcio, keres.KeresAzonosito);
        if (meglevo is { } korabbi)
        {
            if (korabbi.Lenyomat != keres.Lenyomat || korabbi.ProfilAzonosito != profilAzonosito)
                throw new KeresHiba(409, "Ezzel a kérésazonosítóval már más tartalmú rendelés érkezett.");
            var rendeles = ReadOrder(adatbazis, tranzakcio, korabbi.Azonosito).ToGuestView();
            tranzakcio.Commit();
            return (rendeles, false);
        }

        var tetelek = ReadAvailableItems(adatbazis, tranzakcio, keres.Mennyisegek);
        var azonosito = Guid.NewGuid();
        var osszesites = CalculateQuote(adatbazis, tranzakcio, tetelek, keres.Kuponkod);
        var vegosszeg = osszesites.Vegosszeg;
        if (bemenet.VartVegosszeg is int vart && vart != vegosszeg)
            throw new KeresHiba(409, "A rendelés ára megváltozott. Ellenőrizd az új végösszeget.");
        var idopont = DateTimeOffset.UtcNow.ToString("O");

        using var beszuras = CreateCommand(adatbazis, tranzakcio, """
            INSERT INTO rendelesek (azonosito, keres_azonosito, keres_lenyomat, vendeg_neve, telefonszam, megjegyzes, vegosszeg, letrehozva)
            VALUES ($azonosito, $keres, $lenyomat, $nev, $telefonszam, $megjegyzes, $vegosszeg, $idopont)
            """, ("$azonosito", azonosito.ToString()), ("$keres", keres.KeresAzonosito.ToString()), ("$lenyomat", keres.Lenyomat),
            ("$nev", keres.Nev), ("$telefonszam", keres.Telefonszam), ("$megjegyzes", keres.Megjegyzes),
            ("$vegosszeg", vegosszeg), ("$idopont", idopont));
        beszuras.ExecuteNonQuery();
        if (profilAzonosito is Guid profil)
        {
            using var hozzarendeles = CreateCommand(adatbazis, tranzakcio, """
                INSERT INTO rendeles_profilok (rendeles_azonosito, profil_azonosito) VALUES ($rendeles, $profil)
                """, ("$rendeles", azonosito.ToString()), ("$profil", profil.ToString()));
            hozzarendeles.ExecuteNonQuery();
        }

        SaveDiscount(adatbazis, tranzakcio, azonosito, osszesites);
        if (keres.FizetesiMod == "tarca")
            ChargeWallet(adatbazis, tranzakcio, profilAzonosito, azonosito, vegosszeg);
        foreach (var tetel in tetelek)
        {
            using var tetelSor = CreateCommand(adatbazis, tranzakcio, """
                INSERT INTO rendelesi_tetelek (rendeles_azonosito, etel_azonosito, etel_neve, egysegar, mennyiseg)
                VALUES ($rendeles, $etel, $nev, $ar, $mennyiseg)
                """, ("$rendeles", azonosito.ToString()), ("$etel", tetel.EtelAzonosito), ("$nev", tetel.EtelNeve),
                ("$ar", tetel.Egysegar), ("$mennyiseg", tetel.Mennyiseg));
            tetelSor.ExecuteNonQuery();
        }
        tranzakcio.Commit();
        return (new(azonosito, "beerkezett", vegosszeg, idopont, tetelek, osszesites.Kedvezmeny, osszesites.Kuponkod, keres.FizetesiMod), true);
    }

    public RendelesOsszesito GetOrder(Guid azonosito)
    {
        using var adatbazis = OpenConnection();
        using var tranzakcio = adatbazis.BeginTransaction(deferred: true);
        var rendeles = ReadOrder(adatbazis, tranzakcio, azonosito).ToGuestView();
        tranzakcio.Commit();
        return rendeles;
    }

    public IReadOnlyList<RendelesOsszesito> GetOrderHistory(Guid profilAzonosito)
    {
        using var adatbazis = OpenConnection();
        using var tranzakcio = adatbazis.BeginTransaction(deferred: true);
        using var lekerdezes = CreateCommand(adatbazis, tranzakcio, """
            SELECT r.azonosito FROM rendelesek r JOIN rendeles_profilok p ON p.rendeles_azonosito = r.azonosito
            WHERE p.profil_azonosito = $profil ORDER BY r.letrehozva DESC, r.azonosito LIMIT 100
            """, ("$profil", profilAzonosito.ToString()));
        var azonositok = new List<Guid>();
        using (var sorok = lekerdezes.ExecuteReader())
            while (sorok.Read()) azonositok.Add(Guid.Parse(sorok.GetString(0)));
        var rendelesek = azonositok.Select(id => ReadOrder(adatbazis, tranzakcio, id).ToGuestView()).ToArray();
        tranzakcio.Commit();
        return rendelesek;
    }

    public IReadOnlyList<SzemelyzetiRendeles> GetStaffOrders()
    {
        using var adatbazis = OpenConnection();
        using var tranzakcio = adatbazis.BeginTransaction(deferred: true);
        var azonositok = new List<Guid>();
        using var lekerdezes = CreateCommand(adatbazis, tranzakcio, "SELECT azonosito FROM rendelesek ORDER BY letrehozva DESC, azonosito LIMIT 100");
        using (var sorok = lekerdezes.ExecuteReader())
            while (sorok.Read()) azonositok.Add(Guid.Parse(sorok.GetString(0)));
        var eredmeny = azonositok.Select(azonosito => ReadOrder(adatbazis, tranzakcio, azonosito)).ToArray();
        tranzakcio.Commit();
        return eredmeny;
    }

    public SzemelyzetiRendeles UpdateOrderStatus(Guid azonosito, string? kovetkezo)
    {
        using var adatbazis = OpenConnection();
        using var tranzakcio = adatbazis.BeginTransaction();
        var rendeles = ReadOrder(adatbazis, tranzakcio, azonosito);
        RendelesiSzabalyok.ValidateTransition(rendeles.Allapot, kovetkezo);
        if (rendeles.Allapot != kovetkezo)
        {
            using var modositas = CreateCommand(adatbazis, tranzakcio, "UPDATE rendelesek SET allapot = $allapot WHERE azonosito = $azonosito",
                ("$allapot", kovetkezo!), ("$azonosito", azonosito.ToString()));
            modositas.ExecuteNonQuery();
            if (kovetkezo == "torolve") RefundWallet(adatbazis, tranzakcio, azonosito);
        }
        tranzakcio.Commit();
        return rendeles with { Allapot = kovetkezo! };
    }

    private static (Guid Azonosito, string Lenyomat, Guid? ProfilAzonosito)? FindOrderRequest(SqliteConnection adatbazis, SqliteTransaction tranzakcio, Guid keresAzonosito)
    {
        using var lekerdezes = CreateCommand(adatbazis, tranzakcio, """
            SELECT r.azonosito, r.keres_lenyomat, p.profil_azonosito
            FROM rendelesek r LEFT JOIN rendeles_profilok p ON p.rendeles_azonosito = r.azonosito
            WHERE r.keres_azonosito = $keres
            """, ("$keres", keresAzonosito.ToString()));
        using var sor = lekerdezes.ExecuteReader();
        return sor.Read() ? (Guid.Parse(sor.GetString(0)), sor.GetString(1),
            sor.IsDBNull(2) ? null : Guid.Parse(sor.GetString(2))) : null;
    }

    private static List<RendelesiTetel> ReadAvailableItems(SqliteConnection adatbazis, SqliteTransaction tranzakcio,
        SortedDictionary<int, int> mennyisegek)
    {
        var tetelek = new List<RendelesiTetel>(mennyisegek.Count);
        using var lekerdezes = CreateCommand(adatbazis, tranzakcio, "SELECT nev, ar FROM etelek WHERE azonosito = $azonosito AND elerheto = 1",
            ("$azonosito", 0));
        foreach (var (azonosito, mennyiseg) in mennyisegek)
        {
            lekerdezes.Parameters["$azonosito"].Value = azonosito;
            using var sor = lekerdezes.ExecuteReader();
            if (!sor.Read())
                throw new KeresHiba(409, "Az egyik tétel már nem rendelhető. Ellenőrizd az étlapot.");
            tetelek.Add(new(azonosito, sor.GetString(0), sor.GetInt32(1), mennyiseg));
        }
        return tetelek;
    }

    private static SzemelyzetiRendeles ReadOrder(SqliteConnection adatbazis, SqliteTransaction tranzakcio, Guid azonosito)
    {
        using var rendeles = CreateCommand(adatbazis, tranzakcio, """
            SELECT r.allapot, r.vegosszeg, r.letrehozva, r.vendeg_neve, r.telefonszam, r.megjegyzes,
                COALESCE(k.kedvezmeny, 0), k.kuponkod,
                EXISTS (SELECT 1 FROM tarca_muveletek t WHERE t.rendeles_azonosito = r.azonosito AND t.tipus = 'fizetes')
            FROM rendelesek r LEFT JOIN rendeles_kedvezmenyek k ON k.rendeles_azonosito = r.azonosito
            WHERE r.azonosito = $azonosito
            """, ("$azonosito", azonosito.ToString()));
        SzemelyzetiRendeles eredmeny;
        var tetelek = new List<RendelesiTetel>();
        using (var sor = rendeles.ExecuteReader())
        {
            if (!sor.Read()) throw new KeresHiba(404, "A rendelés nem található.");
            eredmeny = new(azonosito, sor.GetString(0), sor.GetInt32(1), sor.GetString(2), tetelek,
                sor.GetString(3), sor.GetString(4), sor.GetString(5), sor.GetInt32(6),
                sor.IsDBNull(7) ? null : sor.GetString(7), sor.GetBoolean(8) ? "tarca" : "atvetel");
        }
        using var tetelSorok = CreateCommand(adatbazis, tranzakcio, """
            SELECT etel_azonosito, etel_neve, egysegar, mennyiseg FROM rendelesi_tetelek WHERE rendeles_azonosito = $azonosito ORDER BY etel_azonosito
            """, ("$azonosito", azonosito.ToString()));
        using var tetelSor = tetelSorok.ExecuteReader();
        while (tetelSor.Read()) tetelek.Add(new(tetelSor.GetInt32(0), tetelSor.GetString(1), tetelSor.GetInt32(2), tetelSor.GetInt32(3)));
        return eredmeny;
    }

    private static void ExecuteSqlFile(SqliteConnection adatbazis, SqliteTransaction tranzakcio, string fajl)
    {
        using var parancs = CreateCommand(adatbazis, tranzakcio, File.ReadAllText(fajl));
        parancs.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var adatbazis = new SqliteConnection(kapcsolatiKarakterlanc);
        try
        {
            adatbazis.Open();
            return adatbazis;
        }
        catch
        {
            adatbazis.Dispose();
            throw;
        }
    }

    private static SqliteCommand CreateCommand(SqliteConnection adatbazis, SqliteTransaction? tranzakcio, string utasitas,
        params (string Nev, object Ertek)[] parameterek)
    {
        var parancs = adatbazis.CreateCommand();
        parancs.CommandText = utasitas;
        parancs.Transaction = tranzakcio;
        foreach (var (nev, ertek) in parameterek) parancs.Parameters.AddWithValue(nev, ertek);
        return parancs;
    }
}

