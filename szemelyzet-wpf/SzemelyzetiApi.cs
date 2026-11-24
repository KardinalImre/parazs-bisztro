using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Parazs.Szemelyzet;

public sealed record Rendeles(Guid Azonosito, string Allapot, int Vegosszeg, string Letrehozva,
    List<RendelesTetel> Tetelek, string VendegNeve, string Telefonszam, string Megjegyzes,
    int Kedvezmeny, string? Kuponkod, string FizetesiMod)
{
    public string AllapotNev => Allapot switch
    {
        "beerkezett" => "Beérkezett", "keszul" => "Készül", "atveheto" => "Átvehető",
        "atadva" => "Átadva", "torolve" => "Törölve", _ => Allapot,
    };
    public string Fizetes => FizetesiMod == "tarca" ? "Demópénztárcával fizetve" : "Fizetés átvételkor";
    public string Osszeg => Megjelenites.FormatCurrency(Vegosszeg);
    public string Idopont => DateTimeOffset.TryParse(Letrehozva, out var idopont)
        ? idopont.ToLocalTime().ToString("g", Megjelenites.Magyar) : Letrehozva;
}
public sealed record RendelesTetel(string EtelNeve, int Mennyiseg, int Egysegar)
{
    public string Felirat => $"{Mennyiseg} × {EtelNeve}  ·  {Megjelenites.FormatCurrency(Egysegar)} / adag";
}
public sealed record Etel(int Azonosito, string Nev, int Ar, string Leiras, bool Elerheto)
{
    public string Elerhetoseg => Elerheto ? "Rendelhető" : "Elrejtve";
    public string ArFelirat => Megjelenites.FormatCurrency(Ar);
}
public sealed record Etlap(List<Etel> Etelek);
public sealed record Jegy(Guid Azonosito, string Email, string Targy, string Uzenet,
    Guid? RendelesAzonosito, string Allapot, string Valasz)
{
    public string AllapotNev => Allapot switch
    {
        "nyitott" => "Beérkezett", "megvalaszolt" => "Megválaszolva", "lezart" => "Lezárva", _ => Allapot,
    };
}
public sealed record SzemelyzetiAdatok(List<Rendeles> Rendelesek, List<Etel> Etelek, List<Jegy> Jegyek);

internal static class Megjelenites
{
    internal static readonly CultureInfo Magyar = CultureInfo.GetCultureInfo("hu-HU");
    internal static string FormatCurrency(int osszeg) => osszeg.ToString("N0", Magyar) + " Ft";
}

public sealed class SzemelyzetiApi(Uri cim, string kulcs) : IDisposable
{
    private readonly HttpClient kliens = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        BaseAddress = cim, Timeout = TimeSpan.FromSeconds(8),
    };

    public static Uri ValidateBaseAddress(string ertek)
    {
        if (!Uri.TryCreate(ertek.Trim(), UriKind.Absolute, out var cim) ||
            (cim.Scheme != Uri.UriSchemeHttps && !(cim.IsLoopback && cim.Scheme == Uri.UriSchemeHttp)) ||
            cim.UserInfo.Length > 0 || cim.Query.Length > 0 || cim.Fragment.Length > 0 || cim.AbsolutePath != "/")
            throw new InvalidOperationException("A szerver címe helyi HTTP- vagy HTTPS-alapcím legyen, például http://127.0.0.1:5080.");
        return cim;
    }

    public async Task<SzemelyzetiAdatok> FetchDashboardAsync()
    {
        var rendelesek = ReadJsonAsync<List<Rendeles>>("rendelesek");
        var etlap = ReadJsonAsync<Etlap>("etlap");
        var jegyek = ReadJsonAsync<List<Jegy>>("jegyek");
        await Task.WhenAll(rendelesek, etlap, jegyek);
        return new(await rendelesek, (await etlap).Etelek, await jegyek);
    }

    public Task UpdateOrderAsync(Guid azonosito, string allapot) =>
        PatchAsync($"rendelesek/{azonosito}", new { allapot });

    public Task UpdateDishAsync(int azonosito, bool elerheto) =>
        PatchAsync($"etelek/{azonosito}", new { elerheto });

    public Task UpdateTicketAsync(Guid azonosito, string allapot, string? valasz) =>
        PatchAsync($"jegyek/{azonosito}", new { allapot, valasz });

    private async Task<T> ReadJsonAsync<T>(string utvonal)
    {
        using var valasz = await SendAsync(HttpMethod.Get, utvonal, null);
        return await valasz.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException("A szerver üres választ adott.");
    }

    private async Task PatchAsync(string utvonal, object tartalom)
    {
        using var valasz = await SendAsync(HttpMethod.Patch, utvonal, tartalom);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod modszer, string utvonal, object? tartalom)
    {
        using var keres = new HttpRequestMessage(modszer, "/api/szemelyzet/" + utvonal);
        keres.Headers.Authorization = new AuthenticationHeaderValue("Bearer", kulcs);
        if (tartalom is not null) keres.Content = JsonContent.Create(tartalom);
        var valasz = await kliens.SendAsync(keres);
        if (valasz.IsSuccessStatusCode) return valasz;
        using (valasz)
        {
            ApiErrorResponse? hiba = null;
            try { hiba = await valasz.Content.ReadFromJsonAsync<ApiErrorResponse>(); }
            catch (JsonException) { }
            catch (NotSupportedException) { }
            throw new InvalidOperationException(hiba?.Hiba ?? $"A kiszolgáló válaszkódja {(int)valasz.StatusCode}.");
        }
    }

    public void Dispose() => kliens.Dispose();
    private sealed record ApiErrorResponse(string? Hiba);
}

