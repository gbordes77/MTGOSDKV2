# Practical Examples - MTGOSDK

This section contains practical examples and reusable code snippets for common use cases with MTGOSDK.

## 🚀 Basic Examples

### Connection and User Information

```csharp
using MTGOSDK.API;
using MTGOSDK.Core.Remoting;

class BasicExample
{
    static void Main()
    {
        try
        {
            // Connect to MTGO client
            var client = RemoteClient.@this;
            Console.WriteLine("✅ Connected to MTGO");
            
            // Version information
            Console.WriteLine($"MTGO Version: {Client.Version}");
            Console.WriteLine($"SDK Version: {Client.CompatibleVersion}");
            
            // Current user
            var user = Client.CurrentUser;
            Console.WriteLine($"User: {user.Name}");
            Console.WriteLine($"ID: {user.Id}");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}
```

### Collection Exploration

```csharp
using MTGOSDK.API.Collection;

class CollectionExample
{
    static void ExploreCollection()
    {
        var collection = new Collection();
        
        Console.WriteLine($"📚 Collection: {collection.Count} cards");
        
        // Search for popular cards
        var searches = new[] { "Lightning Bolt", "Counterspell", "Dark Ritual" };
        
        foreach (var cardName in searches)
        {
            var cards = collection.FindCards(cardName);
            Console.WriteLine($"🔍 {cardName}: {cards.Count()} copies");
            
            foreach (var card in cards.Take(3))
            {
                Console.WriteLine($"  - {card.Name} ({card.Set.Name}) - {card.ManaCost}");
            }
        }
        
        // Color statistics
        Console.WriteLine("\\n🎨 Color distribution:");
        var colorStats = GetColorStatistics(collection);
        foreach (var (color, count) in colorStats)
        {
            Console.WriteLine($"  {color}: {count} cards");
        }
    }
    
    static Dictionary<string, int> GetColorStatistics(Collection collection)
    {
        var stats = new Dictionary<string, int>
        {
            ["White"] = 0, ["Blue"] = 0, ["Black"] = 0, 
            ["Red"] = 0, ["Green"] = 0, ["Colorless"] = 0
        };
        
        foreach (var card in collection.GetAllCards())
        {
            if (string.IsNullOrEmpty(card.Colors))
            {
                stats["Colorless"]++;
                continue;
            }
            
            foreach (char color in card.Colors)
            {
                var colorName = color switch
                {
                    'W' => "White", 'U' => "Blue", 'B' => "Black",
                    'R' => "Red", 'G' => "Green", _ => "Colorless"
                };
                stats[colorName]++;
            }
        }
        
        return stats;
    }
}
```

## 🎮 Game Monitoring

### Active Game Monitor

```csharp
using MTGOSDK.API.Play;
using MTGOSDK.API.Play.Games;

class GameMonitor
{
    private readonly RemoteClient _client;
    private readonly Timer _timer;
    
    public GameMonitor()
    {
        _client = RemoteClient.@this;
        
        // Check games every 5 seconds
        _timer = new Timer(CheckGames, null, 
                          TimeSpan.Zero, TimeSpan.FromSeconds(5));
        
        Console.WriteLine("🎮 Game monitoring started...");
    }
    
    private void CheckGames(object state)
    {
        try
        {
            var games = _client.GetActiveGames();
            
            if (games.Any())
            {
                Console.WriteLine($"\\n⚡ {games.Count()} active game(s):");
                
                foreach (var game in games)
                {
                    DisplayGameInfo(game);
                }
            }
            else
            {
                Console.WriteLine("💤 No active games");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Monitoring error: {ex.Message}");
        }
    }
    
    private void DisplayGameInfo(Game game)
    {
        Console.WriteLine($"  🎯 Game {game.Id}:");
        Console.WriteLine($"    Format: {game.Format}");
        Console.WriteLine($"    State: {game.State}");
        
        if (game.Players?.Any() == true)
        {
            Console.WriteLine($"    Players: {string.Join(", ", game.Players.Select(p => p.Name))}");
        }
        
        if (game.CurrentTurn != null)
        {
            Console.WriteLine($"    Turn: {game.CurrentTurn.Player?.Name}");
            Console.WriteLine($"    Phase: {game.CurrentTurn.Phase}");
        }
    }
    
    public void Stop()
    {
        _timer?.Dispose();
        Console.WriteLine("🛑 Monitoring stopped");
    }
}

// Usage
var monitor = new GameMonitor();
Console.ReadLine(); // Wait for user input
monitor.Stop();
```

### Game Analyzer

