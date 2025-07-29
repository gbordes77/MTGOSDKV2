# MTGOSDK Build Process Diagram

## Vue d'ensemble du processus de build

```mermaid
graph TD
    subgraph "Build Initialization"
        START[dotnet build] --> PROPS[Load Directory.Build.props]
        PROPS --> RESTORE[NuGet Package Restore]
        RESTORE --> PREBUILD[Pre-Build Tasks]
    end
    
    subgraph "MTGO Detection & Extraction"
        PREBUILD --> DETECT[ExtractMTGOInstallation Task]
        DETECT --> CHECK_LOCAL{MTGO Installed Locally?}
        CHECK_LOCAL -->|Yes| LOCAL_PATH[Use Local Installation]
        CHECK_LOCAL -->|No| DOWNLOAD[Download from Deployment Manifest]
        
        DOWNLOAD --> MANIFEST[Parse Application Manifest]
        MANIFEST --> EXTRACT_FILES[Extract MTGO Assemblies]
        EXTRACT_FILES --> TEMP_DIR[Store in Temp Directory]
        
        LOCAL_PATH --> VERSION_CHECK[Get MTGO Version]
        TEMP_DIR --> VERSION_CHECK
    end
    
    subgraph "Reference Assembly Generation"
        VERSION_CHECK --> REF_CHECK{Reference Assemblies Exist?}
        REF_CHECK -->|Yes| SKIP_GEN[Skip Generation]
        REF_CHECK -->|No| GEN_REF[GenerateReferenceAssemblies Task]
        
        GEN_REF --> REFASMER[JetBrains.Refasmer]
        REFASMER --> FILTER[Apply AllowAll Filter]
        FILTER --> CONVERT[Convert to Reference Assembly]
        CONVERT --> VALIDATE[IL Verification]
        VALIDATE --> STORE_REF[Store Reference Assemblies]
        
        SKIP_GEN --> LOAD_REF[Load Existing References]
        STORE_REF --> LOAD_REF
    end
    
    subgraph "SDK Compilation"
        LOAD_REF --> COMPILE_WIN32[Compile MTGOSDK.Win32]
        COMPILE_WIN32 --> COMPILE_MSBUILD[Compile MTGOSDK.MSBuild]
        COMPILE_MSBUILD --> COMPILE_SDK[Compile MTGOSDK]
        
        COMPILE_SDK --> EMBED_RESOURCES[Embed Resources]
        EMBED_RESOURCES --> ILREPACK[ILRepack Embedded Components]
        ILREPACK --> MULTI_TARGET[Multi-Target Compilation]
        
        subgraph "Multi-Targeting"
            MULTI_TARGET --> NET9[.NET 9.0-windows]
            MULTI_TARGET --> NET48[.NET Framework 4.8]
        end
    end
    
    subgraph "Package Creation"
        NET9 --> PACK[Create NuGet Packages]
        NET48 --> PACK
        PACK --> SIGN[Sign Assemblies]
        SIGN --> VALIDATE_PKG[Package Validation]
        VALIDATE_PKG --> LOCAL_FEED[Update Local Feed]
        LOCAL_FEED --> PUBLISH[Publish to Output]
    end
    
    subgraph "Build Optimization"
        COMPILE_SDK --> TIMESTAMP[Build Timestamp Check]
        TIMESTAMP --> INCREMENTAL{Files Changed?}
        INCREMENTAL -->|No| SKIP_COPY[Skip Copy Operations]
        INCREMENTAL -->|Yes| FULL_BUILD[Full Build Process]
        SKIP_COPY --> PACK
        FULL_BUILD --> PACK
    end
    
    %% External Dependencies
    subgraph "External Tools"
        REFASMER_TOOL[JetBrains.Refasmer]
        ILVERIFY_TOOL[Microsoft.ILVerification]
        ILREPACK_TOOL[ILRepack]
        NUGET_TOOL[NuGet.exe]
    end
    
    %% Tool Usage
    REFASMER --> REFASMER_TOOL
    VALIDATE --> ILVERIFY_TOOL
    ILREPACK --> ILREPACK_TOOL
    LOCAL_FEED --> NUGET_TOOL
    
    %% Styling
    classDef process fill:#e3f2fd,stroke:#1976d2,stroke-width:2px
    classDef decision fill:#fff3e0,stroke:#f57c00,stroke-width:2px
    classDef external fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    classDef output fill:#e8f5e8,stroke:#388e3c,stroke-width:2px
    classDef skip fill:#fafafa,stroke:#616161,stroke-width:1px
    
    class START,PROPS,RESTORE,PREBUILD,DETECT,DOWNLOAD,MANIFEST,EXTRACT_FILES,VERSION_CHECK,GEN_REF,REFASMER,FILTER,CONVERT,VALIDATE,COMPILE_WIN32,COMPILE_MSBUILD,COMPILE_SDK,EMBED_RESOURCES,ILREPACK,MULTI_TARGET,NET9,NET48,PACK,SIGN,VALIDATE_PKG,TIMESTAMP,FULL_BUILD process
    class CHECK_LOCAL,REF_CHECK,INCREMENTAL decision
    class REFASMER_TOOL,ILVERIFY_TOOL,ILREPACK_TOOL,NUGET_TOOL external
    class LOCAL_PATH,TEMP_DIR,STORE_REF,LOCAL_FEED,PUBLISH output
    class SKIP_GEN,LOAD_REF,SKIP_COPY skip
```

