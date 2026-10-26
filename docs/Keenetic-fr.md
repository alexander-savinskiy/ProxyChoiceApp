# Keenetic → ProxyChoicePro → Bureau à distance Windows

1. Activez WireGuard sur le routeur professionnel et autorisez son port UDP entrant. Le serveur doit être accessible depuis Internet ; le mode cloud KeenDNS ne transporte pas WireGuard.
2. Avec l’application serveur VPN de KeeneticOS 5.0, ajoutez un client et téléchargez son .conf. Avec une version antérieure, utilisez Autres connexions → WireGuard : enregistrez la clé publique du portable avec son adresse VPN unique /32, puis préparez le fichier client. Suivez les guides officiels ci-dessous pour les règles de pare-feu vers le LAN.
3. Importez le .conf **client** dans ProxyChoicePro → Réseau professionnel. Remplacez les clés et l’adresse client de l’exemple. Si les sous-réseaux se chevauchent, utilisez des routes /32 vers chaque PC. Choisissez un service TCP accessible, par exemple 198.51.100.85:3389.
4. Activez Bureau à distance sur le PC, autorisez le client VPN dans le pare-feu Windows et un compte de connexion. Windows Home peut être client RDP, mais pas serveur.
5. Depuis chez vous, connectez-vous, lancez les diagnostics et ouvrez RDP dans Ordinateurs. Le test du port valide TCP, puis Windows vérifie les identifiants. Le routeur voit un pair WireGuard avec une adresse VPN, pas un client DHCP Wi-Fi.

Utilisez vos vraies adresses de réseau professionnel. L’exemple ne route que les destinations choisies. Le DNS importé n’est pas appliqué ; utilisez les IP. Un service de contrôle inaccessible empêche le statut vert.

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

[ProxyChoicePro](Guide-fr.md) · [work-client.example.conf](work-client.example.conf)
