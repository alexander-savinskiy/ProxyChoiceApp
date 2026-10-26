# ProxyChoicePro 1.1.3

[en](Guide-en.md) · [ru](Guide-ru.md) · [fr](Guide-fr.md) · [de](Guide-de.md) · [es](Guide-es.md) · [zh](Guide-zh.md)

Dos modos: aplicaciones elegidas mediante SOCKS5/HTTP/HTTPS o acceso a la red de trabajo mediante WireGuard y Escritorio remoto de Windows. Pestañas adaptables, modo nocturno, diagnóstico, equipos favoritos y reconexión automática.

Inicio: ejecute ProxyChoicePro.exe o instale ProxyChoicePro-Setup.exe. Elija Red de trabajo, importe el archivo CLIENTE WireGuard .conf de este equipo y conecte. En Equipos, añada la IP del PC y abra RDP. Si las subredes coinciden, use rutas /32 a los PC y un servicio TCP de prueba. El router debe registrar la clave pública del cliente y permitir acceso a la LAN. Consulte la guía Keenetic.

[Keenetic](Keenetic-es.md)

## 1. Qué tráfico usa el proxy

Cuando está conectado, las apps marcadas usan el proxy elegido. Las demás usan el proveedor de Internet. ProxyChoicePro usa siempre el proxy para sus conexiones a Internet y comprobaciones de IP. Las direcciones privadas siguen directas, incluido un módem en LOCAL_ROUTER_IP. Desconectar devuelve la conexión habitual a todas las apps. Es enrutamiento mediante una interfaz TUN, no una VPN permanente con bloqueo tras salir: si se cierra la app o el motor, Windows vuelve al proveedor.

## 2. Conectar y comprobar IP

En modo Proxy, elija SOCKS5, HTTP o HTTPS según su proveedor. Introduzca el servidor sin prefijo URL, el puerto y las credenciales necesarias. Probar proxy verifica el acceso sin cambiar las rutas de Windows. Conectar solicita permisos de administrador para crear la interfaz TUN y verifica la conexión elegida. Comprobar IP muestra la dirección externa de la conexión de prueba; no comprueba cada aplicación. Todo el icono es gris al desconectar, ámbar al conectar, verde tras una comprobación correcta y rojo si falla el acceso. Para la red de trabajo, use WireGuard; consulte la sección 10.

## 3. Elegir Chrome, Edge y YouTube

Marca las apps y pulsa Aplicar. Si estás conectado, el túnel se reinicia. Reinicia las apps afectadas para cerrar conexiones anteriores al cambio. Apps abiertas permite seleccionar varias con Ctrl; Elegir .exe también sirve para apps cerradas. Los navegadores usan un proceso de red común para todas las pestañas: seleccionar Chrome o Edge envía todos los sitios de ese navegador por el proxy. YouTube instalado se añade y marca en el primer inicio si se encuentra. Una app web de YouTube usa el ejecutable de su navegador; YouTube y el navegador comparten la misma selección. No hay una regla web independiente para YouTube.

## 4. Microsoft Store

Microsoft Store está marcado por defecto y se puede desactivar. Las reglas cubren WinStore.App.exe / WinStore.exe y direcciones conocidas de Microsoft para catálogo, licencias, inicio de sesión y descargas usadas por servicios de Windows. Las direcciones compartidas de inicio de sesión y Windows Update también pueden usar el proxy. No se envían todos los procesos svchost.exe por el proxy. Las apps Win32 de terceros en Store pueden descargar desde servidores del editor fuera de la lista de Microsoft; puede ser necesario añadir la aplicación que realiza la descarga. Reinicia Store después de aplicar cambios.

## 5. Varios perfiles y contraseñas

Puedes añadir, renombrar y guardar los perfiles necesarios. Desconecta antes de elegir otro proxy. La selección de apps, Store, UDP e idioma es común a todos los perfiles. Recordar contraseña la cifra mediante Windows DPAPI para el usuario actual de Windows. El archivo no se puede descifrar con otro usuario o en otro ordenador. Desmarca la opción si no quieres guardar esa contraseña. En el primer inicio se pueden importar servidores de ProxyFlow, pero sus excepciones anteriores no se convierten en apps seleccionadas. Los dos programas tienen carpetas de datos separadas.

## 6. Red, DNS y UDP

Se admiten IPv4 e IPv6. El servidor proxy se resuelve antes de iniciar el túnel. DNS en el túnel usa direcciones IP sintéticas para que las reglas sigan a la aplicación aunque Windows resuelva los nombres. La resolución DNS general usa Cloudflare DoH por el proxy. Las conexiones directas usan DNS del adaptador activo o DoH directo como alternativa. Por ello, las solicitudes del servicio DNS pueden usar el proxy también para apps sin marcar. Las IP locales siguen directas. UDP está desactivado por defecto en las apps elegidas para que QUIC del navegador pueda pasar a TCP. Actívalo solo si el servidor SOCKS5 lo admite. HTTP/HTTPS no transportan UDP. UDP e ICMP no admitidos se rechazan solo en rutas elegidas; las demás apps mantienen el acceso directo.

## 7. Desconectar, salir y fallos

