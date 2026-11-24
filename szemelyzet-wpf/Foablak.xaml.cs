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
        UpdateControls();
        Closed += (_, _) => api?.Dispose();
    }

    private async void OnSignInClick(object kuldo, RoutedEventArgs esemeny)
    {
        await RunActionAsync(async () =>
        {
            var kulcs = SzemelyzetiKulcs.Password.Trim();
            if (kulcs.Length is < 16 or > 128 || kulcs.Any(karakter => karakter is < '!' or > '~'))
                throw new InvalidOperationException("Add meg a terminálban megjelenő személyzeti kulcsot.");
            var cim = SzemelyzetiApi.ValidateBaseAddress(SzerverCim.Text);
            var kapcsolat = new SzemelyzetiApi(cim, kulcs);
            try
            {
                var adatok = await kapcsolat.FetchDashboardAsync();
                api = kapcsolat;
                SzemelyzetiKulcs.Clear();
                DisplayData(adatok);
            }
            catch
            {
                kapcsolat.Dispose();
                throw;
            }
        }, "Kapcsolódva a rendelőrendszerhez.");
    }

    private async void OnRefreshClick(object kuldo, RoutedEventArgs esemeny) =>
        await RunActionAsync(RefreshAsync, "A listák frissültek.");

    private void OnSignOutClick(object kuldo, RoutedEventArgs esemeny)
    {
        api?.Dispose();
        api = null;
        SzemelyzetiKulcs.Clear();
        RendelesTabla.ItemsSource = null;
        EtelTabla.ItemsSource = null;
        JegyTabla.ItemsSource = null;
        AllapotUzenet.Text = "Kiléptél a személyzeti felületről.";
        UpdateControls();
    }

    private async void OnAdvanceOrderClick(object kuldo, RoutedEventArgs esemeny)
    {
        if (RendelesTabla.SelectedItem is Rendeles rendeles && kovetkezoAllapot is string allapot)
            await RunActionAsync(() => SaveAndRefreshAsync(() => api!.UpdateOrderAsync(rendeles.Azonosito, allapot)),
                "A rendelés állapota frissült.");
    }

    private async void OnCancelOrderClick(object kuldo, RoutedEventArgs esemeny)
    {
        if (RendelesTabla.SelectedItem is not Rendeles rendeles) return;
        if (MessageBox.Show(this, "Törlöd a kijelölt rendelést?", "Rendelés törlése",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        await RunActionAsync(() => SaveAndRefreshAsync(() => api!.UpdateOrderAsync(rendeles.Azonosito, "torolve")),
            "A rendelést töröltük.");
    }

    private async void OnToggleDishClick(object kuldo, RoutedEventArgs esemeny)
    {
        if (EtelTabla.SelectedItem is Etel etel)
            await RunActionAsync(() => SaveAndRefreshAsync(() => api!.UpdateDishAsync(etel.Azonosito, !etel.Elerheto)),
                "Az étel elérhetősége frissült.");
    }

    private async void OnSaveReplyClick(object kuldo, RoutedEventArgs esemeny)
    {
        if (JegyTabla.SelectedItem is Jegy jegy)
            await RunActionAsync(() => SaveAndRefreshAsync(() => api!.UpdateTicketAsync(jegy.Azonosito, "megvalaszolt", JegyValasz.Text)),
                "A válasz megjelent az ügyfélszolgálati oldalon.");
    }

    private async void OnCloseTicketClick(object kuldo, RoutedEventArgs esemeny)
    {
        if (JegyTabla.SelectedItem is Jegy jegy)
            await RunActionAsync(() => SaveAndRefreshAsync(() => api!.UpdateTicketAsync(jegy.Azonosito, "lezart", null)),
                "Az ügyet lezártuk.");
    }

    private async Task RunActionAsync(Func<Task> feladat, string siker)
    {
        if (foglalt) return;
        foglalt = true;
        AllapotUzenet.Text = "Feldolgozás…";
        UpdateControls();
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
            UpdateControls();
        }
    }

    private async Task SaveAndRefreshAsync(Func<Task> mentes)
    {
        await mentes();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (api is not null) DisplayData(await api.FetchDashboardAsync());
    }

    private void DisplayData(SzemelyzetiAdatok adatok)
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

    private void OnOrderSelected(object kuldo, SelectionChangedEventArgs esemeny)
    {
        RendelesReszletek.DataContext = RendelesTabla.SelectedItem;
        UpdateControls();
    }

    private void OnDishSelected(object kuldo, SelectionChangedEventArgs esemeny) => UpdateControls();

    private void OnTicketSelected(object kuldo, SelectionChangedEventArgs esemeny)
    {
        var jegy = JegyTabla.SelectedItem as Jegy;
        JegyReszletek.DataContext = jegy;
        JegyValasz.Text = jegy?.Valasz ?? "";
        UpdateControls();
    }

    private void UpdateControls()
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

