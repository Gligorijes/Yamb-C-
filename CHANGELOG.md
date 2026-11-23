# Istorija verzija / Build history

## [1.0.0-alpha.11] - 2026-11-24
- Bot učenje je profesionalizovano: sada generalizuje slične pozicije umesto tačnih snimaka table, prati broj partija po naučenoj odluci i sve više veruje iskustvu kako se igra više partija.
- Bot sada uči i kada da završi potez, pored izbora poteza, zadržavanja kockica i najave; nagrada iz završene partije sada jače razlikuje pobedu i poraz.
- Dodat je slider za brzinu animacija u podešavanjima (24–999 fps, podrazumevano 165) koji se primenjuje na sve animacije i čuva se između pokretanja.

## [1.0.0-alpha.10] - 2026-11-20
- Bot sada odbija slabije kombinacije ako ima bolju alternativu: Minimum iznad 9, Maksimum ispod 26, Ful ispod 48 i rani Triling ispod 32 uzimaju se samo kada zaista nema boljeg poteza.
- Animacije sada rade sa željenom frekvencijom od 165 fps, uklonjeno je nepotrebno kašnjenje posle bacanja i senka na privremenim kockama, a kocke se ne rekonstruišu kad se nisu promenile.
- Smanjen je broj redundantnih `GetGameAsync` poziva oko bacanja kocaka i bot poteza.

## [1.0.0-alpha.9] - 2026-11-20
- Bot sada igra strategijski „grandmaster“: rano otvara ograničene kolone, čuva Slobodnu kolonu za kasniju fazu i kažnjava trošenje vrednih redova u pogrešnom trenutku.
- Bot sada cilja 60+ poena u svakoj brojnoj koloni za bonus od 30, prati projekciju ka tom cilju i agresivnije maksimizira razliku Maks–Min.
- Bot odmah upisuje jaku kentu dobijenu u prvom ili drugom bacanju, umesto da je rizikuje i pretori u slabiju kentu u trećem bacanju.
- Dodato je lagano učenje iz završenih partija protiv bota: odluke i ishod se čuvaju u `%LOCALAPPDATA%\Yamb\bot-experience.json` i koriste kao mala korekcija ocene poteza.

## [1.0.0-alpha.8] - 2026-11-20
- Bot sada znatno brže bira kocke za zadržavanje i najavu: pretraga je ograničena na najperspektivnije kombinacije, uz uzorkovanje ishoda bacanja i snažnije keširanje.
- Zadržana je strategija iz alpha.7: rekurzivno planiranje do poslednjeg bacanja, stvarne bonuse i dozvoljene najave.
- Dodata animirana indikacija „Bot razmišlja...“ u zaglavlju partije, vidljiva tokom svih bot poteza.
- Osveženi vizuelni detalji igre: zaobljenije dugmad i ćelije tabele, fokus okvir ćelija i suptilna animacija ulaska tabele.

## [1.0.0-alpha.7] - 2026-11-20
- Bot sada koristi rekurzivno planiranje do poslednjeg bacanja i bira držanje kockica prema očekivanom rezultatu cele runde.
- Bot pri oceni poteza računa stvarne bonuse, uključujući bonus za brojeve i kombinaciju Maks–Min.
- Ispravljeno: bot sada može da najavi red iz kolone Najava i ne razmatra nedozvoljene ćelije Maksimuma posle najave.
- Bot odluke se računaju u pozadini, sa keširanjem rezultata i otpornošću na neočekivane greške.

## [1.0.0-alpha.6] - 2026-11-18
- Unapređen bot: sada procenjuje sve kombinacije zadržavanja kockica i bira potez prema očekivanom rezultatu posle bacanja.
- Bot pažljivije bira najavu i prioritizuje redove koji isplaćuju u koloni Maksimum.
- Pregled poena sada se prikazuje i kada se ćelija dobije fokus, ne samo pri hoveru mišem.

## [1.0.0-alpha.5] - 2026-11-18
- Ispravljeno: brojna polja sada koriste najviše pet kockica, pa šest istih kockica ne može dati veći rezultat.
- Postojeće sačuvane partije sa pogrešnim brojem se pri učitavanju ispravljaju na najviše pet kockica.

## [1.0.0-alpha.4] - 2026-11-18
- Dodat režim igre protiv bota sa posebnom karticom pri izboru broja igrača.
- Bot baca kocke, bira kocke za zadržavanje i upisuje najbolji dostupni rezultat.
- Rezultati bota se ne računaju u rang listu.
- Ispravljeno sečenje teksta na karticama tema i režima igre.
- Unapređeni pregledi tema, animacije prelaza i animacija bacanja kockica.

## [1.0.0-alpha.3] - 2026-11-18
- Kompletan vizueli redizajn menija, podešavanja, igre, pomoći, sačuvanih partija i rang liste.
- Dodate teme Ponoć, Smaragd, Zalazak i Roze pored tamne i svetle teme.
- Nova mreža tema sa bojama, opisima, trenutnom primenom i lokalizovanim nazivima.
- Unapređeni globalni stilovi: gradijenti, senke, hover efekti, skrolbari i ambijentalni sjaj.
- Osveženi dokazi o verziji, snimci ekrana i automatizovane provere za šest tema.

## [1.0.0-alpha.2] - 2026-11-16
- Najava se posle trećeg bacanja potvrđuje ručno, bez automatskog upisa.
- Rang lista prikazuje 50 rezultata i svakog igrača unosi zasebno.
- Novi izbor sačuvanih partija sa detaljima, nastavkom i brisanjem.
- Posebna čestitka za ručni Jamb u koloni Ručno.
- Prečice na tastaturi, F1 pomoć, pretraga pomoći i vraćanje podešavanja.
- Bezbednije čuvanje podešavanja i lokalizovane poruke o greškama.

## [1.0.0-alpha.1] - 2026-11-15
- Prva numerisana alpha verzija; verzija se prikazuje u Pomoći i o igri.
- Nova tamna tema sa krem paletom i svetla tema sa plavom paletom.
- Detaljna pravila za svih 10 kolona, uključujući uslove, tok i strategiju.
- Dodata stranica „O igri“ sa opisom igre i trenutnom verzijom.

### Ranije izmene bez numeracije / Earlier unversioned work
- Podrška za 5 i 6 kockica, čuvanje izbora i prikaz u jednom redu.
- Redizajn glavnog menija sa statusnim oznakama i ikonicama.
- Sekcijski zbrojevi, YAMB zbir i jedinstveni ukupni rezultat.
- Automatsko i ručno računanje, rang lista, podešavanja i čuvanje partije.
