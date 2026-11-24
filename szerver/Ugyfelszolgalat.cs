using Microsoft.Data.Sqlite;

namespace Parazs;

public sealed record JegyBemenet(Guid KeresAzonosito, string? Email, string? Targy,
    string? Uzenet, Guid? RendelesAzonosito);
public sealed record JegyValaszBemenet(string? Allapot, string? Valasz);
public sealed record UgyfelJegy(Guid Azonosito, string Targy, string Allapot, string Valasz, string Letrehozva);
public sealed record SzemelyzetiJegy(Guid Azonosito, string Email, string Targy, string Uzenet,
    Guid? RendelesAzonosito, string Allapot, string Valasz, string Letrehozva)
{
    public UgyfelJegy ToGuestView() => new(Azonosito, Targy, Allapot, Valasz, Letrehozva);
}

public sealed partial class EttermiAdattar
{
    private const string JegyekOszlopai = "azonosito, email, targy, uzenet, rendeles_azonosito, allapot, valasz, letrehozva";

    public (UgyfelJegy Jegy, bool Letrejott) CreateTicket(JegyBemenet bemenet)
    {
        if (bemenet.KeresAzonosito == Guid.Empty)
            throw new KeresHiba(400, "Érvényes kérésazonosító szükséges.");
        var email = NormalizeEmail(bemenet.Email);
        var targy = RendelesiSzabalyok.ValidateText(bemenet.Targy, "Tárgy", 3, 100);
        var uzenet = RendelesiSzabalyok.ValidateText(bemenet.Uzenet, "Üzenet", 10, 2000);
        using var adatbazis = OpenConnection();
        using var tranzakcio = adatbazis.BeginTransaction();
        using var elozo = CreateCommand(adatbazis, tranzakcio, $"""
            SELECT {JegyekOszlopai} FROM ugyfelszolgalati_jegyek WHERE keres_azonosito = $keres
            """, ("$keres", bemenet.KeresAzonosito.ToString()));
        SzemelyzetiJegy? korabbi;
        using (var sor = elozo.ExecuteReader())
            korabbi = sor.Read() ? ReadTicket(sor) : null;
        if (korabbi is not null)
        {
            if (korabbi.Email != email || korabbi.Targy != targy || korabbi.Uzenet != uzenet ||
                korabbi.RendelesAzonosito != bemenet.RendelesAzonosito)
                throw new KeresHiba(409, "Ezzel a kérésazonosítóval már más tartalmú üzenet érkezett.");
            tranzakcio.Commit();
            return (korabbi.ToGuestView(), false);
        }

        if (bemenet.RendelesAzonosito is Guid rendeles)
        {
            using var ellenorzes = CreateCommand(adatbazis, tranzakcio, "SELECT 1 FROM rendelesek WHERE azonosito = $id",
                ("$id", rendeles.ToString()));
            if (ellenorzes.ExecuteScalar() is null)
                throw new KeresHiba(404, "Ehhez a követési kódhoz nem található rendelés.");
        }
        var azonosito = Guid.NewGuid();
        var idopont = DateTimeOffset.UtcNow.ToString("O");
        using var parancs = CreateCommand(adatbazis, tranzakcio, """
            INSERT INTO ugyfelszolgalati_jegyek (azonosito, keres_azonosito, email, targy, uzenet, rendeles_azonosito, letrehozva)
            VALUES ($id, $keres, $email, $targy, $uzenet, $rendeles, $idopont)
            """, ("$id", azonosito.ToString()), ("$keres", bemenet.KeresAzonosito.ToString()), ("$email", email),
            ("$targy", targy), ("$uzenet", uzenet),
            ("$rendeles", (object?)bemenet.RendelesAzonosito?.ToString() ?? DBNull.Value),
            ("$idopont", idopont));
        parancs.ExecuteNonQuery();
        tranzakcio.Commit();
        return (new(azonosito, targy, "nyitott", "", idopont), true);
    }

    public UgyfelJegy GetTicket(Guid azonosito)
    {
        using var adatbazis = OpenConnection();
        return FindTicket(adatbazis, null, azonosito).ToGuestView();
    }

    public IReadOnlyList<SzemelyzetiJegy> GetStaffTickets()
    {
        using var adatbazis = OpenConnection();
        using var parancs = CreateCommand(adatbazis, null, $"""
            SELECT {JegyekOszlopai} FROM ugyfelszolgalati_jegyek
            ORDER BY (allapot = 'lezart'), letrehozva DESC, azonosito DESC LIMIT 100
            """);
        using var sor = parancs.ExecuteReader();
        var jegyek = new List<SzemelyzetiJegy>();
        while (sor.Read()) jegyek.Add(ReadTicket(sor));
        return jegyek;
    }

    public SzemelyzetiJegy UpdateTicket(Guid azonosito, JegyValaszBemenet bemenet)
    {
        if (bemenet.Allapot is not ("megvalaszolt" or "lezart"))
            throw new KeresHiba(400, "A jegy megválaszolható vagy lezárható.");
        using var adatbazis = OpenConnection();
        using var tranzakcio = adatbazis.BeginTransaction();
        var jegy = FindTicket(adatbazis, tranzakcio, azonosito);
        var valasz = bemenet.Valasz is null && bemenet.Allapot == "lezart"
            ? jegy.Valasz
            : RendelesiSzabalyok.ValidateText(bemenet.Valasz, "Válasz", 2, 2000);
        if (jegy.Allapot == "lezart" && (bemenet.Allapot != jegy.Allapot || valasz != jegy.Valasz))
            throw new KeresHiba(409, "A lezárt jegy már nem módosítható.");

        using var parancs = CreateCommand(adatbazis, tranzakcio, """
            UPDATE ugyfelszolgalati_jegyek SET allapot = $allapot, valasz = $valasz WHERE azonosito = $id
            """, ("$allapot", bemenet.Allapot), ("$valasz", valasz), ("$id", azonosito.ToString()));
        parancs.ExecuteNonQuery();
        tranzakcio.Commit();
        return jegy with { Allapot = bemenet.Allapot, Valasz = valasz };
    }

    private static SzemelyzetiJegy FindTicket(SqliteConnection adatbazis, SqliteTransaction? tranzakcio, Guid azonosito)
    {
        using var parancs = CreateCommand(adatbazis, tranzakcio, $"""
            SELECT {JegyekOszlopai} FROM ugyfelszolgalati_jegyek WHERE azonosito = $id
            """, ("$id", azonosito.ToString()));
        using var sor = parancs.ExecuteReader();
        if (!sor.Read()) throw new KeresHiba(404, "Nem található ügyfélszolgálati ügy ezzel a kóddal.");
        return ReadTicket(sor);
    }

    private static SzemelyzetiJegy ReadTicket(SqliteDataReader sor) =>
        new(Guid.Parse(sor.GetString(0)), sor.GetString(1), sor.GetString(2), sor.GetString(3),
            sor.IsDBNull(4) ? null : Guid.Parse(sor.GetString(4)), sor.GetString(5), sor.GetString(6), sor.GetString(7));
}
