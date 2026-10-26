# Keenetic → ProxyChoicePro → Windows-Remotedesktop

1. WireGuard auf dem Arbeitsrouter aktivieren und dessen eingehenden UDP-Port erlauben. Der Server muss aus dem Internet erreichbar sein; KeenDNS im Cloud-Modus transportiert kein WireGuard.
2. Mit der VPN-Server-Anwendung von KeeneticOS 5.0 einen Client hinzufügen und seine .conf herunterladen. Bei älteren Versionen unter Andere Verbindungen → WireGuard den öffentlichen Laptop-Schlüssel und seine eindeutige VPN-Adresse /32 registrieren, dann die Client-Datei erstellen. Die offiziellen Anleitungen unten erklären Firewall-Zugriff auf das LAN.
3. Die **Client**-.conf in ProxyChoicePro → Arbeitsnetz importieren. Schlüssel und freie Client-Adresse im Beispiel ersetzen. Bei überlappenden Heim-/Arbeitsnetzen einzelne /32-Routen verwenden. Einen erreichbaren TCP-Prüfdienst wählen, etwa 198.51.100.85:3389.
4. Remotedesktop auf dem Arbeits-PC aktivieren, den VPN-Client in der Windows-Firewall zulassen und einen berechtigten Benutzer verwenden. Windows Home kann RDP-Client sein, aber keine RDP-Verbindungen annehmen.
5. Zu Hause verbinden, Diagnose ausführen und unter Computer RDP öffnen. Die Portprüfung bestätigt TCP-Erreichbarkeit; Windows prüft die Anmeldedaten. Der Router zeigt einen WireGuard-Peer mit VPN-Adresse statt eines DHCP-WLAN-Clients.

Die tatsächlichen Arbeitsnetz-Adressen verwenden. Nur gewählte Ziele gehen durch VPN. Importierte DNS-Einstellungen werden nicht angewendet; IP-Adressen verwenden. Ein unerreichbarer Prüfdienst verhindert den grünen Status.

```ini
# Template only. Obtain a client profile from your VPN administrator.
# Replace every placeholder before importing this file.
# ProxyChoicePro.Routes = REPLACE_WITH_WORK_ROUTES_CIDR
# ProxyChoicePro.ProbeHost = REPLACE_WITH_WORK_TCP_SERVICE_IP
# ProxyChoicePro.ProbePort = REPLACE_WITH_WORK_TCP_SERVICE_PORT

[Interface]
PrivateKey = REPLACE_WITH_CLIENT_PRIVATE_KEY
Address = REPLACE_WITH_CLIENT_VPN_ADDRESS_CIDR
MTU = 1408

[Peer]
PublicKey = REPLACE_WITH_VPN_SERVER_PUBLIC_KEY
AllowedIPs = REPLACE_WITH_PEER_ALLOWED_IPS_CIDR
Endpoint = REPLACE_WITH_VPN_SERVER_HOST:REPLACE_WITH_VPN_UDP_PORT
PersistentKeepalive = 25
```

[Keenetic: Windows client / older setup](https://support.keenetic.ru/eaeu/lite/kn-1310/en/21427-connecting-to-a-wireguard-vpn-from-windows.html) · [KeeneticOS 5.0 VPN Server](https://support.keenetic.com/sprinter/kn-3711/en/51599-wireguard-vpn-server.html) · [Microsoft: RDP host setup](https://learn.microsoft.com/en-us/windows-server/remote/remote-desktop-services/remotepc/remote-desktop-allow-access)

[ProxyChoicePro](Guide-de.md) · [work-client.example.conf](work-client.example.conf)
