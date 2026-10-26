# ProxyChoicePro 1.1.3

[en](Guide-en.md) · [ru](Guide-ru.md) · [fr](Guide-fr.md) · [de](Guide-de.md) · [es](Guide-es.md) · [zh](Guide-zh.md)

Deux modes : applications choisies via SOCKS5/HTTP/HTTPS, ou accès au réseau professionnel via WireGuard puis Bureau à distance Windows. Onglets adaptatifs, mode nuit, diagnostics, PC favoris et reconnexion automatique.

Démarrage : lancez ProxyChoicePro.exe ou installez ProxyChoicePro-Setup.exe. Choisissez Réseau professionnel, importez le fichier CLIENT WireGuard .conf de cet ordinateur et connectez-vous. Dans Ordinateurs, ajoutez l’IP du PC et ouvrez RDP. Si les sous-réseaux se chevauchent, utilisez des routes /32 vers les PC et un service TCP de contrôle. Le routeur doit enregistrer la clé publique du client et autoriser l’accès au LAN. Consultez le guide Keenetic.

[Keenetic](Keenetic-fr.md)

## 1. Quels flux passent par le proxy

Pendant la connexion, les applications cochées utilisent le proxy choisi. Les autres utilisent votre opérateur. ProxyChoicePro utilise toujours le proxy pour ses connexions Internet et ses contrôles IP. Les adresses privées restent directes, y compris un modem à LOCAL_ROUTER_IP. Déconnecter rétablit l’accès normal pour toutes les apps. Il s’agit de routage via une interface TUN, pas d’un VPN permanent avec coupe-circuit : si l’application ou son moteur s’arrête, Windows revient à l’opérateur.

## 2. Connexion et contrôle IP

En mode Proxy, choisissez SOCKS5, HTTP ou HTTPS selon votre fournisseur. Saisissez le serveur sans préfixe URL, puis le port et les identifiants requis. Tester le proxy vérifie l’accès sans modifier les routes Windows. Connecter demande les droits administrateur pour créer l’interface TUN et vérifie la connexion choisie. Vérifier l’IP affiche l’adresse externe de la connexion de test, sans tester chaque application. L’icône entière est grise à l’arrêt, ambre pendant la connexion, verte après une vérification réussie et rouge en cas d’échec. Pour le réseau professionnel, utilisez WireGuard ; voir la section 10.

## 3. Choisir Chrome, Edge et YouTube

Cochez les apps puis cliquez sur Appliquer. Si la connexion est active, le tunnel redémarre. Redémarrez les apps concernées pour fermer les anciennes connexions. Apps ouvertes permet une sélection multiple avec Ctrl ; Choisir un .exe fonctionne aussi pour les apps fermées. Les navigateurs partagent un processus réseau entre tous les onglets : choisir Chrome ou Edge envoie tous les sites de ce navigateur via le proxy. YouTube installé est ajouté et coché au premier lancement s’il est trouvé. Une application web YouTube utilise l’exécutable de son navigateur ; leur sélection est commune. Aucune règle de site YouTube séparée n’est créée.

## 4. Microsoft Store

Microsoft Store est coché par défaut et peut être désactivé. Les règles couvrent WinStore.App.exe / WinStore.exe et des adresses connues de Microsoft pour catalogue, licences, connexion et téléchargements utilisés par des services Windows. Des adresses partagées de connexion Microsoft et de Windows Update peuvent donc aussi passer par le proxy. Tous les processus svchost.exe ne sont pas redirigés. Des applications Win32 tierces du Store peuvent télécharger depuis des serveurs de l’éditeur absents de la liste Microsoft ; il peut être nécessaire d’ajouter l’application qui télécharge. Redémarrez le Store après modification.

## 5. Plusieurs profils et mots de passe

Ajoutez, renommez et enregistrez les profils nécessaires. Déconnectez avant de changer de proxy. Le choix des apps, Store, UDP et langue est commun aux profils. Mémoriser le mot de passe le chiffre avec Windows DPAPI pour l’utilisateur Windows actuel. Un autre utilisateur ou ordinateur ne peut pas déchiffrer ce fichier. Décochez cette option pour ne pas enregistrer le mot de passe. Au premier lancement, des serveurs de ProxyFlow peuvent être importés, mais ses anciennes exceptions ne deviennent pas des sélections proxy. Les données des deux programmes sont séparées.

## 6. Réseau, DNS et UDP

IPv4 et IPv6 sont pris en charge. Le serveur proxy est résolu avant le lancement du tunnel. DNS dans le tunnel utilise des IP synthétiques pour que les règles suivent l’application, même si Windows résout les noms. La résolution DNS générale utilise Cloudflare DoH via le proxy. Les connexions directes utilisent le DNS de l’adaptateur actif ou DoH direct en secours. Les requêtes du service DNS peuvent donc passer par le proxy même pour des apps non cochées. Les IP locales restent directes. UDP est désactivé par défaut pour les apps choisies, afin que QUIC puisse revenir à TCP. Activez UDP seulement avec un serveur SOCKS5 compatible. HTTP/HTTPS ne transportent pas UDP. UDP et ICMP non pris en charge sont rejetés uniquement pour les routes choisies ; les autres apps restent directes.

## 7. Déconnexion, fermeture et pannes

