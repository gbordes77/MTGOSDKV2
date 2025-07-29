# Quick Start Guide - MTGOSDK

This guide will get you started with MTGOSDK in less than 5 minutes.

## Prerequisites

- **.NET 9.0 SDK** or later
- **Windows 10/11** (required for MTGO)
- **Magic: The Gathering Online** installed and working
- **Visual Studio 2022** or **VS Code** (recommended)

## Installation

### Option 1: Via NuGet Package Manager

```powershell
Install-Package MTGOSDK
```

### Option 2: Via .NET CLI

```bash
dotnet add package MTGOSDK
```

### Option 3: Via PackageReference

```xml
<PackageReference Include="MTGOSDK" Version="*" />
```

## First Program

Create a new console project and add this code:

```csharp
using MTGOSDK.API;
using MTGOSDK.Core.Remoting;

// Connect to MTGO client
var client = RemoteClient.@this;
Console.WriteLine($"Connected to MTGO v{client.Version}");

// Access user information
var user = client.GetCurrentUser();
Console.WriteLine($"Logged in user: {user.Name}");

// Access collection
var collection = client.GetCollection();
Console.WriteLine($"Cards in collection: {collection.Count}");

// Access active games
var games = client.GetActiveGames();
Console.WriteLine($"Active games: {games.Count}");
```

## Usage Examples

### 1. Game Monitoring

```csharp
using MTGOSDK.API;
using MTGOSDK.API.Play;

var client = RemoteClient.@this;

// Monitor active games
client.GameStarted += (sender, game) =>
{
    Console.WriteLine($"New game started: {game.Id}");
    
    // Access game details
    Console.WriteLine($"Format: {game.Format}");
    Console.WriteLine($"Players: {string.Join(", ", game.Players.Select(p => p.Name))}");
};

// Keep program alive
Console.ReadLine();
```

### 2. Collection Management

```csharp
using MTGOSDK.API.Collection;

var client = RemoteClient.@this;
var collection = client.GetCollection();

// Search for specific cards
var lightningBolts = collection.FindCards("Lightning Bolt");
Console.WriteLine($"Lightning Bolts found: {lightningBolts.Count()}");

// Filter by format
var standardCards = collection.GetCardsByFormat("Standard");
Console.WriteLine($"Standard cards: {standardCards.Count()}");

// Get collection value
var totalValue = collection.GetTotalValue();
Console.WriteLine($"Total value: {totalValue:C}");
```

### 3. Chat and Communication

```csharp
using MTGOSDK.API.Chat;

var client = RemoteClient.@this;
var chat = client.GetChatManager();

// Listen for messages
chat.MessageReceived += (sender, message) =>
{
    Console.WriteLine($"[{message.Channel}] {message.Sender}: {message.Content}");
};

// Send a message
chat.SendMessage("General", "Hello everyone!");
```

### 4. Automated Trading

```csharp
using MTGOSDK.API.Trade;

var client = RemoteClient.@this;
var tradeManager = client.GetTradeManager();

// Monitor new trade requests
tradeManager.TradeRequestReceived += (sender, request) =>
{
    Console.WriteLine($"Trade request from {request.Sender}");
    
    // Analyze the offer
    var offeredCards = request.GetOfferedCards();
    var requestedCards = request.GetRequestedCards();
    
    // Auto-accept logic
    if (IsGoodTrade(offeredCards, requestedCards))
    {
        request.Accept();
        Console.WriteLine("Trade automatically accepted");
    }
};
```

## Advanced Configuration

### Client Options

```csharp
var options = new ClientOptions
{
    // Enable verbose logging
    EnableVerboseLogging = true,
    
    // Operation timeout
    OperationTimeout = TimeSpan.FromSeconds(30),
    
    // Object caching
    EnableObjectCaching = true,
    
    // Auto-reconnect
    AutoReconnect = true
};

var client = new RemoteClient(options);
```

### Error Handling

```csharp
try
{
    var client = RemoteClient.@this;
    var user = client.GetCurrentUser();
}
catch (MTGOConnectionException ex)
{
    Console.WriteLine($"Connection error: {ex.Message}");
    // Reconnection logic
}
catch (MTGONotFoundException ex)
{
    Console.WriteLine($"MTGO not found: {ex.Message}");
    // Ask user to launch MTGO
}
```

## Debugging and Logging

### Enable Logging

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
});

var client = new RemoteClient(new ClientOptions
{
    LoggerFactory = loggerFactory
});
```

### Object Inspection

```csharp
// Use dynamic to explore objects
dynamic game = client.GetCurrentGame();

// Inspect available properties
var properties = ((object)game).GetType().GetProperties();
foreach (var prop in properties)
{
    Console.WriteLine($"{prop.Name}: {prop.PropertyType}");
}
```

## Best Practices

### 1. Resource Management

```csharp
// Always dispose resources
using var client = RemoteClient.@this;

// Or use try-finally
var client = RemoteClient.@this;
try
{
    // Your code here
}
finally
{
    client?.Dispose();
}
```

### 2. Async Handling

```csharp
// Use async/await for long operations
public async Task<IEnumerable<Card>> SearchCardsAsync(string name)
{
    return await Task.Run(() =>
    {
        var collection = RemoteClient.@this.GetCollection();
        return collection.FindCards(name);
    });
}
```

### 3. Caching and Performance

```csharp
// Local cache to avoid repeated calls
private static readonly Dictionary<string, User> _userCache = new();

public User GetUser(string name)
{
    if (_userCache.TryGetValue(name, out var cachedUser))
        return cachedUser;
    
    var user = RemoteClient.@this.GetUser(name);
    _userCache[name] = user;
    return user;
}
```

## Common Troubleshooting

### MTGO Not Detected

```csharp
// Check if MTGO is running
if (!Process.GetProcessesByName("MTGO").Any())
{
    Console.WriteLine("MTGO is not running");
    Console.WriteLine("Please launch MTGO and try again");
    return;
}
```

### Connection Errors

```csharp
// Retry logic for unstable connections
public async Task<T> RetryOperation<T>(Func<T> operation, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return operation();
        }
        catch (Exception ex) when (i < maxRetries - 1)
        {
            Console.WriteLine($"Attempt {i + 1} failed: {ex.Message}");
            await Task.Delay(1000 * (i + 1)); // Exponential backoff
        }
    }
    
    throw new InvalidOperationException("All attempts failed");
}
```

## Next Steps

1. **Explore examples**: Check the `/examples` folder for complete use cases
2. **Read API documentation**: [API Reference](../api-reference/README.md)
3. **Join the community**: Check GitHub issues to ask questions
4. **Contribute**: Read the [Contributing Guide](contributing.md)

## Useful Resources

- [SDK Architecture](../architecture/overview.md)
- [Advanced Examples](../examples/README.md)
- [FAQ](../FAQ.md)
- [Troubleshooting](troubleshooting.md)
- [GitHub Repository](https://github.com/videre-project/MTGOSDK)

---

**Need help?** Open an issue on GitHub or check the complete documentation.