```csharp
class GameAnalyzer
{
    public void AnalyzeCurrentGame()
    {
        var client = RemoteClient.@this;
        var game = client.GetCurrentGame();
        
        if (game == null)
        {
            Console.WriteLine("❌ No active game");
            return;
        }
        
        Console.WriteLine($"🔍 Analyzing game {game.Id}");
        Console.WriteLine($"Format: {game.Format}");
        Console.WriteLine($"Turn: {game.TurnNumber}");
        
        // Analyze players
        foreach (var player in game.Players)
        {
            AnalyzePlayer(player);
        }
        
        // Analyze board
        AnalyzeBoard(game);
    }
    
    private void AnalyzePlayer(GamePlayer player)
    {
        Console.WriteLine($"\\n👤 {player.Name}:");
        Console.WriteLine($"  Life: {player.Life}");
        Console.WriteLine($"  Cards in hand: {player.HandSize}");
        Console.WriteLine($"  Available mana: {player.AvailableMana}");
        
        // Permanents
        var creatures = player.Battlefield.Where(c => c.Types.Contains("Creature"));
        Console.WriteLine($"  Creatures: {creatures.Count()}");
        
        foreach (var creature in creatures.Take(5))
        {
            Console.WriteLine($"    - {creature.Name} ({creature.Power}/{creature.Toughness})");
        }
    }
    
    private void AnalyzeBoard(Game game)
    {
        Console.WriteLine("\\n🎲 Board state:");
        
        var allPermanents = game.Players.SelectMany(p => p.Battlefield);
        var permanentsByType = allPermanents.GroupBy(p => p.Types.FirstOrDefault() ?? "Unknown");
        
        foreach (var group in permanentsByType)
        {
            Console.WriteLine($"  {group.Key}: {group.Count()}");
        }
    }
}
```

## 💬 Chat and Communication

### Simple Chat Bot

```csharp
using MTGOSDK.API.Chat;

class SimpleChatBot
{
    private readonly ChannelManager _chatManager;
    private readonly Dictionary<string, Func<string[], string>> _commands;
    
    public SimpleChatBot()
    {
        _chatManager = RemoteClient.@this.GetChatManager();
        _commands = new Dictionary<string, Func<string[], string>>
        {
            ["!help"] = HandleHelp,
            ["!time"] = HandleTime,
            ["!version"] = HandleVersion,
            ["!collection"] = HandleCollection,
            ["!price"] = HandlePrice
        };
        
        // Subscribe to messages
        _chatManager.MessageReceived += OnMessageReceived;
        Console.WriteLine("🤖 Chat bot started");
    }
    
    private void OnMessageReceived(object sender, ChatMessageEventArgs args)
    {
        var message = args.Message;
        
        // Ignore own messages
        if (message.Sender == Client.CurrentUser.Name)
            return;
        
        // Process commands
        if (message.Content.StartsWith("!"))
        {
            ProcessCommand(message);
        }
        
        // Log messages
        Console.WriteLine($"[{message.Channel}] {message.Sender}: {message.Content}");
    }
    
    private void ProcessCommand(Message message)
    {
        var parts = message.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower();
        var args = parts.Skip(1).ToArray();
        
        if (_commands.TryGetValue(command, out var handler))
        {
            try
            {
                var response = handler(args);
                _chatManager.SendMessage(message.Channel, response);
            }
            catch (Exception ex)
            {
                _chatManager.SendMessage(message.Channel, $"❌ Error: {ex.Message}");
            }
        }
    }
    
    private string HandleHelp(string[] args)
    {
        return "🤖 Available commands: " + string.Join(", ", _commands.Keys);
    }
    
    private string HandleTime(string[] args)
    {
        return $"🕐 Current time: {DateTime.Now:HH:mm:ss}";
    }
    
    private string HandleVersion(string[] args)
    {
        return $"📦 MTGO v{Client.Version}, SDK v{Client.CompatibleVersion}";
    }
    
    private string HandleCollection(string[] args)
    {
        var collection = new Collection();
        return $"📚 Collection: {collection.Count} cards";
    }
    
    private string HandlePrice(string[] args)
    {
        if (args.Length == 0)
            return "❓ Usage: !price <card name>";
        
        var cardName = string.Join(" ", args);
        var collection = new Collection();
        var cards = collection.FindCards(cardName);
        
        if (!cards.Any())
            return $"❌ Card not found: {cardName}";
        
        var card = cards.First();
        return $"💰 {card.Name}: {card.Price:C} (estimated)";
    }
}
```

## 🔄 Automated Trading

### Trade Analyzer

