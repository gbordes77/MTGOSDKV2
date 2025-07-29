# Contributing Guide - MTGOSDK

Thank you for your interest in contributing to MTGOSDK! This guide explains how to participate in the project development.

## 🚀 Quick Start

### Prerequisites

- **.NET 9.0 SDK** or later
- **Visual Studio 2022** (v17.13+) or **VS Code**
- **Git** for version control
- **Windows 10/11** (required for MTGO)
- **MTGO** installed and working

### Environment Setup

1. **Fork and Clone**
```bash
# Fork the repository on GitHub
# Then clone your fork
git clone https://github.com/YOUR-USERNAME/MTGOSDK.git
cd MTGOSDK
```

2. **Install Dependencies**
```bash
# Restore NuGet packages
dotnet restore

# Install .NET tools
dotnet tool restore
```

3. **Initial Build**
```bash
# Full project build
dotnet build -c Release

# Or with MSBuild
msbuild /t:Build /p:Configuration=Release
```

4. **Verification**
```bash
# Run tests
dotnet test

# Check formatting
dotnet format --verify-no-changes
```

## 📋 Types of Contributions

### 🐛 Bug Fixes

1. **Identify the Bug**
   - Check if an issue doesn't already exist
   - Create a detailed issue with reproduction steps
   - Assign appropriate labels

2. **Develop the Fix**
   - Create a branch: `fix/bug-description`
   - Write tests to reproduce the bug
   - Implement the fix
   - Verify all tests pass

3. **Submit the PR**
   - Clear description of problem and solution
   - Reference the corresponding issue
   - Include regression tests

### ✨ New Features

1. **Proposal**
   - Create a "Feature Request" issue
   - Discuss approach with maintainers
   - Get approval before starting

2. **Development**
   - Create a branch: `feature/feature-name`
   - Follow existing patterns
   - Document public APIs
   - Add comprehensive tests

3. **Documentation**
   - Update API documentation
   - Add usage examples
   - Update CHANGELOG

### 📚 Documentation

1. **Documentation Types**
   - API documentation
   - Usage guides
   - Practical examples
   - Architecture and design

2. **Standards**
   - Markdown for all documents
   - Testable code examples
   - Screenshots when necessary
   - Consistent internal links

### 🧪 Tests

1. **Test Types**
   - Unit tests (mandatory)
   - Integration tests
   - Performance tests
   - Regression tests

2. **Coverage**
   - Minimum 80% code coverage
   - All critical paths tested
   - Error case testing

## 🏗️ Project Architecture

### Folder Structure

```
MTGOSDK/
├── MTGOSDK/                 # Main package
│   ├── src/
│   │   ├── API/            # Public APIs
│   │   ├── Core/           # Core functionality
│   │   └── Resources/      # Embedded resources
│   └── lib/                # Internal components
├── MTGOSDK.MSBuild/        # Build package
├── MTGOSDK.Win32/          # Win32 APIs
├── MTGOSDK.Tests/          # Tests
├── examples/               # Usage examples
├── docs/                   # Documentation
└── tools/                  # Build tools
```

### Code Patterns

#### 1. DLRWrapper Pattern
```csharp
public sealed class MyClass : DLRWrapper<IMyInterface>
{
    internal override Type type => typeof(IMyInterface);
    internal override dynamic obj => myObject;
    
    // Wrapped properties
    public string Name => @base.Name;
    
    // Wrapped methods
    public void DoSomething() => @base.DoSomething();
}
```

#### 2. Event Proxy Pattern
```csharp
public EventProxy<MyEventArgs> MyEvent =
    new(/* source object */ obj, nameof(MyEvent));
```

#### 3. Object Provider Pattern
```csharp
private static readonly IMyService s_myService =
    ObjectProvider.Get<IMyService>();
```

### Naming Conventions

- **Classes**: PascalCase (`RemoteClient`)
- **Methods**: PascalCase (`GetCollection`)
- **Properties**: PascalCase (`CurrentUser`)
- **Private fields**: camelCase with underscore (`_fieldName`)
- **Constants**: UPPER_CASE (`MAX_RETRY_COUNT`)

## 🔧 Development Process

### Git Workflow

1. **Create a Branch**
```bash
# From main
git checkout main
git pull origin main
git checkout -b feature/my-new-feature
```

2. **Development**
```bash
# Frequent commits with clear messages
git add .
git commit -m "feat: add support for trade events"

# Regular push
git push origin feature/my-new-feature
```

3. **Pull Request**
   - Descriptive title
   - Detailed description
   - Reference related issues
   - Request review

### Commit Messages

