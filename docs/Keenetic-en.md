# Keenetic → ProxyChoicePro → Windows RDP

1. On the work router, enable WireGuard and permit its UDP listening port from the Internet. The endpoint must be reachable; KeenDNS cloud access does not carry WireGuard.
2. On KeeneticOS 5.0 with the VPN Server application, add a client and download its .conf. On older versions, use Other connections → WireGuard: register the laptop's public key with its unique VPN address /32 and assemble the client file. Follow the official setup links below for firewall forwarding to the LAN.
3. Import the **client** .conf in ProxyChoicePro → Work network. The sample file below needs real keys and an unused client address. For home/work subnet overlap, use individual work PC /32 routes. Set the TCP probe to an accessible work address and port, such as 198.51.100.85:3389.
4. Enable Remote Desktop on the work PC, permit the VPN client in Windows Firewall and use an account allowed to sign in. A supported RDP host edition is required; Windows Home can run the client but cannot host incoming RDP.
5. From home, click Connect, run Diagnostics, then Computers → Open RDP. A successful RDP port check confirms TCP reachability; Windows still checks your credentials. The router lists this connection as a WireGuard peer, with a VPN address, rather than as a DHCP Wi-Fi client.

Use the actual work LAN and gateway addresses. This example sends only the chosen work destinations through VPN. The imported DNS field is not applied; use IP addresses. An unavailable probe service prevents a green connection status even if a WireGuard handshake is possible.

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

[ProxyChoicePro](Guide-en.md) · [work-client.example.conf](work-client.example.conf)
