namespace Parazs;

public static class ProfilVegpontok
{
    internal static readonly TimeSpan MunkamenetIdotartama = TimeSpan.FromDays(7);
    private const string SutiNev = "parazs_munkamenet";

    public static void MapProfileEndpoints(this RouteGroupBuilder api, EttermiAdattar adattar)
    {
        var profil = api.MapGroup("/profil");
        profil.MapGet("", (HttpContext kornyezet) => new { profil = GetCurrentProfile(kornyezet, adattar) });
        profil.MapPost("/regisztracio", (RegisztracioBemenet bemenet, HttpContext kornyezet) =>
        {
            var vendeg = adattar.CreateProfile(bemenet);
            SetSessionCookie(kornyezet, adattar, vendeg);
            return Results.Created("/api/profil", vendeg);
        }).RequireRateLimiting("belepes");
        profil.MapPost("/belepes", (BelepesBemenet bemenet, HttpContext kornyezet) =>
        {
            var vendeg = adattar.AuthenticateProfile(bemenet);
            SetSessionCookie(kornyezet, adattar, vendeg);
            return vendeg;
        }).RequireRateLimiting("belepes");
        profil.MapPatch("", (ProfilModositasBemenet bemenet, HttpContext kornyezet) =>
            adattar.UpdateProfile(RequireProfile(kornyezet, adattar), bemenet));
        profil.MapGet("/tarca", (HttpContext kornyezet) =>
            adattar.GetWallet(RequireProfile(kornyezet, adattar).Azonosito));
        profil.MapPost("/tarca/feltoltesek", (TarcaFeltoltesBemenet bemenet, HttpContext kornyezet) =>
            adattar.TopUpWallet(RequireProfile(kornyezet, adattar).Azonosito, bemenet));
        profil.MapGet("/rendelesek", (HttpContext kornyezet) =>
            adattar.GetOrderHistory(RequireProfile(kornyezet, adattar).Azonosito));
        profil.MapDelete("/munkamenet", (HttpContext kornyezet) =>
        {
            adattar.DeleteSession(kornyezet.Request.Cookies[SutiNev]);
            kornyezet.Response.Cookies.Delete(SutiNev, CreateCookieOptions(kornyezet));
            return Results.NoContent();
        });
    }

    public static Vendegprofil? GetCurrentProfile(HttpContext kornyezet, EttermiAdattar adattar) =>
        adattar.GetProfile(kornyezet.Request.Cookies[SutiNev]);

    public static Vendegprofil RequireProfile(HttpContext kornyezet, EttermiAdattar adattar) =>
        GetCurrentProfile(kornyezet, adattar) ?? throw new KeresHiba(401, "Ehhez a művelethez jelentkezz be.");

    private static void SetSessionCookie(HttpContext kornyezet, EttermiAdattar adattar, Vendegprofil profil)
    {
        adattar.DeleteSession(kornyezet.Request.Cookies[SutiNev]);
        var beallitas = CreateCookieOptions(kornyezet);
        beallitas.MaxAge = MunkamenetIdotartama;
        kornyezet.Response.Cookies.Append(SutiNev, adattar.CreateSession(profil.Azonosito), beallitas);
    }

    private static CookieOptions CreateCookieOptions(HttpContext kornyezet) => new()
    {
        HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Strict,
        Secure = kornyezet.Request.IsHttps, Path = "/",
    };
}

