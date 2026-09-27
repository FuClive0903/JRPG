$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))

function Check($condition, [string]$message) {
    if (-not $condition) { throw $message }
}

# This is a scoped reference audit of Unity text serialization, not a YAML loader.
function Read-Objects([string]$relative) {
    $text = Get-Content -LiteralPath (Join-Path $root $relative) -Raw
    $objects = @{}
    foreach ($match in [regex]::Matches($text, '(?ms)^--- !u!\d+ &(-?\d+)[^\r\n]*\r?\n(.*?)(?=^--- !u!|\z)')) {
        $id = $match.Groups[1].Value
        Check (-not $objects.ContainsKey($id)) "Duplicate object ID in ${relative}: $id"
        $objects[$id] = $match.Groups[2].Value
    }
    Check ($objects.Count -gt 0) "No serialized objects in $relative"
    return ,$objects
}

function Ref([string]$body, [string]$field) {
    $match = [regex]::Match($body, '(?m)^  ' + [regex]::Escape($field) + ': \{fileID: (-?\d+)')
    Check $match.Success "Missing reference field: $field"
    return $match.Groups[1].Value
}

function ScriptGuid([string]$relative) {
    $text = Get-Content -LiteralPath (Join-Path $root ($relative + '.meta')) -Raw
    return [regex]::Match($text, '(?m)^guid: (\w+)').Groups[1].Value
}

function SingleScript($objects, [string]$relative) {
    $guid = ScriptGuid $relative
    $matches = @($objects.Keys | Where-Object { $objects[$_] -match "m_Script: .*guid: $guid," })
    Check ($matches.Count -eq 1) "Expected one component for $relative, got $($matches.Count)"
    return $matches[0]
}

function Check-LocalReferences($objects, [string]$label) {
    foreach ($body in $objects.Values) {
        foreach ($match in [regex]::Matches($body, '\{fileID: (-?\d+)\}')) {
            $id = $match.Groups[1].Value
            Check ($id -eq '0' -or $objects.ContainsKey($id)) "Dangling local reference in ${label}: $id"
        }
    }
    Write-Output "PASS local references: $label ($($objects.Count) serialized objects)"
}

$ui = Read-Objects 'Assets/Prefabs/UI/ExplorationCanvas.prefab'
$save = Read-Objects 'Assets/Prefabs/UI/SaveDataMenu.prefab'
$scene = Read-Objects 'Assets/Scenes/Village_scene.unity'
$battle = Read-Objects 'Assets/Scenes/Battle_scene.unity'
Check-LocalReferences $ui 'ExplorationCanvas'
Check-LocalReferences $save 'SaveDataMenu'
Check-LocalReferences $scene 'Village_scene'
Check-LocalReferences $battle 'Battle_scene'
$landmarkId = SingleScript $scene 'Assets/Scipts/Exploration/VillageLandmarkBlockout.cs'
Check ($scene[(Ref $scene[$landmarkId] 'm_GameObject')] -match 'm_Name: VillageLandmarks') 'Village landmarks root is missing.'
Check (($scene.Values -join "`n") -notmatch 'Game.Exploration.VillageRoadLayout') 'Removed road layout remains in village scene.'
Check ($scene[$landmarkId] -match 'landmarkTreeSprite: \{fileID: 21300000, guid: [0-9a-f]+, type: 3\}') 'White landmark tree sprite is not assigned.'
$landmarkSource = Get-Content 'Assets/Scipts/Exploration/VillageLandmarkBlockout.cs' -Raw
Check ($landmarkSource -notmatch 'Residence |Village elder manor|Manor roof mass|Mine rock face|Mine cave mouth|South gate west post') 'Removed building placeholders are still generated.'
Check ($scene.Values -join "`n" -match 'm_Name: PlayerSpawn') 'Village spawn is missing.'
Check (($scene.Values -join "`n") -notmatch 'm_Name: Walls') 'Removed outer walls reappeared in the village.'

$menuId = SingleScript $ui 'Assets/Scipts/Exploration/ExplorationMenuController.cs'
$menu = $ui[$menuId]
Check ((Ref $menu 'sceneController') -eq '0' -and (Ref $menu 'playerController') -eq '0') 'Prefab must not store map-specific references.'
$owner = $ui[(Ref $menu 'm_GameObject')]
Check ($owner -match 'm_IsActive: 1') 'Menu controller owner must remain active.'
foreach ($field in @('explorationMenu', 'resumeButton', 'partyButton', 'partyMenu', 'loadButton', 'saveButton',
    'settingsButton', 'returnTitleButton', 'exitConfirmationPanel', 'exitYesButton', 'exitNoButton',
    'saveSlotMenu', 'saveConfirmationPanel', 'saveYesButton', 'saveNoButton')) {
    $id = Ref $menu $field
    Check ($id -ne '0' -and $ui.ContainsKey($id)) "Missing menu binding: $field"
    if ($field.EndsWith('Button')) {
        Check ($ui[$id] -match 'UnityEngine.UI.Button') "Wrong component type: $field"
    }
}
Check (($ui.Values -join "`n") -notmatch 'UnityEngine.EventSystems.EventSystem|Game.Exploration.ExplorationPlayerController') 'Shared UI contains map or EventSystem components.'
Write-Output 'PASS shared menu internal bindings and map independence'

