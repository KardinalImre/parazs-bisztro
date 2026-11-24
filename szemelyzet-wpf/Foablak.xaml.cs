using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Parazs.Szemelyzet;

public partial class Foablak : Window
{
    private SzemelyzetiApi? api;
    private bool foglalt;
    private string? kovetkezoAllapot;

    public Foablak()
    {
        InitializeComponent();
        FeluletFrissitese();
        Closed += (_, _) => api?.Dispose();
    }

    private async void Belepes(object kuldo, RoutedEventArgs esemeny)
    {
        await Muvelet(async () =>
        {
            var kulcs = SzemelyzetiKulcs.Password.Trim();
            if (kulcs.Length is < 16 or > 128 || kulcs.Any(karakter => karakter is < '!' or > '~'))
                throw new InvalidOperationException("Add meg a terminálban megjelenő személyzeti kulcsot.");
            var cim = SzemelyzetiApi.CimEllenorzese(SzerverCim.Text);
            var kapcsolat = new SzemelyzetiApi(cim, kulcs);
            try
            {
                var adatok = await kapcsolat.Frissites();
                api = kapcsolat;
                SzemelyzetiKulcs.Clear();
                AdatokMegjelenitese(adatok);
            }
            catch
            {
                kapcsolat.Dispose();
                throw;
            }
        }, "Kapcsolódva a rendelőrendszerhez.");
    }

    private async void Frissites(object kuldo, RoutedEventArgs esemeny) =>
        await Muvelet(AdatokFrissitese, "A listák frissültek.");

    private void Kilepes(object kuldo, RoutedEventArgs esemeny)
    {
        api?.Dispose();
        api = null;
        SzemelyzetiKulcs.Clear();
        RendelesTabla.ItemsSource = null;
        EtelTabla.ItemsSource = null;
        JegyTabla.ItemsSource = null;
        AllapotUzenet.Text = "Kiléptél a személyzeti felületről.";
        FeluletFrissitese();
    }

    private async void RendelesTovabb(object kuldo, RoutedEventArgs esemeny)
    {
        if (RendelesTabla.SelectedItem is Rendeles rendeles && kovetkezoAllapot is string allapot)
            await Muvelet(() => ValtozasMentese(() => api!.RendelesModositasa(rendeles.Azonosito, allapot)),
                "A rendelés állapota frissült.");
    }

