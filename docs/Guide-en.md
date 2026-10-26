# ProxyChoicePro 1.1.3

[en](Guide-en.md) · [ru](Guide-ru.md) · [fr](Guide-fr.md) · [de](Guide-de.md) · [es](Guide-es.md) · [zh](Guide-zh.md)

Two connection modes: selected applications through SOCKS5/HTTP/HTTPS, or access to work networks through WireGuard followed by Windows Remote Desktop. Adaptive tabs, night mode, diagnostics, saved PC favorites and automatic recovery are included.

Quick start: open ProxyChoicePro.exe or install ProxyChoicePro-Setup.exe. Select Work network, import a WireGuard CLIENT .conf issued for this laptop, then Connect. Under Computers add the work PC IP and click Open RDP. For overlapping home/work networks, prefer individual work PC /32 routes plus a reachable TCP probe. Importing a router/server profile is not sufficient: the router must register this client’s public key and allow forwarding to the LAN. See the Keenetic setup guide.

[Keenetic](Keenetic-en.md)

## 1. What goes through the proxy

While connected, checked applications use the selected proxy. Unchecked applications use the ISP. ProxyChoicePro itself always uses the proxy for internet connections and IP checks. Private network addresses remain direct, including a modem at LOCAL_ROUTER_IP. Disconnect restores the ordinary connection for every app. This is application routing through a TUN interface, not a permanent VPN or kill switch: if the app exits or its core stops, Windows returns to the ISP.

## 2. Connect and check your IP

In Proxy mode, choose SOCKS5, HTTP or HTTPS as specified by your provider. Enter the server without a URL prefix, then its port and any required credentials. Test proxy checks access without enabling Windows routes. Connect requests Windows administrator permission to create the TUN interface and verifies the selected connection. Check IP shows the external address of the proxy probe; it does not test every selected app. The entire taskbar and tray icon is grey when disconnected, amber while connecting, green after a successful connection check, and red when access fails. For a work network use WireGuard; see topic 10.

## 3. Choose Chrome, Edge and YouTube

Tick the apps you want, then click Apply. During a connection this restarts the tunnel. Restart affected applications to close connections made before the change. Running apps lets you select several programs with Ctrl; Choose .exe also works for apps that are closed. Browsers use one network process for all tabs: selecting Chrome or Edge sends every website in that browser through the proxy. Installed YouTube is added and selected on the first start when found. A YouTube web app uses its browser executable; YouTube and the corresponding browser share one checkbox state. There is no separate YouTube site rule.

## 4. Microsoft Store

Microsoft Store is selected by default and can be disabled. Rules cover WinStore.App.exe / WinStore.exe and known Microsoft catalog, licensing, sign-in and download endpoints used by Windows helper services. Shared Microsoft sign-in and Windows Update endpoints may also use the proxy while this option is on. The app does not route all svchost.exe processes through the proxy. Downloads of third-party Win32 Store apps can use publisher servers that are not covered by Microsoft's list; those transfers may require adding the actual downloader application. Restart Store after applying a change.

## 5. Several proxy profiles and passwords

Add, rename and save as many profiles as you need. Disconnect before choosing another proxy. App choices, Store, UDP and interface language are shared across profiles. Remember password encrypts it with Windows DPAPI for the current Windows user; copying the saved file to another user or computer will not decrypt it. Clear Remember password to avoid saving that profile's password. ProxyFlow endpoints can be imported on first start, but old bypass rules are not turned into proxy selections. The programs keep separate settings folders.

## 6. Network, DNS and UDP

IPv4 and IPv6 are routed. The proxy server is resolved before the tunnel starts. DNS inside the tunnel uses synthetic IP mappings so routing follows the application even when Windows resolves its names. General DNS resolution uses Cloudflare DoH through the proxy; direct connections use DNS discovered from the active adapter, with direct DoH as fallback. Consequently DNS service requests can use the proxy even for unchecked apps. Local IP connections remain direct. UDP is disabled for selected apps by default, allowing browser QUIC to fall back to TCP. Enable UDP only for a SOCKS5 server that supports it. HTTP/HTTPS do not carry UDP. Unsupported UDP and ICMP are rejected only for selected routes; other applications retain direct access.