## Phases du processus de build

### 1. Initialisation du Build
- **Chargement des propriétés** : Directory.Build.props définit les chemins et configurations
- **Restauration NuGet** : Téléchargement des dépendances avec lock files
- **Tâches pré-build** : Préparation de l'environnement de compilation

### 2. MTGO Detection and Extraction
- **Local detection**: Search for existing MTGO installation
- **Download**: If absent, download from deployment manifest
- **Extraction**: Extract MTGO assemblies to temporary directory
- **Versioning**: Determine MTGO version for compatibility

### 3. Reference Assembly Generation
- **Cache verification**: Check if references already exist for this version
- **Refasmer generation**: Convert MTGO assemblies to references
- **Filtering**: Apply AllowAll filter to expose all public types
- **IL validation**: Verify generated IL code validity

### 4. SDK Compilation
- **Sequential compilation**: Win32 → MSBuild → Main SDK
- **Resource integration**: Embedding Launcher and ScubaDiver components
- **ILRepack**: Merge embedded assemblies
- **Multi-targeting**: Compilation for .NET 9 and .NET Framework 4.8

### 5. Build Optimizations
- **Timestamps**: Compare timestamps to avoid unnecessary recompilations
- **Incremental build**: Skip operations if no changes detected
- **Cache intelligent** : Réutilisation des artefacts précédents

### 6. Création des Packages
- **Packaging NuGet** : Création des packages .nupkg et .snupkg
- **Signature** : Signature des assemblies pour la sécurité
- **Validation** : Validation de l'intégrité des packages
- **Feed local** : Mise à jour du feed NuGet local pour les tests

## Outils et Technologies

### Outils de Build
- **JetBrains.Refasmer** : Génération d'assemblies de référence
- **Microsoft.ILVerification** : Validation du code IL
- **ILRepack** : Fusion d'assemblies
- **NuGet.exe** : Gestion des packages

### Optimisations MSBuild
- **Builds déterministes** : Reproductibilité des builds
- **Compilation parallèle** : Utilisation de tous les cœurs CPU
- **Cache de packages** : Réutilisation des packages téléchargés
- **Builds incrémentaux** : Compilation uniquement des fichiers modifiés

## Configuration Avancée

### Multi-Targeting Strategy
```xml
<TargetFrameworks>net9.0-windows;$(MTGOSDKCoreTFM)</TargetFrameworks>
```
- **.NET 9.0-windows** : Version moderne avec toutes les fonctionnalités
- **.NET Framework 4.8** : Compatibilité avec MTGO (même runtime)

### Build Conditionals
- **Windows uniquement** : Certaines tâches ne s'exécutent que sur Windows
- **Mode développement** : Feed local activé avec `UseLocalFeed=true`
- **CI/CD** : Builds déterministes avec `ContinuousIntegrationBuild=true`

### Performance Optimizations
- **Timestamp Comparison** : Évite les recompilations inutiles
- **Parallel Restore** : Restauration parallèle des packages
- **Incremental Linking** : Liaison incrémentale des assemblies
- **Output Caching** : Cache des sorties de compilation