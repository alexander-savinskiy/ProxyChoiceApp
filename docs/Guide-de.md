# ProxyChoicePro 1.1.3

[en](Guide-en.md) · [ru](Guide-ru.md) · [fr](Guide-fr.md) · [de](Guide-de.md) · [es](Guide-es.md) · [zh](Guide-zh.md)

Zwei Modi: ausgewählte Anwendungen über SOCKS5/HTTP/HTTPS oder Zugriff auf das Arbeitsnetz über WireGuard mit Windows-Remotedesktop. Anpassbare Registerkarten, Nachtmodus, Diagnose, gespeicherte PCs und automatische Wiederherstellung.

Start: ProxyChoicePro.exe öffnen oder ProxyChoicePro-Setup.exe installieren. Arbeitsnetz wählen, die WireGuard-CLIENT-Datei .conf für diesen Laptop importieren und verbinden. Unter Computer die IP des Arbeits-PCs hinzufügen und RDP öffnen. Bei überlappenden Netzen einzelne /32-Routen und einen erreichbaren TCP-Prüfdienst verwenden. Der Router muss den öffentlichen Client-Schlüssel kennen und LAN-Zugriff erlauben. Siehe Keenetic-Anleitung.

[Keenetic](Keenetic-de.md)

## 1. Welche Daten den Proxy nutzen

Bei aktiver Verbindung nutzen markierte Apps den gewählten Proxy. Andere Apps nutzen den Internetanbieter. ProxyChoicePro verwendet für eigene Internetverbindungen und IP-Prüfungen stets den Proxy. Private Netzwerkadressen bleiben direkt erreichbar, auch ein Modem unter LOCAL_ROUTER_IP. Trennen stellt für alle Apps die normale Verbindung wieder her. Die App steuert Routen über eine TUN-Schnittstelle. Sie ist kein dauerhaftes VPN mit Kill-Switch: Beim Beenden der App oder des Netzwerkkerns kehrt Windows zum Anbieter zurück.

## 2. Verbindung und IP prüfen

Im Proxy-Modus wählen Sie SOCKS5, HTTP oder HTTPS nach den Angaben Ihres Anbieters. Geben Sie den Server ohne URL-Präfix sowie Port und erforderliche Zugangsdaten ein. Proxy testen prüft den Zugriff ohne Windows-Routen einzurichten. Verbinden fordert Administratorrechte für die TUN-Schnittstelle an und prüft die gewählte Verbindung. IP prüfen zeigt die externe Adresse der Testverbindung; einzelne Anwendungen werden dabei nicht geprüft. Das gesamte Symbol ist getrennt grau, beim Verbinden gelb, nach erfolgreicher Prüfung grün und bei einem Zugriffsfehler rot. Für das Arbeitsnetz verwenden Sie WireGuard; siehe Abschnitt 10.

## 3. Chrome, Edge und YouTube auswählen

Markieren Sie die Apps und klicken Sie auf Anwenden. Bei bestehender Verbindung wird der Tunnel neu gestartet. Starten Sie betroffene Apps neu, damit alte Verbindungen geschlossen werden. Unter Laufende Apps können Sie mit Strg mehrere Programme wählen. EXE wählen funktioniert auch bei geschlossenen Apps. Browser verwenden einen gemeinsamen Netzwerkprozess für alle Tabs: Chrome oder Edge leitet daher alle Websites dieses Browsers über den Proxy. Installiertes YouTube wird beim ersten Start ausgewählt, wenn es gefunden wird. Eine YouTube-Web-App nutzt die EXE ihres Browsers; YouTube und der Browser teilen denselben Auswahlstatus. Es gibt keine eigene YouTube-Websiteregel.

## 4. Microsoft Store

Microsoft Store ist standardmäßig ausgewählt und kann deaktiviert werden. Regeln erfassen WinStore.App.exe / WinStore.exe sowie bekannte Microsoft-Adressen für Katalog, Lizenzen, Anmeldung und Downloads, die auch Windows-Dienste verwenden. Gemeinsame Microsoft-Anmeldung und Windows-Update-Adressen können dadurch ebenfalls den Proxy nutzen. Nicht alle svchost.exe-Prozesse werden umgeleitet. Win32-Apps von Drittanbietern im Store können Dateien von Herausgeberservern außerhalb der Microsoft-Liste laden. Dafür muss gegebenenfalls die tatsächlich ladende Anwendung hinzugefügt werden. Nach Änderungen den Store neu starten.

