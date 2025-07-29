# Collection API - MTGOSDK

L'API Collection permet de gérer et d'interagir avec la collection de cartes MTGO, les decks, et les objets de collection.

## Classes Principales

### Collection

La classe `Collection` représente la collection complète de cartes d'un utilisateur.

```csharp
using MTGOSDK.API.Collection;

// Accès à la collection
var client = RemoteClient.@this;
var collection = client.GetCollection();

// Ou directement
var collection = new Collection();
```

#### Propriétés et Méthodes

```csharp
// Nombre total de cartes
int totalCards = collection.Count;

// Recherche de cartes
var lightningBolts = collection.FindCards("Lightning Bolt");
var redCards = collection.FindCardsByColor("R");

// Filtrage par format
var standardCards = collection.GetCardsByFormat("Standard");
var modernCards = collection.GetCardsByFormat("Modern");

// Valeur de la collection
decimal totalValue = collection.GetTotalValue();
```

#### Événements

```csharp
// Surveillance des changements de collection
collection.ItemsAddedOrRemoved += (sender, args) =>
{
    Console.WriteLine($"Collection modifiée: {args.AddedItems.Count} ajoutées, " +
                     $"{args.RemovedItems.Count} supprimées");
};
```

### Card

La classe `Card` représente une carte individuelle avec toutes ses propriétés.

```csharp
// Accès aux propriétés d'une carte
var card = collection.FindCards("Lightning Bolt").First();

Console.WriteLine($"Nom: {card.Name}");
Console.WriteLine($"Coût: {card.ManaCost}");
Console.WriteLine($"CMC: {card.ConvertedManaCost}");
Console.WriteLine($"Couleurs: {card.Colors}");
Console.WriteLine($"Types: {string.Join(", ", card.Types)}");
Console.WriteLine($"Sous-types: {string.Join(", ", card.Subtypes)}");
Console.WriteLine($"Texte: {card.RulesText}");
Console.WriteLine($"Artiste: {card.Artist}");
Console.WriteLine($"Extension: {card.Set.Name}");
```

#### Propriétés Principales

| Propriété | Type | Description |
|-----------|------|-------------|
| `Name` | `string` | Nom de la carte |
| `ManaCost` | `string` | Coût de mana |
| `ConvertedManaCost` | `int` | Coût converti de mana |
| `Colors` | `string` | Couleurs (ex: "WUBRG") |
| `Types` | `IList<string>` | Types de la carte |
| `Subtypes` | `IList<string>` | Sous-types |
| `RulesText` | `string` | Texte d'oracle |
| `Artist` | `string` | Nom de l'artiste |
| `ArtId` | `int` | ID unique de l'art |
| `Set` | `Set` | Extension d'origine |

### Deck

La classe `Deck` représente un deck avec ses différentes régions (main, sideboard, etc.).

```csharp
// Création ou accès à un deck
var deck = new Deck(deckObject);

// Informations générales
Console.WriteLine($"ID du deck: {deck.DeckId}");
Console.WriteLine($"Légal: {deck.IsLegal}");

// Régions du deck
foreach (var region in deck.Regions)
{
    int count = deck.GetRegionCount(region);
    Console.WriteLine($"{region}: {count} cartes");
}
```

#### Régions de Deck

```csharp
public enum DeckRegion
{
    Main,           // Deck principal
    Sideboard,      // Réserve
    CommandZone,    // Zone de commandement (Commander)
    Maybeboard      // Cartes possibles
}

// Accès aux cartes par région
var mainDeck = deck.GetRegionCards(DeckRegion.Main);
var sideboard = deck.GetRegionCards(DeckRegion.Sideboard);

// Nombre de cartes par région
int mainCount = deck.GetRegionCount(DeckRegion.Main);
int sideCount = deck.GetRegionCount(DeckRegion.Sideboard);
```

## Exemples Pratiques

### Analyse de Collection

