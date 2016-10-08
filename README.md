# VerifySymfHm - Weryfikator bazy danych Symfonia Handel

## Opis

Weryfikator służy do szybkiego wykrywania nieprawidłowości
w bazie danych Symfonia Handel. Do działania wymaga plików
ddf.

Na wstępie należy zaznaczyć, że wymienione tu błędy zdarzają
się bardzo rzadko. Ze względu na rzadkość ich występowania
nieznana jest również ich przyczyna. Może prowadzi do tego
skomplikowany ciąg operacji, z edycją i usuwaniem dokumentów
włącznie. Niemniej jednak szybkie wykrycie tych błędów,
np. poprzez conocne uruchamianie tego programu,
znacznie ułatwi utrzymywanie dużych baz danych.
Błądy szybko wykryty można poprawić bez żadnych konsekwencji,
a może nawet uda się odkryć jego przyczynę.

W przypadku odkrycia przyczyna wystąpienia błędu bardzo
proszę o zgłoszenie tego albo do mnie, albo do Sage.
Wolimy przecież, by błędy nigdy się nie pojawiały.

## Licencja

Kod udostępniany jest na licencji GPL 3.0.
Oznacza to, że można używać go bez ograniczeń
pod warunkiem, że wraz z kodem rozprowadzana jest również
licencja oraz że kod jest cały czas otwarty (open-source).

Nie jest dozwolone wykorzystanie kodu we własnych rozwiązaniach,
których kod źródłowy nie jest udostępniany publicznie,
na licencji kompatybilnej z GPL 3.0.

## Konfiguracja

* ConnectionString
* MinIdDw - `SprawdzWartNowychDostaw` wymaga podania minimalnego
  id, od którego ma zacząć sprawdzania. Baza DW nie ma klucza
  po datach, więc radzimy sobie w ten sposób.
  Jak nie podamy tego parametru - za każdym razem sprawdzi
  całą bazę.

## Wykrywane błędy

Weryfikator pozwala wykryć następujące błędy:

### Wydanie po złych cenach (`SprawdzCenyWydanZDostaw`)

Jeżeli rekordy PW mają odmienne ceny od DW.
Błąd objawia się zniekształceniem starych wydruków
stanów magazynowych. Nagle pokazują błędne dane.
Przy tym od pewnego dnia wstecz stan towaru
na dany dzień nie jest równy stanowi wg dostaw, czyli
temu, co pokazuje się po dwukrotnym kliknięciu w stan
magazynowy.

### Nieprawidłowy stan wartościowy dostawy (`SprawdzWartNowychDostaw`)

Wydaniom po złych cenach może towarzyszyć nieprawidłowy
stan dostawy. Stan ten może być nieprawidłowy jeszcze
zanim nastąpi pierwsze wydanie towaru. Wtedy najłatwiej
sytuację naprawić i nie ma żadnych negatywnych skutków.

Procedura wykrywa stan, w których wartość początkowa dostawy
nie jest równa wartości bieżącej, pomimo tego, że ilości
początkowa i bieżąca są sobie równe.
