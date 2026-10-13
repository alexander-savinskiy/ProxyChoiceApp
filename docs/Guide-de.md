# ProxyChoiceApp 1.0.0 — Benutzerhandbuch

Alexander Savinskiy · [A.savinskiy@gmai.com](mailto:A.savinskiy@gmai.com)

[English](Guide-en.md) · [Русский](Guide-ru.md) · [Deutsch](Guide-de.md) · [Español](Guide-es.md)

## 1. Welche Daten den Proxy nutzen

Bei aktiver Verbindung nutzen markierte Apps den gewählten Proxy. Andere Apps nutzen den Internetanbieter. ProxyChoiceApp verwendet für eigene Internetverbindungen und IP-Prüfungen stets den Proxy. Private Netzwerkadressen bleiben direkt erreichbar, auch ein Modem unter 192.168.8.1. Trennen stellt für alle Apps die normale Verbindung wieder her. Die App steuert Routen über eine TUN-Schnittstelle. Sie ist kein dauerhaftes VPN mit Kill-Switch: Beim Beenden der App oder des Netzwerkkerns kehrt Windows zum Anbieter zurück.

## 2. Verbindung und IP prüfen

Wählen Sie SOCKS5, HTTP oder HTTPS entsprechend Ihrem Proxyanbieter. Geben Sie Hostname oder IP ohne URL-Präfix ein und den Port separat. Tragen Sie bei Bedarf Benutzername und Passwort ein. Proxy testen prüft den Zugriff ohne Tunnel. Verbinden prüft zuerst den Proxy, fordert dann Windows-Administratorrechte an und aktiviert die Routen. IP prüfen zeigt die externe IP der Prüfverbindung; nicht jede App wird einzeln geprüft. Die Symbole in Taskleiste und Infobereich sind nach erfolgreicher Prüfung grün und bei Trennung oder fehlendem Proxyzugriff rot.

## 3. Chrome, Edge und YouTube auswählen

Markieren Sie die Apps und klicken Sie auf Anwenden. Bei bestehender Verbindung wird der Tunnel neu gestartet. Starten Sie betroffene Apps neu, damit alte Verbindungen geschlossen werden. Unter Laufende Apps können Sie mit Strg mehrere Programme wählen. EXE wählen funktioniert auch bei geschlossenen Apps. Browser verwenden einen gemeinsamen Netzwerkprozess für alle Tabs: Chrome oder Edge leitet daher alle Websites dieses Browsers über den Proxy. Installiertes YouTube wird beim ersten Start ausgewählt, wenn es gefunden wird. Eine YouTube-Web-App nutzt die EXE ihres Browsers; YouTube und der Browser teilen denselben Auswahlstatus. Es gibt keine eigene YouTube-Websiteregel.

## 4. Microsoft Store

Microsoft Store ist standardmäßig ausgewählt und kann deaktiviert werden. Regeln erfassen WinStore.App.exe / WinStore.exe sowie bekannte Microsoft-Adressen für Katalog, Lizenzen, Anmeldung und Downloads, die auch Windows-Dienste verwenden. Gemeinsame Microsoft-Anmeldung und Windows-Update-Adressen können dadurch ebenfalls den Proxy nutzen. Nicht alle svchost.exe-Prozesse werden umgeleitet. Win32-Apps von Drittanbietern im Store können Dateien von Herausgeberservern außerhalb der Microsoft-Liste laden. Dafür muss gegebenenfalls die tatsächlich ladende Anwendung hinzugefügt werden. Nach Änderungen den Store neu starten.

## 5. Mehrere Proxys und Passwörter

Sie können Profile hinzufügen, benennen und speichern. Vor einem Proxywechsel trennen. App-Auswahl, Store, UDP und Sprache gelten für alle Profile. Passwort speichern verschlüsselt das Passwort mit Windows DPAPI für den aktuellen Windows-Benutzer. Andere Benutzer und Computer können diese Datei nicht entschlüsseln. Deaktivieren Sie die Option, wenn das Passwort nicht gespeichert werden soll. Beim ersten Start können Proxyadressen aus ProxyFlow übernommen werden, aber frühere Ausnahmen werden nicht als Proxy-Auswahl übernommen. Beide Programme haben getrennte Datenordner.

