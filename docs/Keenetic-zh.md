# Keenetic → ProxyChoicePro → Windows 远程桌面

1. 在工作路由器启用 WireGuard，并允许来自互联网的 UDP 监听端口。服务器必须可以从外部访问；KeenDNS 云访问不能传输 WireGuard。
2. 若使用 KeeneticOS 5.0 的 VPN 服务器应用，添加客户端并下载其 .conf。较早版本可在“其他连接 → WireGuard”登记笔记本公钥和独立的 VPN /32 地址，然后准备客户端文件。局域网转发防火墙设置参阅下方官方指南。
3. 在 ProxyChoicePro 的工作网络模式导入**客户端** .conf。替换示例密钥和未占用的客户端地址。家庭和工作子网重叠时，使用各工作电脑的 /32 路由。选择可访问的 TCP 测试服务，例如 198.51.100.85:3389。
4. 在工作电脑启用远程桌面，允许 VPN 客户端通过 Windows 防火墙，并使用有登录权限的账户。Windows Home 可以作为 RDP 客户端，但不能接受远程桌面连接。
5. 在家连接，运行诊断，再到电脑标签页打开 RDP。端口测试确认 TCP 可达，Windows 再验证账户。路由器显示的是带 VPN 地址的 WireGuard 对等端，而不是 DHCP 无线客户端。

请填写实际工作网络地址。只有所选目的地址经过 VPN。导入的 DNS 设置不会应用；请使用 IP 地址。如果测试服务不可达，即使 WireGuard 可以握手，连接也不会显示绿色。

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

[ProxyChoicePro](Guide-zh.md) · [work-client.example.conf](work-client.example.conf)
