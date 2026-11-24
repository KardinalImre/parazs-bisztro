using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Parazs;

var epito = WebApplication.CreateBuilder(args);
if (string.IsNullOrWhiteSpace(epito.Configuration["urls"]))
    epito.WebHost.UseUrls("http://127.0.0.1:5080");
epito.WebHost.ConfigureKestrel(beallitasok => beallitasok.Limits.MaxRequestBodySize = 16 * 1024);
epito.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
epito.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

epito.Services.AddRateLimiter(beallitasok =>
{
    beallitasok.RejectionStatusCode = 429;
    beallitasok.AddPolicy("belepes", kornyezet => RateLimitPartition.GetFixedWindowLimiter(
        kornyezet.Connection.RemoteIpAddress?.ToString() ?? "helyi",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

var mappa = epito.Environment.ContentRootPath;
var alapertelmezettAdatbazis = Path.Combine(mappa, "adatok", "parazs.sqlite3");
var adatbazisUt = Path.GetFullPath(epito.Configuration["PARAZS_ADATBAZIS"] ??
    epito.Configuration["PARAZS_DB"] ?? AlapertelmezettAdatbazis());
var adattar = new EttermiAdattar(adatbazisUt);
adattar.Inicializalas(Path.Combine(AppContext.BaseDirectory, "adatbazis"));

var beallitottKulcs = epito.Configuration["PARAZS_SZEMELYZETI_KULCS"] ??
    epito.Configuration["PARAZS_ADMIN_KEY"];
if (beallitottKulcs is not null && (beallitottKulcs.Length is < 16 or > 128 ||
    beallitottKulcs.Any(karakter => karakter is < '!' or > '~')))
    throw new InvalidOperationException("A személyzeti kulcs 16–128 látható ASCII karakterből álljon.");
var szemelyzetiKulcs = beallitottKulcs ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
var elvartHitelesites = SHA256.HashData(Encoding.UTF8.GetBytes($"Bearer {szemelyzetiKulcs}"));

var alkalmazas = epito.Build();
alkalmazas.Lifetime.ApplicationStarted.Register(() =>
{
    var kiszolgalo = alkalmazas.Services.GetRequiredService<IServer>();
    var cimek = kiszolgalo.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
    foreach (var cim in cimek)
    {
        Console.WriteLine($"Kiszolgálás címe: {cim}");
        Console.WriteLine($"Személyzeti felület: {cim}/szemelyzet");
    }
    if (beallitottKulcs is null)
        Console.WriteLine($"Személyzeti kulcs: {szemelyzetiKulcs}");
    Console.WriteLine("Leállítás: Ctrl+C.");
});

alkalmazas.UseExceptionHandler(hiba => hiba.Run(kornyezet =>
    Results.Json(new { hiba = "Szerverhiba. Próbáld meg újra." }, statusCode: 500).ExecuteAsync(kornyezet)));
alkalmazas.UseStatusCodePages(async allapotKornyezet =>
{
    var kornyezet = allapotKornyezet.HttpContext;
    var kod = kornyezet.Response.StatusCode;
    var uzenet = kod switch
    {
        400 => "A kérés érvénytelen. Ellenőrizd a megadott adatokat.",
        404 => "A kért oldal vagy erőforrás nem található.",
        405 => "Ez a művelet itt nem érhető el.",
        413 => "A kérés túl nagy.",
        415 => "A kérés formátuma nem támogatott.",
        429 => "Túl sok kérés érkezett. Próbáld meg egy perc múlva.",
        _ => "A kérés sikertelen.",
    };
    if (kornyezet.Request.Path.StartsWithSegments("/api"))
        await Results.Json(new { hiba = uzenet }, statusCode: kod).ExecuteAsync(kornyezet);
    else
    {
        kornyezet.Response.ContentType = "text/plain; charset=utf-8";
        await kornyezet.Response.WriteAsync(uzenet);
    }
});
alkalmazas.Use(async (kornyezet, kovetkezo) =>
{
    kornyezet.Response.Headers["X-Content-Type-Options"] = "nosniff";
    kornyezet.Response.Headers["Referrer-Policy"] = "no-referrer";
    kornyezet.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; base-uri 'none'; frame-ancestors 'none'; object-src 'none'";
    if (kornyezet.Request.Path.StartsWithSegments("/api"))
        kornyezet.Response.Headers.CacheControl = "no-store";
    try
    {
        await kovetkezo(kornyezet);
    }
    catch (KeresHiba hiba) when (!kornyezet.Response.HasStarted)
    {
        await Results.Json(new { hiba = hiba.Message }, statusCode: hiba.AllapotKod).ExecuteAsync(kornyezet);
    }
});
alkalmazas.UseDefaultFiles();
alkalmazas.UseStaticFiles();
alkalmazas.UseRateLimiter();

var api = alkalmazas.MapGroup("/api");
api.ProfilVegpontokFelvetele(adattar);
api.MapGet("/etlap", () => adattar.EtlapLekerese());
api.MapPost("/rendelesek/osszesites", (OsszesitesBemenet bemenet) => new { vegosszeg = adattar.KosarOsszesitese(bemenet) });
api.MapPost("/rendelesek", (RendelesBemenet bemenet) =>
{
    var (rendeles, letrejott) = adattar.RendelesLetrehozasa(bemenet);
    return letrejott ? Results.Created($"/api/rendelesek/{rendeles.Azonosito}", rendeles) : Results.Ok(rendeles);
});
api.MapGet("/rendelesek/{azonosito:guid}", (Guid azonosito) => adattar.RendelesLekerese(azonosito));
var szemelyzet = api.MapGroup("/szemelyzet");
szemelyzet.AddEndpointFilter(async (kornyezet, kovetkezo) =>
{
    var fejlec = kornyezet.HttpContext.Request.Headers.Authorization.ToString();
    var kapottHitelesites = SHA256.HashData(Encoding.UTF8.GetBytes(fejlec));
    if (CryptographicOperations.FixedTimeEquals(kapottHitelesites, elvartHitelesites)) return await kovetkezo(kornyezet);
    kornyezet.HttpContext.Response.Headers.WWWAuthenticate = "Bearer";
    return Results.Json(new { hiba = "Érvénytelen vagy hiányzó személyzeti kulcs." }, statusCode: 401);
});
szemelyzet.MapGet("/rendelesek", () => adattar.RendelesekLekerese());
szemelyzet.MapGet("/etlap", () => adattar.EtlapLekerese(elfogyottakkal: true));
szemelyzet.MapPatch("/rendelesek/{azonosito:guid}", (Guid azonosito, AllapotBemenet bemenet) =>
    adattar.AllapotBeallitasa(azonosito, bemenet.Allapot));
szemelyzet.MapPatch("/etelek/{azonosito:int}", (int azonosito, ElerhetosegBemenet bemenet) =>
{
    adattar.ElerhetosegBeallitasa(azonosito, bemenet.Elerheto);
    return Results.NoContent();
});

alkalmazas.MapGet("/admin", () => Results.Redirect("/szemelyzet"));
alkalmazas.MapFallbackToFile("/szemelyzet", "index.html");
alkalmazas.MapFallbackToFile("/profil", "index.html");
alkalmazas.Run();

string AlapertelmezettAdatbazis()
{
    if (File.Exists(alapertelmezettAdatbazis)) return alapertelmezettAdatbazis;
    var regiAdatbazis = Path.Combine(mappa, "..", "server", "data", "parazs.sqlite3");
    return File.Exists(regiAdatbazis) ? regiAdatbazis : alapertelmezettAdatbazis;
}