Use [Conventional Commits](https://www.conventionalcommits.org/) format:

```
type(scope): description

[optional body]

[optional footer]
```

**Types:**
- `feat`: new feature
- `fix`: bug fix
- `docs`: documentation
- `style`: formatting, no code change
- `refactor`: refactoring
- `test`: adding/modifying tests
- `chore`: maintenance tasks

**Examples:**
```
feat(collection): add FindCardsByArtist method
fix(remoting): fix memory leak in RemoteObject
docs(api): update Collection documentation
test(trade): add tests for TradeManager
```

### Code Review

#### Checklist for Reviewers

- [ ] Code follows project conventions
- [ ] Tests are present and passing
- [ ] Documentation is updated
- [ ] No regression introduced
- [ ] Acceptable performance
- [ ] Security respected

#### Checklist for Contributors

- [ ] Code formatted with `dotnet format`
- [ ] All tests pass
- [ ] Documentation updated
- [ ] Examples added if necessary
- [ ] CHANGELOG updated
- [ ] No compilation warnings

## 🧪 Testing and Quality

### Running Tests

```bash
# All tests
dotnet test

# Tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Specific tests
dotnet test --filter "Category=Unit"
dotnet test --filter "FullyQualifiedName~Collection"
```

### Writing Tests

#### Unit Tests
```csharp
[Test]
public void GetCollection_ShouldReturnValidCollection()
{
    // Arrange
    var client = RemoteClient.@this;
    
    // Act
    var collection = client.GetCollection();
    
    // Assert
    Assert.That(collection, Is.Not.Null);
    Assert.That(collection.Count, Is.GreaterThan(0));
}
```

#### Integration Tests
```csharp
[Test]
[Category("Integration")]
public void TradeManager_ShouldHandleTradeRequest()
{
    // Requires MTGO running
    Assume.That(RemoteClient.IsInitialized, Is.True);
    
    var tradeManager = RemoteClient.@this.GetTradeManager();
    // Test logic...
}
```

### Quality Tools

```bash
# Code formatting
dotnet format

# Static analysis
dotnet build --verbosity normal

# Package verification
dotnet list package --outdated
```

## 📖 Documentation

### Documentation Standards

#### XML Documentation
```csharp
/// <summary>
/// Retrieves the user's collection of cards.
/// </summary>
/// <returns>A <see cref="Collection"/> containing all owned cards.</returns>
/// <exception cref="MTGOConnectionException">
/// Thrown when MTGO is not running or connection fails.
/// </exception>
public Collection GetCollection()
{
    // Implementation
}
```

#### Markdown Documentation
- Use hierarchical titles
- Include code examples
- Add navigation links
- Maintain style consistency

### Documentation Updates

1. **API Reference**
   - Document all public APIs
   - Include usage examples
   - Explain possible exceptions

2. **Guides**
   - Step-by-step tutorials
   - Common use cases
   - Best practices

3. **Architecture**
   - Up-to-date diagrams
   - Pattern explanations
   - Design decisions

## 🚀 Release and Deployment

### Release Process

1. **Preparation**
   - Update CHANGELOG
   - Verify compatibility
   - Run all tests

2. **Versioning**
   - Follow [Semantic Versioning](https://semver.org/)
   - MAJOR.MINOR.PATCH
   - Pre-releases: 1.0.0-alpha.1

3. **Publication**
   - Git tag with version
   - Build NuGet packages
   - Automatic publication via CI/CD

### Compatibility

- **Breaking Changes**: Increment MAJOR
- **New Features**: Increment MINOR
- **Bug Fixes**: Increment PATCH

## 🤝 Community

### Communication

- **GitHub Issues**: Bugs and feature requests
- **GitHub Discussions**: Questions and discussions
- **Pull Requests**: Code reviews and technical discussions

### Code of Conduct

- Respect all contributors
- Be constructive in criticism
- Help new contributors
- Maintain an inclusive environment

### Recognition

Contributors are recognized in:
- CONTRIBUTORS.md file
- Release notes
- Project documentation

## 🔍 Debugging and Troubleshooting

### Debug Environment

```csharp
// Configuration for debugging
var options = new ClientOptions
{
    EnableVerboseLogging = true,
    LogLevel = LogLevel.Debug
};
```

### Useful Tools

- **Visual Studio Debugger**: Step-by-step debugging
- **dotnet-trace**: Performance profiling
- **dotnet-dump**: Memory dump analysis
- **Process Monitor**: File access monitoring

### Common Issues

1. **MTGO Not Detected**
   - Verify MTGO is running
   - Check permissions
   - Restart as administrator

2. **Build Errors**
   - Clean and rebuild
   - Check .NET versions
   - Restore NuGet packages

3. **Failing Tests**
   - Check MTGO state
   - Clean test caches
   - Run individually

## 📝 Contribution Checklist

Before submitting your PR:

### Code
- [ ] Code compiles without warnings
- [ ] All tests pass
- [ ] Code formatted with `dotnet format`
- [ ] No dead or commented code
- [ ] Appropriate error handling

### Tests
- [ ] Unit tests added
- [ ] Code coverage maintained
- [ ] Integration tests if necessary
- [ ] Regression tests for bugs

### Documentation
- [ ] XML documentation for public APIs
- [ ] User documentation updated
- [ ] Usage examples added
- [ ] CHANGELOG updated

### Git
- [ ] Conventional commit messages
- [ ] Branch up to date with main
- [ ] No merge commits
- [ ] Clean history

---

**Thank you for contributing to MTGOSDK! 🎉**

For any questions, feel free to open an issue or discussion on GitHub.

**Navigation:**
- [⬅️ Quick Start Guide](quick-start.md)
- [➡️ Troubleshooting](troubleshooting.md)
- [🏠 Main Documentation](../README.md)