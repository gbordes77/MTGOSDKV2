# 📋 TODO Tracker - MTGOSDK

*Last updated: July 29, 2025*

This file centralizes all TODOs, FIXMEs, and HACKs identified in the MTGOSDK project, classified by priority and autonomy level.

## 🟢 AUTONOMOUS (Kiro can resolve alone)

### 📚 Documentation & Tests

| ID | Priority | File | Line | Description | Status |
|----|----------|------|------|-------------|--------|
| DOC-001 | Medium | `MTGOSDK.Tests/src/Tests/MTGOSDK.API/Collection.cs` | 25 | Get an ItemCollection instance of the collection in the heap | ⏳ Todo |
| DOC-002 | Medium | `MTGOSDK.Tests/src/Tests/MTGOSDK.API/Trade.cs` | 56 | Implement ValidateTrade method | ⏳ Todo |
| DOC-003 | Low | `MTGOSDK/src/API/Collection/ItemCollection.cs` | 38 | Add AddItem, RemoveItem, AddRange, RemoveRange | ⏳ Todo |

### 🔧 Simple Improvements

| ID | Priority | File | Line | Description | Status |
|----|----------|------|------|-------------|--------|
| IMP-001 | Low | `MTGOSDK/src/Core/Remoting/Reflection/RemoteParameterInfo.cs` | 29 | Type needs to be converted to a remote type ? | ⏳ Todo |
| IMP-002 | Low | `MTGOSDK/src/Core/Remoting/Interop/RemoteObjectRef.cs` | 133 | I think addresses as token should be reworked | ⏳ Todo |
| IMP-003 | Low | `MTGOSDK/src/Core/Remoting/Types/RemoteObject.cs` | 102 | Add a check for amount of parameters and types (need to be dynamics) | ⏳ Todo |

## 🟡 COLLABORATION REQUIRED (Validation needed)

### ⚡ Critical Performance

| ID | Priority | File | Line | Description | Status |
|----|----------|------|------|-------------|--------|
| PERF-001 | **Critical** | `MTGOSDK/src/API/Play/Games/Game.cs` | 43 | Use a more efficient method of retrieving view model objects without traversing the client's managed heap | ⏳ Todo |
| PERF-002 | High | `MTGOSDK/src/API/Play/Games/Actions/CardSelectorAction.cs` | 33 | Map CTN to GameCard (i.e. ICardDataManager.GetCardDefinitionForTextureNumber(CTN)) | ⏳ Todo |

### 🏗️ Architecture & Design

| ID | Priority | File | Line | Description | Status |
|----|----------|------|------|-------------|--------|
| ARCH-001 | High | `MTGOSDK/src/Core/Remoting/RemoteHandle.cs` | 334 | Registering here in the cache is a hack but we couldn't register within "TypeResolver.Resolve" | ⏳ Todo |
| ARCH-002 | High | `MTGOSDK/src/Core/Remoting/Types/RemoteTypesFactory.cs` | 47 | Registring here in the cache is a hack but we couldn't register within "TypeResolver.Resolve" | ⏳ Todo |
| ARCH-003 | Medium | `MTGOSDK/src/Core/Remoting/Types/RemoteType.cs` | 45 | This ctor is experimental because it makes a LOT of assumptions | ⏳ Todo |

### 🔍 Type Resolution