Déconnecter arrête le moteur appartenant à l’application et restaure les routes. Quitter fait de même ; réduire la fenêtre laisse la connexion active. Un Windows Job Object arrête les enfants si l’app principale plante. Le démarrage, les contrôles et l’arrêt ont des délais et un arrêt forcé de secours. Si seul le serveur proxy est indisponible, les apps choisies gardent leur route proxy et les autres utilisent l’opérateur. Si le moteur réseau s’arrête, le tunnel cesse et toutes les apps reviennent à l’opérateur. Ne connectez pas simultanément ProxyFlow, un autre VPN TUN et ProxyChoicePro lors des tests.

## 8. Débit, volume et journal

La carte d’état affiche réception, envoi et le dernier débit mesuré. Les totaux concernent la session actuelle et incluent les connexions directes et proxy traitées par le moteur. Ce ne sont pas des compteurs de facturation du fournisseur. Un Mo correspond ici à 1 048 576 octets. Appliquer des règles crée une nouvelle session et remet les compteurs à zéro. Le Journal contient les événements de l’app et les dernières lignes du moteur. Effacer supprime les entrées actuelles de l’affichage et de l’export ; les nouvelles continuent. Le fichier du moteur reste ouvert jusqu’à la déconnexion puis est supprimé. Exporter masque les identifiants et mots de passe. Le journal peut contenir des sites et chemins d’applications.

## 9. Dépannage et fichiers

En cas d’échec, vérifiez protocole, serveur, port, identifiant et mot de passe puis testez le proxy. Si une app montre l’ancienne IP, vérifiez son chemin .exe, appliquez puis redémarrez-la. Lanceurs et programmes enfants peuvent nécessiter leurs propres entrées. Les processus impossibles à inspecter s’ajoutent avec Choisir un .exe. L’application cible Windows 10/11 x64 et inclut runtime et moteur ; aucune installation .NET n’est nécessaire. L’installateur crée des raccourcis et un désinstalleur ; l’EXE autonome fonctionne sans installation. Données : %LOCALAPPDATA%\ProxyChoicePro. Auteur : Alexander Savinskiy, a.savinskiy@gmail.com. Sources GPL-3.0-or-later ; licences ci-dessous.

## 10. Réseau professionnel

Importez le fichier client WireGuard .conf créé pour cet ordinateur. Le profil contient les clés, votre adresse VPN, le serveur et les réseaux autorisés. Enregistrez-le une fois, puis utilisez Connecter. Les routes professionnelles s’appliquent à toutes les applications, dont le Bureau à distance Windows. Le reste du trafic Internet utilise la connexion habituelle. Avec AllowedIPs=0.0.0.0/0, indiquez les sous-réseaux professionnels et un service TCP de test dans les options. Les scripts et les profils à plusieurs pairs sont refusés. Le DNS du fichier n’est pas installé ; utilisez les IP des PC.

## 11. Ordinateurs professionnels et RDP

Ajoutez le nom, l’adresse IPv4/IPv6 et le port TCP du PC. Le port RDP par défaut est 3389. Le nom d’utilisateur est facultatif ; le client Windows demande le mot de passe, qui n’est jamais écrit dans le fichier .rdp. Connectez le profil, sélectionnez le PC et cliquez sur Ouvrir le bureau à distance. L’IP doit appartenir aux routes professionnelles. Le PC doit être allumé, accepter RDP et autoriser le client VPN dans son pare-feu. Le test RDP vérifie le port TCP, pas le mot de passe ni la session Windows.

## 12. Diagnostic, reprise et mode nuit

Le diagnostic vérifie séparément le moteur, l’accès via le tunnel chiffré, le service RDP et les sous-réseaux qui se chevauchent. Le moteur seul ne prouve pas l’accès au réseau. Après deux tests échoués ou une sortie de veille, la reprise automatique attend 5, 10, 20, 40 puis 60 secondes entre les essais. Déconnecter ou Arrêter la reconnexion annule les essais. Vert : accès confirmé ; gris : déconnecté ; orange : connexion ; rouge : accès défaillant. Mode nuit et langue sont enregistrés. Les onglets passent à la ligne et le formulaire devient vertical dans une fenêtre étroite.

## 13. Liste des applications et profils prêts à importer

L’onglet Applications propose Chrome, Edge, les navigateurs installés, ChatGPT, Codex et codex-computer-use-swift. Les applications absentes sont indiquées comme indisponibles. Codex regroupe les exécutables détectés du bureau, de la ligne de commande et du mode code. Les entrées partageant un exécutable ont une sélection liée. Microsoft Store dispose d’une case séparée. Ces choix concernent le mode Proxy ; les routes du travail s’appliquent à toutes les applications.

Un fichier client préparé peut contenir les commentaires ci-dessous. Routes définit les destinations du travail ; ProbeHost et ProbePort choisissent un service TCP accessible. L’importation vérifie leur conformité à AllowedIPs. Les clients WireGuard standard ignorent ces commentaires.

```ini
# ProxyChoicePro.Routes = REPLACE_WITH_WORK_ROUTES_CIDR
# ProxyChoicePro.ProbeHost = REPLACE_WITH_WORK_TCP_SERVICE_IP
# ProxyChoicePro.ProbePort = REPLACE_WITH_WORK_TCP_SERVICE_PORT
```

© 2026 Alexander Savinskiy · a.savinskiy@gmail.com · GPL-3.0-or-later