    private async void RendelesTorlese(object kuldo, RoutedEventArgs esemeny)
    {
        if (RendelesTabla.SelectedItem is not Rendeles rendeles) return;
        if (MessageBox.Show(this, "Törlöd a kijelölt rendelést?", "Rendelés törlése",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        await Muvelet(() => ValtozasMentese(() => api!.RendelesModositasa(rendeles.Azonosito, "torolve")),
            "A rendelést töröltük.");
    }

    private async void EtelValtasa(object kuldo, RoutedEventArgs esemeny)
    {
        if (EtelTabla.SelectedItem is Etel etel)
            await Muvelet(() => ValtozasMentese(() => api!.EtelModositasa(etel.Azonosito, !etel.Elerheto)),
                "Az étel elérhetősége frissült.");
    }

    private async void JegyValaszMentese(object kuldo, RoutedEventArgs esemeny)
    {
        if (JegyTabla.SelectedItem is Jegy jegy)
            await Muvelet(() => ValtozasMentese(() => api!.JegyModositasa(jegy.Azonosito, "megvalaszolt", JegyValasz.Text)),
                "A válasz megjelent az ügyfélszolgálati oldalon.");
    }

    private async void JegyLezarasa(object kuldo, RoutedEventArgs esemeny)
    {
        if (JegyTabla.SelectedItem is Jegy jegy)
            await Muvelet(() => ValtozasMentese(() => api!.JegyModositasa(jegy.Azonosito, "lezart", null)),
                "Az ügyet lezártuk.");
    }

    private async Task Muvelet(Func<Task> feladat, string siker)
    {
        if (foglalt) return;
        foglalt = true;
        AllapotUzenet.Text = "Feldolgozás…";
        FeluletFrissitese();
        try
        {
            await feladat();
            AllapotUzenet.Text = siker;
        }
        catch (HttpRequestException) { AllapotUzenet.Text = "A szerver nem érhető el. Ellenőrizd a címet és a szerver indítását."; }
        catch (TaskCanceledException) { AllapotUzenet.Text = "A szerver válasza túl sokáig tartott. Frissítsd a listát."; }
        catch (JsonException) { AllapotUzenet.Text = "A szerver adatai nem olvashatók. Ellenőrizd az alkalmazás verzióját."; }
        catch (InvalidOperationException hiba) { AllapotUzenet.Text = hiba.Message; }
        finally
        {
            foglalt = false;
            FeluletFrissitese();
        }
    }

    private async Task ValtozasMentese(Func<Task> mentes)
    {
        await mentes();
        await AdatokFrissitese();
    }

    private async Task AdatokFrissitese()
    {
        if (api is not null) AdatokMegjelenitese(await api.Frissites());
    }

    private void AdatokMegjelenitese(SzemelyzetiAdatok adatok)
    {
        var rendeles = (RendelesTabla.SelectedItem as Rendeles)?.Azonosito;
        var etel = (EtelTabla.SelectedItem as Etel)?.Azonosito;
        var jegy = (JegyTabla.SelectedItem as Jegy)?.Azonosito;
        RendelesTabla.ItemsSource = adatok.Rendelesek;
        EtelTabla.ItemsSource = adatok.Etelek;
        JegyTabla.ItemsSource = adatok.Jegyek;
        RendelesTabla.SelectedItem = adatok.Rendelesek.Find(sor => sor.Azonosito == rendeles);
        EtelTabla.SelectedItem = adatok.Etelek.Find(sor => sor.Azonosito == etel);
        JegyTabla.SelectedItem = adatok.Jegyek.Find(sor => sor.Azonosito == jegy);
    }

    private void RendelesKijelolve(object kuldo, SelectionChangedEventArgs esemeny)
    {
        RendelesReszletek.DataContext = RendelesTabla.SelectedItem;
        FeluletFrissitese();
    }

    private void EtelKijelolve(object kuldo, SelectionChangedEventArgs esemeny) => FeluletFrissitese();

    private void JegyKijelolve(object kuldo, SelectionChangedEventArgs esemeny)
    {
        var jegy = JegyTabla.SelectedItem as Jegy;
        JegyReszletek.DataContext = jegy;
        JegyValasz.Text = jegy?.Valasz ?? "";
        FeluletFrissitese();
    }

    private void FeluletFrissitese()
    {
        var kapcsolodva = api is not null;
        var szerkesztheto = kapcsolodva && !foglalt;
        BelepesGomb.IsEnabled = !kapcsolodva && !foglalt;
        FrissitesGomb.IsEnabled = KilepesGomb.IsEnabled = szerkesztheto;
        SzerverCim.IsEnabled = SzemelyzetiKulcs.IsEnabled = !kapcsolodva && !foglalt;
        Lapok.Visibility = kapcsolodva ? Visibility.Visible : Visibility.Collapsed;
        BelepesSugo.Visibility = kapcsolodva ? Visibility.Collapsed : Visibility.Visible;
        RendelesTabla.IsEnabled = EtelTabla.IsEnabled = JegyTabla.IsEnabled = szerkesztheto;

        var rendeles = RendelesTabla.SelectedItem as Rendeles;
        (kovetkezoAllapot, RendelesKovetkezo.Content) = rendeles?.Allapot switch
        {
            "beerkezett" => ("keszul", "Elkészítés indítása"),
            "keszul" => ("atveheto", "Átvehetőre állítás"),
            "atveheto" => ("atadva", "Átadás rögzítése"),
            _ => ((string?)null, "Állapot módosítása"),
        };
        RendelesKovetkezo.IsEnabled = RendelesTorlesGomb.IsEnabled = szerkesztheto && kovetkezoAllapot is not null;
        RendelesReszletek.Visibility = rendeles is null ? Visibility.Hidden : Visibility.Visible;
        var etel = EtelTabla.SelectedItem as Etel;
        EtelGomb.Content = etel?.Elerheto == false ? "Visszakapcsolás" : "Elrejtés";
        EtelGomb.IsEnabled = szerkesztheto && etel is not null;
        var jegy = JegyTabla.SelectedItem as Jegy;
        JegyReszletek.Visibility = jegy is null ? Visibility.Hidden : Visibility.Visible;
        JegyValasz.IsReadOnly = !szerkesztheto || jegy?.Allapot == "lezart";
        ValaszGomb.IsEnabled = LezarasGomb.IsEnabled = szerkesztheto && jegy is { Allapot: not "lezart" };
    }
}