## 7. Disconnect, close and failed connections

Disconnect stops the owned network core and restores routes. Exit does the same; minimizing leaves the connection active. A Windows Job Object terminates the app's children if the parent crashes. Startup, checks and shutdown have deadlines and forced-stop fallback. If only the proxy becomes unreachable, selected apps remain on its route while unchecked apps continue using the ISP. If the network core itself exits, the tunnel stops and all apps return to the ISP. Do not run ProxyFlow, another TUN VPN and ProxyChoicePro simultaneously when testing routing.

## 8. Speed, traffic and journal

The status card shows received and sent traffic and the latest speed sample. Totals are for the current tunnel session and include direct and proxy connections handled by the core; they are not separate proxy-provider billing counters. MB is calculated as 1,048,576 bytes. Applying a change creates a new session and resets the totals. The Log tab shows app events and a recent core log tail. Clear removes the current entries from the display and export; new entries continue normally. The engine file remains open until disconnect and is then deleted. Export saves the visible journal with credentials hidden. Logs can still contain sites and application paths.

## 9. Troubleshooting and files

If connection fails, check the protocol, server, port and credentials, then run Test proxy. For an app still using the old IP, confirm its exact .exe path, apply the selection and restart it. Launchers and child programs may need separate executable entries. Apps inaccessible to process inspection can be added with Choose .exe. The application is built for Windows 10/11 x64 and includes its runtime and engine. No .NET installation is required. The installer creates normal shortcuts and an uninstaller; the standalone EXE runs without installation. Data is under %LOCALAPPDATA%\ProxyChoicePro. Contact: Alexander Savinskiy, a.savinskiy@gmail.com. Source is GPL-3.0-or-later; component licenses are available below.

## 10. Work network

Import the WireGuard client .conf file created for this computer. The profile contains authentication keys, your VPN address, the server and permitted networks. Save it once and use Connect. Work routes apply to every application, including Windows Remote Desktop. Other Internet traffic remains on your ordinary connection. In a profile with AllowedIPs=0.0.0.0/0, enter specific work subnets and a reachable TCP check service in the profile options. Scripts and multiple peers are rejected. DNS from the imported file is not installed; use work PC IP addresses.

## 11. Work computers and RDP

Add the name, IPv4/IPv6 address and TCP port of a work PC. The default RDP port is 3389. A username is optional; passwords are requested by the Windows Remote Desktop client and are never written to the .rdp file. Connect the work profile, select the computer and click Open remote desktop. The target IP must be in your work routes. The target PC must be on and support incoming RDP, with its firewall allowing the VPN client. Test RDP checks TCP availability, not the password or Windows session.

## 12. Diagnostics, recovery and night mode

Diagnostics separately checks the engine, access through the encrypted work tunnel, the selected RDP service and overlapping local/work subnets. A running engine alone is not proof of work network access. After two failed health checks or waking from sleep, automatic recovery retries after 5, 10, 20, 40 and then 60 seconds. Disconnect or Stop reconnecting cancels retries. Each successful session has a green icon; grey means disconnected, amber means connecting, red means failed access. Night mode and language are saved. Tabs wrap on narrow windows; the connection form stacks vertically.

## 13. Application list and ready profiles

The Applications tab includes Chrome, Edge, installed browsers, ChatGPT, Codex and codex-computer-use-swift. Missing applications are shown as unavailable. Codex includes its detected desktop, command-line and code-mode executables. Entries that share an executable have linked selections. Microsoft Store has a separate checkbox. These selections apply in Proxy mode; work routes apply to all applications.

A prepared client file can contain the comments below. Routes set the work destinations; ProbeHost and ProbePort select a reachable TCP service. The importer validates them against AllowedIPs. Standard WireGuard clients ignore these comments.

```ini
# ProxyChoicePro.Routes = REPLACE_WITH_WORK_ROUTES_CIDR
# ProxyChoicePro.ProbeHost = REPLACE_WITH_WORK_TCP_SERVICE_IP
# ProxyChoicePro.ProbePort = REPLACE_WITH_WORK_TCP_SERVICE_PORT
```

© 2026 Alexander Savinskiy · a.savinskiy@gmail.com · GPL-3.0-or-later
