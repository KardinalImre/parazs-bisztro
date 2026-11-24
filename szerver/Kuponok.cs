using Microsoft.Data.Sqlite;

namespace Parazs;

public sealed record KosarOsszesites(int Reszosszeg, int Kedvezmeny, int Vegosszeg, string? Kuponkod);

public sealed partial class EttermiAdattar
{
    private static KosarOsszesites Osszesites(SqliteConnection adatbazis, SqliteTransaction tranzakcio,
        IReadOnlyList<RendelesiTetel> tetelek, string? kuponkod)
    {
        var reszosszeg = tetelek.Sum(tetel => tetel.Egysegar * tetel.Mennyiseg);
        if (kuponkod is null) return new(reszosszeg, 0, reszosszeg, null);

        using var parancs = Parancs(adatbazis, tranzakcio, """
            SELECT tipus, ertek, minimum_osszeg FROM kuponok WHERE kod = $kod AND aktiv = 1
            """, ("$kod", kuponkod));
        using var sor = parancs.ExecuteReader();
        if (!sor.Read()) throw new KeresHiba(422, "Ez a kuponkód nem érvényes.");
        var tipus = sor.GetString(0);
        var ertek = sor.GetInt32(1);
        var minimum = sor.GetInt32(2);
        if (reszosszeg < minimum)
            throw new KeresHiba(422, $"A kupon legalább {minimum:N0} Ft értékű kosárhoz érvényes.");
        var kedvezmeny = tipus == "szazalek" ? (int)((long)reszosszeg * ertek / 100) : ertek;
        kedvezmeny = Math.Min(kedvezmeny, reszosszeg - 1);
        return new(reszosszeg, kedvezmeny, reszosszeg - kedvezmeny, kuponkod);
    }

    private static void KuponMentese(SqliteConnection adatbazis, SqliteTransaction tranzakcio,
        Guid rendeles, KosarOsszesites osszesites)
    {
        if (osszesites.Kuponkod is null) return;
        using var parancs = Parancs(adatbazis, tranzakcio, """
            INSERT INTO rendeles_kedvezmenyek (rendeles_azonosito, kuponkod, kedvezmeny)
            VALUES ($rendeles, $kod, $kedvezmeny)
            """, ("$rendeles", rendeles.ToString()), ("$kod", osszesites.Kuponkod),
            ("$kedvezmeny", osszesites.Kedvezmeny));
        parancs.ExecuteNonQuery();
    }
}

