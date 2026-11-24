using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace Parazs;

public sealed record Vendegprofil(Guid Azonosito, string Nev, string Email, string Telefonszam);
public sealed record RegisztracioBemenet(string? Nev, string? Email, string? Telefonszam, string? Jelszo);
public sealed record BelepesBemenet(string? Email, string? Jelszo);
public sealed record ProfilModositasBemenet(string? Nev, string? Telefonszam);

public sealed partial class EttermiAdattar
{
    private static readonly PasswordHasher<string> JelszoTitkosito = new();
    private static readonly string MintaLenyomat = JelszoTitkosito.HashPassword("", Guid.NewGuid().ToString());

    public Vendegprofil CreateProfile(RegisztracioBemenet bemenet)
    {
        var email = NormalizeEmail(bemenet.Email);
        var nev = ValidateProfileName(bemenet.Nev);
        var telefonszam = ValidateProfilePhone(bemenet.Telefonszam);
        if (bemenet.Jelszo is null || bemenet.Jelszo.Length is < 10 or > 128)
            throw new KeresHiba(400, "A jelszó 10–128 karakter hosszú legyen.");
        var azonosito = Guid.NewGuid();
        var lenyomat = JelszoTitkosito.HashPassword(email, bemenet.Jelszo);

        using var adatbazis = OpenConnection();
        using var parancs = CreateCommand(adatbazis, null, """
            INSERT INTO profilok (azonosito, nev, email, telefonszam, jelszo_lenyomat)
            VALUES ($id, $nev, $email, $telefon, $lenyomat)
            ON CONFLICT(email) DO NOTHING
            """, ("$id", azonosito.ToString()), ("$nev", nev), ("$email", email), ("$telefon", telefonszam),
            ("$lenyomat", lenyomat));
        if (parancs.ExecuteNonQuery() == 0)
            throw new KeresHiba(409, "Ez az e-mail-cím már regisztrált.");
        return new(azonosito, nev, email, telefonszam);
    }

    public Vendegprofil AuthenticateProfile(BelepesBemenet bemenet)
    {
        var email = NormalizeEmail(bemenet.Email);
        if (bemenet.Jelszo is null || bemenet.Jelszo.Length is < 10 or > 128)
            throw new KeresHiba(401, "Hibás e-mail-cím vagy jelszó.");

        using var adatbazis = OpenConnection();
        using var parancs = CreateCommand(adatbazis, null, """
            SELECT azonosito, nev, email, telefonszam, jelszo_lenyomat FROM profilok WHERE email = $email
            """, ("$email", email));
        using var sor = parancs.ExecuteReader();
        var talalt = sor.Read();
        var lenyomat = talalt ? sor.GetString(4) : MintaLenyomat;
        var ellenorzes = JelszoTitkosito.VerifyHashedPassword(email, lenyomat, bemenet.Jelszo);
        if (!talalt || ellenorzes == PasswordVerificationResult.Failed)
            throw new KeresHiba(401, "Hibás e-mail-cím vagy jelszó.");
        return ReadProfile(sor);
    }

    public string CreateSession(Guid profilAzonosito)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var lejart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var adatbazis = OpenConnection();
        using var tranzakcio = adatbazis.BeginTransaction();
        using var torles = CreateCommand(adatbazis, tranzakcio, "DELETE FROM munkamenetek WHERE lejarat <= $most", ("$most", lejart));
        torles.ExecuteNonQuery();
        using var parancs = CreateCommand(adatbazis, tranzakcio, """
            INSERT INTO munkamenetek (lenyomat, profil_azonosito, lejarat) VALUES ($lenyomat, $profil, $lejarat)
            """, ("$lenyomat", HashToken(token)), ("$profil", profilAzonosito.ToString()),
            ("$lejarat", lejart + (long)ProfilVegpontok.MunkamenetIdotartama.TotalSeconds));
        parancs.ExecuteNonQuery();
        tranzakcio.Commit();
        return token;
    }

    public Vendegprofil? GetProfile(string? token)
    {
        if (token?.Length != 64) return null;
        using var adatbazis = OpenConnection();
        using var parancs = CreateCommand(adatbazis, null, """
            SELECT p.azonosito, p.nev, p.email, p.telefonszam
            FROM profilok p JOIN munkamenetek m ON p.azonosito = m.profil_azonosito
            WHERE m.lenyomat = $lenyomat AND m.lejarat > $most
            """, ("$lenyomat", HashToken(token)), ("$most", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        using var sor = parancs.ExecuteReader();
        return sor.Read() ? ReadProfile(sor) : null;
    }

    public Vendegprofil UpdateProfile(Vendegprofil profil, ProfilModositasBemenet bemenet)
    {
        var nev = ValidateProfileName(bemenet.Nev);
        var telefonszam = ValidateProfilePhone(bemenet.Telefonszam);
        using var adatbazis = OpenConnection();
        using var parancs = CreateCommand(adatbazis, null, """
            UPDATE profilok SET nev = $nev, telefonszam = $telefon WHERE azonosito = $id
            """, ("$nev", nev), ("$telefon", telefonszam), ("$id", profil.Azonosito.ToString()));
        parancs.ExecuteNonQuery();
        return profil with { Nev = nev, Telefonszam = telefonszam };
    }

    public void DeleteSession(string? token)
    {
        if (token?.Length != 64) return;
        using var adatbazis = OpenConnection();
        using var parancs = CreateCommand(adatbazis, null, "DELETE FROM munkamenetek WHERE lenyomat = $lenyomat",
            ("$lenyomat", HashToken(token)));
        parancs.ExecuteNonQuery();
    }

    internal static string NormalizeEmail(string? ertek)
    {
        var email = ertek?.Trim().ToLowerInvariant();
        if (email is null || email.Length > 254 || !MailAddress.TryCreate(email, out var cim) ||
            cim.Address != email || !cim.Host.Contains('.'))
            throw new KeresHiba(400, "Adj meg érvényes e-mail-címet.");
        return email;
    }

    private static string ValidateProfileName(string? ertek) =>
        RendelesiSzabalyok.ValidateText(ertek, "Név", 2, 80);

    private static string ValidateProfilePhone(string? ertek) =>
        string.IsNullOrWhiteSpace(ertek) ? "" : RendelesiSzabalyok.ValidatePhone(ertek);

    private static Vendegprofil ReadProfile(SqliteDataReader sor) =>
        new(Guid.Parse(sor.GetString(0)), sor.GetString(1), sor.GetString(2), sor.GetString(3));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