## 6. Netzwerk, DNS und UDP

IPv4 und IPv6 werden unterstützt. Der Proxyhost wird vor dem Tunnelstart aufgelöst. DNS im Tunnel verwendet synthetische IP-Adressen, damit Regeln der App folgen, auch wenn Windows Namen auflöst. Allgemeine DNS-Auflösung nutzt Cloudflare DoH über den Proxy. Direkte Verbindungen verwenden DNS des aktiven Adapters, ersatzweise direktes DoH. Daher können DNS-Dienstanfragen auch bei nicht markierten Apps über den Proxy laufen. Lokale IP-Verbindungen bleiben direkt. UDP ist für gewählte Apps standardmäßig deaktiviert, damit Browser-QUIC auf TCP zurückfallen kann. UDP nur aktivieren, wenn der SOCKS5-Server es unterstützt. HTTP/HTTPS transportieren kein UDP. Nicht unterstütztes UDP und ICMP werden nur für ausgewählte Routen abgewiesen; andere Apps behalten Direktzugriff.

## 7. Trennen, Beenden und Ausfälle

Trennen beendet den eigenen Netzwerkkern und stellt Routen wieder her. Beenden macht dasselbe; Minimieren hält die Verbindung aktiv. Ein Windows Job Object beendet Kindprozesse nach einem Absturz der App. Start, Prüfungen und Beenden haben Zeitlimits und bei Bedarf eine erzwungene Beendigung. Ist nur der Proxyserver unerreichbar, bleiben gewählte Apps auf der Proxyroute; andere nutzen weiter den Anbieter. Beendet sich der Netzwerkkern, stoppt der Tunnel und alle Apps nutzen wieder den Anbieter. Beim Testen nicht gleichzeitig ProxyFlow, ein anderes TUN-VPN und ProxyChoiceApp verbinden.

## 8. Geschwindigkeit, Datenmenge und Protokoll

Die Statuskarte zeigt Empfang, Versand und die letzte Geschwindigkeitsmessung. Die Zähler gelten für die aktuelle Tunnelsitzung und enthalten direkte sowie Proxyverbindungen des Kerns. Sie sind keine gesonderten Abrechnungszähler des Proxyanbieters. Ein MB wird als 1.048.576 Byte berechnet. Neue Regeln starten eine neue Sitzung und setzen die Zähler zurück. Das Protokoll enthält App-Ereignisse und letzte Einträge des Kerns. Leeren entfernt aktuelle Einträge aus Anzeige und Export. Neue Einträge erscheinen weiterhin. Die Kerndatei bleibt bis zum Trennen geöffnet und wird danach gelöscht. Export speichert das sichtbare Protokoll mit verdeckten Zugangsdaten. Websites und App-Pfade können trotzdem enthalten sein.

## 9. Fehler beheben und Dateien

Bei Verbindungsfehlern Protokoll, Server, Port, Name und Passwort prüfen und Proxy testen wählen. Nutzt eine App die alte IP, den genauen EXE-Pfad prüfen, Auswahl anwenden und die App neu starten. Launcher und Kindprogramme benötigen eventuell eigene Einträge. Nicht auslesbare Prozesse lassen sich über EXE wählen hinzufügen. Das Programm ist für Windows 10/11 x64 und enthält Laufzeit und Netzwerkkern; .NET muss nicht installiert werden. Der Installer erstellt Verknüpfungen und ein Deinstallationsprogramm. Die einzelne EXE funktioniert ohne Installation. Daten liegen unter %LOCALAPPDATA%\ProxyChoiceApp. Autor: Alexander Savinskiy, A.savinskiy@gmai.com. Quellcode: GPL-3.0-or-later. Komponentenlizenzen stehen unten.
