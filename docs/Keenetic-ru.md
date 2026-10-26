# Keenetic → ProxyChoicePro → удалённый рабочий стол Windows

1. На рабочем роутере включите WireGuard и разрешите его входящий UDP-порт из интернета. Сервер должен быть доступен извне; облачный режим KeenDNS не передаёт WireGuard.
2. В KeeneticOS 5.0 с приложением VPN-сервера добавьте клиента и скачайте его .conf. В более ранних версиях откройте «Другие подключения → WireGuard»: добавьте открытый ключ ноутбука и его отдельный VPN-адрес /32, затем составьте клиентский файл. Настройку доступа в LAN через межсетевой экран смотрите в официальных инструкциях ниже.
3. Импортируйте **клиентский** .conf во вкладке подключения ProxyChoicePro, режим «Рабочая сеть». В примере ниже замените ключи и выберите свободный адрес клиента. Если домашняя и рабочая подсети совпадают, задайте маршруты отдельных рабочих ПК /32. Адрес TCP-проверки — доступный рабочий сервис, например 198.51.100.85:3389.
4. На рабочем ПК включите удалённый рабочий стол, разрешите VPN-клиента в брандмауэре Windows и используйте учётную запись с правом входа. Нужна редакция Windows с поддержкой RDP-сервера; Home может подключаться, но не принимать RDP.
5. Дома нажмите «Подключить», выполните диагностику и откройте RDP во вкладке «Компьютеры». Проверка порта подтверждает TCP-доступ, а учётные данные проверит Windows. На роутере ноутбук будет WireGuard-пиром с VPN-адресом; адрес домашней Wi-Fi/DHCP-сети ему не назначается.

Подставьте настоящие адреса рабочей LAN и шлюза. Пример направляет через VPN только выбранные рабочие адреса. DNS из файла не применяется; используйте IP. Недоступность выбранного сервиса проверки не позволит показать зелёный статус, даже если обмен WireGuard возможен.

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

[ProxyChoicePro](Guide-ru.md) · [work-client.example.conf](work-client.example.conf)
