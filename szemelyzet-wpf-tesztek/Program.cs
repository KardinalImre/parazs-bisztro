using Parazs.Szemelyzet;

static void Require(bool teljesult, string uzenet)
{
    if (!teljesult) throw new InvalidOperationException(uzenet);
}

var cim = SzemelyzetiApi.ValidateBaseAddress(args[0]);
var kulcs = Environment.GetEnvironmentVariable("PARAZS_TESZT_KULCS")
    ?? throw new InvalidOperationException("Hiányzik a tesztszerver kulcsa.");
var rendelesId = Guid.Parse(args[1]);
var jegyId = Guid.Parse(args[2]);
using var api = new SzemelyzetiApi(cim, kulcs);

var indulaskor = await api.FetchDashboardAsync();
var rendeles = indulaskor.Rendelesek.Single(tetel => tetel.Azonosito == rendelesId);
Require(rendeles.Allapot == "beerkezett" && rendeles.Tetelek.Count == 1, "A rendelés adatai hiányosak.");
Require(indulaskor.Etelek.Single(tetel => tetel.Azonosito == 1).Elerheto, "Hiányzik a választható étel.");
Require(indulaskor.Jegyek.Single(tetel => tetel.Azonosito == jegyId).Allapot == "nyitott", "Hiányzik a jegy.");

await api.UpdateOrderAsync(rendelesId, "keszul");
await api.UpdateDishAsync(1, false);
await api.UpdateTicketAsync(jegyId, "megvalaszolt", "A WPF-kliens próbaüzenete.");
var modositasUtan = await api.FetchDashboardAsync();
Require(modositasUtan.Rendelesek.Single(tetel => tetel.Azonosito == rendelesId).Allapot == "keszul",
    "A rendelési állapot frissítése sikertelen.");
Require(!modositasUtan.Etelek.Single(tetel => tetel.Azonosito == 1).Elerheto, "Az elrejtés sikertelen.");
Require(modositasUtan.Jegyek.Single(tetel => tetel.Azonosito == jegyId).Valasz == "A WPF-kliens próbaüzenete.",
    "Az ügyfélszolgálati válasz mentése sikertelen.");
await api.UpdateTicketAsync(jegyId, "lezart", null);

using var hibasKulcs = new SzemelyzetiApi(cim, "ervenytelen-probakulcs");
try
{
    await hibasKulcs.FetchDashboardAsync();
    throw new Exception("A hibás kulcs hozzáférést kapott.");
}
catch (InvalidOperationException hiba)
{
    Require(hiba.Message.Contains("személyzeti kulcs", StringComparison.Ordinal), "Hiányzik a szerver magyar hibaüzenete.");
}

try
{
    SzemelyzetiApi.ValidateBaseAddress("http://example.com");
    throw new Exception("A kliens elfogadta a titkosítatlan külső címet.");
}
catch (InvalidOperationException hiba)
{
    Require(hiba.Message.Contains("HTTPS", StringComparison.Ordinal), "Hiányzik a címmel kapcsolatos hibaüzenet.");
}
Console.WriteLine("A WPF-kliens API-műveletei sikeresek.");

