using Microsoft.Data.Sqlite;

namespace Parazs;

public sealed record TarcaFeltoltesBemenet(Guid KeresAzonosito, int Osszeg);
public sealed record TarcaTetel(string Tipus, int Osszeg, string Letrehozva, Guid? RendelesAzonosito);
public sealed record TarcaOsszesito(long Egyenleg, IReadOnlyList<TarcaTetel> Muveletek);

public sealed partial class EttermiAdattar
{
    public TarcaOsszesito TarcaLekerese(Guid profil)
    {
        using var adatbazis = KapcsolatMegnyitasa();
        using var tranzakcio = adatbazis.BeginTransaction(deferred: true);
        var eredmeny = TarcaOlvasasa(adatbazis, tranzakcio, profil);
        tranzakcio.Commit();
        return eredmeny;
    }

    public TarcaOsszesito TarcaFeltoltese(Guid profil, TarcaFeltoltesBemenet bemenet)
    {
        if (bemenet.KeresAzonosito == Guid.Empty || bemenet.Osszeg is not (1000 or 5000 or 10000))
            throw new KeresHiba(400, "Válassz 1 000, 5 000 vagy 10 000 Ft próbaösszeget.");
        using var adatbazis = KapcsolatMegnyitasa();
        using var tranzakcio = adatbazis.BeginTransaction();
        using var korabbi = Parancs(adatbazis, tranzakcio, """
            SELECT osszeg FROM tarca_muveletek WHERE profil_azonosito = $profil AND keres_azonosito = $keres
            """, ("$profil", profil.ToString()), ("$keres", bemenet.KeresAzonosito.ToString()));
        var korabbiOsszeg = korabbi.ExecuteScalar();
        if (korabbiOsszeg is not null && Convert.ToInt32(korabbiOsszeg) != bemenet.Osszeg)
            throw new KeresHiba(409, "Ehhez a feltöltéshez már más összeg tartozik.");
        if (korabbiOsszeg is null)
        {
            if (TarcaEgyenleg(adatbazis, tranzakcio, profil) + bemenet.Osszeg > 100000)
                throw new KeresHiba(422, "A próbaegyenleg feltöltéssel legfeljebb 100 000 Ft lehet.");
            TarcaMuveletMentese(adatbazis, tranzakcio, profil, "feltoltes", bemenet.Osszeg, null, bemenet.KeresAzonosito);
        }
        var eredmeny = TarcaOlvasasa(adatbazis, tranzakcio, profil);
        tranzakcio.Commit();
        return eredmeny;
    }

    private static void TarcasFizetes(SqliteConnection adatbazis, SqliteTransaction tranzakcio,
        Guid? profil, Guid rendeles, int osszeg)
    {
        if (profil is null) throw new KeresHiba(401, "A pénztárcás fizetéshez jelentkezz be.");
        if (TarcaEgyenleg(adatbazis, tranzakcio, profil.Value) < osszeg)
            throw new KeresHiba(422, "A demópénztárcában nincs elegendő egyenleg.");
        TarcaMuveletMentese(adatbazis, tranzakcio, profil.Value, "fizetes", -osszeg, rendeles, null);
    }

    private static void TarcasJovairas(SqliteConnection adatbazis, SqliteTransaction tranzakcio, Guid rendeles)
    {
        using var lekerdezes = Parancs(adatbazis, tranzakcio, """
            SELECT profil_azonosito, osszeg FROM tarca_muveletek
            WHERE rendeles_azonosito = $rendeles AND tipus = 'fizetes'
            """, ("$rendeles", rendeles.ToString()));
        Guid profil;
        int osszeg;
        using (var sor = lekerdezes.ExecuteReader())
        {
            if (!sor.Read()) return;
            profil = Guid.Parse(sor.GetString(0));
            osszeg = -sor.GetInt32(1);
        }
        TarcaMuveletMentese(adatbazis, tranzakcio, profil, "jovairas", osszeg, rendeles, null);
    }

    private static long TarcaEgyenleg(SqliteConnection adatbazis, SqliteTransaction tranzakcio, Guid profil)
    {
        using var parancs = Parancs(adatbazis, tranzakcio, """
            SELECT COALESCE(SUM(osszeg), 0) FROM tarca_muveletek WHERE profil_azonosito = $profil
            """, ("$profil", profil.ToString()));
        return Convert.ToInt64(parancs.ExecuteScalar());
    }

    private static TarcaOsszesito TarcaOlvasasa(SqliteConnection adatbazis, SqliteTransaction tranzakcio, Guid profil)
    {
        var egyenleg = TarcaEgyenleg(adatbazis, tranzakcio, profil);
        using var parancs = Parancs(adatbazis, tranzakcio, """
            SELECT tipus, osszeg, letrehozva, rendeles_azonosito FROM tarca_muveletek
            WHERE profil_azonosito = $profil ORDER BY letrehozva DESC, azonosito DESC LIMIT 50
            """, ("$profil", profil.ToString()));
        var muveletek = new List<TarcaTetel>();
        using var sor = parancs.ExecuteReader();
        while (sor.Read())
            muveletek.Add(new(sor.GetString(0), sor.GetInt32(1), sor.GetString(2),
                sor.IsDBNull(3) ? null : Guid.Parse(sor.GetString(3))));
        return new(egyenleg, muveletek);
    }

    private static void TarcaMuveletMentese(SqliteConnection adatbazis, SqliteTransaction tranzakcio,
        Guid profil, string tipus, int osszeg, Guid? rendeles, Guid? keres)
    {
        using var parancs = Parancs(adatbazis, tranzakcio, """
            INSERT INTO tarca_muveletek (azonosito, profil_azonosito, tipus, osszeg, letrehozva,
                rendeles_azonosito, keres_azonosito)
            VALUES ($id, $profil, $tipus, $osszeg, $idopont, $rendeles, $keres)
            """, ("$id", Guid.NewGuid().ToString()), ("$profil", profil.ToString()), ("$tipus", tipus),
            ("$osszeg", osszeg), ("$idopont", DateTimeOffset.UtcNow.ToString("O")),
            ("$rendeles", (object?)rendeles?.ToString() ?? DBNull.Value),
            ("$keres", (object?)keres?.ToString() ?? DBNull.Value));
        parancs.ExecuteNonQuery();
    }
}

