# ProxyChoicePro 1.1.3

## Русский

Исправлен путь сетевого обмена WireGuard после запуска VPN-адаптера в Windows с отключённым IPv6. Теперь рабочее подключение использует обычный маршрут Windows, который использовался при предварительной проверке. IP сервера VPN исключён из перехватываемых маршрутов, чтобы служебные пакеты WireGuard не возвращались в собственный туннель. Переход на IPv4 и выбранные рабочие маршруты сохраняются.

После запуска адаптера программа ждёт подтверждённого доступа к выбранному TCP-сервису до 25 секунд. До успешной проверки состояние не становится зелёным. В течение подключения доступна кнопка «Отмена». При сбое журнал содержит проверяемый адрес, причину тайм-аута и сообщения движка перед остановкой.

Закройте старую ProxyChoicePro, перенесите новый EXE на тот же компьютер и запустите. Профили и ключи, сохранённые пользователем Windows на этом компьютере, сохраняются. EXE не содержит личного профиля WireGuard. Проверьте номер 1.1.3 внизу окна. Выберите рабочий профиль и подключитесь. При повторном сбое сохраните журнал через «Экспорт журнала».

## English

Fixed WireGuard transport routing after the Windows VPN adapter starts on IPv6-disabled systems. Work connections now use the ordinary Windows route already used during preflight. The resolved VPN server IP is excluded from captured routes so WireGuard transport packets cannot return through their own tunnel. IPv4 fallback and the selected work routes are retained.

The application waits up to 25 seconds for confirmed TCP access after starting the adapter. It does not show a green status before access succeeds. Cancel is available during the connection attempt. On failure, the journal preserves the checked destination, a readable timeout and engine messages before shutdown.

Close the old ProxyChoicePro, copy the new EXE to the same computer and run it. Existing profiles and keys saved by that Windows user are retained. The EXE contains no personal WireGuard profile. Check that the footer shows 1.1.3, select the work profile and connect. If it still fails, export the journal.

## Deutsch

Der WireGuard-Transport nach dem Start des Windows-VPN-Adapters wurde für Systeme mit deaktiviertem IPv6 korrigiert. Die Arbeitsverbindung verwendet nun dieselbe normale Windows-Route wie die Vorabprüfung. Die aufgelöste IP-Adresse des VPN-Servers wird von den erfassten Routen ausgenommen, damit WireGuard-Pakete nicht in den eigenen Tunnel zurückkehren. IPv4-Fallback und ausgewählte Arbeitsrouten bleiben erhalten.

Nach dem Adapterstart wartet die Anwendung bis zu 25 Sekunden auf bestätigten TCP-Zugriff. Der Status wird erst bei erfolgreichem Zugriff grün. Während des Verbindungsversuchs steht Abbrechen zur Verfügung. Bei Fehlern enthält das Protokoll Zieladresse, verständliche Zeitüberschreitung und die Meldungen der Engine vor dem Beenden.

Die alte ProxyChoicePro schließen, die neue EXE auf denselben Computer kopieren und starten. Vorhandene Profile und Schlüssel dieses Windows-Benutzers bleiben erhalten. Die EXE enthält kein persönliches WireGuard-Profil. Die Fußzeile muss 1.1.3 anzeigen. Das Arbeitsprofil auswählen und verbinden. Bei einem erneuten Fehler das Protokoll exportieren.

## Español

Se corrigió el transporte WireGuard tras iniciar el adaptador VPN de Windows en sistemas con IPv6 desactivado. La conexión de trabajo utiliza ahora la misma ruta normal de Windows que la comprobación previa. La IP resuelta del servidor VPN queda excluida de las rutas capturadas para evitar que los paquetes WireGuard regresen a su propio túnel. Se mantienen el inicio alternativo mediante IPv4 y las rutas de trabajo seleccionadas.

Tras iniciar el adaptador, la aplicación espera hasta 25 segundos para confirmar el acceso TCP. El estado solo cambia a verde cuando el acceso funciona. Durante la conexión está disponible Cancelar. Si falla, el registro conserva el destino, una explicación del tiempo de espera y los mensajes del motor antes de detenerlo.

Cierre la ProxyChoicePro anterior, copie el nuevo EXE al mismo equipo y ejecútelo. Se conservan los perfiles y las claves guardados por ese usuario de Windows. El EXE no contiene ningún perfil personal de WireGuard. Compruebe que el pie de ventana muestra 1.1.3, seleccione el perfil de trabajo y conecte. Si vuelve a fallar, exporte el registro.
