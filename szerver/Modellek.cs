namespace Parazs;

public sealed record Kategoria(int Azonosito, string Nev);
public sealed record Etel(int Azonosito, int KategoriaAzonosito, string Nev, string Leiras, int Ar, bool Elerheto);
public sealed record Etlap(IReadOnlyList<Kategoria> Kategoriak, IReadOnlyList<Etel> Etelek);
public sealed record RendelesiTetelBemenet(int EtelAzonosito, int Mennyiseg);
public sealed record RendelesBemenet(Guid KeresAzonosito, string? VendegNeve, string? Telefonszam,
    string? Megjegyzes, List<RendelesiTetelBemenet?>? Tetelek, int? VartVegosszeg = null);
public sealed record OsszesitesBemenet(List<RendelesiTetelBemenet?>? Tetelek);
public sealed record RendelesiTetel(int EtelAzonosito, string EtelNeve, int Egysegar, int Mennyiseg);
public sealed record RendelesOsszesito(Guid Azonosito, string Allapot, int Vegosszeg, string Letrehozva,
    IReadOnlyList<RendelesiTetel> Tetelek);
public sealed record SzemelyzetiRendeles(Guid Azonosito, string Allapot, int Vegosszeg, string Letrehozva,
    IReadOnlyList<RendelesiTetel> Tetelek, string VendegNeve, string Telefonszam, string Megjegyzes)
{
    public RendelesOsszesito Vendegnek() => new(Azonosito, Allapot, Vegosszeg, Letrehozva, Tetelek);
}
public sealed record AllapotBemenet(string? Allapot);
public sealed record ElerhetosegBemenet(bool? Elerheto);
public sealed class KeresHiba(int allapotKod, string uzenet) : Exception(uzenet)
{
    public int AllapotKod { get; } = allapotKod;
}