```csharp
public class CollectionAnalyzer
{
    private readonly Collection _collection;
    
    public CollectionAnalyzer()
    {
        _collection = new Collection();
    }
    
    public void AnalyzeCollection()
    {
        // Statistiques générales
        Console.WriteLine($"Total de cartes: {_collection.Count}");
        
        // Analyse par couleur
        var colorStats = AnalyzeByColor();
        foreach (var (color, count) in colorStats)
        {
            Console.WriteLine($"{color}: {count} cartes");
        }
        
        // Cartes les plus chères
        var expensiveCards = GetMostExpensiveCards(10);
        Console.WriteLine("\\nCartes les plus chères:");
        foreach (var card in expensiveCards)
        {
            Console.WriteLine($"{card.Name}: {card.Price:C}");
        }
        
        // Valeur totale
        Console.WriteLine($"\\nValeur totale: {_collection.GetTotalValue():C}");
    }
    
    private Dictionary<string, int> AnalyzeByColor()
    {
        var stats = new Dictionary<string, int>();
        
        foreach (var card in _collection.GetAllCards())
        {
            foreach (char color in card.Colors)
            {
                string colorName = GetColorName(color);
                stats[colorName] = stats.GetValueOrDefault(colorName, 0) + 1;
            }
        }
        
        return stats;
    }
    
    private string GetColorName(char color) => color switch
    {
        'W' => "Blanc",
        'U' => "Bleu", 
        'B' => "Noir",
        'R' => "Rouge",
        'G' => "Vert",
        _ => "Incolore"
    };
    
    private IEnumerable<Card> GetMostExpensiveCards(int count)
    {
        return _collection.GetAllCards()
            .OrderByDescending(c => c.Price)
            .Take(count);
    }
}
```

### Constructeur de Deck

```csharp
public class DeckBuilder
{
    private readonly Collection _collection;
    private readonly List<CardQuantityPair> _mainDeck;
    private readonly List<CardQuantityPair> _sideboard;
    
    public DeckBuilder(Collection collection)
    {
        _collection = collection;
        _mainDeck = new List<CardQuantityPair>();
        _sideboard = new List<CardQuantityPair>();
    }
    
    public void AddCard(string cardName, int quantity, bool toSideboard = false)
    {
        var card = _collection.FindCards(cardName).FirstOrDefault();
        if (card == null)
        {
            throw new ArgumentException($"Carte non trouvée: {cardName}");
        }
        
        var pair = new CardQuantityPair(card, quantity);
        
        if (toSideboard)
            _sideboard.Add(pair);
        else
            _mainDeck.Add(pair);
    }
    
    public Deck BuildDeck(string deckName)
    {
        // Validation du deck
        ValidateDeck();
        
        // Création du deck
        var deckData = CreateDeckData(deckName);
        return new Deck(deckData);
    }
    
    private void ValidateDeck()
    {
        int mainCount = _mainDeck.Sum(c => c.Quantity);
        if (mainCount < 60)
        {
            throw new InvalidOperationException($"Deck trop petit: {mainCount}/60 cartes");
        }
        
        int sideCount = _sideboard.Sum(c => c.Quantity);
        if (sideCount > 15)
        {
            throw new InvalidOperationException($"Sideboard trop grand: {sideCount}/15 cartes");
        }
        
        // Vérifier les limites de cartes
        foreach (var card in _mainDeck.Concat(_sideboard))
        {
            if (card.Quantity > 4 && !IsBasicLand(card.Card))
            {
                throw new InvalidOperationException($"Trop d'exemplaires de {card.Card.Name}: {card.Quantity}/4");
            }
        }
    }
    
    private bool IsBasicLand(Card card)
    {
        var basicLands = new[] { "Plains", "Island", "Swamp", "Mountain", "Forest" };
        return basicLands.Contains(card.Name);
    }
}
```

### Surveillance de Collection

