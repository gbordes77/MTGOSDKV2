# MTGOSDK Remoting System & DLR Architecture

## Vue d'ensemble du système de remoting

```mermaid
graph TB
    subgraph "User Application Process"
        subgraph "MTGOSDK Client"
            USER_CODE[User Application Code]
            CLIENT_API[MTGOSDK Client API]
            REMOTE_CLIENT[RemoteClient Singleton]
            OBJECT_CACHE[ObjectCache]
        end
        
        subgraph "DLR (Dynamic Language Runtime)"
            DYNAMIC_PROXY[DynamicRemoteObject]
            DLR_BINDER[C# Runtime Binder]
            IMPROMPTU[ImpromptuInterface]
            TYPE_PROXY[TypeProxy<T>]
        end
        
        subgraph "Remoting Infrastructure"
            REMOTE_HANDLE[RemoteHandle]
            COMMUNICATOR[DiverCommunicator]
            OBJECT_REF[RemoteObjectRef]
            PRIMITIVES[PrimitivesEncoder]
        end
    end
    
    subgraph "Inter-Process Communication"
        IPC_CHANNEL[Named Pipes / TCP]
        SERIALIZATION[Object Serialization]
        CALLBACKS[Callback System]
    end
    
    subgraph "MTGO Client Process"
        subgraph "Injected Components"
            SCUBA_DIVER[ScubaDiver Assembly]
            HARMONY_HOOKS[Harmony Patches]
            REVERSE_COMM[ReverseCommunicator]
        end
        
        subgraph "MTGO Runtime"
            MTGO_OBJECTS[MTGO Game Objects]
            MANAGED_HEAP[.NET Managed Heap]
            CLR_RUNTIME[CLR Runtime]
            OBJECT_PINNING[Object Pinning]
        end
        
        subgraph "Memory Inspection"
            CLRMD[Microsoft.Diagnostics.Runtime]
            SNAPSHOT[SnapshotRuntime]
            HEAP_WALKER[Heap Walker]
            TYPE_RESOLVER[Type Resolver]
        end
    end
    
    %% User Flow
    USER_CODE -->|"dynamic obj = client.GetGame()"| CLIENT_API
    CLIENT_API --> REMOTE_CLIENT
    REMOTE_CLIENT --> OBJECT_CACHE
    OBJECT_CACHE -->|"Cache Miss"| REMOTE_HANDLE
    
    %% DLR Integration
    REMOTE_HANDLE --> DYNAMIC_PROXY
    DYNAMIC_PROXY -->|"obj.SomeMethod()"| DLR_BINDER
    DLR_BINDER --> IMPROMPTU
    IMPROMPTU --> TYPE_PROXY
    
    %% Remoting Communication
    TYPE_PROXY --> COMMUNICATOR
    COMMUNICATOR --> PRIMITIVES
    PRIMITIVES --> IPC_CHANNEL
    
    %% Cross-Process Communication
    IPC_CHANNEL --> SERIALIZATION
    SERIALIZATION --> CALLBACKS
    CALLBACKS --> REVERSE_COMM
    
    %% MTGO Process Handling
    REVERSE_COMM --> SCUBA_DIVER
    SCUBA_DIVER --> HARMONY_HOOKS
    HARMONY_HOOKS -->|"Method Interception"| MTGO_OBJECTS
    
    %% Memory Access
    SCUBA_DIVER --> CLRMD
    CLRMD --> SNAPSHOT
    SNAPSHOT --> HEAP_WALKER
    HEAP_WALKER --> MANAGED_HEAP
    
    %% Object Resolution
    MANAGED_HEAP --> TYPE_RESOLVER
    TYPE_RESOLVER --> OBJECT_PINNING
    OBJECT_PINNING --> OBJECT_REF
    OBJECT_REF -->|"Return Reference"| COMMUNICATOR
    
    %% Return Path
    COMMUNICATOR -->|"Cached Result"| OBJECT_CACHE
    OBJECT_CACHE -->|"Typed Proxy"| USER_CODE
    
    %% Styling
    classDef userSpace fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    classDef dlr fill:#e3f2fd,stroke:#1565c0,stroke-width:2px
    classDef remoting fill:#fff3e0,stroke:#ef6c00,stroke-width:2px
    classDef ipc fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    classDef injected fill:#ffebee,stroke:#c62828,stroke-width:2px
    classDef mtgo fill:#fce4ec,stroke:#ad1457,stroke-width:2px
    classDef memory fill:#e0f2f1,stroke:#00695c,stroke-width:2px
    
    class USER_CODE,CLIENT_API,REMOTE_CLIENT,OBJECT_CACHE userSpace
    class DYNAMIC_PROXY,DLR_BINDER,IMPROMPTU,TYPE_PROXY dlr
    class REMOTE_HANDLE,COMMUNICATOR,OBJECT_REF,PRIMITIVES remoting
    class IPC_CHANNEL,SERIALIZATION,CALLBACKS ipc
    class SCUBA_DIVER,HARMONY_HOOKS,REVERSE_COMM injected
    class MTGO_OBJECTS,MANAGED_HEAP,CLR_RUNTIME,OBJECT_PINNING mtgo
    class CLRMD,SNAPSHOT,HEAP_WALKER,TYPE_RESOLVER memory
```

## Architecture détaillée du système de remoting

### 1. Couche Utilisateur (User Space)