Desconectar detiene el motor propio y restaura las rutas. Salir hace lo mismo; minimizar mantiene la conexión. Un Windows Job Object termina los procesos hijos si la app principal falla. Inicio, comprobaciones y cierre tienen plazos y terminación forzada si es necesaria. Si solo falla el servidor proxy, las apps elegidas mantienen su ruta proxy y las demás siguen por proveedor. Si termina el motor de red, el túnel se detiene y todas las apps vuelven al proveedor. No conectes simultáneamente ProxyFlow, otra VPN TUN y ProxyChoicePro al probar las rutas.

## 8. Velocidad, megabytes y registro

La tarjeta de estado muestra recepción, envío y la última velocidad medida. Los totales son de la sesión de túnel actual e incluyen conexiones directas y por proxy procesadas por el motor; no son contadores de facturación del proveedor de proxy. Se calcula MB como 1.048.576 bytes. Aplicar reglas crea una nueva sesión y reinicia los contadores. El Registro contiene eventos de la app y las últimas líneas del motor. Limpiar quita las entradas actuales de la vista y exportación; siguen apareciendo entradas nuevas. El archivo del motor permanece abierto hasta desconectar y después se elimina. Exportar guarda el registro visible ocultando credenciales. Puede incluir sitios y rutas de aplicaciones.

## 9. Resolver problemas y archivos

Si falla la conexión, revisa protocolo, servidor, puerto, usuario y contraseña y pulsa Probar proxy. Si una app muestra la IP anterior, revisa la ruta exacta del .exe, aplica la selección y reinicia. Los lanzadores y procesos hijos pueden necesitar entradas propias. Los procesos que no se pueden inspeccionar se añaden con Elegir .exe. La app es para Windows 10/11 x64 e incluye entorno de ejecución y motor; no hace falta instalar .NET. El instalador crea accesos directos y desinstalador; el EXE independiente funciona sin instalar. Los datos están en %LOCALAPPDATA%\ProxyChoicePro. Autor: Alexander Savinskiy, a.savinskiy@gmail.com. Código GPL-3.0-or-later; las licencias de componentes están abajo.

## 10. Red de trabajo

Importa el archivo cliente WireGuard .conf creado para este equipo. El perfil contiene claves, dirección VPN, servidor y redes permitidas. Guárdalo una vez y usa Conectar. Las rutas de trabajo se aplican a todas las aplicaciones, incluido el Escritorio remoto de Windows. El resto del tráfico de Internet usa la conexión habitual. Con AllowedIPs=0.0.0.0/0, indica subredes de trabajo concretas y un servicio TCP de prueba en las opciones. No se admiten scripts ni varios pares. No se instala el DNS del archivo; usa las IP de los PC.

## 11. Equipos de trabajo y RDP

Añade el nombre, la dirección IPv4/IPv6 y el puerto TCP del PC. El puerto RDP predeterminado es 3389. El usuario es opcional; el cliente de Windows solicita la contraseña y nunca la escribe en el archivo .rdp. Conecta el perfil, selecciona el PC y pulsa Abrir escritorio remoto. La IP debe estar en las rutas de trabajo. El PC debe estar encendido, admitir RDP entrante y permitir al cliente VPN en su cortafuegos. La prueba RDP comprueba el puerto TCP, no la contraseña ni la sesión de Windows.

## 12. Diagnóstico, recuperación y modo nocturno

El diagnóstico comprueba por separado el motor, el acceso por el túnel cifrado, el servicio RDP y subredes solapadas. El motor activo no confirma el acceso a la red. Tras dos pruebas fallidas o salir de la suspensión, la recuperación espera 5, 10, 20, 40 y después 60 segundos entre intentos. Desconectar o Detener reconexión cancela los intentos. Verde: acceso confirmado; gris: desconectado; ámbar: conectando; rojo: error de acceso. Se guardan el modo nocturno y el idioma. Las pestañas se ajustan y el formulario se apila en ventanas estrechas.

## 13. Lista de aplicaciones y perfiles preparados

La pestaña Aplicaciones incluye Chrome, Edge, navegadores instalados, ChatGPT, Codex y codex-computer-use-swift. Las aplicaciones ausentes aparecen como no disponibles. Codex agrupa los ejecutables detectados de la aplicación de escritorio, la línea de comandos y el modo de código. Las entradas que comparten un ejecutable tienen la selección vinculada. Microsoft Store tiene una casilla independiente. Estas selecciones se aplican en modo Proxy; las rutas de trabajo se aplican a todas las aplicaciones.

Un archivo de cliente preparado puede incluir los comentarios siguientes. Routes define los destinos de trabajo; ProbeHost y ProbePort seleccionan un servicio TCP accesible. La importación comprueba su correspondencia con AllowedIPs. Los clientes WireGuard estándar ignoran estos comentarios.

```ini
# ProxyChoicePro.Routes = REPLACE_WITH_WORK_ROUTES_CIDR
# ProxyChoicePro.ProbeHost = REPLACE_WITH_WORK_TCP_SERVICE_IP
# ProxyChoicePro.ProbePort = REPLACE_WITH_WORK_TCP_SERVICE_PORT
```

© 2026 Alexander Savinskiy · a.savinskiy@gmail.com · GPL-3.0-or-later