```csharp
public class CollectionMonitor
{
    private readonly Collection _collection;
    private readonly Dictionary<string, int> _previousCounts;
    
    public event EventHandler<CollectionChangeEventArgs> CollectionChanged;
    
    public CollectionMonitor()
    {
        _collection = new Collection();
        _previousCounts = new Dictionary<string, int>();
        
        // S'abonner aux événements
        _collection.ItemsAddedOrRemoved += OnCollectionChanged;
        
        // Initialiser les compteurs
        InitializeCounts();
    }
    
    private void InitializeCounts()
    {
        foreach (var card in _collection.GetAllCards())
        {
            _previousCounts[card.Name] = _collection.GetCardCount(card.Name);
        }
    }
    
    private void OnCollectionChanged(object sender, CardGroupingItemsChangedEventArgs args)
    {
        var changes = new List<CardChange>();
        
        // Analyser les cartes ajoutées
        foreach (var item in args.AddedItems)
        {
            if (item is Card card)
            {
                var oldCount = _previousCounts.GetValueOrDefault(card.Name, 0);
                var newCount = _collection.GetCardCount(card.Name);
                
                changes.Add(new CardChange
                {
                    Card = card,
                    OldQuantity = oldCount,
                    NewQuantity = newCount,
                    ChangeType = ChangeType.Added
                });
                
                _previousCounts[card.Name] = newCount;
            }
        }
        
        // Analyser les cartes supprimées
        foreach (var item in args.RemovedItems)
        {
            if (item is Card card)
            {
                var oldCount = _previousCounts.GetValueOrDefault(card.Name, 0);
                var newCount = _collection.GetCardCount(card.Name);
                
                changes.Add(new CardChange
                {
                    Card = card,
                    OldQuantity = oldCount,
                    NewQuantity = newCount,
                    ChangeType = ChangeType.Removed
                });
                
                _previousCounts[card.Name] = newCount;
            }
        }
        
        // Notifier les changements
        if (changes.Any())
        {
            CollectionChanged?.Invoke(this, new CollectionChangeEventArgs(changes));
        }
    }
}

public class CollectionChangeEventArgs : EventArgs
{
    public IReadOnlyList<CardChange> Changes { get; }
    
    public CollectionChangeEventArgs(IEnumerable<CardChange> changes)
    {
        Changes = changes.ToList().AsReadOnly();
    }
}

public class CardChange
{
    public Card Card { get; set; }
    public int OldQuantity { get; set; }
    public int NewQuantity { get; set; }
    public ChangeType ChangeType { get; set; }
    public int QuantityChange => NewQuantity - OldQuantity;
}

public enum ChangeType
{
    Added,
    Removed,
    Modified
}
```

## Recherche et Filtrage

### Recherche Avancée

```csharp
public static class CollectionExtensions
{
    // Recherche par nom (partiel)
    public static IEnumerable<Card> FindCards(this Collection collection, string name)
    {
        return collection.GetAllCards()
            .Where(c => c.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }
    
    // Recherche par couleur
    public static IEnumerable<Card> FindCardsByColor(this Collection collection, string colors)
    {
        return collection.GetAllCards()
            .Where(c => colors.All(color => c.Colors.Contains(color)));
    }
    
    // Recherche par type
    public static IEnumerable<Card> FindCardsByType(this Collection collection, string type)
    {
        return collection.GetAllCards()
            .Where(c => c.Types.Any(t => t.Contains(type, StringComparison.OrdinalIgnoreCase)));
    }
    
    // Recherche par CMC
    public static IEnumerable<Card> FindCardsByCMC(this Collection collection, int cmc)
    {
        return collection.GetAllCards()
            .Where(c => c.ConvertedManaCost == cmc);
    }
    
    // Recherche par extension
    public static IEnumerable<Card> FindCardsBySet(this Collection collection, string setCode)
    {
        return collection.GetAllCards()
            .Where(c => c.Set.Code.Equals(setCode, StringComparison.OrdinalIgnoreCase));
    }
    
    // Recherche complexe
    public static IEnumerable<Card> FindCards(this Collection collection, 
        Func<Card, bool> predicate)
    {
        return collection.GetAllCards().Where(predicate);
    }
}

// Exemples d'utilisation
var collection = new Collection();

// Toutes les cartes rouges
var redCards = collection.FindCardsByColor("R");

// Créatures de coût 3
var threeCostCreatures = collection.FindCards(c => 
    c.ConvertedManaCost == 3 && c.Types.Contains("Creature"));

// Cartes de l'extension "DOM"
var dominaria = collection.FindCardsBySet("DOM");

// Sorts instantanés bleus
var blueInstants = collection.FindCards(c => 
    c.Colors.Contains("U") && c.Types.Contains("Instant"));
```

## Gestion des Erreurs

```csharp
try
{
    var collection = new Collection();
    var cards = collection.FindCards("Lightning Bolt");
    
    if (!cards.Any())
    {
        Console.WriteLine("Aucune carte trouvée");
        return;
    }
    
    foreach (var card in cards)
    {
        Console.WriteLine($"Trouvé: {card.Name} ({card.Set.Name})");
    }
}
catch (MTGOConnectionException ex)
{
    Console.WriteLine($"Erreur de connexion: {ex.Message}");
}
catch (CollectionException ex)
{
    Console.WriteLine($"Erreur de collection: {ex.Message}");
}
```

---

**Navigation :**
- [⬅️ API Reference](../README.md)
- [➡️ Play API](../play/README.md)
- [🏠 Documentation Principale](../../README.md)