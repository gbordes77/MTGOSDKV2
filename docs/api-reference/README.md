# API Reference - MTGOSDK

Cette section contient la documentation complète de l'API MTGOSDK, organisée par domaines fonctionnels.

## 🚀 Démarrage Rapide

```csharp
using MTGOSDK.API;
using MTGOSDK.Core.Remoting;

// Connexion au client MTGO
var client = RemoteClient.@this;

// Accès aux APIs principales
var collection = client.GetCollection();
var user = client.GetCurrentUser();
var games = client.GetActiveGames();
```

## 📚 APIs Principales

### [Client & Configuration](client.md)
- `RemoteClient` - Client principal pour la connexion MTGO
- `ClientOptions` - Options de configuration
- `ObjectCache` - Système de cache des objets

### [Collection](collection/README.md)
- `Collection` - Gestion de la collection de cartes
- `Card` - Représentation d'une carte
- `Deck` - Gestion des decks
- `ItemCollection` - Collections d'objets

### [Play & Games](play/README.md)
- `Game` - Parties en cours
- `Match` - Matches et résultats
- `Event` - Événements et tournois
- `Queue` - Files d'attente

### [Chat & Communication](chat/README.md)
- `ChannelManager` - Gestion des canaux de chat
- `Message` - Messages de chat
- `Channel` - Canaux de discussion

### [Trade](trade/README.md)
- `TradeManager` - Gestion des échanges
- `TradeEscrow` - Système d'escrow
- `TradePost` - Publications d'échange

### [Users](users/README.md)
- `User` - Informations utilisateur
- `UserManager` - Gestion des utilisateurs
- `Avatar` - Avatars et profils

### [Events](events/README.md)
- Système d'événements complet
- Notifications en temps réel
- Gestion des callbacks

### [Settings](settings/README.md)
- `SettingsService` - Configuration MTGO
- `ISetting` - Interface des paramètres
- Gestion des préférences

## 🔧 Utilitaires

### [Interface](interface/README.md)
- `DialogService` - Gestion des dialogues
- `WindowUtilities` - Utilitaires de fenêtres
- ViewModels et interfaces

## 📖 Guides d'Utilisation

- [Guide de Démarrage Rapide](../guides/quick-start.md)
- [Exemples Pratiques](../examples/README.md)
- [Patterns Courants](patterns.md)
- [Gestion d'Erreurs](error-handling.md)

## 🎯 Exemples par Cas d'Usage

### Surveillance de Parties
```csharp
var client = RemoteClient.@this;
client.GameStarted += (sender, game) => {
    Console.WriteLine($"Nouvelle partie: {game.Format}");
};
```

### Gestion de Collection
```csharp
var collection = client.GetCollection();
var cards = collection.FindCards("Lightning Bolt");
Console.WriteLine($"Trouvé: {cards.Count()} cartes");
```

### Chat Automatisé
```csharp
var chat = client.GetChatManager();
chat.MessageReceived += (sender, msg) => {
    if (msg.Content.Contains("!help")) {
        chat.SendMessage(msg.Channel, "Commandes disponibles: !deck, !price");
    }
};
```

### Trading
```csharp
var tradeManager = client.GetTradeManager();
tradeManager.TradeRequestReceived += (sender, request) => {
    var offered = request.GetOfferedCards();
    if (IsGoodTrade(offered)) {
        request.Accept();
    }
};
```

## 🔍 Recherche Rapide

| Fonctionnalité | Classe Principale | Namespace |
|----------------|-------------------|-----------|
| Connexion MTGO | `RemoteClient` | `MTGOSDK.Core.Remoting` |
| Collection | `Collection` | `MTGOSDK.API.Collection` |
| Parties | `Game` | `MTGOSDK.API.Play.Games` |
| Chat | `ChannelManager` | `MTGOSDK.API.Chat` |
| Trading | `TradeManager` | `MTGOSDK.API.Trade` |
| Utilisateurs | `UserManager` | `MTGOSDK.API.Users` |
| Événements | `BaseEvent` | `MTGOSDK.API.Events` |
| Configuration | `SettingsService` | `MTGOSDK.API.Settings` |

## 📝 Conventions

### Naming
- **Classes** : PascalCase (`RemoteClient`)
- **Méthodes** : PascalCase (`GetCollection()`)
- **Propriétés** : PascalCase (`CurrentUser`)
- **Événements** : PascalCase (`GameStarted`)

### Patterns
- **Async/Await** : Utilisé pour les opérations longues
- **Events** : Pattern Observer pour les notifications
- **Dispose** : IDisposable pour la gestion des ressources
- **Dynamic** : Objets dynamiques pour l'interopérabilité

### Types de Retour
- **Collections** : `IEnumerable<T>` ou `IList<T>`
- **Optionnels** : Types nullable (`User?`)
- **Async** : `Task<T>` pour les opérations asynchrones
- **Events** : `EventHandler<T>` pour les événements

## ⚠️ Notes Importantes

### Performance
- Utilisez le cache d'objets quand possible
- Évitez les appels répétés aux mêmes propriétés
- Préférez les événements aux polling

### Gestion Mémoire
- Disposez les ressources avec `using`
- Désabonnez-vous des événements
- Évitez les références circulaires

### Thread Safety
- Les APIs sont thread-safe sauf indication contraire
- Utilisez `ConfigureAwait(false)` dans les bibliothèques
- Attention aux callbacks d'événements

---

**Navigation :**
- [⬅️ Documentation Principale](../README.md)
- [🚀 Guide de Démarrage](../guides/quick-start.md)
- [📖 Architecture](../architecture/overview.md)