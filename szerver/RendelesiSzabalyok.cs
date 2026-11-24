using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Parazs;

internal sealed record EllenorzottRendeles(Guid KeresAzonosito, string Nev, string Telefonszam, string Megjegyzes,
    SortedDictionary<int, int> Mennyisegek, string Lenyomat);

internal static class RendelesiSzabalyok
{
    private static readonly Dictionary<string, string[]> Atmenetek = new(StringComparer.Ordinal)
    {
        ["beerkezett"] = ["keszul", "torolve"],
        ["keszul"] = ["atveheto", "torolve"],
        ["atveheto"] = ["atadva", "torolve"],
        ["atadva"] = [],
        ["torolve"] = [],
    };

    public static void AtmenetEllenorzese(string jelenlegi, string? kovetkezo)
    {
        if (kovetkezo is null || !Atmenetek.ContainsKey(kovetkezo))
            throw new KeresHiba(400, "Ismeretlen rendelési állapot.");
        if (jelenlegi != kovetkezo && !Atmenetek[jelenlegi].Contains(kovetkezo))
            throw new KeresHiba(409, "A kért állapot ebből az állapotból nem választható.");
    }

    public static EllenorzottRendeles Ellenorzes(RendelesBemenet bemenet)
    {
        var nev = SzovegMezo(bemenet.VendegNeve, "Név", 2, 80);
        var telefonszam = SzovegMezo(bemenet.Telefonszam, "Telefonszám", 7, 25);
        var megjegyzes = SzovegMezo(bemenet.Megjegyzes ?? "", "Megjegyzés", 0, 300);
        if (!Regex.IsMatch(telefonszam, @"^\+?[0-9 ().-]+$") || telefonszam.Count(char.IsAsciiDigit) is < 7 or > 15)
            throw new KeresHiba(400, "Adj meg érvényes telefonszámot.");
        if (bemenet.KeresAzonosito == Guid.Empty)
            throw new KeresHiba(400, "Érvényes kérésazonosító szükséges.");
        var mennyisegek = TetelekEllenorzese(bemenet.Tetelek);
        // A korábbi rendelések lenyomatát az eredeti mezőnevekkel kell újraszámolni.
        var lenyomat = JsonSerializer.SerializeToUtf8Bytes(new
        {
            name = nev, phone = telefonszam, note = megjegyzes, quantities = mennyisegek,
        });
        return new(bemenet.KeresAzonosito, nev, telefonszam, megjegyzes, mennyisegek,
            Convert.ToHexString(SHA256.HashData(lenyomat)));
    }

    public static SortedDictionary<int, int> TetelekEllenorzese(List<RendelesiTetelBemenet?>? tetelek)
    {
        if (tetelek is null || tetelek.Count is < 1 or > 30)
            throw new KeresHiba(400, "A rendelés 1–30 különböző tételt tartalmazhat.");

        var mennyisegek = new SortedDictionary<int, int>();
        foreach (var tetel in tetelek)
        {
            if (tetel is null || tetel.EtelAzonosito < 1 || tetel.Mennyiseg is < 1 or > 20)
                throw new KeresHiba(400, "Minden tételhez érvényes étel és 1–20 adag szükséges.");
            if (!mennyisegek.TryAdd(tetel.EtelAzonosito, tetel.Mennyiseg))
                throw new KeresHiba(400, "Egy étel csak egyszer szerepelhet a tételek között.");
        }
        return mennyisegek;
    }

    private static string SzovegMezo(string? ertek, string mezo, int also, int felso)
    {
        var tisztitott = ertek?.Trim();
        if (tisztitott is null || tisztitott.Length < also || tisztitott.Length > felso)
            throw new KeresHiba(400, $"{mezo} — a megengedett hossz {also}–{felso} karakter.");
        return tisztitott;
    }
}

