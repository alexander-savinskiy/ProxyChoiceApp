# ProxyChoiceApp 1.0.0 — User guide

Alexander Savinskiy · [A.savinskiy@gmai.com](mailto:A.savinskiy@gmai.com)

[English](Guide-en.md) · [Русский](Guide-ru.md) · [Deutsch](Guide-de.md) · [Español](Guide-es.md)

## 1. What goes through the proxy

While connected, checked applications use the selected proxy. Unchecked applications use the ISP. ProxyChoiceApp itself always uses the proxy for internet connections and IP checks. Private network addresses remain direct, including a modem at 192.168.8.1. Disconnect restores the ordinary connection for every app. This is application routing through a TUN interface, not a permanent VPN or kill switch: if the app exits or its core stops, Windows returns to the ISP.

## 2. Connect and check your IP

Choose SOCKS5, HTTP or HTTPS exactly as specified by your proxy provider. Enter a server name or IP without a URL prefix, and enter the port separately. Add the username and password if required. Test proxy checks access without enabling the tunnel. Connect first tests the proxy, then requests Windows administrator permission and enables routing. Check IP shows the external IP of the proxy probe; it does not individually verify every app. The taskbar and tray icon are green after a successful check and red when disconnected or the proxy is unavailable.

## 3. Choose Chrome, Edge and YouTube

Tick the apps you want, then click Apply. During a connection this restarts the tunnel. Restart affected applications to close connections made before the change. Running apps lets you select several programs with Ctrl; Choose .exe also works for apps that are closed. Browsers use one network process for all tabs: selecting Chrome or Edge sends every website in that browser through the proxy. Installed YouTube is added and selected on the first start when found. A YouTube web app uses its browser executable; YouTube and the corresponding browser share one checkbox state. There is no separate YouTube site rule.

## 4. Microsoft Store

Microsoft Store is selected by default and can be disabled. Rules cover WinStore.App.exe / WinStore.exe and known Microsoft catalog, licensing, sign-in and download endpoints used by Windows helper services. Shared Microsoft sign-in and Windows Update endpoints may also use the proxy while this option is on. The app does not route all svchost.exe processes through the proxy. Downloads of third-party Win32 Store apps can use publisher servers that are not covered by Microsoft's list; those transfers may require adding the actual downloader application. Restart Store after applying a change.

## 5. Several proxy profiles and passwords

Add, rename and save as many profiles as you need. Disconnect before choosing another proxy. App choices, Store, UDP and interface language are shared across profiles. Remember password encrypts it with Windows DPAPI for the current Windows user; copying the saved file to another user or computer will not decrypt it. Clear Remember password to avoid saving that profile's password. ProxyFlow endpoints can be imported on first start, but old bypass rules are not turned into proxy selections. The programs keep separate settings folders.

## 6. Network, DNS and UDP

IPv4 and IPv6 are routed. The proxy server is resolved before the tunnel starts. DNS inside the tunnel uses synthetic IP mappings so routing follows the application even when Windows resolves its names. General DNS resolution uses Cloudflare DoH through the proxy; direct connections use DNS discovered from the active adapter, with direct DoH as fallback. Consequently DNS service requests can use the proxy even for unchecked apps. Local IP connections remain direct. UDP is disabled for selected apps by default, allowing browser QUIC to fall back to TCP. Enable UDP only for a SOCKS5 server that supports it. HTTP/HTTPS do not carry UDP. Unsupported UDP and ICMP are rejected only for selected routes; other applications retain direct access.

## 7. Disconnect, close and failed connections

Disconnect stops the owned network core and restores routes. Exit does the same; minimizing leaves the connection active. A Windows Job Object terminates the app's children if the parent crashes. Startup, checks and shutdown have deadlines and forced-stop fallback. If only the proxy becomes unreachable, selected apps remain on its route while unchecked apps continue using the ISP. If the network core itself exits, the tunnel stops and all apps return to the ISP. Do not run ProxyFlow, another TUN VPN and ProxyChoiceApp simultaneously when testing routing.

## 8. Speed, traffic and journal

The status card shows received and sent traffic and the latest speed sample. Totals are for the current tunnel session and include direct and proxy connections handled by the core; they are not separate proxy-provider billing counters. MB is calculated as 1,048,576 bytes. Applying a change creates a new session and resets the totals. The Log tab shows app events and a recent core log tail. Clear removes the current entries from the display and export; new entries continue normally. The engine file remains open until disconnect and is then deleted. Export saves the visible journal with credentials hidden. Logs can still contain sites and application paths.

## 9. Troubleshooting and files

If connection fails, check the protocol, server, port and credentials, then run Test proxy. For an app still using the old IP, confirm its exact .exe path, apply the selection and restart it. Launchers and child programs may need separate executable entries. Apps inaccessible to process inspection can be added with Choose .exe. The application is built for Windows 10/11 x64 and includes its runtime and engine. No .NET installation is required. The installer creates normal shortcuts and an uninstaller; the standalone EXE runs without installation. Data is under %LOCALAPPDATA%\ProxyChoiceApp. Contact: Alexander Savinskiy, A.savinskiy@gmai.com. Source is GPL-3.0-or-later; component licenses are available below.
