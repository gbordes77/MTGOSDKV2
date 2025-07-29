# Client API - MTGOSDK

L'API Client fournit le point d'entrée principal pour interagir avec MTGO et gérer la connexion.

## Classes Principales

### RemoteClient

La classe `RemoteClient` est le singleton principal qui gère la connexion au processus MTGO.

```csharp
using MTGOSDK.Core.Remoting;

// Accès au client singleton
var client = RemoteClient.@this;
```

#### Propriétés Statiques

| Propriété | Type | Description |
|-----------|------|-------------|
| `@this` | `RemoteClient` | Instance singleton du client |
| `@client` | `RemoteHandle` | Handle vers le processus MTGO |
| `@process` | `Process` | Processus MTGO attaché |
| `IsInitialized` | `bool` | Indique si le client est initialisé |

#### Méthodes Principales

```csharp
// Connexion et initialisation
var client = RemoteClient.@this;

// Vérification de l'état
if (RemoteClient.IsInitialized)
{
    Console.WriteLine("Client déjà connecté");
}

// Accès aux APIs
var collection = client.GetCollection();
var user = client.GetCurrentUser();
var games = client.GetActiveGames();
```

### Client

La classe `Client` fournit l'API haut niveau pour interagir avec MTGO.

```csharp
using MTGOSDK.API;

// Création d'un client avec options
var options = new ClientOptions
{
    CreateProcess = true,
    StartMinimized = false,
    CloseOnExit = true
};

var client = new Client(options);
```

#### Propriétés

| Propriété | Type | Description |
|-----------|------|-------------|
| `Version` | `Version` | Version du client MTGO en cours |
| `CompatibleVersion` | `Version` | Version compatible avec le SDK |
| `CurrentUser` | `User` | Utilisateur actuellement connecté |

#### Exemple d'Utilisation

```csharp
using MTGOSDK.API;
using MTGOSDK.Core.Remoting;

class Program
{
    static void Main()
    {
        try
        {
            // Connexion au client MTGO
            var client = RemoteClient.@this;
            
            // Vérification de la version
            Console.WriteLine($"MTGO Version: {Client.Version}");
            Console.WriteLine($"SDK Compatible: {Client.CompatibleVersion}");
            
            // Informations utilisateur
            var user = Client.CurrentUser;
            Console.WriteLine($"Utilisateur: {user.Name}");
            
            // Utilisation des APIs
            var collection = client.GetCollection();
            Console.WriteLine($"Cartes: {collection.Count}");
            
        }
        catch (MTGOConnectionException ex)
        {
            Console.WriteLine($"Erreur de connexion: {ex.Message}");
        }
    }
}
```

### ClientOptions

Structure de configuration pour personnaliser le comportement du client.

```csharp
public struct ClientOptions
{
    // Démarrer un nouveau processus MTGO
    public bool CreateProcess { get; init; } = false;
    
    // Démarrer MTGO minimisé
    public bool StartMinimized { get; init; } = false;
    
    // Fermer MTGO à la sortie
    public bool CloseOnExit { get; init; } = false;
    
    // Accepter automatiquement l'EULA
    public bool AcceptEULAPrompt { get; init; } = false;
}
```

#### Exemples de Configuration

```csharp
// Configuration par défaut (attacher au processus existant)
var defaultOptions = new ClientOptions();

// Configuration pour développement
var devOptions = new ClientOptions
{
    CreateProcess = true,
    StartMinimized = true,
    CloseOnExit = true,
    AcceptEULAPrompt = true
};

// Configuration pour production
var prodOptions = new ClientOptions
{
    CreateProcess = false,  // Attacher au processus existant
    CloseOnExit = false     // Ne pas fermer MTGO
};
```

## Gestion des Erreurs

### Exceptions Communes

```csharp
try
{
    var client = RemoteClient.@this;
    var user = client.GetCurrentUser();
}
catch (MTGOConnectionException ex)
{
    // MTGO n'est pas en cours d'exécution
    Console.WriteLine($"Connexion impossible: {ex.Message}");
}
catch (MTGONotFoundException ex)
{
    // MTGO n'est pas installé
    Console.WriteLine($"MTGO non trouvé: {ex.Message}");
}
catch (MTGOVersionException ex)
{
    // Version incompatible
    Console.WriteLine($"Version incompatible: {ex.Message}");
}
```

