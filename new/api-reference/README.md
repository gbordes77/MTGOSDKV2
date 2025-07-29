# API Reference - MTGOSDK

This section contains the complete MTGOSDK API documentation, organized by functional domains.

## 🚀 Quick Start

```csharp
using MTGOSDK.API;
using MTGOSDK.Core.Remoting;

// Connect to MTGO client
var client = RemoteClient.@this;

// Access main APIs
var collection = client.GetCollection();
var user = client.GetCurrentUser();
var games = client.GetActiveGames();
```

## 📚 Main APIs

### [Client & Configuration](client.md)
- `RemoteClient` - Main client for MTGO connection
- `ClientOptions` - Configuration options
- `ObjectCache` - Object caching system

### [Collection](collection/README.md)
- `Collection` - Card collection management
- `Card` - Card representation
- `Deck` - Deck management
- `ItemCollection` - Object collections

### [Play & Games](play/README.md)
- `Game` - Active games
- `Match` - Matches and results
- `Event` - Events and tournaments
- `Queue` - Queues

### [Chat & Communication](chat/README.md)
- `ChannelManager` - Chat channel management
- `Message` - Chat messages
- `Channel` - Discussion channels

### [Trade](trade/README.md)
- `TradeManager` - Trade management
- `TradeEscrow` - Escrow system
- `TradePost` - Trade posts

### [Users](users/README.md)
- `User` - User information
- `UserManager` - User management
- `Avatar` - Avatars and profiles

### [Events](events/README.md)
- Complete event system
- Real-time notifications
- Callback management

### [Settings](settings/README.md)
- `SettingsService` - MTGO configuration
- `ISetting` - Settings interface
- Preferences management

## 🔧 Utilities

### [Interface](interface/README.md)
- `DialogService` - Dialog management
- `WindowUtilities` - Window utilities
- ViewModels and interfaces

## 📖 Usage Guides

- [Quick Start Guide](../guides/quick-start.md)
- [Practical Examples](../examples/README.md)
- [Common Patterns](patterns.md)
- [Error Handling](error-handling.md)

## 🎯 Examples by Use Case

### Game Monitoring
```csharp
var client = RemoteClient.@this;
client.GameStarted += (sender, game) => {
    Console.WriteLine($"New game: {game.Format}");
};
```

### Collection Management
```csharp
var collection = client.GetCollection();
var cards = collection.FindCards("Lightning Bolt");
Console.WriteLine($"Found: {cards.Count()} cards");
```

### Automated Chat
```csharp
var chat = client.GetChatManager();
chat.MessageReceived += (sender, msg) => {
    if (msg.Content.Contains("!help")) {
        chat.SendMessage(msg.Channel, "Available commands: !deck, !price");
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

## 🔍 Quick Reference

| Functionality | Main Class | Namespace |
|---------------|------------|-----------|
| MTGO Connection | `RemoteClient` | `MTGOSDK.Core.Remoting` |
| Collection | `Collection` | `MTGOSDK.API.Collection` |
| Games | `Game` | `MTGOSDK.API.Play.Games` |
| Chat | `ChannelManager` | `MTGOSDK.API.Chat` |
| Trading | `TradeManager` | `MTGOSDK.API.Trade` |
| Users | `UserManager` | `MTGOSDK.API.Users` |
| Events | `BaseEvent` | `MTGOSDK.API.Events` |
| Configuration | `SettingsService` | `MTGOSDK.API.Settings` |

## 📝 Conventions

### Naming
- **Classes**: PascalCase (`RemoteClient`)
- **Methods**: PascalCase (`GetCollection()`)
- **Properties**: PascalCase (`CurrentUser`)
- **Events**: PascalCase (`GameStarted`)

### Patterns
- **Async/Await**: Used for long operations
- **Events**: Observer pattern for notifications
- **Dispose**: IDisposable for resource management
- **Dynamic**: Dynamic objects for interoperability

### Return Types
- **Collections**: `IEnumerable<T>` or `IList<T>`
- **Optional**: Nullable types (`User?`)
- **Async**: `Task<T>` for async operations
- **Events**: `EventHandler<T>` for events

## ⚠️ Important Notes

### Performance
- Use object cache when possible
- Avoid repeated calls to same properties
- Prefer events over polling

### Memory Management
- Dispose resources with `using`
- Unsubscribe from events
- Avoid circular references

### Thread Safety
- APIs are thread-safe unless noted otherwise
- Use `ConfigureAwait(false)` in libraries
- Be careful with event callbacks

---

**Navigation:**
- [⬅️ Main Documentation](../README.md)
- [🚀 Quick Start](../guides/quick-start.md)
- [📖 Architecture](../architecture/overview.md)