```csharp
using MTGOSDK.API.Trade;

class TradeAnalyzer
{
    private readonly TradeManager _tradeManager;
    private readonly Collection _collection;
    
    public TradeAnalyzer()
    {
        _tradeManager = RemoteClient.@this.GetTradeManager();
        _collection = new Collection();
        
        _tradeManager.TradeRequestReceived += OnTradeRequest;
        Console.WriteLine("🔄 Trade analyzer started");
    }
    
    private void OnTradeRequest(object sender, TradeRequestEventArgs args)
    {
        var request = args.Request;
        Console.WriteLine($"\\n📨 Trade request from {request.Sender}");
        
        try
        {
            AnalyzeTrade(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Analysis error: {ex.Message}");
        }
    }
    
    private void AnalyzeTrade(TradeRequest request)
    {
        var offered = request.GetOfferedCards();
        var requested = request.GetRequestedCards();
        
        Console.WriteLine("📤 Cards offered:");
        decimal offeredValue = 0;
        foreach (var card in offered)
        {
            Console.WriteLine($"  - {card.Name} x{card.Quantity} ({card.EstimatedPrice:C} each)");
            offeredValue += card.EstimatedPrice * card.Quantity;
        }
        
        Console.WriteLine("📥 Cards requested:");
        decimal requestedValue = 0;
        foreach (var card in requested)
        {
            Console.WriteLine($"  - {card.Name} x{card.Quantity} ({card.EstimatedPrice:C} each)");
            requestedValue += card.EstimatedPrice * card.Quantity;
            
            // Check if we own the card
            var owned = _collection.GetCardCount(card.Name);
            if (owned < card.Quantity)
            {
                Console.WriteLine($"    ⚠️  Not enough in stock: {owned}/{card.Quantity}");
            }
        }
        
        // Value analysis
        Console.WriteLine($"\\n💰 Value analysis:");
        Console.WriteLine($"  Offered: {offeredValue:C}");
        Console.WriteLine($"  Requested: {requestedValue:C}");
        Console.WriteLine($"  Difference: {(offeredValue - requestedValue):C}");
        
        var ratio = requestedValue > 0 ? offeredValue / requestedValue : 0;
        Console.WriteLine($"  Ratio: {ratio:P1}");
        
        // Recommendation
        var recommendation = GetTradeRecommendation(ratio, offered, requested);
        Console.WriteLine($"\\n🎯 Recommendation: {recommendation}");
    }
    
    private string GetTradeRecommendation(decimal ratio, 
        IEnumerable<CardQuantityPair> offered, 
        IEnumerable<CardQuantityPair> requested)
    {
        // Check if we have all requested cards
        foreach (var card in requested)
        {
            if (_collection.GetCardCount(card.Name) < card.Quantity)
            {
                return "❌ DECLINE - Missing cards";
            }
        }
        
        // Analyze value ratio
        if (ratio >= 1.1m)
        {
            return "✅ ACCEPT - Very good trade";
        }
        else if (ratio >= 1.0m)
        {
            return "🤔 CONSIDER - Fair trade";
        }
        else if (ratio >= 0.9m)
        {
            return "⚠️  NEGOTIATE - Slightly unfavorable";
        }
        else
        {
            return "❌ DECLINE - Too unfavorable";
        }
    }
}
```

## 🛠️ Utilities and Tools

### Collection Backup

```csharp
class CollectionBackup
{
    public void BackupCollection(string filePath)
    {
        var collection = new Collection();
        var backup = new CollectionBackupData
        {
            BackupDate = DateTime.Now,
            UserName = Client.CurrentUser.Name,
            TotalCards = collection.Count,
            Cards = collection.GetAllCards().Select(card => new CardBackupData
            {
                Name = card.Name,
                SetCode = card.Set.Code,
                Quantity = collection.GetCardCount(card.Name),
                EstimatedPrice = card.Price
            }).ToList()
        };
        
        var json = JsonConvert.SerializeObject(backup, Formatting.Indented);
        File.WriteAllText(filePath, json);
        
        Console.WriteLine($"💾 Collection backed up: {filePath}");
        Console.WriteLine($"📊 {backup.TotalCards} cards, value: {backup.TotalValue:C}");
    }
    
    public void RestoreCollection(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine("❌ Backup file not found");
            return;
        }
        
        var json = File.ReadAllText(filePath);
        var backup = JsonConvert.DeserializeObject<CollectionBackupData>(json);
        
        Console.WriteLine($"📂 Backup from {backup.BackupDate:dd/MM/yyyy HH:mm}");
        Console.WriteLine($"👤 User: {backup.UserName}");
        Console.WriteLine($"📊 {backup.TotalCards} cards, value: {backup.TotalValue:C}");
        
        // Compare with current collection
        CompareWithCurrent(backup);
    }
    
    private void CompareWithCurrent(CollectionBackupData backup)
    {
        var currentCollection = new Collection();
        
        Console.WriteLine("\\n🔍 Comparison with current collection:");
        
        foreach (var backupCard in backup.Cards.Take(10))
        {
            var currentCount = currentCollection.GetCardCount(backupCard.Name);
            var difference = currentCount - backupCard.Quantity;
            
            if (difference != 0)
            {
                var symbol = difference > 0 ? "📈" : "📉";
                Console.WriteLine($"  {symbol} {backupCard.Name}: {backupCard.Quantity} -> {currentCount} ({difference:+#;-#;0})");
            }
        }
    }
}

class CollectionBackupData
{
    public DateTime BackupDate { get; set; }
    public string UserName { get; set; }
    public int TotalCards { get; set; }
    public List<CardBackupData> Cards { get; set; }
    public decimal TotalValue => Cards?.Sum(c => c.EstimatedPrice * c.Quantity) ?? 0;
}

class CardBackupData
{
    public string Name { get; set; }
    public string SetCode { get; set; }
    public int Quantity { get; set; }
    public decimal EstimatedPrice { get; set; }
}
```

