namespace Parazs;

public static class ProfilVegpontok
{
    internal static readonly TimeSpan MunkamenetIdotartama = TimeSpan.FromDays(7);
    private const string SutiNev = "parazs_munkamenet";

    public static void ProfilVegpontokFelvetele(this RouteGroupBuilder api, EttermiAdattar adattar)
    {
        var profil = api.MapGroup("/profil");
        profil.MapGet("", (HttpContext kornyezet) => new { profil = AktualisProfil(kornyezet, adattar) });
        profil.MapPost("/regisztracio", (RegisztracioBemenet bemenet, HttpContext kornyezet) =>
        {
            var vendeg = adattar.ProfilLetrehozasa(bemenet);
            MunkamenetBeallitasa(kornyezet, adattar, vendeg);
            return Results.Created("/api/profil", vendeg);
        }).RequireRateLimiting("belepes");
        profil.MapPost("/belepes", (BelepesBemenet bemenet, HttpContext kornyezet) =>
        {
            var vendeg = adattar.ProfilBejelentkezese(bemenet);
            MunkamenetBeallitasa(kornyezet, adattar, vendeg);
            return vendeg;
        }).RequireRateLimiting("belepes");
        profil.MapPatch("", (ProfilModositasBemenet bemenet, HttpContext kornyezet) =>
            adattar.ProfilModositasa(SzuksegesProfil(kornyezet, adattar), bemenet));
        profil.MapGet("/rendelesek", (HttpContext kornyezet) =>
            adattar.RendelesiElozmenyek(SzuksegesProfil(kornyezet, adattar).Azonosito));
        profil.MapDelete("/munkamenet", (HttpContext kornyezet) =>
        {
            adattar.MunkamenetTorlese(kornyezet.Request.Cookies[SutiNev]);
            kornyezet.Response.Cookies.Delete(SutiNev, SutiBeallitas(kornyezet));
            return Results.NoContent();
        });
    }

    public static Vendegprofil? AktualisProfil(HttpContext kornyezet, EttermiAdattar adattar) =>
        adattar.ProfilLekerese(kornyezet.Request.Cookies[SutiNev]);

    public static Vendegprofil SzuksegesProfil(HttpContext kornyezet, EttermiAdattar adattar) =>
        AktualisProfil(kornyezet, adattar) ?? throw new KeresHiba(401, "Ehhez a művelethez jelentkezz be.");

    private static void MunkamenetBeallitasa(HttpContext kornyezet, EttermiAdattar adattar, Vendegprofil profil)
    {
        adattar.MunkamenetTorlese(kornyezet.Request.Cookies[SutiNev]);
        var beallitas = SutiBeallitas(kornyezet);
        beallitas.MaxAge = MunkamenetIdotartama;
        kornyezet.Response.Cookies.Append(SutiNev, adattar.MunkamenetLetrehozasa(profil.Azonosito), beallitas);
    }

    private static CookieOptions SutiBeallitas(HttpContext kornyezet) => new()
    {
        HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Strict,
        Secure = kornyezet.Request.IsHttps, Path = "/",
    };
}

