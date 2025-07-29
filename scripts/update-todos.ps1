# Script de mise à jour automatique du TODO Tracker
# Usage: .\scripts\update-todos.ps1

param(
    [string]$OutputFile = "TODO-TRACKER.md",
    [switch]$Verbose = $false
)

# Configuration
$TodoPatterns = @("TODO", "FIXME", "HACK", "XXX")
$FileExtensions = @("*.cs", "*.md", "*.csproj", "*.props", "*.targets")
$ExcludePaths = @(
    "bin\*",
    "obj\*",
    "packages\*",
    ".git\*",
    ".vs\*",
    "node_modules\*",
    "TODO-TRACKER.md"
)

# Fonction pour analyser les TODOs
function Get-TodoItems {
    param([string]$RootPath)
    
    $todos = @()
    $idCounter = 1
    
    Write-Host "🔍 Analyse des fichiers pour les TODOs..." -ForegroundColor Cyan
    
    foreach ($extension in $FileExtensions) {
        $files = Get-ChildItem -Path $RootPath -Filter $extension -Recurse | 
                 Where-Object { 
                     $exclude = $false
                     foreach ($excludePath in $ExcludePaths) {
                         if ($_.FullName -like "*$excludePath*") {
                             $exclude = $true
                             break
                         }
                     }
                     -not $exclude
                 }
        
        foreach ($file in $files) {
            if ($Verbose) {
                Write-Host "  📄 Analyse: $($file.Name)" -ForegroundColor Gray
            }
            
            $content = Get-Content $file.FullName -ErrorAction SilentlyContinue
            if (-not $content) { continue }
            
            for ($i = 0; $i -lt $content.Length; $i++) {
                $line = $content[$i]
                
                foreach ($pattern in $TodoPatterns) {
                    if ($line -match "(?i)$pattern") {
                        # Extraire le commentaire TODO
                        $comment = ""
                        if ($line -match "(?i)//\s*$pattern\s*:?\s*(.+)") {
                            $comment = $matches[1].Trim()
                        } elseif ($line -match "(?i)/\*\s*$pattern\s*:?\s*(.+)\s*\*/") {
                            $comment = $matches[1].Trim()
                        } elseif ($line -match "(?i)<!--\s*$pattern\s*:?\s*(.+)\s*-->") {
                            $comment = $matches[1].Trim()
                        } else {
                            $comment = $line.Trim()
                        }
                        
                        # Lire les lignes suivantes pour le contexte
                        $context = @()
                        for ($j = $i + 1; $j -lt [Math]::Min($i + 3, $content.Length); $j++) {
                            if ($content[$j].Trim() -match "^//|^/\*|^\*|^<!--" -and 
                                $content[$j].Trim() -notmatch "^\s*$") {
                                $context += $content[$j].Trim()
                            } else {
                                break
                            }
                        }
                        
                        $relativePath = $file.FullName.Replace($RootPath, "").TrimStart('\', '/')
                        
                        $todo = [PSCustomObject]@{
                            Id = $idCounter++
                            Pattern = $pattern
                            File = $relativePath
                            Line = $i + 1
                            Comment = $comment
                            Context = $context -join " "
                            Priority = Get-TodoPriority -Comment $comment -Pattern $pattern
                            Category = Get-TodoCategory -File $relativePath -Comment $comment
                            Complexity = Get-TodoComplexity -Comment $comment -Context ($context -join " ")
                            Autonomy = Get-TodoAutonomy -File $relativePath -Comment $comment -Pattern $pattern
                        }
                        
                        $todos += $todo
                        
                        if ($Verbose) {
                            Write-Host "    ✅ Trouvé: $pattern à la ligne $($i + 1)" -ForegroundColor Green
                        }
                    }
                }
            }
        }
    }
    
    Write-Host "📊 Total: $($todos.Count) TODOs trouvés" -ForegroundColor Yellow
    return $todos
}

# Fonction pour déterminer la priorité
function Get-TodoPriority {
    param([string]$Comment, [string]$Pattern)
    
    $comment = $Comment.ToLower()
    
    if ($Pattern -eq "FIXME" -or $comment -match "critical|urgent|important|break|crash|bug") {
        return "Critique"
    } elseif ($comment -match "performance|slow|optimize|efficient|heap|memory") {
        return "Haute"
    } elseif ($comment -match "should|could|might|consider|improve") {
        return "Moyenne"
    } else {
        return "Basse"
    }
}

# Fonction pour déterminer la catégorie
function Get-TodoCategory {
    param([string]$File, [string]$Comment)
    
    $file = $File.ToLower()
    $comment = $Comment.ToLower()
    
    if ($file -match "test" -or $comment -match "test") {
        return "Tests"
    } elseif ($comment -match "performance|optimize|efficient|slow") {
        return "Performance"
    } elseif ($comment -match "architecture|design|refactor|hack") {
        return "Architecture"
    } elseif ($comment -match "type|generic|cast|resolve") {
        return "Type Resolution"
    } elseif ($comment -match "document|comment|example") {
        return "Documentation"
    } elseif ($comment -match "bug|fix|break|error") {
        return "Bug"
    } elseif ($comment -match "feature|implement|add|support") {
        return "Feature"
    } else {
        return "Amélioration"
    }
}

# Fonction pour déterminer la complexité
function Get-TodoComplexity {
    param([string]$Comment, [string]$Context)
    
    $text = ($Comment + " " + $Context).ToLower()
    
    if ($text -match "architecture|refactor|rewrite|complex|generic|recursive") {
        return "Complexe"
    } elseif ($text -match "implement|add|create|support|validate") {
        return "Modérée"
    } else {
        return "Simple"
    }
}

# Fonction pour déterminer l'autonomie
function Get-TodoAutonomy {
    param([string]$File, [string]$Comment, [string]$Pattern)
    
    $file = $File.ToLower()
    $comment = $Comment.ToLower()
    
    # Autonome : documentation, tests simples, améliorations mineures
    if ($file -match "test" -or 
        $comment -match "document|comment|example|simple|add.*method|implement.*test") {
        return "Autonome"
    }
    
    # Expert : bugs critiques, architecture, performance
    if ($Pattern -eq "FIXME" -or 
        $comment -match "break|crash|hack|architecture|refactor|performance|heap|memory|generic") {
        return "Expert"
    }
    
    # Collaboration : tout le reste
    return "Collaboration"
}

# Fonction pour générer le markdown
function Generate-TodoMarkdown {
    param([array]$Todos, [string]$OutputPath)
    
    Write-Host "📝 Génération du fichier markdown..." -ForegroundColor Cyan
    
    $markdown = @"
# 📋 TODO Tracker - MTGOSDK

*Dernière mise à jour : $(Get-Date -Format "dd MMMM yyyy à HH:mm")*

Ce fichier centralise tous les TODOs, FIXMEs, et HACKs identifiés dans le projet MTGOSDK, classés par priorité et niveau d'autonomie.

"@

    # Grouper par autonomie
    $autonomeItems = $Todos | Where-Object { $_.Autonomy -eq "Autonome" }
    $collaborationItems = $Todos | Where-Object { $_.Autonomy -eq "Collaboration" }
    $expertItems = $Todos | Where-Object { $_.Autonomy -eq "Expert" }
    
    # Section Autonome
    $markdown += @"

## 🟢 AUTONOME (Kiro peut résoudre seul)

"@
    
    if ($autonomeItems.Count -gt 0) {
        $autonomeByCategory = $autonomeItems | Group-Object Category
        foreach ($group in $autonomeByCategory) {
            $markdown += @"

### $($group.Name)

| ID | Priorité | Fichier | Ligne | Description | Statut |
|----|----------|---------|-------|-------------|--------|
"@
            foreach ($item in $group.Group | Sort-Object Priority, File) {
                $id = "$($group.Name.Substring(0,3).ToUpper())-$('{0:D3}' -f $item.Id)"
                $markdown += "`n| $id | $($item.Priority) | ``$($item.File)`` | $($item.Line) | $($item.Comment) | ⏳ Todo |"
            }
        }
    } else {
        $markdown += "`n*Aucun item autonome identifié.*"
    }
    
    # Section Collaboration
    $markdown += @"

## 🟡 COLLABORATION REQUISE (Validation nécessaire)

"@
    
    if ($collaborationItems.Count -gt 0) {
        $collaborationByCategory = $collaborationItems | Group-Object Category
        foreach ($group in $collaborationByCategory) {
            $markdown += @"

### $($group.Name)

| ID | Priorité | Fichier | Ligne | Description | Statut |
|----|----------|---------|-------|-------------|--------|
"@
            foreach ($item in $group.Group | Sort-Object Priority, File) {
                $id = "$($group.Name.Substring(0,3).ToUpper())-$('{0:D3}' -f $item.Id)"
                $priorityIcon = if ($item.Priority -eq "Critique") { "**Critique**" } else { $item.Priority }
                $markdown += "`n| $id | $priorityIcon | ``$($item.File)`` | $($item.Line) | $($item.Comment) | ⏳ Todo |"
            }
        }
    } else {
        $markdown += "`n*Aucun item de collaboration identifié.*"
    }
    
    # Section Expert
    $markdown += @"

## 🔴 EXPERTISE TECHNIQUE REQUISE

"@
    
    if ($expertItems.Count -gt 0) {
        $expertByCategory = $expertItems | Group-Object Category
        foreach ($group in $expertByCategory) {
            $markdown += @"

### $($group.Name)

| ID | Priorité | Fichier | Ligne | Description | Statut |
|----|----------|---------|-------|-------------|--------|
"@
            foreach ($item in $group.Group | Sort-Object Priority, File) {
                $id = "$($group.Name.Substring(0,3).ToUpper())-$('{0:D3}' -f $item.Id)"
                $priorityIcon = if ($item.Priority -eq "Critique") { "**Critique**" } else { $item.Priority }
                $markdown += "`n| $id | $priorityIcon | ``$($item.File)`` | $($item.Line) | $($item.Comment) | ⏳ Todo |"
            }
        }
    } else {
        $markdown += "`n*Aucun item expert identifié.*"
    }
    
    # Statistiques
    $totalItems = $Todos.Count
    $critiqueCount = ($Todos | Where-Object { $_.Priority -eq "Critique" }).Count
    $hauteCount = ($Todos | Where-Object { $_.Priority -eq "Haute" }).Count
    $moyenneCount = ($Todos | Where-Object { $_.Priority -eq "Moyenne" }).Count
    $basseCount = ($Todos | Where-Object { $_.Priority -eq "Basse" }).Count
    
    $markdown += @"

## 📊 Statistiques

### Par Priorité
- **Critique** : $critiqueCount items ($(if($totalItems -gt 0){[math]::Round($critiqueCount/$totalItems*100)}else{0})%)
- **Haute** : $hauteCount items ($(if($totalItems -gt 0){[math]::Round($hauteCount/$totalItems*100)}else{0})%)
- **Moyenne** : $moyenneCount items ($(if($totalItems -gt 0){[math]::Round($moyenneCount/$totalItems*100)}else{0})%)
- **Basse** : $basseCount items ($(if($totalItems -gt 0){[math]::Round($basseCount/$totalItems*100)}else{0})%)

### Par Catégorie
- **🟢 Autonome** : $($autonomeItems.Count) items ($(if($totalItems -gt 0){[math]::Round($autonomeItems.Count/$totalItems*100)}else{0})%)
- **🟡 Collaboration** : $($collaborationItems.Count) items ($(if($totalItems -gt 0){[math]::Round($collaborationItems.Count/$totalItems*100)}else{0})%)
- **🔴 Expertise** : $($expertItems.Count) items ($(if($totalItems -gt 0){[math]::Round($expertItems.Count/$totalItems*100)}else{0})%)

### Par Type
"@
    
    $categoryStats = $Todos | Group-Object Category | Sort-Object Count -Descending
    foreach ($stat in $categoryStats) {
        $markdown += "`n- **$($stat.Name)** : $($stat.Count) items"
    }
    
    $markdown += @"

## 🔄 Processus de Mise à Jour

Ce fichier est mis à jour automatiquement par le script ``scripts/update-todos.ps1`` :

``````powershell
# Exécuter pour mettre à jour le tracker
.\scripts\update-todos.ps1
``````

### Workflow de Résolution

1. **Assignation** : Assigner l'item à un développeur
2. **En cours** : Changer le statut à "🔄 En cours"
3. **Review** : Soumettre pour review si nécessaire
4. **Terminé** : Marquer comme "✅ Terminé"
5. **Archivage** : Déplacer vers l'historique après 30 jours

## 📝 Notes

- Les IDs sont uniques et ne doivent pas être réutilisés
- Les priorités peuvent être ajustées selon les besoins du projet
- Les items marqués comme "Critique" doivent être traités en priorité
- Ce fichier doit être synchronisé avec les issues GitHub

---

*Pour ajouter un nouveau TODO, utilisez le format standard dans le code et exécutez le script de mise à jour.*
"@

    # Écrire le fichier
    $markdown | Out-File -FilePath $OutputPath -Encoding UTF8
    Write-Host "✅ Fichier généré: $OutputPath" -ForegroundColor Green
}

# Script principal
try {
    $rootPath = Get-Location
    Write-Host "🚀 Démarrage de l'analyse des TODOs dans: $rootPath" -ForegroundColor Magenta
    
    # Analyser les TODOs
    $todos = Get-TodoItems -RootPath $rootPath
    
    if ($todos.Count -eq 0) {
        Write-Host "🎉 Aucun TODO trouvé dans le projet!" -ForegroundColor Green
        return
    }
    
    # Générer le markdown
    Generate-TodoMarkdown -Todos $todos -OutputPath $OutputFile
    
    # Résumé
    Write-Host "`n📈 Résumé:" -ForegroundColor Magenta
    Write-Host "  Total TODOs: $($todos.Count)" -ForegroundColor White
    Write-Host "  Autonomes: $(($todos | Where-Object { $_.Autonomy -eq 'Autonome' }).Count)" -ForegroundColor Green
    Write-Host "  Collaboration: $(($todos | Where-Object { $_.Autonomy -eq 'Collaboration' }).Count)" -ForegroundColor Yellow
    Write-Host "  Expert: $(($todos | Where-Object { $_.Autonomy -eq 'Expert' }).Count)" -ForegroundColor Red
    Write-Host "  Critiques: $(($todos | Where-Object { $_.Priority -eq 'Critique' }).Count)" -ForegroundColor Red
    
    Write-Host "`n✨ Analyse terminée avec succès!" -ForegroundColor Green
    
} catch {
    Write-Error "❌ Erreur lors de l'analyse: $($_.Exception.Message)"
    exit 1
}