### Vérifications Préalables

```csharp
// Vérifier si MTGO est en cours d'exécution
if (!Process.GetProcessesByName("MTGO").Any())
{
    Console.WriteLine("MTGO n'est pas en cours d'exécution");
    return;
}

// Vérifier la compatibilité des versions
if (Client.Version != Client.CompatibleVersion)
{
    Console.WriteLine($"Attention: Version MTGO {Client.Version} " +
                     $"vs SDK {Client.CompatibleVersion}");
}
```

## Patterns Avancés

### Singleton Thread-Safe

```csharp
public class MTGOService
{
    private static readonly Lazy<RemoteClient> _client = 
        new(() => RemoteClient.@this);
    
    public static RemoteClient Client => _client.Value;
    
    public static bool IsConnected => 
        RemoteClient.IsInitialized && Client != null;
}
```

### Reconnexion Automatique

```csharp
public class RobustMTGOClient
{
    private RemoteClient _client;
    private readonly ClientOptions _options;
    
    public RobustMTGOClient(ClientOptions options)
    {
        _options = options;
        Connect();
    }
    
    private void Connect()
    {
        try
        {
            _client = RemoteClient.@this;
        }
        catch (MTGOConnectionException)
        {
            // Retry logic
            Thread.Sleep(5000);
            Connect();
        }
    }
    
    public T ExecuteWithRetry<T>(Func<RemoteClient, T> operation)
    {
        try
        {
            return operation(_client);
        }
        catch (Exception)
        {
            // Reconnect and retry
            Connect();
            return operation(_client);
        }
    }
}
```

### Monitoring de Connexion

```csharp
public class ConnectionMonitor
{
    private readonly Timer _timer;
    private bool _wasConnected;
    
    public event EventHandler<bool> ConnectionChanged;
    
    public ConnectionMonitor()
    {
        _timer = new Timer(CheckConnection, null, 
                          TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }
    
    private void CheckConnection(object state)
    {
        bool isConnected = RemoteClient.IsInitialized;
        
        if (isConnected != _wasConnected)
        {
            _wasConnected = isConnected;
            ConnectionChanged?.Invoke(this, isConnected);
        }
    }
}
```

## Bonnes Pratiques

### 1. Gestion des Ressources

```csharp
// Utiliser using pour les ressources
using var client = new Client(options);

// Ou gérer manuellement
var client = RemoteClient.@this;
try
{
    // Votre code ici
}
finally
{
    client?.Dispose();
}
```

### 2. Vérifications de Sécurité

```csharp
// Toujours vérifier la connexion
if (!RemoteClient.IsInitialized)
{
    throw new InvalidOperationException("Client non initialisé");
}

// Vérifier les permissions
if (!HasMTGOPermissions())
{
    throw new SecurityException("Permissions insuffisantes");
}
```

### 3. Configuration Environnementale

```csharp
public static ClientOptions GetEnvironmentOptions()
{
    return Environment.GetEnvironmentVariable("ENVIRONMENT") switch
    {
        "Development" => new ClientOptions 
        { 
            CreateProcess = true, 
            StartMinimized = true,
            CloseOnExit = true 
        },
        "Testing" => new ClientOptions 
        { 
            AcceptEULAPrompt = true 
        },
        _ => new ClientOptions() // Production defaults
    };
}
```

## Debugging et Diagnostics

### Logging

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
});

// Le client utilise automatiquement le logger configuré
var client = RemoteClient.@this;
```

### Inspection d'État

```csharp
public static void DiagnoseConnection()
{
    Console.WriteLine($"MTGO Process: {Process.GetProcessesByName("MTGO").Length}");
    Console.WriteLine($"SDK Initialized: {RemoteClient.IsInitialized}");
    Console.WriteLine($"MTGO Version: {Client.Version}");
    Console.WriteLine($"SDK Version: {Client.CompatibleVersion}");
    
    if (RemoteClient.IsInitialized)
    {
        var client = RemoteClient.@this;
        Console.WriteLine($"Current User: {Client.CurrentUser?.Name ?? "None"}");
    }
}
```

---

**Navigation :**
- [⬅️ API Reference](README.md)
- [➡️ Collection API](collection/README.md)
- [🏠 Documentation Principale](../README.md)