$partyId = SingleScript $ui 'Assets/Scipts/UI/PartyMenu.cs'
Check ((Ref $menu 'partyMenu') -eq $partyId) 'Party page is not connected to the menu.'
foreach ($field in @('button', 'nameText', 'detailsText')) {
    $refs = [regex]::Matches($ui[$partyId], '(?m)^\s+(?:- )?' + $field + ': \{fileID: (\d+)\}')
    Check ($refs.Count -eq 3) "Expected three party $field bindings."
    Check (@($refs | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique).Count -eq 3) "Duplicated party $field bindings."
    foreach ($ref in $refs) {
        $target = $ui[$ref.Groups[1].Value]
        $type = if ($field -eq 'button') { 'UnityEngine.UI.Button' } else { 'TMPro.TextMeshProUGUI' }
        Check ($target -match [regex]::Escape($type)) "Wrong party component type: $field"
    }
}
Write-Output 'PASS three party slots, names and information texts'

$saveGuid = ScriptGuid 'Assets/Prefabs/UI/SaveDataMenu.prefab'
foreach ($id in $ui.Keys) {
    $body = $ui[$id]
    $source = [regex]::Match($body, 'm_CorrespondingSourceObject: \{fileID: (-?\d+), guid: ' + $saveGuid + ',')
    if ($source.Success) {
        Check ($save.ContainsKey($source.Groups[1].Value)) "Missing nested save source object: $id"
        $instance = $ui[(Ref $body 'm_PrefabInstance')]
        Check ($instance -match "m_SourcePrefab: .*guid: $saveGuid,") 'Wrong nested save Prefab source.'
    }
}
$saveMenuId = SingleScript $save 'Assets/Scipts/UI/SaveSlotMenu.cs'
Check ($ui[(Ref $menu 'saveSlotMenu')] -match "m_CorrespondingSourceObject: \{fileID: $saveMenuId,") 'Menu does not target the shared save component.'
Write-Output 'PASS nested SaveDataMenu source references'

$canvasGuid = ScriptGuid 'Assets/Prefabs/UI/ExplorationCanvas.prefab'
$instances = @($scene.Values | Where-Object { $_ -match "m_SourcePrefab: .*guid: $canvasGuid," })
Check ($instances.Count -eq 1) 'Exploration scene must have exactly one shared Canvas instance.'
$overrides = [regex]::Matches($instances[0], '(?s)- target: \{fileID: ' + $menuId + ', guid: ' + $canvasGuid + ', type: 3\}\s+propertyPath: (\w+)\s+value:[^\r\n]*\s+objectReference: \{fileID: (\d+)\}')
Check ($overrides.Count -eq 2) 'Expected the two map-specific menu bindings.'
$expected = @{
    sceneController = SingleScript $scene 'Assets/Scipts/Exploration/ExplorationSceneController.cs'
    playerController = SingleScript $scene 'Assets/Scipts/Exploration/ExplorationPlayerController.cs'
}
foreach ($entry in $expected.GetEnumerator()) {
    $matching = @($overrides | Where-Object { $_.Groups[1].Value -eq $entry.Key -and $_.Groups[2].Value -eq $entry.Value })
    Check ($matching.Count -eq 1) "Wrong map-specific binding: $($entry.Key)"
}
Check (@($scene.Values | Where-Object { $_ -match 'UnityEngine.EventSystems.EventSystem' }).Count -eq 1) 'Expected one EventSystem.'
$inputId = SingleScript $scene 'Assets/Scipts/UI/KeyboardMenuInputModule.cs'
foreach ($field in @('m_PointAction', 'm_LeftClickAction', 'm_RightClickAction', 'm_MiddleClickAction', 'm_ScrollWheelAction')) {
    Check ((Ref $scene[$inputId] $field) -eq '0') "Unexpected mouse binding: $field"
}
Write-Output 'PASS scene Canvas overrides and keyboard-only EventSystem'

$encounterId = SingleScript $scene 'Assets/Scipts/Exploration/ExplorationEncounterController.cs'
Check ($scene[$encounterId] -match '(?m)^  allowRandomEncounters: 1\r?$') 'Village demo must enable random encounters.'
foreach ($entry in $expected.GetEnumerator()) {
    Check ((Ref $scene[$encounterId] $entry.Key) -eq $entry.Value) "Encounter and menu use different $($entry.Key)"
}
$viewId = SingleScript $battle 'Assets/Scipts/Animation/BattleEnvironmentView.cs'
$viewOwner = Ref $battle[$viewId] 'm_GameObject'
Check ($battle[$viewOwner] -match 'm_Name: BattleEnvironmentRoot' -and $battle[$viewOwner] -match 'm_IsActive: 1') 'Background root missing or inactive.'
Write-Output "PASS encounter bindings and active battle background root"
Write-Output "INFO map background fileID: $(Ref $scene[$encounterId] 'battleEnvironmentPrefab'); default background fileID: $(Ref $battle[$viewId] 'defaultEnvironmentPrefab')"
Write-Output 'Static audit only: no Unity lifecycle, rendering, scene loading or player saves were exercised.'