## 5. Mehrere Proxys und Passwörter

Sie können Profile hinzufügen, benennen und speichern. Vor einem Proxywechsel trennen. App-Auswahl, Store, UDP und Sprache gelten für alle Profile. Passwort speichern verschlüsselt das Passwort mit Windows DPAPI für den aktuellen Windows-Benutzer. Andere Benutzer und Computer können diese Datei nicht entschlüsseln. Deaktivieren Sie die Option, wenn das Passwort nicht gespeichert werden soll. Beim ersten Start können Proxyadressen aus ProxyFlow übernommen werden, aber frühere Ausnahmen werden nicht als Proxy-Auswahl übernommen. Beide Programme haben getrennte Datenordner.

## 6. Netzwerk, DNS und UDP

IPv4 und IPv6 werden unterstützt. Der Proxyhost wird vor dem Tunnelstart aufgelöst. DNS im Tunnel verwendet synthetische IP-Adressen, damit Regeln der App folgen, auch wenn Windows Namen auflöst. Allgemeine DNS-Auflösung nutzt Cloudflare DoH über den Proxy. Direkte Verbindungen verwenden DNS des aktiven Adapters, ersatzweise direktes DoH. Daher können DNS-Dienstanfragen auch bei nicht markierten Apps über den Proxy laufen. Lokale IP-Verbindungen bleiben direkt. UDP ist für gewählte Apps standardmäßig deaktiviert, damit Browser-QUIC auf TCP zurückfallen kann. UDP nur aktivieren, wenn der SOCKS5-Server es unterstützt. HTTP/HTTPS transportieren kein UDP. Nicht unterstütztes UDP und ICMP werden nur für ausgewählte Routen abgewiesen; andere Apps behalten Direktzugriff.

## 7. Trennen, Beenden und Ausfälle

Trennen beendet den eigenen Netzwerkkern und stellt Routen wieder her. Beenden macht dasselbe; Minimieren hält die Verbindung aktiv. Ein Windows Job Object beendet Kindprozesse nach einem Absturz der App. Start, Prüfungen und Beenden haben Zeitlimits und bei Bedarf eine erzwungene Beendigung. Ist nur der Proxyserver unerreichbar, bleiben gewählte Apps auf der Proxyroute; andere nutzen weiter den Anbieter. Beendet sich der Netzwerkkern, stoppt der Tunnel und alle Apps nutzen wieder den Anbieter. Beim Testen nicht gleichzeitig ProxyFlow, ein anderes TUN-VPN und ProxyChoicePro verbinden.

## 8. Geschwindigkeit, Datenmenge und Protokoll

Die Statuskarte zeigt Empfang, Versand und die letzte Geschwindigkeitsmessung. Die Zähler gelten für die aktuelle Tunnelsitzung und enthalten direkte sowie Proxyverbindungen des Kerns. Sie sind keine gesonderten Abrechnungszähler des Proxyanbieters. Ein MB wird als 1.048.576 Byte berechnet. Neue Regeln starten eine neue Sitzung und setzen die Zähler zurück. Das Protokoll enthält App-Ereignisse und letzte Einträge des Kerns. Leeren entfernt aktuelle Einträge aus Anzeige und Export. Neue Einträge erscheinen weiterhin. Die Kerndatei bleibt bis zum Trennen geöffnet und wird danach gelöscht. Export speichert das sichtbare Protokoll mit verdeckten Zugangsdaten. Websites und App-Pfade können trotzdem enthalten sein.

## 9. Fehler beheben und Dateien

