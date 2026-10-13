# ProxyChoiceApp 1.0.0 — Guía de usuario

Alexander Savinskiy · [A.savinskiy@gmai.com](mailto:A.savinskiy@gmai.com)

[English](Guide-en.md) · [Русский](Guide-ru.md) · [Deutsch](Guide-de.md) · [Español](Guide-es.md)

## 1. Qué tráfico usa el proxy

Cuando está conectado, las apps marcadas usan el proxy elegido. Las demás usan el proveedor de Internet. ProxyChoiceApp usa siempre el proxy para sus conexiones a Internet y comprobaciones de IP. Las direcciones privadas siguen directas, incluido un módem en 192.168.8.1. Desconectar devuelve la conexión habitual a todas las apps. Es enrutamiento mediante una interfaz TUN, no una VPN permanente con bloqueo tras salir: si se cierra la app o el motor, Windows vuelve al proveedor.

## 2. Conectar y comprobar IP

Elige SOCKS5, HTTP o HTTPS según tu proveedor de proxy. Introduce el nombre o IP del servidor sin prefijo URL y el puerto por separado. Añade usuario y contraseña si son necesarios. Probar proxy comprueba el acceso sin activar el túnel. Conectar prueba primero el proxy, solicita permisos de administrador de Windows y activa las rutas. Comprobar IP muestra la IP externa de la conexión de prueba; no verifica por separado cada app. El icono de la barra de tareas y bandeja es verde tras una comprobación correcta y rojo al desconectar o perder el acceso al proxy.

## 3. Elegir Chrome, Edge y YouTube

Marca las apps y pulsa Aplicar. Si estás conectado, el túnel se reinicia. Reinicia las apps afectadas para cerrar conexiones anteriores al cambio. Apps abiertas permite seleccionar varias con Ctrl; Elegir .exe también sirve para apps cerradas. Los navegadores usan un proceso de red común para todas las pestañas: seleccionar Chrome o Edge envía todos los sitios de ese navegador por el proxy. YouTube instalado se añade y marca en el primer inicio si se encuentra. Una app web de YouTube usa el ejecutable de su navegador; YouTube y el navegador comparten la misma selección. No hay una regla web independiente para YouTube.

## 4. Microsoft Store

Microsoft Store está marcado por defecto y se puede desactivar. Las reglas cubren WinStore.App.exe / WinStore.exe y direcciones conocidas de Microsoft para catálogo, licencias, inicio de sesión y descargas usadas por servicios de Windows. Las direcciones compartidas de inicio de sesión y Windows Update también pueden usar el proxy. No se envían todos los procesos svchost.exe por el proxy. Las apps Win32 de terceros en Store pueden descargar desde servidores del editor fuera de la lista de Microsoft; puede ser necesario añadir la aplicación que realiza la descarga. Reinicia Store después de aplicar cambios.

## 5. Varios perfiles y contraseñas

Puedes añadir, renombrar y guardar los perfiles necesarios. Desconecta antes de elegir otro proxy. La selección de apps, Store, UDP e idioma es común a todos los perfiles. Recordar contraseña la cifra mediante Windows DPAPI para el usuario actual de Windows. El archivo no se puede descifrar con otro usuario o en otro ordenador. Desmarca la opción si no quieres guardar esa contraseña. En el primer inicio se pueden importar servidores de ProxyFlow, pero sus excepciones anteriores no se convierten en apps seleccionadas. Los dos programas tienen carpetas de datos separadas.

## 6. Red, DNS y UDP

Se admiten IPv4 e IPv6. El servidor proxy se resuelve antes de iniciar el túnel. DNS en el túnel usa direcciones IP sintéticas para que las reglas sigan a la aplicación aunque Windows resuelva los nombres. La resolución DNS general usa Cloudflare DoH por el proxy. Las conexiones directas usan DNS del adaptador activo o DoH directo como alternativa. Por ello, las solicitudes del servicio DNS pueden usar el proxy también para apps sin marcar. Las IP locales siguen directas. UDP está desactivado por defecto en las apps elegidas para que QUIC del navegador pueda pasar a TCP. Actívalo solo si el servidor SOCKS5 lo admite. HTTP/HTTPS no transportan UDP. UDP e ICMP no admitidos se rechazan solo en rutas elegidas; las demás apps mantienen el acceso directo.

## 7. Desconectar, salir y fallos

Desconectar detiene el motor propio y restaura las rutas. Salir hace lo mismo; minimizar mantiene la conexión. Un Windows Job Object termina los procesos hijos si la app principal falla. Inicio, comprobaciones y cierre tienen plazos y terminación forzada si es necesaria. Si solo falla el servidor proxy, las apps elegidas mantienen su ruta proxy y las demás siguen por proveedor. Si termina el motor de red, el túnel se detiene y todas las apps vuelven al proveedor. No conectes simultáneamente ProxyFlow, otra VPN TUN y ProxyChoiceApp al probar las rutas.

## 8. Velocidad, megabytes y registro

La tarjeta de estado muestra recepción, envío y la última velocidad medida. Los totales son de la sesión de túnel actual e incluyen conexiones directas y por proxy procesadas por el motor; no son contadores de facturación del proveedor de proxy. Se calcula MB como 1.048.576 bytes. Aplicar reglas crea una nueva sesión y reinicia los contadores. El Registro contiene eventos de la app y las últimas líneas del motor. Limpiar quita las entradas actuales de la vista y exportación; siguen apareciendo entradas nuevas. El archivo del motor permanece abierto hasta desconectar y después se elimina. Exportar guarda el registro visible ocultando credenciales. Puede incluir sitios y rutas de aplicaciones.

## 9. Resolver problemas y archivos

Si falla la conexión, revisa protocolo, servidor, puerto, usuario y contraseña y pulsa Probar proxy. Si una app muestra la IP anterior, revisa la ruta exacta del .exe, aplica la selección y reinicia. Los lanzadores y procesos hijos pueden necesitar entradas propias. Los procesos que no se pueden inspeccionar se añaden con Elegir .exe. La app es para Windows 10/11 x64 e incluye entorno de ejecución y motor; no hace falta instalar .NET. El instalador crea accesos directos y desinstalador; el EXE independiente funciona sin instalar. Los datos están en %LOCALAPPDATA%\ProxyChoiceApp. Autor: Alexander Savinskiy, A.savinskiy@gmai.com. Código GPL-3.0-or-later; las licencias de componentes están abajo.
