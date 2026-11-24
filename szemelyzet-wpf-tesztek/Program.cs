using Parazs.Szemelyzet;

static void Kovetel(bool teljesult, string uzenet)
{
    if (!teljesult) throw new InvalidOperationException(uzenet);
}

var cim = SzemelyzetiApi.CimEllenorzese(args[0]);
var kulcs = Environment.GetEnvironmentVariable("PARAZS_TESZT_KULCS")
    ?? throw new InvalidOperationException("Hiányzik a tesztszerver kulcsa.");
var rendelesId = Guid.Parse(args[1]);
var jegyId = Guid.Parse(args[2]);
using var api = new SzemelyzetiApi(cim, kulcs);

var indulaskor = await api.Frissites();
var rendeles = indulaskor.Rendelesek.Single(tetel => tetel.Azonosito == rendelesId);
Kovetel(rendeles.Allapot == "beerkezett" && rendeles.Tetelek.Count == 1, "A rendelés adatai hiányosak.");
Kovetel(indulaskor.Etelek.Single(tetel => tetel.Azonosito == 1).Elerheto, "Hiányzik a választható étel.");
Kovetel(indulaskor.Jegyek.Single(tetel => tetel.Azonosito == jegyId).Allapot == "nyitott", "Hiányzik a jegy.");

await api.RendelesModositasa(rendelesId, "keszul");
await api.EtelModositasa(1, false);
await api.JegyModositasa(jegyId, "megvalaszolt", "A WPF-kliens próbaüzenete.");
var modositasUtan = await api.Frissites();
Kovetel(modositasUtan.Rendelesek.Single(tetel => tetel.Azonosito == rendelesId).Allapot == "keszul",
    "A rendelési állapot frissítése sikertelen.");
Kovetel(!modositasUtan.Etelek.Single(tetel => tetel.Azonosito == 1).Elerheto, "Az elrejtés sikertelen.");
Kovetel(modositasUtan.Jegyek.Single(tetel => tetel.Azonosito == jegyId).Valasz == "A WPF-kliens próbaüzenete.",
    "Az ügyfélszolgálati válasz mentése sikertelen.");
await api.JegyModositasa(jegyId, "lezart", null);

using var hibasKulcs = new SzemelyzetiApi(cim, "ervenytelen-probakulcs");
try
{
    await hibasKulcs.Frissites();
    throw new Exception("A hibás kulcs hozzáférést kapott.");
}
catch (InvalidOperationException hiba)
{
    Kovetel(hiba.Message.Contains("személyzeti kulcs", StringComparison.Ordinal), "Hiányzik a szerver magyar hibaüzenete.");
}

try
{
    SzemelyzetiApi.CimEllenorzese("http://example.com");
    throw new Exception("A kliens elfogadta a titkosítatlan külső címet.");
}
catch (InvalidOperationException hiba)
{
    Kovetel(hiba.Message.Contains("HTTPS", StringComparison.Ordinal), "Hiányzik a címmel kapcsolatos hibaüzenet.");
}
Console.WriteLine("A WPF-kliens API-műveletei sikeresek.");