Bei Verbindungsfehlern Protokoll, Server, Port, Name und Passwort prüfen und Proxy testen wählen. Nutzt eine App die alte IP, den genauen EXE-Pfad prüfen, Auswahl anwenden und die App neu starten. Launcher und Kindprogramme benötigen eventuell eigene Einträge. Nicht auslesbare Prozesse lassen sich über EXE wählen hinzufügen. Das Programm ist für Windows 10/11 x64 und enthält Laufzeit und Netzwerkkern; .NET muss nicht installiert werden. Der Installer erstellt Verknüpfungen und ein Deinstallationsprogramm. Die einzelne EXE funktioniert ohne Installation. Daten liegen unter %LOCALAPPDATA%\ProxyChoicePro. Autor: Alexander Savinskiy, a.savinskiy@gmail.com. Quellcode: GPL-3.0-or-later. Komponentenlizenzen stehen unten.

## 10. Arbeitsnetz

Importieren Sie die für diesen Computer erstellte WireGuard-Clientdatei .conf. Das Profil enthält Schlüssel, VPN-Adresse, Server und erlaubte Netze. Einmal speichern, danach Verbinden wählen. Arbeitsrouten gelten für alle Anwendungen einschließlich Windows-Remotedesktop. Anderer Internetverkehr nutzt die normale Verbindung. Bei AllowedIPs=0.0.0.0/0 bestimmte Arbeitsnetze und einen erreichbaren TCP-Testdienst in den Optionen angeben. Skripte und mehrere Peers werden nicht unterstützt. DNS aus der Datei wird nicht installiert; verwenden Sie die IPs der Arbeits-PCs.

## 11. Arbeitscomputer und RDP

Name, IPv4/IPv6-Adresse und TCP-Port des Arbeits-PCs hinzufügen. RDP nutzt standardmäßig Port 3389. Benutzername ist optional; das Passwort fragt Windows-Remotedesktop ab und schreibt es nie in die .rdp-Datei. Arbeitsprofil verbinden, PC wählen und Remotedesktop öffnen. Die Ziel-IP muss in den Arbeitsrouten liegen. Der PC muss eingeschaltet sein, eingehendes RDP unterstützen und den VPN-Client in seiner Firewall zulassen. Der RDP-Test prüft den TCP-Port, nicht Passwort oder Windows-Anmeldung.

## 12. Diagnose, Wiederherstellung und Nachtmodus

Die Diagnose prüft Modul, Zugriff durch den verschlüsselten Arbeitstunnel, RDP-Dienst und überschneidende Subnetze getrennt. Ein laufendes Modul bestätigt keinen Netzwerkzugriff. Nach zwei fehlgeschlagenen Tests oder Standby wird nach 5, 10, 20, 40 und danach 60 Sekunden erneut versucht. Trennen oder Wiederverbindung stoppen beendet die Versuche. Grün: bestätigter Zugriff; Grau: getrennt; Gelb: Verbindung; Rot: Zugriffsfehler. Nachtmodus und Sprache werden gespeichert. In schmalen Fenstern werden Tabs umgebrochen und Formularbereiche untereinander angeordnet.

## 13. Anwendungsliste und vorbereitete Profile

Die Registerkarte Anwendungen enthält Chrome, Edge, installierte Browser, ChatGPT, Codex und codex-computer-use-swift. Fehlende Programme erscheinen als nicht verfügbar. Codex fasst die erkannten ausführbaren Dateien der Desktop-App, der Befehlszeile und des Codemodus zusammen. Einträge mit derselben ausführbaren Datei haben eine verknüpfte Auswahl. Microsoft Store besitzt ein eigenes Kontrollkästchen. Die Auswahl gilt im Proxymodus; Arbeitsrouten gelten für alle Anwendungen.

Eine vorbereitete Clientdatei kann die folgenden Kommentare enthalten. Routes legt die Arbeitsziele fest; ProbeHost und ProbePort wählen einen erreichbaren TCP-Dienst. Der Import prüft die Übereinstimmung mit AllowedIPs. Standard-WireGuard-Clients ignorieren diese Kommentare.

```ini
# ProxyChoicePro.Routes = REPLACE_WITH_WORK_ROUTES_CIDR
# ProxyChoicePro.ProbeHost = REPLACE_WITH_WORK_TCP_SERVICE_IP
# ProxyChoicePro.ProbePort = REPLACE_WITH_WORK_TCP_SERVICE_PORT
```

© 2026 Alexander Savinskiy · a.savinskiy@gmail.com · GPL-3.0-or-later
