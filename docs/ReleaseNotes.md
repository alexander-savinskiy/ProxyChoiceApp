# ProxyChoicePro 1.1.3

**Alexander Savinskiy · a.savinskiy@gmail.com**

**Русский.** Выбирайте приложения для SOCKS5/HTTP/HTTPS-прокси или подключайте рабочую сеть через свой клиентский профиль WireGuard. Версия 1.1.3 исправляет обмен WireGuard после запуска адаптера в Windows с отключённым IPv6, позволяет отменить подключение и сохраняет подробности ошибки перед остановкой.

Публичная сборка не содержит личных IP, серверов, паролей, ключей VPN или настроенных рабочих компьютеров. На чистой установке профили пустые. Образец `docs/work-client.example.conf` содержит только заглушки `REPLACE_WITH_...`: незаполненный образец не подключается. Свои параметры вводите и импортируйте локально. Уже сохранённые данные текущего пользователя Windows при обновлении остаются локальными.

**English.** Choose applications for SOCKS5/HTTP/HTTPS proxies or access a work network using your own WireGuard client profile. Version 1.1.3 corrects WireGuard transport on IPv6-disabled Windows, adds connection cancellation and preserves failure details. The public package contains no personal IPs, servers, credentials, VPN keys or configured work computers. A clean installation starts with empty profiles. The import example contains only placeholders and is rejected until completed. Existing settings saved by the current Windows user stay local during an update.

**Deutsch.** Ausgewählte Apps über SOCKS5/HTTP/HTTPS oder Zugriff auf das Arbeitsnetz über ein eigenes WireGuard-Clientprofil. Version 1.1.3 korrigiert den Transport bei deaktiviertem IPv6, ergänzt Abbrechen und erhält Fehlerdetails. Das öffentliche Paket enthält keine persönlichen IPs, Zugangsdaten, VPN-Schlüssel oder eingerichteten Arbeitscomputer. Eine saubere Installation beginnt mit leeren Profilen; die Importvorlage enthält nur Platzhalter.

**Español.** Seleccione aplicaciones para SOCKS5/HTTP/HTTPS o acceda a la red de trabajo mediante su propio perfil WireGuard. La versión 1.1.3 corrige el transporte con IPv6 desactivado, permite cancelar y conserva los errores. El paquete público no contiene IP personales, credenciales, claves VPN ni equipos configurados. Una instalación limpia empieza con perfiles vacíos y el ejemplo solo contiene marcadores.

Use **ProxyChoicePro-Setup.exe** to install or **ProxyChoicePro.exe** for standalone use. Windows 10/11 x64; no separate .NET installation is required. Verify downloads with **SHA256SUMS.txt**. See the six-language guides for setup, routing and troubleshooting.

The app now uses adaptive tabs, a saved night mode and a visible window outline. The entire tray/taskbar icon follows connection status with one color and no mixed-color overlay. Interface, installer and detailed documentation are available in English, Russian, French, German, Spanish and Chinese.

The application list now includes installed browsers, ChatGPT, Codex with grouped desktop/CLI/code-mode processes, and codex-computer-use-swift. Microsoft Store retains its separate selection. Prepared WireGuard files support validated route and TCP probe comments for immediate import.

Work mode routes only the chosen work networks and keeps normal Internet routing. DNS from imported profiles is not installed; use work PC IP addresses. A personal client profile and router-side WireGuard registration are required. Existing SOCKS5/HTTP/HTTPS proxy features remain available in Proxy mode. Old profiles migrate into a separate encrypted ProxyChoicePro data folder.

Assets include the standalone EXE, installer, portable ZIP, complete source ZIP with the exact engine source, licenses and SHA-256 checksums. Validation uses real local encrypted WireGuard traffic, authenticated proxy routing and process cleanup. Remote access from another Internet connection must still be verified with the deployed router/client profile.
