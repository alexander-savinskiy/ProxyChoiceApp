# Keenetic → ProxyChoicePro → Escritorio remoto de Windows

1. Active WireGuard en el router del trabajo y permita su puerto UDP de entrada. El servidor debe ser accesible desde Internet; el modo nube KeenDNS no transporta WireGuard.
2. Con la aplicación de servidor VPN de KeeneticOS 5.0, añada un cliente y descargue su .conf. En versiones anteriores, use Otras conexiones → WireGuard: registre la clave pública del portátil y su dirección VPN única /32 y prepare el archivo cliente. Consulte las guías oficiales para permitir el tráfico hacia la LAN.
3. Importe el .conf **cliente** en ProxyChoicePro → Red de trabajo. Sustituya las claves y la dirección libre del ejemplo. Si las subredes coinciden, use rutas /32 a cada PC. Elija un servicio TCP accesible, por ejemplo 198.51.100.85:3389.
4. Active Escritorio remoto en el PC, permita el cliente VPN en el firewall de Windows y use una cuenta autorizada. Windows Home puede ser cliente RDP, pero no aceptar conexiones RDP.
5. Desde casa, conecte, ejecute Diagnóstico y abra RDP en Equipos. La prueba de puerto verifica TCP; Windows comprobará las credenciales. El router muestra un par WireGuard con dirección VPN, no un cliente DHCP Wi-Fi.

Use las direcciones reales de la red de trabajo. Solo los destinos elegidos pasan por VPN. El DNS importado no se aplica; use IP. Un servicio de prueba inaccesible impide el estado verde.

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

[ProxyChoicePro](Guide-es.md) · [work-client.example.conf](work-client.example.conf)