| ID | Priority | File | Line | Description | Status |
|----|----------|------|------|-------------|--------|
| TYPE-001 | High | `MTGOSDK/src/Core/Memory/Snapshot/UnifiedAppDomain.cs` | 60 | Switch to recursive type-resolution for complex types like Dictionary<FirstAssembly.FirstType, SecondAssembly.SecondType> | ⏳ Todo |
| TYPE-002 | High | `MTGOSDK/src/API/Collection/CollectionManager.cs` | 117 | Fix type casting of nested types, i.e. Dictionary<string, CardSet> | ⏳ Todo |
| TYPE-003 | Medium | `MTGOSDK/src/Core/Memory/Snapshot/UnifiedAppDomain.cs` | 74 | Does this event work? it turns List<int> and List<string> both to List`1? | ⏳ Todo |

## 🔴 TECHNICAL EXPERTISE REQUIRED

### 🚨 Critical Bugs

| ID | Priority | File | Line | Description | Status |
|----|----------|------|------|-------------|--------|
| BUG-001 | **Critical** | `MTGOSDK/src/Core/Remoting/Reflection/RemoteFieldInfo.cs` | 105 | This will break on the first enum value which represents 2 or more flags | ⏳ Todo |
| BUG-002 | **Critical** | `MTGOSDK/src/Core/Remoting/RemoteActivator.cs` | 38 | This breaks on the first enum value which has 2 or more flags | ⏳ Todo |
| BUG-003 | **Critical** | `MTGOSDK/src/Core/Remoting/Interop/RemoteFunctionsInvokeHelper.cs` | 101 | This will break on the first enum value which represents 2 or more flags | ⏳ Todo |

### 🔧 Missing Features

| ID | Priority | File | Line | Description | Status |
|----|----------|------|------|-------------|--------|
| FEAT-001 | High | `MTGOSDK/src/Core/Remoting/Types/DynamicRemoteObject.cs` | 429 | Cannot hook to remote events yet | ⏳ Todo |
| FEAT-002 | High | `MTGOSDK/src/Core/Remoting/Types/DynamicRemoteObject.cs` | 580 | Cannot hook to remote events yet | ⏳ Todo |
| FEAT-003 | Medium | `MTGOSDK/src/Core/Remoting/Interop/PrimitivesEncoder.cs` | 50 | Support arrays of RemoteObjects/DynamicRemoteObject | ⏳ Todo |

### 🛠️ Complex Improvements

| ID | Priority | File | Line | Description | Status |
|----|----------|------|------|-------------|--------|
| COMP-001 | Medium | `MTGOSDK/src/Core/Remoting/Interop/RemoteFunctionsInvokeHelper.cs` | 92 | Actually validate parameters and expected parameters | ⏳ Todo |
| COMP-002 | Medium | `MTGOSDK/src/Core/Remoting/Types/DynamicRemoteObject.cs` | 69 | We COULD possibly check the args types (local ones, RemoteObjects, DynamicObjects, ...) if we still have multiple results | ⏳ Todo |
| COMP-003 | Low | `MTGOSDK/src/Core/Reflection/Extensions/TypeExtensions.cs` | 192 | Apply more comprehensive check to ensure that these types are present in the consumer AppDomain and have `ToString` and `Parse` methods | ⏳ Todo |

### 🐛 Required Fixes

| ID | Priority | File | Line | Description | Status |
|----|----------|------|------|-------------|--------|
| FIX-001 | High | `MTGOSDK/src/API/Play/History/HistoricalItem.cs` | 50 | Historical items do not have an actual PlayerEvent | ⏳ Todo |
| FIX-002 | Medium | `MTGOSDK/src/Core/Remoting/Types/RemoteTypesFactory.cs` | 279 | Add stub method to indicate this error to the users? | ⏳ Todo |
| FIX-003 | Medium | `MTGOSDK/src/Core/Remoting/Types/RemoteTypesFactory.cs` | 289 | Add stub method to indicate this error to the users? | ⏳ Todo |
| FIX-004 | Medium | `MTGOSDK/src/Core/Remoting/Types/RemoteTypesFactory.cs` | 316 | Add stub method to indicate this error to the users? | ⏳ Todo |

### 🔬 Generic Issues

| ID | Priority | File | Line | Description | Status |
|----|----------|------|------|-------------|--------|
| GEN-001 | High | `MTGOSDK/src/Core/Remoting/Types/RemoteTypesFactory.cs` | 313 | This sometimes throws because of generic results (like List<SomeAssembly.SomeObject>) | ⏳ Todo |

## 📊 Statistics

### By Priority
- **Critical**: 4 items (17%)
- **High**: 8 items (33%)
- **Medium**: 9 items (38%)
- **Low**: 3 items (12%)

### By Category
- **🟢 Autonomous**: 6 items (25%)
- **🟡 Collaboration**: 8 items (33%)
- **🔴 Expertise**: 10 items (42%)

### By Type
- **Performance**: 2 items
- **Architecture**: 3 items
- **Bugs**: 3 items
- **Features**: 3 items
- **Documentation**: 3 items
- **Type Resolution**: 3 items
- **Others**: 7 items

## 🎯 Recommended Action Plan

### Phase 1 - Critical (Immediate)
1. **BUG-001, BUG-002, BUG-003**: Fix enum flags bugs
2. **PERF-001**: Optimize ViewModel retrieval

### Phase 2 - High Priority (Short term)
1. **ARCH-001, ARCH-002**: Refactor type cache
2. **TYPE-001, TYPE-002**: Improve type resolution
3. **FEAT-001, FEAT-002**: Implement remote events

### Phase 3 - Medium Priority (Medium term)
1. **COMP-001**: Parameter validation
2. **FIX-001**: Fix HistoricalItems
3. **GEN-001**: Generic type handling

### Phase 4 - Low Priority (Long term)
1. **DOC-001, DOC-002, DOC-003**: Complete documentation
2. **IMP-001, IMP-002, IMP-003**: Minor improvements

## 🔄 Update Process

This file is automatically updated by the `scripts/update-todos.ps1` script:

```powershell
# Run to update the tracker
.\scripts\update-todos.ps1
```

### Resolution Workflow

1. **Assignment**: Assign item to a developer
2. **In Progress**: Change status to "🔄 In Progress"
3. **Review**: Submit for review if necessary
4. **Complete**: Mark as "✅ Complete"
5. **Archive**: Move to history after 30 days

## 📝 Notes

- IDs are unique and should not be reused
- Priorities can be adjusted based on project needs
- Items marked as "Critical" should be treated with priority
- This file should be synchronized with GitHub issues

---

*To add a new TODO, use the standard format in code and run the update script.*