#### Client API
```csharp
// L'utilisateur interagit avec des objets typés
var client = RemoteClient.@this;
var game = client.GetGame(); // Retourne un proxy dynamique
dynamic gameState = game.CurrentState; // Navigation transparente
```

#### Object Cache
- **Cache intelligent** : Évite les appels répétés au processus distant
- **Weak References** : Permet le garbage collection des objets non utilisés
- **Type Safety** : Maintient les informations de type pour les proxies

### 2. Dynamic Language Runtime (DLR)

#### DynamicRemoteObject
```csharp
public class DynamicRemoteObject : DynamicObject
{
    // Intercepte tous les appels de méthodes/propriétés
    public override bool TryInvokeMember(InvokeMemberBinder binder, 
                                       object[] args, out object result)
    {
        // Résolution dynamique et appel distant
    }
}
```

#### ImpromptuInterface Integration
- **Typed Proxies** : Conversion des objets dynamiques en interfaces typées
- **Compile-time Safety** : Vérification des types à la compilation
- **Performance** : Cache des delegates pour les appels répétés

### 3. Infrastructure de Remoting

#### Communication Pipeline
```mermaid
sequenceDiagram
    participant User as User Code
    participant Proxy as DynamicProxy
    participant Comm as Communicator
    participant Diver as ScubaDiver
    participant MTGO as MTGO Object
    
    User->>Proxy: obj.GetCards()
    Proxy->>Comm: InvokeMethod("GetCards", [])
    Comm->>Diver: Serialize & Send
    Diver->>MTGO: Invoke on Real Object
    MTGO->>Diver: Return Result
    Diver->>Comm: Serialize & Return
    Comm->>Proxy: Deserialize Result
    Proxy->>User: Return Typed Result
```

#### Object Reference Management
- **Pinning Strategy** : Les objets MTGO sont "épinglés" en mémoire
- **Address Tracking** : Suivi des adresses mémoire des objets distants
- **Lifecycle Management** : Nettoyage automatique des références

### 4. Injection et Hooking

#### ScubaDiver Assembly
```csharp
// Injecté dans le processus MTGO
public class ScubaDiver
{
    // Point d'entrée pour les appels distants
    public object InvokeMethod(ulong objectAddress, string methodName, object[] args)
    {
        // Résolution de l'objet depuis l'adresse
        // Invocation de la méthode via réflection
        // Retour du résultat sérialisé
    }
}
```

#### Harmony Patches
- **Method Interception** : Interception des méthodes MTGO critiques
- **Event Hooking** : Capture des événements du jeu
- **State Monitoring** : Surveillance des changements d'état

### 5. Memory Management avec ClrMD

#### Heap Inspection
```csharp
// Accès direct au heap managé de MTGO
var runtime = DataTarget.AttachToProcess(mtgoProcess.Id).ClrVersions[0].CreateRuntime();
foreach (var obj in runtime.Heap.EnumerateObjects())
{
    // Inspection des objets sans les modifier
    var type = obj.Type;
    var fields = type.Fields;
}
```

#### Type Resolution
- **Dynamic Type Loading** : Chargement des types MTGO à la volée
- **Generic Support** : Gestion des types génériques complexes
- **Assembly Resolution** : Résolution des assemblies MTGO

## Flux d'exécution détaillé

### 1. Initialisation
1. **Process Attachment** : Attachement au processus MTGO via ClrMD
2. **Injection** : Injection de ScubaDiver dans le processus cible
3. **Communication Setup** : Établissement du canal de communication
4. **Type Discovery** : Découverte des types MTGO disponibles

### 2. Appel de Méthode
1. **Dynamic Binding** : Le DLR résout l'appel de méthode
2. **Serialization** : Sérialisation des paramètres
3. **IPC Transfer** : Transfert via Named Pipes ou TCP
4. **Remote Execution** : Exécution dans le processus MTGO
5. **Result Return** : Retour du résultat sérialisé
6. **Proxy Creation** : Création d'un proxy pour le résultat

### 3. Gestion Mémoire
1. **Object Pinning** : Épinglage des objets référencés
2. **Weak References** : Utilisation de références faibles côté client
3. **Garbage Collection** : Nettoyage automatique des objets non utilisés
4. **Memory Pressure** : Gestion de la pression mémoire

## Advantages of this Architecture

### Performance
- **Lazy Loading** : Chargement à la demande des objets
- **Intelligent Caching** : Cache multi-niveaux pour éviter les appels répétés
- **Batch Operations** : Regroupement des opérations pour réduire la latence

### Robustesse
- **Error Recovery** : Récupération automatique des erreurs de communication
- **Process Isolation** : Isolation complète entre SDK et MTGO
- **Memory Safety** : Pas de corruption mémoire possible

### Flexibilité
- **Dynamic Typing** : Support complet du typage dynamique C#
- **Type Safety** : Conversion vers des interfaces typées quand nécessaire
- **Extensibility** : Architecture extensible pour nouveaux types d'objets

## Technologies Clés

- **Microsoft.Diagnostics.Runtime (ClrMD)** : Inspection du heap managé
- **ImpromptuInterface** : Conversion dynamic → typed interfaces
- **Lib.Harmony** : Patching et hooking de méthodes
- **C# DLR** : Dynamic Language Runtime pour les appels dynamiques
- **Named Pipes/TCP** : Communication inter-processus