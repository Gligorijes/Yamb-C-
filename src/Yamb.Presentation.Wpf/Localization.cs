using System.Globalization;
using System.Windows;

namespace Yamb.Presentation.Wpf;

public static class Localization
{
    private static ResourceDictionary? _dictionary;

    public static AppLanguage Language { get; private set; } = AppLanguage.Serbian;

    public static event EventHandler? LanguageChanged;

    public static string Text(string key)
    {
        var value = System.Windows.Application.Current?.TryFindResource(key);
        return value as string ?? key;
    }

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Text(key), args);

    public static void Apply(AppLanguage language)
    {
        Language = language;

        var application = System.Windows.Application.Current;
        if (application is null) return;

        var dictionary = new ResourceDictionary();
        foreach (var (key, translations) in Strings)
        {
            dictionary[key] = language == AppLanguage.English ? translations.English : translations.Serbian;
        }

        if (_dictionary is not null)
            application.Resources.MergedDictionaries.Remove(_dictionary);

        _dictionary = dictionary;
        application.Resources.MergedDictionaries.Add(dictionary);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    private static readonly Dictionary<string, (string Serbian, string English)> Strings = new()
    {
        ["L.Nav.Menu"] = ("Meni", "Menu"),
        ["L.Nav.NewGame"] = ("Nova partija", "New game"),
        ["L.Nav.Game"] = ("Partija", "Game"),
        ["L.Nav.SavedGames"] = ("Sačuvane partije", "Saved games"),
        ["L.Nav.Leaderboard"] = ("Najbolji rezultati", "High scores"),
        ["L.Nav.Settings"] = ("Podešavanja", "Settings"),
        ["L.Nav.Help"] = ("Pomoć", "Help"),
        ["L.Main.Fullscreen"] = ("Ceo ekran (F11)", "Fullscreen (F11)"),
        ["L.Main.ExitFullscreen"] = ("Izađi iz celog ekrana (F11)", "Exit fullscreen (F11)"),
        ["L.App.StartErrorTitle"] = ("Yamb nije mogao da se pokrene", "Yamb failed to start"),
        ["L.App.ErrorTitle"] = ("Greška", "Error"),
        ["L.Error.NameRequired"] = ("Potrebno je uneti ime igrača.", "At least one player name is required."),
        ["L.Error.NameEmpty"] = ("Ime igrača ne može biti prazno.", "Player names cannot be empty."),
        ["L.Error.PlayerRequired"] = ("Potreban je najmanje jedan igrač.", "At least one player is required."),
        ["L.Error.TooManyPlayers"] = ("Partija podržava najviše dva igrača.", "The game supports at most two players."),
        ["L.Error.ComputerNotReady"] = ("Igra protiv računara još nije dostupna.", "Playing against the computer is not available yet."),
        ["L.Error.SinglePlayerCount"] = ("Partija za jednog igrača zahteva tačno jednog igrača.", "Single player mode requires exactly one player."),
        ["L.Error.TwoPlayerCount"] = ("Partija za dva igrača zahteva tačno dva igrača.", "Two player mode requires exactly two players."),
        ["L.Error.RollFirst"] = ("Najpre bacite kockice.", "Roll the dice first."),
        ["L.Error.HoldsAfterFinal"] = ("Zadržane kockice se ne mogu menjati posle trećeg bacanja.", "Holds cannot be changed after the final roll."),
        ["L.Error.MaxRolls"] = ("Najviše tri bacanja u potezu. Prvo upišite rezultat.", "Maximum three rolls per turn. Write a score first."),
        ["L.Error.AnnouncePhase"] = ("Najava je moguća samo posle prvog bacanja.", "Announcement can only be made after the first roll."),
        ["L.Error.AlreadyAnnounced"] = ("U ovom potezu je već postavljena najava.", "A row has already been announced this turn."),
        ["L.Error.WriteBeforeRoll"] = ("Morate baciti kockice pre upisa rezultata.", "You must roll at least once before writing a score."),
        ["L.Error.OtherTurn"] = ("Na potezu je {0}.", "It is {0}'s turn."),
        ["L.Error.InvalidPlayer"] = ("Izabran je nevažeći igrač.", "The selected player is invalid."),
        ["L.Error.GameNotFound"] = ("Sačuvana partija nije pronađena.", "The saved game was not found."),
        ["L.Error.WriteAnnouncement"] = ("Najavili ste polje; ovog poteza možete upisati samo to polje u koloni Najava.", "You announced a cell; this turn you can only write that cell in the Announcement column."),
        ["L.Error.NotAvailable"] = ("Izabrano polje nije dostupno po trenutnim pravilima.", "The selected cell is not available under the current rules."),
        ["L.Error.MaximumColumn"] = ("Maksimalna kolona zahteva najveći mogući rezultat za izabrani red.", "The Maximum column requires the highest possible score for the selected row."),
        ["L.Error.AnnouncementFilled"] = ("To polje u koloni Najava je već popunjeno.", "That cell in the Announcement column is already filled."),
        ["L.Error.ColumnMissing"] = ("Izabrana kolona ne postoji na ovom listu.", "The selected column is not part of this sheet."),

        ["L.Menu.Eyebrow"] = ("GLAVNI MENI", "MAIN MENU"),
        ["L.Menu.Title"] = ("Spremni za partiju?", "Ready for a game?"),
        ["L.Menu.Subtitle"] = ("Izaberite sledeći potez.", "Choose your next move."),
        ["L.Menu.DiceCount"] = ("{0} kockica", "{0} dice"),
        ["L.Menu.ThemeChip"] = ("Tema: {0}", "Theme: {0}"),
        ["L.Menu.AutoOn"] = ("Automatsko računanje", "Auto scoring"),
        ["L.Menu.AutoOff"] = ("Ručno računanje", "Manual scoring"),
        ["L.Menu.NewGame"] = ("Nova partija", "New game"),
        ["L.Menu.QuickPlay"] = ("BRZO START", "QUICK PLAY"),
        ["L.Menu.NewGameSubtitle"] = ("Započnite igru za jednog ili dva igrača", "Start a game for one or two players"),
        ["L.Menu.Resume"] = ("Nastavi partiju", "Resume game"),
        ["L.Menu.ResumeSubtitle"] = ("Izaberite sačuvanu partiju", "Choose a saved game"),
        ["L.Menu.NoSaved"] = ("Nema sačuvane partije", "No saved game"),
        ["L.Menu.Settings"] = ("Podešavanja", "Settings"),
        ["L.Menu.SettingsSubtitle"] = ("Izgled, jezik i računanje", "Look, language and scoring"),
        ["L.Menu.Leaderboard"] = ("Najbolji rezultati", "High scores"),
        ["L.Menu.LeaderboardSubtitle"] = ("Rang lista na ovom računaru", "Rankings on this computer"),
        ["L.Menu.Help"] = ("Pomoć", "Help"),
        ["L.Menu.HelpSubtitle"] = ("Pravila i tok igre", "Rules and gameplay"),
        ["L.Menu.Exit"] = ("Kraj", "Exit"),
        ["L.Menu.ExitSubtitle"] = ("Zatvorite aplikaciju", "Close the app"),

        ["L.New.Eyebrow"] = ("NOVA PARTIJA", "NEW GAME"),
        ["L.New.Title"] = ("Podesite partiju u tri koraka", "Set up a game in three steps"),
        ["L.New.Subtitle"] = ("Najpre izaberite broj kolona, zatim broj igrača i na kraju imena.", "First choose the number of columns, then the number of players, and finally their names."),
        ["L.New.Step1"] = ("KORAK 1 · BROJ KOLONA", "STEP 1 · COLUMN COUNT"),
        ["L.New.Step1Title"] = ("Izaberite širinu tabele", "Choose the sheet size"),
        ["L.New.Step1Subtitle"] = ("Izaberite jednu od standardnih balkanskih tabli sa 4 do 10 kolona.", "Choose one of the standard Balkan sheets with 4 to 10 columns."),
        ["L.New.Columns4"] = ("kolone", "columns"),
        ["L.New.ColumnsMany"] = ("kolona", "columns"),
        ["L.New.Variant4"] = ("Klasik", "Classic"),
        ["L.New.Variant6"] = ("Standardna", "Standard"),
        ["L.New.Variant7"] = ("Sedmica", "Sevens"),
        ["L.New.Variant8"] = ("Proširena", "Extended"),
        ["L.New.Variant9"] = ("Nokaut", "Knockout"),
        ["L.New.Variant10"] = ("Puna tabla", "Full board"),
        ["L.New.Step2"] = ("KORAK 2 · BROJ IGRAČA", "STEP 2 · PLAYERS"),
        ["L.New.Step2Title"] = ("Koliko igrača igra?", "How many players?"),
        ["L.New.Step2Subtitle"] = ("U igri za dva igrača listovi se pune naizmenično, a pobednik je igrač sa većim ukupnim rezultatom.", "In a two-player game the sheets are filled alternately, and the player with the higher total wins."),
        ["L.New.OnePlayer"] = ("igrač", "player"),
        ["L.New.OnePlayerSubtitle"] = ("Sam protiv tabele", "Solo against the sheet"),
        ["L.New.TwoPlayers"] = ("igrača", "players"),
        ["L.New.TwoPlayersSubtitle"] = ("Naizmenični potezi", "Alternating turns"),
        ["L.New.Step3"] = ("KORAK 3 · IMENA", "STEP 3 · NAMES"),
        ["L.New.Step3Title"] = ("Unesite imena igrača", "Enter player names"),
        ["L.New.Step3Subtitle"] = ("Imena se prikazuju tokom partije i u tabeli najboljih rezultata.", "Names are shown during the game and on the high score table."),
        ["L.New.Player1"] = ("Ime igrača 1", "Player 1 name"),
        ["L.New.Player2"] = ("Ime igrača 2", "Player 2 name"),
        ["L.New.DefaultPlayer1"] = ("Igrač 1", "Player 1"),
        ["L.New.DefaultPlayer2"] = ("Igrač 2", "Player 2"),
        ["L.New.StepCount"] = ("Korak {0}/3", "Step {0}/3"),
        ["L.New.Back"] = ("Nazad", "Back"),
        ["L.New.Previous"] = ("Prethodni", "Previous"),
        ["L.New.Next"] = ("Dalje", "Next"),
        ["L.New.Start"] = ("Počni partiju", "Start game"),

        ["L.SavedGames.Eyebrow"] = ("SAČUVANE PARTIJE", "SAVED GAMES"),
        ["L.SavedGames.Title"] = ("Nastavite gde ste stali", "Continue where you left off"),
        ["L.SavedGames.Subtitle"] = ("Izaberite sačuvanu partiju ili obrišite one koje vam više ne trebaju.", "Choose a saved game or delete the ones you no longer need."),
        ["L.SavedGames.Meta"] = ("{0} kolona · {1} kockica", "{0} columns · {1} dice"),
        ["L.SavedGames.Turn"] = ("Na potezu: {0}", "Turn: {0}"),
        ["L.SavedGames.Progress"] = ("Popunjeno {0}/{1} polja", "{0}/{1} cells filled"),
        ["L.SavedGames.SavedAt"] = ("Sačuvano {0}", "Saved {0}"),
        ["L.SavedGames.Continue"] = ("Nastavi", "Continue"),
        ["L.SavedGames.Delete"] = ("Obriši", "Delete"),
        ["L.SavedGames.Back"] = ("Nazad", "Back"),
        ["L.SavedGames.EmptyTitle"] = ("Nema sačuvanih partija", "No saved games"),
        ["L.SavedGames.EmptyText"] = ("Započnite novu partiju i ona će se automatski sačuvati posle svakog poteza.", "Start a new game and it will be saved automatically after every move."),
        ["L.SavedGames.DeleteTitle"] = ("Obriši partiju?", "Delete game?"),
        ["L.SavedGames.DeleteText"] = ("Partija „{0}” biće trajno obrisana.", "The game “{0}” will be permanently deleted."),
        ["L.SavedGames.DeleteYes"] = ("Obriši", "Delete"),
        ["L.SavedGames.DeleteNo"] = ("Otkaži", "Cancel"),

        ["L.Settings.Eyebrow"] = ("PODEŠAVANJA", "SETTINGS"),
        ["L.Settings.Title"] = ("Prilagodite igru", "Customize the game"),
        ["L.Settings.Subtitle"] = ("Izaberite kockice, izgled, jezik i način računanja", "Choose the dice, look, language and scoring behavior"),
        ["L.Settings.Theme"] = ("TEMA", "THEME"),
        ["L.Settings.ThemeHint"] = ("Birajte boje, kontrast i atmosferu cele aplikacije.", "Choose the colors, contrast and mood of the entire app."),
        ["L.Settings.ThemeDark"] = ("Tamna", "Dark"),
        ["L.Settings.ThemeDarkSubtitle"] = ("Podrazumevana", "Default"),
        ["L.Settings.ThemeLight"] = ("Svetla", "Light"),
        ["L.Settings.ThemeLightSubtitle"] = ("Visok kontrast", "High contrast"),
        ["L.Settings.ThemeMidnight"] = ("Ponoć", "Midnight"),
        ["L.Settings.ThemeMidnightSubtitle"] = ("Plavo noćno nebo", "Deep blue night"),
        ["L.Settings.ThemeEmerald"] = ("Smaragd", "Emerald"),
        ["L.Settings.ThemeEmeraldSubtitle"] = ("Zelena šuma", "Forest green"),
        ["L.Settings.ThemeSunset"] = ("Zalazak", "Sunset"),
        ["L.Settings.ThemeSunsetSubtitle"] = ("Topla narandžasta", "Warm amber glow"),
        ["L.Settings.ThemeRose"] = ("Roze", "Rose"),
        ["L.Settings.ThemeRoseSubtitle"] = ("Meke ružičaste nijanse", "Soft berry light"),
        ["L.Settings.Face"] = ("IZGLED LICA", "DICE FACES"),
        ["L.Settings.Pips"] = ("Tačkice", "Pips"),
        ["L.Settings.PipsSubtitle"] = ("Klasičan izgled kockice", "Classic dice look"),
        ["L.Settings.Numbers"] = ("Brojevi", "Numbers"),
        ["L.Settings.NumbersSubtitle"] = ("Vrednost ispisana brojem", "Value shown as a number"),
        ["L.Settings.Roman"] = ("Rimski brojevi", "Roman numerals"),
        ["L.Settings.RomanSubtitle"] = ("Vrednost ispisana rimskim brojem", "Value shown in Roman numerals"),
        ["L.Settings.Language"] = ("JEZIK", "LANGUAGE"),
        ["L.Settings.AutoCalculate"] = ("AUTOMATSKO RAČUNANJE", "AUTO CALCULATION"),
        ["L.Settings.AutoCalculateSubtitle"] = ("Prikazuje zbirove sekcija i ukupan rezultat tokom partije.", "Shows section totals and the overall result during the game."),
        ["L.Settings.AutoCalculateOption"] = ("Prikaži zbirove sekcija i ukupan rezultat tokom partije", "Show section totals and the overall result during the game"),
        ["L.Settings.DiceCount"] = ("BROJ KOCKICA", "DICE COUNT"),
        ["L.Settings.DiceFive"] = ("5", "5"),
        ["L.Settings.DiceSix"] = ("6", "6"),
        ["L.Settings.DiceUnit"] = ("kockica", "dice"),
        ["L.Settings.Size"] = ("VELIČINA KOCKICA", "DICE SIZE"),
        ["L.Settings.Preview"] = ("Primer prikaza kockica.", "Preview of the dice."),
        ["L.Settings.StraightInfo"] = ("Kenta: iz 1. bacanja 66 poena, iz 2. bacanja 56 poena, iz 3. bacanja 46 poena.", "Straight: 66 points from the first roll, 56 from the second, 46 from the third."),
        ["L.Settings.Reset"] = ("Vrati podrazumevano", "Reset to defaults"),
        ["L.Settings.ResetTitle"] = ("Vraćanje podešavanja", "Reset settings"),
        ["L.Settings.ResetConfirm"] = ("Da li želite da vratite sva podešavanja na podrazumevane vrednosti?", "Do you want to reset all settings to their default values?"),
        ["L.Settings.SaveErrorTitle"] = ("Podešavanja nisu sačuvana", "Settings were not saved"),
        ["L.Settings.SaveError"] = ("Došlo je do greške pri čuvanju podešavanja: {0}", "An error occurred while saving settings: {0}"),
        ["L.Settings.LoadErrorTitle"] = ("Podešavanja nisu učitana", "Settings were not loaded"),
        ["L.Settings.LoadError"] = ("Podešavanja su vraćena na podrazumevane vrednosti. Detalji: {0}", "Settings were restored to default values. Details: {0}"),
        ["L.Settings.Back"] = ("Nazad", "Back"),

        ["L.Leaderboard.Eyebrow"] = ("RANG LISTA", "LEADERBOARD"),
        ["L.Leaderboard.Title"] = ("Najbolji rezultati", "High scores"),
        ["L.Leaderboard.Subtitle"] = ("Pedeset najboljih rezultata na ovom računaru", "Top fifty scores on this computer"),
        ["L.Leaderboard.Rank"] = ("MESTO", "RANK"),
        ["L.Leaderboard.Players"] = ("IGRAČI", "PLAYERS"),
        ["L.Leaderboard.Score"] = ("SKOR", "SCORE"),
        ["L.Leaderboard.Date"] = ("DATUM", "DATE"),
        ["L.Leaderboard.Empty"] = ("Još nema rezultata.", "No scores yet."),
        ["L.Leaderboard.Back"] = ("Nazad", "Back"),
        ["L.Leaderboard.Clear"] = ("Obriši rezultate", "Clear scores"),
        ["L.Leaderboard.ClearTitle"] = ("Brisanje rezultata", "Clear scores"),
        ["L.Leaderboard.ClearText"] = ("Da li ste sigurni da želite da obrišete sve rezultate?", "Are you sure you want to delete all scores?"),
        ["L.Leaderboard.No"] = ("Ne", "No"),
        ["L.Leaderboard.Yes"] = ("Da", "Yes"),

        ["L.Help.Eyebrow"] = ("POMOĆ", "HELP"),
        ["L.Help.Title"] = ("Pravila i tok igre", "Rules and gameplay"),
        ["L.Help.Subtitle"] = ("Sve o redovima, kolonama, bonusima i cilju partije", "Everything about rows, columns, bonuses and the goal"),
        ["L.Help.Search"] = ("PRETRAGA", "SEARCH"),
        ["L.Help.NoResults"] = ("Nema rezultata za uneti pojam.", "No results for that search."),
        ["L.Help.Contents"] = ("SADRŽAJ", "CONTENTS"),
        ["L.Help.Back"] = ("Nazad", "Back"),
        ["L.Help.Previous"] = ("< Prethodno", "< Previous"),
        ["L.Help.Next"] = ("Sledeće >", "Next >"),
        ["L.Help.Rows"] = ("Redovi", "Rows"),
        ["L.Help.Columns"] = ("Kolone", "Columns"),
        ["L.Help.Bonuses"] = ("Bonusi", "Bonuses"),
        ["L.Help.Goal"] = ("Cilj igre", "Goal"),
        ["L.Help.About"] = ("O igri", "About"),
        ["L.Help.VersionLabel"] = ("VERZIJA APLIKACIJE", "APP VERSION"),
        ["L.Help.RowsPage"] = (
            """
            REDOVI

            1, 2, 3, 4, 5, 6 - zbir svih kockica koje pokazuju taj broj.
            Max - zbir najboljih 5 kockica.
            Min - zbir najlošijih 5 kockica.

            Kenta - pet kockica u nizu (1-2-3-4-5 ili 2-3-4-5-6):
              iz prvog bacanja 66 poena,
              iz drugog bacanja 56 poena,
              iz trećeg bacanja 46 poena.

            Triling - tri iste kockice + 20 poena.
            Ful - tri iste + dve iste + 30 poena.
            Poker - četiri iste kockice + 40 poena.
            Jamb - pet istih kockica + 50 poena.

            Baca se 6 kockica, ali se uvek računa samo 5.
            """,
            """
            ROWS

            1, 2, 3, 4, 5, 6 - the sum of all dice showing that number.
            Max - the sum of the best 5 dice.
            Min - the sum of the worst 5 dice.

            Straight - five dice in sequence (1-2-3-4-5 or 2-3-4-5-6):
              from the first roll 66 points,
              from the second roll 56 points,
              from the third roll 46 points.

            Three of a kind - three equal dice + 20 points.
            Full house - three equal + two equal + 30 points.
            Four of a kind - four equal dice + 40 points.
            Yamb - five equal dice + 50 points.

            Six dice are rolled, but only 5 are always counted.
            """),
        ["L.Help.ColumnsPage"] = (
            """
            KOLONE

            Svaka kolona ima 13 redova, ali svaka ima svoje pravilo
            popunjavanja. Redosled ispod odgovara redosledu kolona na listu.

            1. DOLE
               Popunjava se strogo odozgo nadole:
               1, 2, 3, 4, 5, 6, Max, Min, Kenta, Triling, Ful, Poker, Jamb.
               U svakom potezu dostupan je samo prvi prazan red i
               preskakanje nije moguće. Rane redove morate da popunite
               lošijim bacanjima, a Jamb dolazi tek na kraju.

            2. SLOBODNA
               U svakom potezu možete upisati bilo koji prazan red.
               Najfleksibilnija kolona; čuvajte je za jaka bacanja,
               bonus 1 i redove koje teško pogađate.

            3. OZGO
               Popunjava se strogo odozdo nagore:
               Jamb, Poker, Ful, Triling, Kenta, Min, Max, 6, 5, 4, 3, 2, 1.
               Kraj kolone je red 1, zato rano obezbedite dobru jedinicu.

            4. NAJAVA
               Posle prvog bacanja kliknite polje u ovoj koloni da biste
               ga najavili. Najavljeno polje je označeno i samo ono sme
               da se upiše u ovom potezu, u koloni Najava. Najava se ne
               može promeniti. Posle trećeg bacanja označeno polje
               ostaje spremno, a upisujete ga ručno klikom. Najavljujte
               red koji realno možete da ostvarite tim bacanjem.

            5. RUČNO
               Upis je dozvoljen samo u potezu u kome su sve kockice
               bačene odjednom, bez zadržavanja pre poslednjeg bacanja.
               Ako ste zadržali makar jednu kockicu i ponovo bacali,
               ova kolona u tom potezu nije dostupna. Koristite je kada
               vam treba potpuno nov set kockica.

            6. ODOZGO I ODOZDO
               U svakom potezu birate između prvog praznog reda odozgo
               (1, 2, 3, ...) i prvog praznog reda odozdo (Jamb, Poker,
               ...). Time ubrzavate popunjavanje i lakše izbegavate
               upis lošeg rezultata u važan red.

            7. OD SREDINE
               Birate između prvog praznog reda nagore od Max:
               Max, 6, 5, 4, 3, 2, 1,
               i prvog praznog reda nadole od Min:
               Min, Kenta, Triling, Ful, Poker, Jamb.
               Redovi 1 i Jamb dolaze na kraju svojih nizova, zato ih
               planirajte unapred.

            8. DIRIGOVANA
               Red je određen poslednjom najavom. U igri za dva igrača
               to je red koji je protivnik poslednji najavio, a u igri
               za jednog igrača vaš poslednji najavljeni red. Ako je taj
               red već popunjen, ova kolona u tom potezu nema dostupno
               polje. Zato najavu birate i zbog protivnika.

            9. OBAVEZNA
               Zaključana je dok sve ostale kolone osim Obavezne i
               Maksimalne nisu popunjene. Kada se otključa, popunjava se
               strogo odozgo nadole, kao kolona DOLE. Tada obično ostaje
               malo poteza, pa joj ostavite redove koje sigurno možete
               da ostvarite.

            10. MAKSIMALNA
                Možete izabrati bilo koji prazan red, ali upis je
                dozvoljen samo ako bacanje daje teorijski maksimum za
                taj red. Na primer: 1 = 5, 2 = 10, 3 = 15, 4 = 20,
                5 = 25, 6 = 30, Max = 30, Min = 5, Kenta = 66,
                Triling = 38, Ful = 48, Poker = 64, Jamb = 80.
                Ako bacanje nije maksimalno, polje se ne može upisati.
            """,
            """
            COLUMNS

            Every column has 13 rows, but each one follows its own
            filling rule. The order below matches the sheet columns.

            1. DOWN
               Filled strictly from top to bottom:
               1, 2, 3, 4, 5, 6, Max, Min, Straight, Three of a kind,
               Full house, Four of a kind, Yamb.
               Only the first empty row is available each turn and
               skipping is not possible. Early rows must absorb weaker
               rolls, while Yamb comes only at the end.

            2. FREE
               Any empty row can be written in any turn. This is the
               most flexible column; save it for strong rolls, bonus 1
               and rows that are hard to hit.

            3. UP
               Filled strictly from bottom to top:
               Yamb, Four of a kind, Full house, Three of a kind,
               Straight, Min, Max, 6, 5, 4, 3, 2, 1.
               The column ends with row 1, so secure a good 1 early.

            4. ANNOUNCEMENT
               After the first roll, click a cell in this column to
               announce it. The announced cell is marked and only it
               may be written this turn, in the Announcement column.
               The announcement cannot be changed. After the third roll
               the marked cell stays ready and you write it manually by
               clicking it. Announce a row you can realistically achieve
               with the current dice.

            5. HAND
               A score may be written only in a turn where all dice were
               rolled at once, without holding any die before the final
               roll. If you held even one die and rolled again, this
               column is unavailable that turn. Use it when you need a
               completely fresh set of dice.

            6. BOTH ENDS
               Each turn you choose between the first empty row from the
               top (1, 2, 3, ...) and the first empty row from the bottom
               (Yamb, Four of a kind, ...). This speeds up the column and
               helps avoid writing a weak score into an important row.

            7. MIDDLE OUT
               Choose between the first empty row upwards from Max:
               Max, 6, 5, 4, 3, 2, 1,
               and the first empty row downwards from Min:
               Min, Straight, Three of a kind, Full house,
               Four of a kind, Yamb.
               Rows 1 and Yamb come at the end of their chains, so plan
               them in advance.

            8. DIRECTED
               The row is determined by the latest announcement. In a
               two-player game it is the row the opponent announced
               last; in a single-player game it is your own last
               announced row. If that row is already filled, this
               column has no available cell that turn. Your announcement
               therefore also affects the opponent.

            9. OBLIGATORY
               Locked until every other column except Obligatory and
               Maximum is complete. When it unlocks, it is filled
               strictly from top to bottom, like the DOWN column. Usually
               few turns remain, so leave rows there that you can score
               reliably.

            10. MAXIMUM
                Any empty row can be selected, but the score may be
                written only if the roll produces the theoretical maximum
                for that row. For example: 1 = 5, 2 = 10, 3 = 15, 4 = 20,
                5 = 25, 6 = 30, Max = 30, Min = 5, Straight = 66,
                Three of a kind = 38, Full house = 48,
                Four of a kind = 64, Yamb = 80.
                If the roll is not maximal, the cell cannot be written.
            """),
        ["L.Help.BonusesPage"] = (
            """
            BONUSI

            Bonus 1 - zbir redova 1-6 u koloni. Ako je zbir 60 ili
              više, dobija se još 30 poena.
            Bonus 2 - razlika između Max i Min pomnožena vrednošću
              reda 1 u istoj koloni. Može biti i negativna.
            Bonus 3 - zbir Kente, Trilinga, Fula, Pokera i Jamba
              u koloni.

            Rezultat svake sekcije upisuje se u red Suma, a zbir svih
            kolona za tu sekciju u kolonu YAMB.
            """,
            """
            BONUSES

            Bonus 1 - the sum of rows 1-6 in a column. If the sum is 60
              or more, another 30 points are awarded.
            Bonus 2 - the difference between Max and Min multiplied by the
              value of row 1 in the same column. It can be negative.
            Bonus 3 - the sum of Straight, Three of a kind, Full house,
              Four of a kind and Yamb in a column.

            Each section's result is written in its Sum row, and the sum
            of all columns for that section in the YAMB column.
            """),
        ["L.Help.GoalPage"] = (
            """
            CILJ IGRE

            Popunite sva polja na svom listu i ostvarite
            što veći ukupan rezultat.

            Ukupan rezultat (SVE UKUPNO) je zbir svih upisanih polja i
            svih ostvarenih bonusa.

            U igri za 2 igrača pobeđuje igrač sa većim ukupnim
            rezultatom.
            """,
            """
            GOAL

            Fill every cell on your sheet and score as many points as
            possible.

            The total result (TOTAL) is the sum of all written cells and
            all earned bonuses.

            In a two-player game, the player with the higher total wins.
            """),
        ["L.Help.AboutPage"] = (
            """
            O IGRI

            Jamb je igra sa kockicama za jednog ili dva igrača. Cilj je
            da popunite sve kolone svog lista i ostvarite što veći
            ukupan rezultat.

            U potezu bacate kockice do tri puta. Između bacanja možete
            zadržati kockice koje vam odgovaraju. Posle bacanja upisujete
            rezultat u jedno dostupno polje. Kada su sve kolone popunjene,
            igra se završava i sabiraju se svi poeni i bonusi.

            List može imati 4, 6, 7, 8, 9 ili 10 kolona, u zavisnosti od
            izabranog pravila. Podrazumevana verzija ima 7 kolona. Igra
            može koristiti 6 kockica, pri čemu se za Max, Min i
            kombinacije računa 5 kockica, ili 5 kockica kada sve ulaze u
            rezultat.

            Rezultati se čuvaju lokalno, a najbolji rezultati se prikazuju
            na rang listi. Podešavanja pamte temu, jezik, veličinu i broj
            kockica, izgled lica i način računanja.

            TRENUTNA VERZIJA
            {0}
            """,
            """
            ABOUT

            Yamb is a dice game for one or two players. The goal is to
            fill every column on your sheet and achieve the highest
            possible total score.

            On a turn you roll the dice up to three times. Between rolls
            you may hold the dice you want to keep. After rolling you
            write the result into one available cell. When every column
            is filled, the game ends and all points and bonuses are
            added up.

            A sheet can have 4, 6, 7, 8, 9 or 10 columns depending on the
            selected rules. The default game uses 7 columns. The game can
            use 6 dice, where Max, Min and combinations count 5 dice, or
            5 dice where all dice count.

            Results are stored locally, and the best scores are shown on
            the leaderboard. Settings remember the theme, language, die
            size and count, face style and scoring method.

            CURRENT VERSION
            {0}
            """),

        ["L.Game.SinglePlayer"] = ("igra za jednog igrača", "single player game"),
        ["L.Game.TwoPlayer"] = ("igra za 2 igrača", "two player game"),
        ["L.Game.Menu"] = ("Meni", "Menu"),
        ["L.Game.PausedTitle"] = ("Zaustavljeno", "Paused"),
        ["L.Game.PausedText"] = ("Da li ste sigurni da želite da prekinete trenutnu partiju i vratiti se u glavni meni?", "Are you sure you want to stop the current game and return to the main menu?"),
        ["L.Game.Yes"] = ("Da", "Yes"),
        ["L.Game.No"] = ("Ne", "No"),
        ["L.Game.NotAvailable"] = ("Polje nije na redu.", "That cell is not available."),
        ["L.Game.CannotWrite"] = ("Ne možete da upišete.", "You cannot write there."),
        ["L.Game.Ok"] = ("U redu", "OK"),
        ["L.Game.GameOver"] = ("Kraj igre", "Game over"),
        ["L.Game.Continue"] = ("Nastavi", "Continue"),
        ["L.Game.HandYambTitle"] = ("Neverovatna sreća!", "Incredible luck!"),
        ["L.Game.HandYambText"] = ("Ručni Jamb! Postigli ste najveći podvig u koloni Ručno. Čestitamo!", "Hand Yamb! You achieved the ultimate feat in the Hand column. Congratulations!"),
        ["L.Game.SheetComplete"] = ("Popunili ste ceo list!", "You filled the whole sheet!"),
        ["L.Game.Winner"] = ("{0} pobeđuje!", "{0} wins!"),
        ["L.Game.Draw"] = ("Nerešeno!", "Draw!"),
        ["L.Game.PlayerResult"] = ("{0}: {1} poena", "{0}: {1} points"),
        ["L.Game.Turn"] = ("Na potezu: {0}", "Turn: {0}"),
        ["L.Game.Shortcuts"] = ("R / razmak - bacanje  ·  1-6 - zadrži  ·  Enter - upiši  ·  F1 - pomoć", "R / Space - roll  ·  1-6 - hold  ·  Enter - write  ·  F1 - help"),
        ["L.Game.Progress"] = ("popunjeno polja: {0}/{1}", "filled cells: {0}/{1}"),
        ["L.Game.Rolls0"] = ("0 bacanja", "0 rolls"),
        ["L.Game.Rolls1"] = ("1 bacanje", "1 roll"),
        ["L.Game.RollsN"] = ("{0} bacanja", "{0} rolls"),
        ["L.Game.Status"] = ("STATUS", "STATUS"),
        ["L.Game.Dice"] = ("KOCKICE", "DICE"),
        ["L.Game.DiceHint"] = ("Kliknite kockicu da je zadržite između bacanja.", "Click a die to hold it between rolls."),
        ["L.Game.Result"] = ("SVE UKUPNO", "GRAND TOTAL"),
        ["L.Game.Active"] = ("NA POTEZU", "ACTIVE"),
        ["L.Game.Roll"] = ("Bacaj", "Roll"),
        ["L.Game.Sum"] = ("SUMA", "SUM"),
        ["L.Game.HoldTooltip"] = ("Držite - kliknite da otpustite", "Held - click to release"),
        ["L.Game.HoldHint"] = ("Kliknite da zadržite", "Click to hold"),

        ["L.Game.Column.Downward.Name"] = ("Dole", "Down"),
        ["L.Game.Column.Downward.Tooltip"] = ("Popunjava se odozgo nadole", "Filled from top to bottom"),
        ["L.Game.Column.Free.Name"] = ("Slobodna", "Free"),
        ["L.Game.Column.Free.Tooltip"] = ("Bilo koji redosled", "Any order"),
        ["L.Game.Column.Upward.Name"] = ("Ozgo", "Up"),
        ["L.Game.Column.Upward.Tooltip"] = ("Popunjava se odozdo nagore", "Filled from bottom to top"),
        ["L.Game.Column.Announcement.Name"] = ("Najava", "Announcement"),
        ["L.Game.Column.Announcement.Tooltip"] = ("Najava posle prvog bacanja", "Announce after the first roll"),
        ["L.Game.Column.Hand.Name"] = ("Ručno", "Hand"),
        ["L.Game.Column.Hand.Tooltip"] = ("Ručno – kada su sve kockice bačene", "Hand - when all dice are rolled at once"),
        ["L.Game.Column.BothEnds.Name"] = ("Odozgo i odozdo", "Both ends"),
        ["L.Game.Column.BothEnds.Tooltip"] = ("Naizmenično odozgo i odozdo", "Alternately from the top and the bottom"),
        ["L.Game.Column.MiddleOut.Name"] = ("Od sredine", "Middle out"),
        ["L.Game.Column.MiddleOut.Tooltip"] = ("Naizmenično od sredine nagore i nadole", "Alternately from the middle upwards and downwards"),
        ["L.Game.Column.Directed.Name"] = ("Dirigovana", "Directed"),
        ["L.Game.Column.Directed.Tooltip"] = ("Dirigovana – red iz protivnikove najave", "Directed - the row from the opponent's announcement"),
        ["L.Game.Column.Obligatory.Name"] = ("Obavezna", "Obligatory"),
        ["L.Game.Column.Obligatory.Tooltip"] = ("Obavezna – otključava se na kraju", "Obligatory - unlocked at the end"),
        ["L.Game.Column.Maximum.Name"] = ("Maksimalna", "Maximum"),
        ["L.Game.Column.Maximum.Tooltip"] = ("Samo maksimalni rezultat za izabrani red", "Only the maximum score for the chosen row"),

        ["L.Game.Row.Ones"] = ("1", "1"),
        ["L.Game.Row.Twos"] = ("2", "2"),
        ["L.Game.Row.Threes"] = ("3", "3"),
        ["L.Game.Row.Fours"] = ("4", "4"),
        ["L.Game.Row.Fives"] = ("5", "5"),
        ["L.Game.Row.Sixes"] = ("6", "6"),
        ["L.Game.Row.Max"] = ("Max", "Max"),
        ["L.Game.Row.Min"] = ("Min", "Min"),
        ["L.Game.Row.Straight"] = ("Kenta", "Straight"),
        ["L.Game.Row.ThreeOfAKind"] = ("Triling", "3 of a kind"),
        ["L.Game.Row.FullHouse"] = ("Ful", "Full house"),
        ["L.Game.Row.FourOfAKind"] = ("Poker", "4 of a kind"),
        ["L.Game.Row.Yamb"] = ("Jamb", "Yamb"),

        ["L.Game.Row.Ones.Tooltip"] = ("Zbir svih kockica sa vrednošću 1", "Sum of all dice showing 1"),
        ["L.Game.Row.Twos.Tooltip"] = ("Zbir svih kockica sa vrednošću 2", "Sum of all dice showing 2"),
        ["L.Game.Row.Threes.Tooltip"] = ("Zbir svih kockica sa vrednošću 3", "Sum of all dice showing 3"),
        ["L.Game.Row.Fours.Tooltip"] = ("Zbir svih kockica sa vrednošću 4", "Sum of all dice showing 4"),
        ["L.Game.Row.Fives.Tooltip"] = ("Zbir svih kockica sa vrednošću 5", "Sum of all dice showing 5"),
        ["L.Game.Row.Sixes.Tooltip"] = ("Zbir svih kockica sa vrednošću 6", "Sum of all dice showing 6"),
        ["L.Game.Row.Max.Tooltip"] = ("Zbir najboljih pet kockica", "Sum of the best five dice"),
        ["L.Game.Row.Min.Tooltip"] = ("Zbir najlošijih pet kockica", "Sum of the worst five dice"),
        ["L.Game.Row.Straight.Tooltip"] = ("Pet kockica u nizu", "Five dice in sequence"),
        ["L.Game.Row.ThreeOfAKind.Tooltip"] = ("Tri iste kockice + 20", "Three of a kind + 20"),
        ["L.Game.Row.FullHouse.Tooltip"] = ("Tri iste i dve iste + 30", "Three of a kind and a pair + 30"),
        ["L.Game.Row.FourOfAKind.Tooltip"] = ("Četiri iste kockice + 40", "Four of a kind + 40"),
        ["L.Game.Row.Yamb.Tooltip"] = ("Pet istih kockica + 50", "Five of a kind + 50"),

        ["L.Game.Die.Magenta"] = ("magenta", "magenta"),
        ["L.Game.Die.Yellow"] = ("žuta", "yellow"),
        ["L.Game.Die.Blue"] = ("plava", "blue"),
        ["L.Game.Die.Green"] = ("zelena", "green"),
        ["L.Game.Die.Red"] = ("crvena", "red"),
        ["L.Game.Die.White"] = ("bela", "white")
    };
}