## 🚀 Complete Application

### MTGO Administration Console

```csharp
class MTGOConsole
{
    private readonly RemoteClient _client;
    private readonly Dictionary<string, Action> _commands;
    private bool _running;
    
    public MTGOConsole()
    {
        _client = RemoteClient.@this;
        _running = true;
        
        _commands = new Dictionary<string, Action>
        {
            ["help"] = ShowHelp,
            ["status"] = ShowStatus,
            ["collection"] = ShowCollection,
            ["games"] = ShowGames,
            ["chat"] = ShowChat,
            ["backup"] = BackupCollection,
            ["report"] = GenerateReport,
            ["exit"] = Exit
        };
    }
    
    public void Run()
    {
        Console.WriteLine("🎮 MTGO Administration Console");
        Console.WriteLine("Type 'help' to see available commands");
        
        while (_running)
        {
            Console.Write("\\nMTGO> ");
            var input = Console.ReadLine()?.Trim().ToLower();
            
            if (string.IsNullOrEmpty(input))
                continue;
            
            if (_commands.TryGetValue(input, out var command))
            {
                try
                {
                    command();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"❓ Unknown command: {input}");
            }
        }
    }
    
    private void ShowHelp()
    {
        Console.WriteLine("\\n📋 Available commands:");
        Console.WriteLine("  help       - Show this help");
        Console.WriteLine("  status     - MTGO connection status");
        Console.WriteLine("  collection - Collection information");
        Console.WriteLine("  games      - Active games");
        Console.WriteLine("  chat       - Chat statistics");
        Console.WriteLine("  backup     - Backup collection");
        Console.WriteLine("  report     - Generate detailed report");
        Console.WriteLine("  exit       - Quit application");
    }
    
    private void ShowStatus()
    {
        Console.WriteLine("\\n🔍 Connection status:");
        Console.WriteLine($"  MTGO Version: {Client.Version}");
        Console.WriteLine($"  SDK Version: {Client.CompatibleVersion}");
        Console.WriteLine($"  User: {Client.CurrentUser.Name}");
        Console.WriteLine($"  Connected: {RemoteClient.IsInitialized}");
    }
    
    private void ShowCollection()
    {
        var collection = new Collection();
        Console.WriteLine("\\n📚 Collection:");
        Console.WriteLine($"  Total: {collection.Count:N0} cards");
        Console.WriteLine($"  Value: {collection.GetTotalValue():C}");
        
        // Quick statistics
        var recentCards = collection.GetAllCards()
            .Where(c => c.Set.ReleaseDate > DateTime.Now.AddYears(-1))
            .Count();
        Console.WriteLine($"  Recent cards: {recentCards:N0}");
    }
    
    private void ShowGames()
    {
        var games = _client.GetActiveGames();
        Console.WriteLine($"\\n🎮 Active games: {games.Count()}");
        
        foreach (var game in games.Take(5))
        {
            Console.WriteLine($"  - {game.Format} (Turn {game.TurnNumber})");
        }
    }
    
    private void ShowChat()
    {
        Console.WriteLine("\\n💬 Chat:");
        Console.WriteLine("  Feature in development...");
    }
    
    private void BackupCollection()
    {
        var fileName = $"collection_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        var backup = new CollectionBackup();
        backup.BackupCollection(fileName);
    }
    
    private void GenerateReport()
    {
        var fileName = $"collection_report_{DateTime.Now:yyyyMMdd_HHmmss}.md";
        var generator = new ReportGenerator();
        generator.GenerateCollectionReport(fileName);
    }
    
    private void Exit()
    {
        Console.WriteLine("👋 Goodbye!");
        _running = false;
    }
}

// Entry point
class Program
{
    static void Main()
    {
        try
        {
            var console = new MTGOConsole();
            console.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Fatal error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
```

---

**Navigation:**
- [⬅️ API Reference](../api-reference/README.md)
- [➡️ Quick Start Guide](../guides/quick-start.md)
- [🏠 Main Documentation](../README.md)