using System.Globalization;
using DetMath;
using DetMap.Core;
using DetMap.DbCommands;
using DetMap.Layers;
using DetMap.Pathfinding;
using DetMap.Schema;
using DetMap.Serialization;
using DetMap.Tables;

const int MapWidth = 64;
const int MapHeight = 64;
const int UnitCount = 10;
const int TickCount = 480;
const uint Seed = 123456789;

const double SeaLevel = 0.34;
const double HillLevel = 0.62;
const double MountainLevel = 0.80;
Fix64 diagonalStepMultiplier = Fix64.FromRatio(141, 100);

string outputDirectory = CreateOutputDirectory();
Console.WriteLine("DetMap settlement growth demo");
Console.WriteLine($"Output: {outputDirectory}");

var database = new DetSpatialDatabase(MapWidth, MapHeight);
database.SetGlobal("worldSeed", Fix64.FromInt((int)(Seed % 1_000_000)));
database.SetGlobal("seaLevel", ToFix64(SeaLevel));
database.SetGlobal("tickBudget", Fix64.FromInt(TickCount));

var walkable = database.Grid.CreateBitLayer("walkable");
var water = database.Grid.CreateBitLayer("water");
var roads = database.Grid.CreateBitLayer("roads");
var buildingAccess = database.Grid.CreateBitLayer("buildingAccess");
var terrainHeight = database.Grid.CreateFix64Layer("height");
var terrainType = database.Grid.CreateByteLayer("terrainType");
var fertility = database.Grid.CreateByteLayer("fertility");
var resourceTypeLayer = database.Grid.CreateByteLayer("resourceType");
var resourceAmountLayer = database.Grid.CreateIntLayer("resourceAmount");
var structureTypeLayer = database.Grid.CreateByteLayer("structureType");
var placementIdLayer = database.Grid.CreateIntLayer("placementId");
placementIdLayer.Fill(-1);

var unitsByCell = database.Grid.CreateCellIndex("unitsByCell");
var placementAnchorsByCell = database.Grid.CreateCellIndex("placementAnchors");
var resourceNodesByCell = database.Grid.CreateCellIndex("resourceNodesByCell");

var documentMetadata = database.CreateTable("documentMetadata");
var metadataKey = documentMetadata.CreateStringColumn("key");
var metadataValue = documentMetadata.CreateStringColumn("value");

var simulationSettings = database.CreateTable("simulationSettings");
var simulationSettingsName = simulationSettings.CreateStringColumn("name");
var simulationCellMeters = simulationSettings.CreateFix64Column("cellMeters");
var simulationTickSeconds = simulationSettings.CreateFix64Column("tickSeconds");
var simulationSettingsSummary = simulationSettings.CreateStringColumn("movementSummary", DetColumnOptions.Derived("cellMeters,tickSeconds"));

var players = database.CreateTable("players");
var playerName = players.CreateStringColumn("name");
var playerRole = players.CreateStringColumn("role");
var playerSummary = players.CreateStringColumn("summary", DetColumnOptions.Derived("role"));

var directors = database.CreateTable("directors");
var directorName = directors.CreateStringColumn("name");
var directorRole = directors.CreateStringColumn("role");
var directorActiveBuildOrderId = directors.CreateIntColumn("activeBuildOrderId");
var directorActiveSiteId = directors.CreateIntColumn("activeSiteId");
var directorSummary = directors.CreateStringColumn("summary", DetColumnOptions.Derived("activeBuildOrderId,activeSiteId"));

var playerBuildOrders = database.CreateTable("playerBuildOrders");
var buildOrderPlayerId = playerBuildOrders.CreateIntColumn("playerId");
var buildOrderLabel = playerBuildOrders.CreateStringColumn("label");
var buildOrderBuildingKind = playerBuildOrders.CreateByteColumn("buildingKind");
var buildOrderPreferredAnchorX = playerBuildOrders.CreateIntColumn("preferredAnchorX");
var buildOrderPreferredAnchorY = playerBuildOrders.CreateIntColumn("preferredAnchorY");
var buildOrderLinkedResourceKind = playerBuildOrders.CreateByteColumn("linkedResourceKind");
var buildOrderSequence = playerBuildOrders.CreateIntColumn("sequence");
var buildOrderStatus = playerBuildOrders.CreateByteColumn("status");
var buildOrderPlacedBuildingId = playerBuildOrders.CreateIntColumn("placedBuildingId");
var buildOrderDesiredBuilders = playerBuildOrders.CreateIntColumn("desiredBuilders");
var buildOrderDesiredHaulers = playerBuildOrders.CreateIntColumn("desiredHaulers");
var buildOrderSummary = playerBuildOrders.CreateStringColumn("summary", DetColumnOptions.Derived("label,buildingKind,sequence,status,placedBuildingId"));
var buildOrdersByStatus = playerBuildOrders.CreateByteIndex("buildOrdersByStatus", buildOrderStatus);
var buildOrdersByBuildingKind = playerBuildOrders.CreateByteIndex("buildOrdersByBuildingKind", buildOrderBuildingKind);

var spatialDefinitions = database.CreateTable("spatialDefinitions");
var spatialDefinitionName = spatialDefinitions.CreateStringColumn("name");
var spatialDefinitionCategory = spatialDefinitions.CreateStringColumn("category");
var spatialDefinitionLayout = spatialDefinitions.CreateStringColumn("layoutText");
var spatialDefinitionKind = spatialDefinitions.CreateByteColumn("kind");
var spatialDefinitionWidth = spatialDefinitions.CreateIntColumn("width");
var spatialDefinitionHeight = spatialDefinitions.CreateIntColumn("height");
var spatialDefinitionSolidCellCount = spatialDefinitions.CreateIntColumn("solidCellCount");
var spatialDefinitionConnectorCount = spatialDefinitions.CreateIntColumn("connectorCount");
var spatialDefinitionLayoutPreview = spatialDefinitions.CreateStringColumn("layoutPreview", DetColumnOptions.Derived("layoutText"));
var spatialDefinitionFootprintSummary = spatialDefinitions.CreateStringColumn("footprintSummary", DetColumnOptions.Derived("width,height,solidCellCount,connectorCount"));
var spatialDefinitionConnectorSummary = spatialDefinitions.CreateStringColumn("connectorSummary", DetColumnOptions.Derived("layoutText"));
var spatialDefinitionStorageWood = spatialDefinitions.CreateIntColumn("storageWoodCapacity");
var spatialDefinitionStorageStone = spatialDefinitions.CreateIntColumn("storageStoneCapacity");
var spatialDefinitionStorageFood = spatialDefinitions.CreateIntColumn("storageFoodCapacity");
var spatialDefinitionStorageSummary = spatialDefinitions.CreateStringColumn("storageSummary", DetColumnOptions.Derived("storageWoodCapacity,storageStoneCapacity,storageFoodCapacity"));
var spatialDefinitionPopulationCapacity = spatialDefinitions.CreateIntColumn("populationCapacity");
var spatialDefinitionBuildWood = spatialDefinitions.CreateIntColumn("buildWoodCost");
var spatialDefinitionBuildStone = spatialDefinitions.CreateIntColumn("buildStoneCost");
var spatialDefinitionBuildFood = spatialDefinitions.CreateIntColumn("buildFoodCost");
var spatialDefinitionBuildWork = spatialDefinitions.CreateIntColumn("buildWorkRequired");
var spatialDefinitionBuildSummary = spatialDefinitions.CreateStringColumn("buildSummary", DetColumnOptions.Derived("buildWoodCost,buildStoneCost,buildFoodCost,buildWorkRequired,populationCapacity"));

var resourceDefinitions = database.CreateTable("resourceDefinitions");
var resourceDefinitionName = resourceDefinitions.CreateStringColumn("name");
var resourceDefinitionKind = resourceDefinitions.CreateByteColumn("kind");
var resourceDefinitionPreferredTerrain = resourceDefinitions.CreateByteColumn("preferredTerrain");
var resourceDefinitionMinYield = resourceDefinitions.CreateIntColumn("minYield");
var resourceDefinitionMaxYield = resourceDefinitions.CreateIntColumn("maxYield");
var resourceDefinitionYieldSummary = resourceDefinitions.CreateStringColumn("yieldSummary", DetColumnOptions.Derived("preferredTerrain,minYield,maxYield"));

var unitDefinitions = database.CreateTable("unitDefinitions");
var unitDefinitionName = unitDefinitions.CreateStringColumn("name");
var unitDefinitionRole = unitDefinitions.CreateByteColumn("role");
var unitDefinitionCarryCapacity = unitDefinitions.CreateIntColumn("carryCapacity");
var unitDefinitionWorkBatch = unitDefinitions.CreateIntColumn("workBatch");
var unitDefinitionMoveMetersPerTick = unitDefinitions.CreateFix64Column("moveMetersPerTick");
var unitDefinitionRoleSummary = unitDefinitions.CreateStringColumn("roleSummary", DetColumnOptions.Derived("role,carryCapacity,workBatch,moveMetersPerTick"));

var buildings = database.CreateTable("buildings");
var buildingName = buildings.CreateStringColumn("name");
var buildingDefinitionId = buildings.CreateIntColumn("definitionId");
var buildingType = buildings.CreateByteColumn("buildingType");
var buildingStatus = buildings.CreateByteColumn("status");
var buildingBuildOrder = buildings.CreateIntColumn("buildOrder");
var buildingAnchorX = buildings.CreateIntColumn("anchorX");
var buildingAnchorY = buildings.CreateIntColumn("anchorY");
var buildingAccessX = buildings.CreateIntColumn("primaryAccessX");
var buildingAccessY = buildings.CreateIntColumn("primaryAccessY");
var buildingWood = buildings.CreateIntColumn("stockWood");
var buildingStone = buildings.CreateIntColumn("stockStone");
var buildingFood = buildings.CreateIntColumn("stockFood");
var buildingRequiredWood = buildings.CreateIntColumn("requiredWood");
var buildingRequiredStone = buildings.CreateIntColumn("requiredStone");
var buildingRequiredFood = buildings.CreateIntColumn("requiredFood");
var buildingRequiredWork = buildings.CreateIntColumn("requiredWork");
var buildingProgress = buildings.CreateIntColumn("constructionProgress");
var buildingPopulationCapacity = buildings.CreateIntColumn("populationCapacity");
var buildingPopulationGranted = buildings.CreateIntColumn("populationGranted");
var buildingsByStatus = buildings.CreateByteIndex("buildingsByStatus", buildingStatus);
var buildingsByBuildOrder = buildings.CreateIntIndex("buildingsByBuildOrder", buildingBuildOrder);

var resourceNodes = database.CreateTable("resourceNodes");
var resourceNodeDefinitionId = resourceNodes.CreateIntColumn("definitionId");
var resourceNodeType = resourceNodes.CreateByteColumn("resourceType");
var resourceNodeAmount = resourceNodes.CreateIntColumn("amount");
var resourceNodePosX = resourceNodes.CreateIntColumn("posX");
var resourceNodePosY = resourceNodes.CreateIntColumn("posY");
var resourceNodeLabel = resourceNodes.CreateStringColumn("label");
var resourceNodesByType = resourceNodes.CreateByteIndex("resourceNodesByType", resourceNodeType);

var units = database.CreateTable("units");
var unitDefinitionId = units.CreateIntColumn("definitionId");
var unitName = units.CreateStringColumn("name");
var unitRole = units.CreateByteColumn("role");
var unitState = units.CreateByteColumn("state");
var unitTask = units.CreateStringColumn("task");
var unitPosX = units.CreateIntColumn("posX");
var unitPosY = units.CreateIntColumn("posY");
var unitDestX = units.CreateIntColumn("destX");
var unitDestY = units.CreateIntColumn("destY");
var unitHomeBuildingId = units.CreateIntColumn("homeBuildingId");
var unitDeliveryBuildingId = units.CreateIntColumn("deliveryBuildingId");
var unitTargetResourceId = units.CreateIntColumn("targetResourceId");
var unitCarryType = units.CreateByteColumn("carryType");
var unitCarryAmount = units.CreateIntColumn("carryAmount");
var unitMoveProgressMeters = units.CreateFix64Column("moveProgressMeters");
var unitsByRole = units.CreateByteIndex("unitsByRole", unitRole);
var unitsByState = units.CreateByteIndex("unitsByState", unitState);
var unitsByDeliveryBuilding = units.CreateIntIndex("unitsByDeliveryBuilding", unitDeliveryBuildingId);

var paths = database.CreatePathStore("unitPaths", 512);
var pathfinder = new DetPathfinder(MapWidth, MapHeight);

var terrainNoise = new PerlinNoise2D(Seed);
var fertilityNoise = new PerlinNoise2D(Seed ^ 0x9E3779B9u);
var forestNoise = new PerlinNoise2D(Seed ^ 0xA341316Cu);
var featureNoise = new PerlinNoise2D(Seed ^ 0xC8013EA4u);
var rng = new DemoRandom(Seed);
DetDbCommandList? nextFrameCommands = null;
Dictionary<string, int>? nextFrameRowIds = null;
DetPathStore? nextFramePaths = null;
DetDbCommandList? lastAppliedCommandList = null;
DetDbApplyResult? lastApplyResult = null;
DetDbFrameRecord? lastFrameRecord = null;

var spatialDefinitionsByRowId = new Dictionary<int, CompiledSpatialDefinition>();
var spatialDefinitionRowByKind = new Dictionary<BuildingKind, int>();
var resourceDefinitionRowByKind = new Dictionary<ResourceKind, int>();
var unitDefinitionRowByRole = new Dictionary<UnitRoleKind, int>();
int simulationSettingsRowId = -1;
int playerRowId = -1;
int directorRowId = -1;
int loggingCampId = -1;
int huntingLodgeId = -1;

SeedDocumentMetadata();
SeedSimulationSettings();
SeedDefinitions();
GenerateWorld();

var (settlementX, settlementY) = FindSettlementAnchor();

PlacedBuildingSite settlementHallSite = SeedBuilding("Settlement Hall", BuildingKind.SettlementHall, settlementX, settlementY, isComplete: true, buildOrder: 0);
int settlementHallId = settlementHallSite.RowId;

SeedActors();
buildingWood.Set(settlementHallId, 36);
buildingStone.Set(settlementHallId, 20);
buildingFood.Set(settlementHallId, 20);

SeedPlayerBuildOrders(settlementX, settlementY);

int settlementRoadX = settlementHallSite.PrimaryAccessX;
int settlementRoadY = settlementHallSite.PrimaryAccessY;
var woodNodeIds = PlaceResourceNodes(ResourceKind.Wood, 8, 6, settlementX, settlementY, settlementRoadX, settlementRoadY);
var foodNodeIds = PlaceResourceNodes(ResourceKind.Food, 6, 7, settlementX, settlementY, settlementRoadX, settlementRoadY);

for (int i = 0; i < UnitCount; i++)
{
    int rowId = units.CreateRow();
    UnitRoleKind role = UnitRoleKind.Idle;
    int homeBuildingId = settlementHallId;
    int deliveryBuildingId = settlementHallId;
    int definitionRowId = unitDefinitionRowByRole[role];
    var (spawnX, spawnY) = FindUnitSpawnNearBuilding(settlementHallId);

    unitDefinitionId.Set(rowId, definitionRowId);
    unitName.Set(rowId, $"{GetRoleLabel(role)}_{i:D2}");
    unitRole.Set(rowId, (byte)role);
    unitState.Set(rowId, (byte)GetInitialState(role));
    unitTask.Set(rowId, "Idle at Settlement Hall");
    unitPosX.Set(rowId, spawnX);
    unitPosY.Set(rowId, spawnY);
    unitDestX.Set(rowId, spawnX);
    unitDestY.Set(rowId, spawnY);
    unitHomeBuildingId.Set(rowId, homeBuildingId);
    unitDeliveryBuildingId.Set(rowId, deliveryBuildingId);
    unitTargetResourceId.Set(rowId, -1);
    unitCarryType.Set(rowId, (byte)ResourceKind.None);
    unitCarryAmount.Set(rowId, 0);
    unitMoveProgressMeters.Set(rowId, Fix64.Zero);
    unitsByCell.Place(rowId, spawnX, spawnY);

    paths.Clear(rowId);
}

WriteSnapshot(database, outputDirectory);
PrintSummary();

for (int tick = 0; tick < TickCount; tick++)
{
    BeginNextFrameStep();
    SimulateTick();
    RunDirectorStep();
    lastApplyResult = CommitNextFrameStep();
    lastFrameRecord = lastAppliedCommandList is null
        ? null
        : DetDbFrameRecord.Create(
            database.Tick,
            database.ComputeStateHashHex(),
            database.ComputeFrameHashHex(),
            lastAppliedCommandList);
    WriteSnapshot(database, outputDirectory, lastFrameRecord);
    PrintSummary();
}

Console.WriteLine($"Done. Wrote {TickCount + 1} snapshots.");
return;

void SeedDocumentMetadata()
{
    AddMetadata("title", "DetMap Settlement Growth Scenario");
    AddMetadata("domain", "City building");
    AddMetadata("style", "Small settlement growth");
    AddMetadata("authoring", "Definitions, placements, resources, agents, and DB commands are all visible as tables");
    AddMetadata("layoutLegend", "#=solid footprint, A=anchor, r=road connector, .=empty");
}

void SeedSimulationSettings()
{
    simulationSettingsRowId = simulationSettings.CreateRow();
    simulationSettingsName.Set(simulationSettingsRowId, "Default profile");
    simulationCellMeters.Set(simulationSettingsRowId, Fix64.One);
    simulationTickSeconds.Set(simulationSettingsRowId, Fix64.FromRatio(4, 5));
    simulationSettingsSummary.Set(
        simulationSettingsRowId,
        BuildSimulationSettingsSummary(
            simulationCellMeters.Get(simulationSettingsRowId),
            simulationTickSeconds.Get(simulationSettingsRowId)));
}

void BeginNextFrameStep()
{
    DetSpatialDatabase nextFrame = database.PrepareNextFrame();
    nextFrameCommands = new DetDbCommandList();
    nextFrameRowIds = new Dictionary<string, int>(StringComparer.Ordinal);
    nextFramePaths = nextFrame.GetPathStore("unitPaths");
}

DetDbApplyResult CommitNextFrameStep()
{
    DetDbCommandList commands = RequireNextFrameCommands();
    DetDbApplyResult result = DetDbCommandApplier.ApplyToPreparedNextFrame(database, commands);
    database.CommitNextFrame();
    lastAppliedCommandList = commands;
    nextFrameCommands = null;
    nextFrameRowIds = null;
    nextFramePaths = null;
    return result;
}

DetDbCommandList RequireNextFrameCommands()
{
    return nextFrameCommands ?? throw new InvalidOperationException("Next frame command list is not active.");
}

DetPathStore RequireNextFramePaths()
{
    return nextFramePaths ?? throw new InvalidOperationException("Next frame path store is not active.");
}

int AllocateNextFrameRowId(DetTable table)
{
    nextFrameRowIds ??= new Dictionary<string, int>(StringComparer.Ordinal);
    if (!nextFrameRowIds.TryGetValue(table.Name, out int nextRowId))
        nextRowId = table.PeekNextRowId();

    nextFrameRowIds[table.Name] = nextRowId + 1;
    return nextRowId;
}

void AddMetadata(string key, string value)
{
    int rowId = documentMetadata.CreateRow();
    metadataKey.Set(rowId, key);
    metadataValue.Set(rowId, value);
}

void SeedActors()
{
    playerRowId = players.CreateRow();
    playerName.Set(playerRowId, "Player");
    playerRole.Set(playerRowId, "Plans settlement growth");
    playerSummary.Set(playerRowId, "Queues new sites, sets build order, and can override worker priorities");

    directorRowId = directors.CreateRow();
    directorName.Set(directorRowId, "Director");
    directorRole.Set(directorRowId, "Assigns units to the most important active work");
    directorActiveBuildOrderId.Set(directorRowId, -1);
    directorActiveSiteId.Set(directorRowId, -1);
    directorSummary.Set(directorRowId, "Idle until the first player build order becomes active");
}

void SeedPlayerBuildOrders(int settlementX, int settlementY)
{
    AddPlayerBuildOrder("South Cottage", BuildingKind.House, settlementX, settlementY + 8, sequence: 1, desiredBuilders: 1, desiredHaulers: 1);
    AddPlayerBuildOrder("West Cottage", BuildingKind.House, settlementX - 7, settlementY + 3, sequence: 2, desiredBuilders: 1, desiredHaulers: 1);
    AddPlayerBuildOrder("East Cottage", BuildingKind.House, settlementX + 7, settlementY + 3, sequence: 3, desiredBuilders: 1, desiredHaulers: 1);
    AddPlayerBuildOrder("Logging Camp", BuildingKind.LoggingCamp, settlementX - 10, settlementY - 6, sequence: 4, linkedResourceKind: ResourceKind.Wood, desiredBuilders: 2, desiredHaulers: 2);
    AddPlayerBuildOrder("Hunting Lodge", BuildingKind.HuntingLodge, settlementX + 10, settlementY - 6, sequence: 5, linkedResourceKind: ResourceKind.Food, desiredBuilders: 2, desiredHaulers: 2);
    playerSummary.Set(playerRowId, BuildPlayerSummary());
}

void AddPlayerBuildOrder(
    string label,
    BuildingKind kind,
    int preferredAnchorX,
    int preferredAnchorY,
    int sequence,
    ResourceKind linkedResourceKind = ResourceKind.None,
    int desiredBuilders = 1,
    int desiredHaulers = 1)
{
    int rowId = playerBuildOrders.CreateRow();
    buildOrderPlayerId.Set(rowId, playerRowId);
    buildOrderLabel.Set(rowId, label);
    buildOrderBuildingKind.Set(rowId, (byte)kind);
    buildOrderPreferredAnchorX.Set(rowId, preferredAnchorX);
    buildOrderPreferredAnchorY.Set(rowId, preferredAnchorY);
    buildOrderLinkedResourceKind.Set(rowId, (byte)linkedResourceKind);
    buildOrderSequence.Set(rowId, sequence);
    buildOrderStatus.Set(rowId, (byte)BuildOrderStatusKind.Planned);
    buildOrderPlacedBuildingId.Set(rowId, -1);
    buildOrderDesiredBuilders.Set(rowId, desiredBuilders);
    buildOrderDesiredHaulers.Set(rowId, desiredHaulers);
    buildOrderSummary.Set(rowId, BuildBuildOrderSummary(label, kind, sequence, BuildOrderStatusKind.Planned, -1, desiredBuilders, desiredHaulers));
}

void SeedDefinitions()
{
    RegisterSpatialDefinition(
        BuildingKind.SettlementHall,
        "Settlement Hall",
        "Hub",
        "..r..\n.###.\n##A##\n.###.\n.....",
        storageWoodCapacity: 240,
        storageStoneCapacity: 160,
        storageFoodCapacity: 120,
        populationCapacity: 0,
        buildWoodCost: 0,
        buildStoneCost: 0,
        buildFoodCost: 0,
        buildWorkRequired: 0);

    RegisterSpatialDefinition(
        BuildingKind.House,
        "Cottage",
        "Housing",
        "..r.\n.##.\n.#A#\n.....",
        storageWoodCapacity: 8,
        storageStoneCapacity: 4,
        storageFoodCapacity: 8,
        populationCapacity: 1,
        buildWoodCost: 8,
        buildStoneCost: 2,
        buildFoodCost: 0,
        buildWorkRequired: 10);

    RegisterSpatialDefinition(
        BuildingKind.LoggingCamp,
        "Logging Camp",
        "Production",
        "..r..\n.###.\n##A#.\n.....",
        storageWoodCapacity: 80,
        storageStoneCapacity: 8,
        storageFoodCapacity: 4,
        populationCapacity: 0,
        buildWoodCost: 12,
        buildStoneCost: 4,
        buildFoodCost: 0,
        buildWorkRequired: 14);

    RegisterSpatialDefinition(
        BuildingKind.HuntingLodge,
        "Hunting Lodge",
        "Production",
        "..r..\n.###.\n.#A#.\n.....",
        storageWoodCapacity: 24,
        storageStoneCapacity: 8,
        storageFoodCapacity: 60,
        populationCapacity: 0,
        buildWoodCost: 12,
        buildStoneCost: 2,
        buildFoodCost: 0,
        buildWorkRequired: 14);

    RegisterResourceDefinition(ResourceKind.Wood, "Wood", TerrainKind.Forest, 36, 64);
    RegisterResourceDefinition(ResourceKind.Stone, "Stone", TerrainKind.Hill, 0, 0);
    RegisterResourceDefinition(ResourceKind.Food, "Food", TerrainKind.Grass, 28, 52);

    RegisterUnitDefinition(UnitRoleKind.Idle, "Settler", carryCapacity: 0, workBatch: 0, moveMetersPerTick: Fix64.One);
    RegisterUnitDefinition(UnitRoleKind.Hauler, "Hauler", carryCapacity: 6, workBatch: 0, moveMetersPerTick: Fix64.One);
    RegisterUnitDefinition(UnitRoleKind.Builder, "Builder", carryCapacity: 0, workBatch: 3, moveMetersPerTick: Fix64.One);
    RegisterUnitDefinition(UnitRoleKind.Woodcutter, "Woodcutter", carryCapacity: 5, workBatch: 3, moveMetersPerTick: Fix64.One);
    RegisterUnitDefinition(UnitRoleKind.Hunter, "Hunter", carryCapacity: 4, workBatch: 2, moveMetersPerTick: Fix64.One);
}

void RegisterSpatialDefinition(
    BuildingKind kind,
    string name,
    string category,
    string layoutText,
    int storageWoodCapacity,
    int storageStoneCapacity,
    int storageFoodCapacity,
    int populationCapacity,
    int buildWoodCost,
    int buildStoneCost,
    int buildFoodCost,
    int buildWorkRequired)
{
    var compiled = CompileSpatialDefinition(
        kind,
        name,
        category,
        layoutText,
        storageWoodCapacity,
        storageStoneCapacity,
        storageFoodCapacity,
        populationCapacity,
        buildWoodCost,
        buildStoneCost,
        buildFoodCost,
        buildWorkRequired);
    int rowId = spatialDefinitions.CreateRow();

    spatialDefinitionName.Set(rowId, name);
    spatialDefinitionCategory.Set(rowId, category);
    spatialDefinitionLayout.Set(rowId, layoutText);
    spatialDefinitionKind.Set(rowId, (byte)kind);
    spatialDefinitionWidth.Set(rowId, compiled.Width);
    spatialDefinitionHeight.Set(rowId, compiled.Height);
    spatialDefinitionSolidCellCount.Set(rowId, compiled.FootprintOffsets.Length);
    spatialDefinitionConnectorCount.Set(rowId, compiled.ConnectorOffsets.Length);
    spatialDefinitionLayoutPreview.Set(rowId, BuildLayoutPreview(layoutText));
    spatialDefinitionFootprintSummary.Set(rowId, BuildFootprintSummary(compiled));
    spatialDefinitionConnectorSummary.Set(rowId, BuildConnectorSummary(compiled));
    spatialDefinitionStorageWood.Set(rowId, storageWoodCapacity);
    spatialDefinitionStorageStone.Set(rowId, storageStoneCapacity);
    spatialDefinitionStorageFood.Set(rowId, storageFoodCapacity);
    spatialDefinitionStorageSummary.Set(rowId, BuildStorageSummary(storageWoodCapacity, storageStoneCapacity, storageFoodCapacity));
    spatialDefinitionPopulationCapacity.Set(rowId, populationCapacity);
    spatialDefinitionBuildWood.Set(rowId, buildWoodCost);
    spatialDefinitionBuildStone.Set(rowId, buildStoneCost);
    spatialDefinitionBuildFood.Set(rowId, buildFoodCost);
    spatialDefinitionBuildWork.Set(rowId, buildWorkRequired);
    spatialDefinitionBuildSummary.Set(rowId, BuildConstructionSummary(buildWoodCost, buildStoneCost, buildFoodCost, buildWorkRequired));

    compiled.DefinitionRowId = rowId;
    spatialDefinitionsByRowId[rowId] = compiled;
    spatialDefinitionRowByKind[kind] = rowId;
}

void RegisterResourceDefinition(ResourceKind kind, string name, TerrainKind preferredTerrain, int minYield, int maxYield)
{
    int rowId = resourceDefinitions.CreateRow();
    resourceDefinitionName.Set(rowId, name);
    resourceDefinitionKind.Set(rowId, (byte)kind);
    resourceDefinitionPreferredTerrain.Set(rowId, (byte)preferredTerrain);
    resourceDefinitionMinYield.Set(rowId, minYield);
    resourceDefinitionMaxYield.Set(rowId, maxYield);
    resourceDefinitionYieldSummary.Set(rowId, BuildYieldSummary(preferredTerrain, minYield, maxYield));
    resourceDefinitionRowByKind[kind] = rowId;
}

void RegisterUnitDefinition(UnitRoleKind role, string name, int carryCapacity, int workBatch, Fix64 moveMetersPerTick)
{
    int rowId = unitDefinitions.CreateRow();
    unitDefinitionName.Set(rowId, name);
    unitDefinitionRole.Set(rowId, (byte)role);
    unitDefinitionCarryCapacity.Set(rowId, carryCapacity);
    unitDefinitionWorkBatch.Set(rowId, workBatch);
    unitDefinitionMoveMetersPerTick.Set(rowId, moveMetersPerTick);
    unitDefinitionRoleSummary.Set(rowId, BuildUnitDefinitionSummary(role, carryCapacity, workBatch, moveMetersPerTick));
    unitDefinitionRowByRole[role] = rowId;
}

void GenerateWorld()
{
    for (int y = 0; y < MapHeight; y++)
    {
        for (int x = 0; x < MapWidth; x++)
        {
            double height01 = SampleHeight01(x, y);
            double fertility01 = SampleFertility01(x, y);
            double forest01 = forestNoise.Fractal(x * 0.075, y * 0.075, 3, 0.55, 2.15);

            TerrainKind terrain = ClassifyTerrain(height01, fertility01, forest01);
            bool isWater = terrain == TerrainKind.Water;
            bool isWalkable = terrain is not TerrainKind.Water and not TerrainKind.Mountain;

            terrainHeight.Set(x, y, ToFix64(height01 * 12.0));
            terrainType.Set(x, y, (byte)terrain);
            fertility.Set(x, y, (byte)Math.Round(fertility01 * 100.0));
            water.Set(x, y, isWater);
            walkable.Set(x, y, isWalkable);
            roads.Set(x, y, false);
            buildingAccess.Set(x, y, false);
            resourceTypeLayer.Set(x, y, (byte)ResourceKind.None);
            resourceAmountLayer.Set(x, y, 0);
            structureTypeLayer.Set(x, y, 0);
            placementIdLayer.Set(x, y, -1);
        }
    }
}

double SampleHeight01(int x, int y)
{
    double baseNoise = terrainNoise.Fractal(x * 0.060, y * 0.060, 4, 0.52, 2.05);
    double detailNoise = featureNoise.Fractal((x + 300) * 0.140, (y + 300) * 0.140, 2, 0.60, 2.20);

    double nx = ((double)x / (MapWidth - 1)) - 0.5;
    double ny = ((double)y / (MapHeight - 1)) - 0.5;
    double islandFalloff = Math.Sqrt((nx * nx) + (ny * ny)) * 1.30;

    double height01 = (baseNoise * 0.72) + (detailNoise * 0.28);
    height01 = height01 - islandFalloff + 0.50;
    return Clamp01(height01);
}

double SampleFertility01(int x, int y)
{
    double raw = fertilityNoise.Fractal((x + 800) * 0.080, (y + 800) * 0.080, 3, 0.55, 2.00);
    double moisture = featureNoise.Fractal((x + 1600) * 0.050, (y + 1600) * 0.050, 2, 0.60, 2.10);
    return Clamp01((raw * 0.70) + (moisture * 0.30));
}

TerrainKind ClassifyTerrain(double height01, double fertility01, double forest01)
{
    if (height01 < SeaLevel)
        return TerrainKind.Water;

    if (height01 >= MountainLevel)
        return TerrainKind.Mountain;

    if (height01 >= HillLevel)
        return TerrainKind.Hill;

    if (forest01 > 0.58 && fertility01 > 0.42)
        return TerrainKind.Forest;

    return TerrainKind.Grass;
}

(int x, int y) FindSettlementAnchor()
{
    int centerX = MapWidth / 2;
    int centerY = MapHeight / 2;

    for (int radius = 0; radius < Math.Max(MapWidth, MapHeight); radius++)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = centerX + dx;
                int y = centerY + dy;
                if (!InBounds(x, y) || !walkable.Get(x, y))
                    continue;

                TerrainKind terrain = (TerrainKind)terrainType.Get(x, y);
                if (terrain is TerrainKind.Grass or TerrainKind.Forest)
                    return (x, y);
            }
        }
    }

    throw new InvalidOperationException("Failed to find a settlement anchor on walkable land.");
}

PlacedBuildingSite SeedBuilding(string label, BuildingKind kind, int preferredAnchorX, int preferredAnchorY, bool isComplete, int buildOrder)
{
    int definitionRowId = spatialDefinitionRowByKind[kind];
    CompiledSpatialDefinition definition = spatialDefinitionsByRowId[definitionRowId];
    var (anchorX, anchorY) = FindNearbyPlacementAnchor(definition, preferredAnchorX, preferredAnchorY);
    var primaryAccess = GetPrimaryConnectorWorldCell(definition, anchorX, anchorY);
    int initialProgress = isComplete ? definition.BuildWorkRequired : GetInitialConstructionProgress(definition);
    BuildingStatusKind initialStatus = isComplete ? BuildingStatusKind.Complete : BuildingStatusKind.Delivering;

    int rowId = buildings.CreateRow();
    buildingName.Set(rowId, label);
    buildingDefinitionId.Set(rowId, definitionRowId);
    buildingType.Set(rowId, (byte)kind);
    buildingStatus.Set(rowId, (byte)initialStatus);
    buildingBuildOrder.Set(rowId, buildOrder);
    buildingAnchorX.Set(rowId, anchorX);
    buildingAnchorY.Set(rowId, anchorY);
    buildingAccessX.Set(rowId, primaryAccess.x);
    buildingAccessY.Set(rowId, primaryAccess.y);
    buildingWood.Set(rowId, 0);
    buildingStone.Set(rowId, 0);
    buildingFood.Set(rowId, 0);
    buildingRequiredWood.Set(rowId, definition.BuildWoodCost);
    buildingRequiredStone.Set(rowId, definition.BuildStoneCost);
    buildingRequiredFood.Set(rowId, definition.BuildFoodCost);
    buildingRequiredWork.Set(rowId, definition.BuildWorkRequired);
    buildingProgress.Set(rowId, initialProgress);
    buildingPopulationCapacity.Set(rowId, definition.PopulationCapacity);
    buildingPopulationGranted.Set(rowId, 0);
    placementAnchorsByCell.Place(rowId, anchorX, anchorY);

    ApplyBuildingFootprintDirect(rowId, definition, anchorX, anchorY);
    return new PlacedBuildingSite(rowId, anchorX, anchorY, primaryAccess.x, primaryAccess.y);
}

PlacedBuildingSite QueueCreateBuilding(string label, BuildingKind kind, int preferredAnchorX, int preferredAnchorY, bool isComplete, int buildOrder)
{
    int definitionRowId = spatialDefinitionRowByKind[kind];
    CompiledSpatialDefinition definition = spatialDefinitionsByRowId[definitionRowId];
    var (anchorX, anchorY) = FindNearbyPlacementAnchor(definition, preferredAnchorX, preferredAnchorY);
    var primaryAccess = GetPrimaryConnectorWorldCell(definition, anchorX, anchorY);
    int initialProgress = isComplete ? definition.BuildWorkRequired : GetInitialConstructionProgress(definition);
    BuildingStatusKind initialStatus = isComplete ? BuildingStatusKind.Complete : BuildingStatusKind.Delivering;

    DetDbCommandList commands = RequireNextFrameCommands();
    int rowId = AllocateNextFrameRowId(buildings);
    commands.CreateRow("buildings", rowId);
    commands.SetString("buildings", "name", rowId, label);
    commands.SetInt("buildings", "definitionId", rowId, definitionRowId);
    commands.SetByte("buildings", "buildingType", rowId, (byte)kind);
    commands.SetByte("buildings", "status", rowId, (byte)initialStatus);
    commands.SetInt("buildings", "buildOrder", rowId, buildOrder);
    commands.SetInt("buildings", "anchorX", rowId, anchorX);
    commands.SetInt("buildings", "anchorY", rowId, anchorY);
    commands.SetInt("buildings", "primaryAccessX", rowId, primaryAccess.x);
    commands.SetInt("buildings", "primaryAccessY", rowId, primaryAccess.y);
    commands.SetInt("buildings", "stockWood", rowId, 0);
    commands.SetInt("buildings", "stockStone", rowId, 0);
    commands.SetInt("buildings", "stockFood", rowId, 0);
    commands.SetInt("buildings", "requiredWood", rowId, definition.BuildWoodCost);
    commands.SetInt("buildings", "requiredStone", rowId, definition.BuildStoneCost);
    commands.SetInt("buildings", "requiredFood", rowId, definition.BuildFoodCost);
    commands.SetInt("buildings", "requiredWork", rowId, definition.BuildWorkRequired);
    commands.SetInt("buildings", "constructionProgress", rowId, initialProgress);
    commands.SetInt("buildings", "populationCapacity", rowId, definition.PopulationCapacity);
    commands.SetInt("buildings", "populationGranted", rowId, 0);
    commands.PlaceRow("placementAnchors", rowId, anchorX, anchorY);

    QueueBuildingFootprint(rowId, definition, anchorX, anchorY);
    return new PlacedBuildingSite(rowId, anchorX, anchorY, primaryAccess.x, primaryAccess.y);
}

int GetInitialConstructionProgress(CompiledSpatialDefinition definition)
{
    if (definition.BuildWorkRequired <= 0)
        return 0;

    return Math.Max(1, (definition.BuildWorkRequired + 99) / 100);
}

(int x, int y) FindNearbyPlacementAnchor(CompiledSpatialDefinition definition, int preferredAnchorX, int preferredAnchorY)
{
    for (int radius = 0; radius <= 14; radius++)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int anchorX = preferredAnchorX + dx;
                int anchorY = preferredAnchorY + dy;
                if (CanPlaceBuilding(definition, anchorX, anchorY))
                    return (anchorX, anchorY);
            }
        }
    }

    throw new InvalidOperationException($"Failed to place {definition.Name} near {preferredAnchorX},{preferredAnchorY}.");
}

bool CanPlaceBuilding(CompiledSpatialDefinition definition, int anchorX, int anchorY)
{
    foreach (var offset in definition.FootprintOffsets)
    {
        int x = anchorX + offset.X;
        int y = anchorY + offset.Y;
        if (!InBounds(x, y) || !walkable.Get(x, y))
            return false;

        if (placementIdLayer.Get(x, y) >= 0 || resourceNodesByCell.CountAt(x, y) > 0)
            return false;
    }

    foreach (var offset in definition.ConnectorOffsets)
    {
        int x = anchorX + offset.X;
        int y = anchorY + offset.Y;
        if (!InBounds(x, y) || !walkable.Get(x, y))
            return false;

        if (placementIdLayer.Get(x, y) >= 0 || resourceNodesByCell.CountAt(x, y) > 0)
            return false;
    }

    return true;
}

void ApplyBuildingFootprintDirect(int buildingRowId, CompiledSpatialDefinition definition, int anchorX, int anchorY)
{
    foreach (var offset in definition.FootprintOffsets)
    {
        int x = anchorX + offset.X;
        int y = anchorY + offset.Y;
        placementIdLayer.Set(x, y, buildingRowId);
        structureTypeLayer.Set(x, y, (byte)definition.Kind);
        walkable.Set(x, y, false);
    }

    foreach (var offset in definition.ConnectorOffsets)
    {
        int x = anchorX + offset.X;
        int y = anchorY + offset.Y;
        buildingAccess.Set(x, y, true);
        roads.Set(x, y, true);
    }
}

void QueueBuildingFootprint(int buildingRowId, CompiledSpatialDefinition definition, int anchorX, int anchorY)
{
    DetDbCommandList commands = RequireNextFrameCommands();
    foreach (var offset in definition.FootprintOffsets)
    {
        int x = anchorX + offset.X;
        int y = anchorY + offset.Y;
        commands.SetIntCell("placementId", x, y, buildingRowId);
        commands.SetByteCell("structureType", x, y, (byte)definition.Kind);
        commands.SetBitCell("walkable", x, y, false);
    }

    foreach (var offset in definition.ConnectorOffsets)
    {
        int x = anchorX + offset.X;
        int y = anchorY + offset.Y;
        commands.SetBitCell("buildingAccess", x, y, true);
        commands.SetBitCell("roads", x, y, true);
    }
}

List<int> PlaceResourceNodes(
    ResourceKind kind,
    int desiredCount,
    int minDistanceFromSettlement,
    int settlementX,
    int settlementY,
    int pathOriginX,
    int pathOriginY)
{
    int definitionRowId = resourceDefinitionRowByKind[kind];
    TerrainKind preferredTerrain = (TerrainKind)resourceDefinitionPreferredTerrain.Get(definitionRowId);
    int minYield = resourceDefinitionMinYield.Get(definitionRowId);
    int maxYield = resourceDefinitionMaxYield.Get(definitionRowId);
    string label = resourceDefinitionName.Get(definitionRowId) ?? GetResourceLabel(kind);

    var created = new List<int>(desiredCount);

    for (int attempt = 0; attempt < desiredCount * 120 && created.Count < desiredCount; attempt++)
    {
        int x = rng.NextInt(MapWidth);
        int y = rng.NextInt(MapHeight);
        if (!InBounds(x, y) || !walkable.Get(x, y))
            continue;

        TerrainKind terrain = (TerrainKind)terrainType.Get(x, y);
        bool relaxedTerrain = attempt > desiredCount * 60;
        if (!MatchesResourceTerrain(kind, preferredTerrain, terrain, relaxedTerrain))
            continue;

        if (DistanceSquared(x, y, settlementX, settlementY) < minDistanceFromSettlement * minDistanceFromSettlement)
            continue;

        if (resourceNodesByCell.CountAt(x, y) > 0 || placementIdLayer.Get(x, y) >= 0 || buildingAccess.Get(x, y) || roads.Get(x, y))
            continue;

        if (!pathfinder.FindPath(pathOriginX, pathOriginY, x, y, walkable).IsValid)
            continue;

        int amount = minYield + rng.NextInt(Math.Max(1, maxYield - minYield + 1));
        int rowId = resourceNodes.CreateRow();
        resourceNodeDefinitionId.Set(rowId, definitionRowId);
        resourceNodeType.Set(rowId, (byte)kind);
        resourceNodeAmount.Set(rowId, amount);
        resourceNodePosX.Set(rowId, x);
        resourceNodePosY.Set(rowId, y);
        resourceNodeLabel.Set(rowId, $"{label} Node {created.Count:D2}");
        resourceNodesByCell.Place(rowId, x, y);
        resourceTypeLayer.Set(x, y, (byte)kind);
        resourceAmountLayer.Set(x, y, amount);
        created.Add(rowId);
    }

    if (created.Count == 0)
        throw new InvalidOperationException($"Failed to place any nodes for {kind}.");

    return created;
}

bool MatchesResourceTerrain(ResourceKind kind, TerrainKind preferredTerrain, TerrainKind terrain, bool relaxedTerrain)
{
    if (terrain == preferredTerrain)
        return true;

    if (!relaxedTerrain)
        return false;

    return kind switch
    {
        ResourceKind.Wood => terrain is TerrainKind.Forest or TerrainKind.Grass,
        ResourceKind.Stone => terrain is TerrainKind.Hill or TerrainKind.Grass,
        ResourceKind.Food => terrain is TerrainKind.Grass or TerrainKind.Forest,
        _ => false,
    };
}

void QueueRoadCells(int x0, int y0, int x1, int y1)
{
    DetDbCommandList commands = RequireNextFrameCommands();
    foreach (var (x, y) in TraceLine(x0, y0, x1, y1))
    {
        if (!InBounds(x, y) || !walkable.Get(x, y))
            continue;

        commands.SetBitCell("roads", x, y, true);
    }
}

void ConnectRoadFromCellToResource(int fromX, int fromY, int resourceRowId)
{
    if (resourceRowId < 0)
        return;

    var (x1, y1) = GetResourcePosition(resourceRowId);
    QueueRoadCells(fromX, fromY, x1, y1);
}

IEnumerable<(int x, int y)> TraceLine(int x0, int y0, int x1, int y1)
{
    int dx = Math.Abs(x1 - x0);
    int dy = Math.Abs(y1 - y0);
    int sx = x0 < x1 ? 1 : -1;
    int sy = y0 < y1 ? 1 : -1;
    int err = dx - dy;

    while (true)
    {
        yield return (x0, y0);
        if (x0 == x1 && y0 == y1)
            yield break;

        int e2 = err * 2;
        if (e2 > -dy)
        {
            err -= dy;
            x0 += sx;
        }
        if (e2 < dx)
        {
            err += dx;
            y0 += sy;
        }
    }
}

void RunDirectorStep()
{
    var (activeBuildOrderId, activeSiteId) = EnsureDirectorHasActiveBuildOrder();
    UpdateBuildingStatuses(activeSiteId);
    UpdateBuildOrderStatuses(activeBuildOrderId, activeSiteId);
    GrantPopulationFromCompletedHomes();
    ReleaseConstructionUnits(activeSiteId);
    RebalanceProducerPool(activeBuildOrderId, activeSiteId);
    AssignConstructionUnits(activeBuildOrderId, activeSiteId);
    AssignProductionUnits();
    QueuePlayerSummary(activeBuildOrderId, activeSiteId);
    QueueDirectorState(activeBuildOrderId, activeSiteId);
}

 (int ActiveBuildOrderId, int ActiveSiteId) EnsureDirectorHasActiveBuildOrder()
{
    int activeBuildOrderId = GetActiveBuildOrderRowId();
    if (activeBuildOrderId >= 0)
        return (activeBuildOrderId, GetBuildOrderPlacedBuildingIdCurrent(activeBuildOrderId));

    int nextBuildOrderId = GetNextPendingBuildOrderRowId();
    if (nextBuildOrderId < 0)
        return (-1, -1);

    PlacedBuildingSite placedSite = QueueCreateBuilding(
        buildOrderLabel.Get(nextBuildOrderId) ?? $"Build order {nextBuildOrderId}",
        (BuildingKind)buildOrderBuildingKind.Get(nextBuildOrderId),
        buildOrderPreferredAnchorX.Get(nextBuildOrderId),
        buildOrderPreferredAnchorY.Get(nextBuildOrderId),
        isComplete: false,
        buildOrder: buildOrderSequence.Get(nextBuildOrderId));

    QueueBuildOrderPlacedBuildingId(nextBuildOrderId, placedSite.RowId, BuildOrderStatusKind.Active);
    QueueBuildOrderStatus(nextBuildOrderId, BuildOrderStatusKind.Active, placedSite.RowId);

    BuildingKind siteKind = (BuildingKind)buildOrderBuildingKind.Get(nextBuildOrderId);
    if (siteKind == BuildingKind.LoggingCamp)
        loggingCampId = placedSite.RowId;
    else if (siteKind == BuildingKind.HuntingLodge)
        huntingLodgeId = placedSite.RowId;

    var (settlementAccessX, settlementAccessY) = GetPrimaryBuildingAccessCell(settlementHallId);
    QueueRoadCells(settlementAccessX, settlementAccessY, placedSite.PrimaryAccessX, placedSite.PrimaryAccessY);

    ResourceKind linkedResourceKind = (ResourceKind)buildOrderLinkedResourceKind.Get(nextBuildOrderId);
    if (linkedResourceKind != ResourceKind.None)
    {
        List<int> resourceNodeIds = linkedResourceKind == ResourceKind.Wood ? woodNodeIds : foodNodeIds;
        int resourceRowId = PickClosestResourceNode(resourceNodeIds, placedSite.PrimaryAccessX, placedSite.PrimaryAccessY);
        ConnectRoadFromCellToResource(placedSite.PrimaryAccessX, placedSite.PrimaryAccessY, resourceRowId);
    }

    return (nextBuildOrderId, placedSite.RowId);
}

int GetActiveConstructionSiteId()
{
    int activeBuildOrderId = GetActiveBuildOrderRowId();
    if (activeBuildOrderId < 0)
        return -1;

    int buildOrderSequenceValue = buildOrderSequence.Get(activeBuildOrderId);
    foreach (int rowId in GetBuildingsByBuildOrderCurrent(buildOrderSequenceValue))
        return rowId;

    return GetBuildOrderPlacedBuildingIdCurrent(activeBuildOrderId);
}

int GetActiveBuildOrderRowId()
{
    foreach (int rowId in GetBuildOrdersByStatusCurrent(BuildOrderStatusKind.Active).OrderBy(id => buildOrderSequence.Get(id)))
    {
        return rowId;
    }

    return -1;
}

int GetNextPendingBuildOrderRowId()
{
    int priorityProducerOrderId = GetPriorityProducerBuildOrderRowId();
    if (priorityProducerOrderId >= 0)
        return priorityProducerOrderId;

    foreach (int rowId in GetBuildOrdersByStatusCurrent(BuildOrderStatusKind.Planned).OrderBy(id => buildOrderSequence.Get(id)))
    {
        return rowId;
    }

    return -1;
}

int GetPriorityProducerBuildOrderRowId()
{
    int loggingCampOrderId = FindPlannedBuildOrderByKind(BuildingKind.LoggingCamp);
    if (loggingCampOrderId >= 0 && !IsBuildingComplete(loggingCampId))
        return loggingCampOrderId;

    int huntingLodgeOrderId = FindPlannedBuildOrderByKind(BuildingKind.HuntingLodge);
    if (huntingLodgeOrderId >= 0 && !IsBuildingComplete(huntingLodgeId))
        return huntingLodgeOrderId;

    return -1;
}

int FindPlannedBuildOrderByKind(BuildingKind kind)
{
    foreach (int rowId in GetBuildOrdersByKindCurrent(kind).OrderBy(id => buildOrderSequence.Get(id)))
    {
        if (GetBuildOrderStatusCurrent(rowId) == BuildOrderStatusKind.Planned)
            return rowId;
    }

    return -1;
}

BuildOrderStatusKind GetBuildOrderStatusCurrent(int rowId)
{
    return (BuildOrderStatusKind)buildOrderStatus.Get(rowId);
}

int GetBuildOrderPlacedBuildingIdCurrent(int rowId)
{
    return buildOrderPlacedBuildingId.Get(rowId);
}

IEnumerable<int> GetBuildOrdersByStatusCurrent(BuildOrderStatusKind status)
{
    foreach (int rowId in buildOrdersByStatus.GetRowIds((byte)status))
        yield return rowId;
}

IEnumerable<int> GetBuildOrdersByKindCurrent(BuildingKind kind)
{
    foreach (int rowId in buildOrdersByBuildingKind.GetRowIds((byte)kind))
        yield return rowId;
}

void UpdateBuildOrderStatuses(int activeBuildOrderId, int activeSiteId)
{
    foreach (int rowId in playerBuildOrders.GetRowIds())
    {
        BuildOrderStatusKind nextStatus = rowId == activeBuildOrderId
            ? BuildOrderStatusKind.Active
            : BuildOrderStatusKind.Planned;

        int placedBuildingId = rowId == activeBuildOrderId && activeSiteId >= 0
            ? activeSiteId
            : buildOrderPlacedBuildingId.Get(rowId);
        if (placedBuildingId >= 0 && IsBuildingComplete(placedBuildingId))
            nextStatus = BuildOrderStatusKind.Complete;

        QueueBuildOrderStatus(rowId, nextStatus, placedBuildingId);
    }
}

BuildOrderStatusKind GetProjectedBuildOrderStatus(int rowId, int activeBuildOrderId, int activeSiteId)
{
    BuildOrderStatusKind status = rowId == activeBuildOrderId
        ? BuildOrderStatusKind.Active
        : BuildOrderStatusKind.Planned;

    int placedBuildingId = rowId == activeBuildOrderId ? activeSiteId : buildOrderPlacedBuildingId.Get(rowId);
    if (placedBuildingId >= 0 && IsBuildingComplete(placedBuildingId))
        status = BuildOrderStatusKind.Complete;

    return status;
}

string BuildPlayerSummary(int activeBuildOrderId = -1, int activeSiteId = -1)
{
    int planned = 0;
    int active = 0;
    int complete = 0;
    foreach (int rowId in playerBuildOrders.GetRowIds())
    {
        switch (GetProjectedBuildOrderStatus(rowId, activeBuildOrderId, activeSiteId))
        {
            case BuildOrderStatusKind.Planned:
                planned++;
                break;
            case BuildOrderStatusKind.Active:
                active++;
                break;
            case BuildOrderStatusKind.Complete:
                complete++;
                break;
        }
    }

    return $"Plans {playerBuildOrders.GetRowIds().Count()} sites | active {active} | planned {planned} | complete {complete}";
}

void QueuePlayerSummary(int activeBuildOrderId = -1, int activeSiteId = -1)
{
    RequireNextFrameCommands().SetString("players", "summary", playerRowId, BuildPlayerSummary(activeBuildOrderId, activeSiteId));
}

void QueueDirectorState(int activeBuildOrderId, int activeSiteId)
{
    string summary = BuildDirectorSummary(activeBuildOrderId, activeSiteId);
    DetDbCommandList commands = RequireNextFrameCommands();
    commands.SetInt("directors", "activeBuildOrderId", directorRowId, activeBuildOrderId);
    commands.SetInt("directors", "activeSiteId", directorRowId, activeSiteId);
    commands.SetString("directors", "summary", directorRowId, summary);
}

void QueueBuildOrderStatus(int rowId, BuildOrderStatusKind value, int? placedBuildingIdOverride = null)
{
    int placedBuildingId = placedBuildingIdOverride ?? buildOrderPlacedBuildingId.Get(rowId);
    string summary = BuildBuildOrderSummary(
        buildOrderLabel.Get(rowId) ?? $"Order {rowId}",
        (BuildingKind)buildOrderBuildingKind.Get(rowId),
        buildOrderSequence.Get(rowId),
        value,
        placedBuildingId,
        buildOrderDesiredBuilders.Get(rowId),
        buildOrderDesiredHaulers.Get(rowId));

    DetDbCommandList commands = RequireNextFrameCommands();
    commands.SetByte("playerBuildOrders", "status", rowId, (byte)value);
    commands.SetString("playerBuildOrders", "summary", rowId, summary);
}

void QueueBuildOrderPlacedBuildingId(int rowId, int placedBuildingId, BuildOrderStatusKind? statusOverride = null)
{
    BuildOrderStatusKind status = statusOverride ?? (BuildOrderStatusKind)buildOrderStatus.Get(rowId);
    string summary = BuildBuildOrderSummary(
        buildOrderLabel.Get(rowId) ?? $"Order {rowId}",
        (BuildingKind)buildOrderBuildingKind.Get(rowId),
        buildOrderSequence.Get(rowId),
        status,
        placedBuildingId,
        buildOrderDesiredBuilders.Get(rowId),
        buildOrderDesiredHaulers.Get(rowId));

    DetDbCommandList commands = RequireNextFrameCommands();
    commands.SetInt("playerBuildOrders", "placedBuildingId", rowId, placedBuildingId);
    commands.SetString("playerBuildOrders", "summary", rowId, summary);
}

void UpdateBuildingStatuses(int activeSiteId)
{
    foreach (int rowId in buildings.GetRowIds())
    {
        BuildingStatusKind status = rowId == activeSiteId
            ? (ConstructionSiteNeedsMaterials(rowId) ? BuildingStatusKind.Delivering : BuildingStatusKind.Building)
            : BuildingStatusKind.Planned;

        if (buildingBuildOrder.Get(rowId) == 0 || IsBuildingComplete(rowId))
            status = BuildingStatusKind.Complete;

        QueueBuildingStatus(rowId, status);
    }
}

void ReleaseConstructionUnits(int activeSiteId)
{
    foreach (int rowId in GetUnitsByRoleCurrent(UnitRoleKind.Builder).ToList())
    {
        if (activeSiteId < 0 || GetUnitDeliveryBuildingIdCurrent(rowId) != activeSiteId || !ConstructionMaterialsReady(activeSiteId))
            QueueUnitIdle(rowId, "Waiting at Settlement Hall");
    }

    foreach (int rowId in GetUnitsByRoleCurrent(UnitRoleKind.Hauler).ToList())
    {
        if (GetUnitCarryAmountCurrent(rowId) == 0 && (activeSiteId < 0 || GetUnitDeliveryBuildingIdCurrent(rowId) != activeSiteId || !ConstructionSiteNeedsMaterials(activeSiteId)))
            QueueUnitIdle(rowId, "Waiting at Settlement Hall");
    }

    foreach (int rowId in GetUnitsByRoleCurrent(UnitRoleKind.Woodcutter).ToList())
    {
        if (!IsBuildingComplete(loggingCampId))
            QueueUnitIdle(rowId, "Waiting for Logging Camp");
    }

    foreach (int rowId in GetUnitsByRoleCurrent(UnitRoleKind.Hunter).ToList())
    {
        if (!IsBuildingComplete(huntingLodgeId))
            QueueUnitIdle(rowId, "Waiting for Hunting Lodge");
    }
}

void RebalanceProducerPool(int activeBuildOrderId, int activeSiteId)
{
    if (activeBuildOrderId < 0 || activeSiteId < 0)
        return;

    int additionalBuildersNeeded = GetDesiredBuilderCount(activeBuildOrderId, activeSiteId) - CountUnitsAssignedToBuilding(UnitRoleKind.Builder, activeSiteId);
    int additionalHaulersNeeded = GetDesiredHaulerCount(activeBuildOrderId, activeSiteId) - CountUnitsAssignedToBuilding(UnitRoleKind.Hauler, activeSiteId);
    int missingConstructionWorkers = Math.Max(0, additionalBuildersNeeded) + Math.Max(0, additionalHaulersNeeded);
    if (missingConstructionWorkers <= CountUnitsByRole(UnitRoleKind.Idle))
        return;

    int reserveNeeded = missingConstructionWorkers - CountUnitsByRole(UnitRoleKind.Idle);
    foreach (int rowId in FindClosestReassignableProducerUnits(buildingAccessX.Get(settlementHallId), buildingAccessY.Get(settlementHallId), reserveNeeded))
        QueueUnitIdle(rowId, "Director reassigned worker for construction");
}

void AssignConstructionUnits(int activeBuildOrderId, int activeSiteId)
{
    if (activeBuildOrderId < 0 || activeSiteId < 0)
        return;

    var (targetX, targetY) = GetPrimaryBuildingAccessCell(activeSiteId);
    int desiredBuilders = GetDesiredBuilderCount(activeBuildOrderId, activeSiteId);
    int desiredHaulers = GetDesiredHaulerCount(activeBuildOrderId, activeSiteId);

    if (desiredBuilders > 0)
    {
        int assignedBuilders = CountUnitsAssignedToBuilding(UnitRoleKind.Builder, activeSiteId);
        foreach (int rowId in FindClosestIdleUnits(targetX, targetY, desiredBuilders - assignedBuilders))
            QueueUnitBuilder(rowId, activeSiteId);
    }

    if (desiredHaulers > 0)
    {
        int assignedHaulers = CountUnitsAssignedToBuilding(UnitRoleKind.Hauler, activeSiteId);
        foreach (int rowId in FindClosestIdleUnits(buildingAccessX.Get(settlementHallId), buildingAccessY.Get(settlementHallId), desiredHaulers - assignedHaulers))
            QueueUnitHauler(rowId, activeSiteId);
    }
}

void AssignProductionUnits()
{
    AssignHarvesters(UnitRoleKind.Woodcutter, loggingCampId);
    AssignHarvesters(UnitRoleKind.Hunter, huntingLodgeId);
}

void GrantPopulationFromCompletedHomes()
{
    foreach (int rowId in GetBuildingsByStatusCurrent(BuildingStatusKind.Complete))
    {
        if ((BuildingKind)buildingType.Get(rowId) != BuildingKind.House)
            continue;

        int capacity = buildingPopulationCapacity.Get(rowId);
        int granted = GetBuildingPopulationGrantedCurrent(rowId);
        while (granted < capacity)
        {
            QueueSpawnSettler(rowId);
            granted++;
        }

        if (granted != GetBuildingPopulationGrantedCurrent(rowId))
            QueueBuildingPopulationGranted(rowId, granted);
    }
}

void QueueSpawnSettler(int sourceBuildingId)
{
    var (spawnX, spawnY) = FindUnitSpawnNearBuilding(sourceBuildingId);
    int rowId = AllocateNextFrameRowId(units);
    string name = $"Settler_{rowId:D2}";

    DetDbCommandList commands = RequireNextFrameCommands();
    commands.CreateRow("units", rowId);
    commands.SetInt("units", "definitionId", rowId, unitDefinitionRowByRole[UnitRoleKind.Idle]);
    commands.SetString("units", "name", rowId, name);
    commands.SetByte("units", "role", rowId, (byte)UnitRoleKind.Idle);
    commands.SetByte("units", "state", rowId, (byte)UnitStateKind.Idle);
    commands.SetString("units", "task", rowId, $"New settler from {buildingName.Get(sourceBuildingId)}");
    commands.SetInt("units", "posX", rowId, spawnX);
    commands.SetInt("units", "posY", rowId, spawnY);
    commands.SetInt("units", "destX", rowId, spawnX);
    commands.SetInt("units", "destY", rowId, spawnY);
    commands.SetInt("units", "homeBuildingId", rowId, settlementHallId);
    commands.SetInt("units", "deliveryBuildingId", rowId, settlementHallId);
    commands.SetInt("units", "targetResourceId", rowId, -1);
    commands.SetByte("units", "carryType", rowId, (byte)ResourceKind.None);
    commands.SetInt("units", "carryAmount", rowId, 0);
    commands.SetFix64("units", "moveProgressMeters", rowId, Fix64.Zero);
    commands.PlaceRow("unitsByCell", rowId, spawnX, spawnY);

    ClearNextPath(rowId);
}

void QueueUnitIdle(int rowId, string task)
{
    QueueUnitRole(rowId, UnitRoleKind.Idle);
    QueueUnitState(rowId, UnitStateKind.Idle);
    QueueUnitTask(rowId, task);
    QueueUnitHomeBuildingId(rowId, settlementHallId);
    QueueUnitDeliveryBuildingId(rowId, settlementHallId);
    QueueUnitTargetResourceId(rowId, -1);
    QueueUnitCarryType(rowId, ResourceKind.None);
    QueueUnitCarryAmount(rowId, 0);
    ClearDestination(rowId);
}

void QueueUnitBuilder(int rowId, int siteId)
{
    QueueUnitRole(rowId, UnitRoleKind.Builder);
    QueueUnitState(rowId, UnitStateKind.ToDropoff);
    QueueUnitTask(rowId, $"Build {buildingName.Get(siteId)}");
    QueueUnitHomeBuildingId(rowId, settlementHallId);
    QueueUnitDeliveryBuildingId(rowId, siteId);
    QueueUnitTargetResourceId(rowId, -1);
    QueueUnitCarryType(rowId, ResourceKind.None);
    QueueUnitCarryAmount(rowId, 0);
}

void QueueUnitHauler(int rowId, int siteId)
{
    QueueUnitRole(rowId, UnitRoleKind.Hauler);
    QueueUnitState(rowId, UnitStateKind.ToPickup);
    QueueUnitTask(rowId, $"Haul materials to {buildingName.Get(siteId)}");
    QueueUnitHomeBuildingId(rowId, settlementHallId);
    QueueUnitDeliveryBuildingId(rowId, siteId);
    QueueUnitTargetResourceId(rowId, -1);
    QueueUnitCarryType(rowId, ResourceKind.None);
    QueueUnitCarryAmount(rowId, 0);
}

void QueueUnitHarvester(int rowId, UnitRoleKind role, int homeBuildingId)
{
    QueueUnitRole(rowId, role);
    QueueUnitState(rowId, UnitStateKind.ToSource);
    QueueUnitTask(rowId, $"Walk to {GetResourceLabel(GetHarvestResourceKind(role))} node");
    QueueUnitHomeBuildingId(rowId, homeBuildingId);
    QueueUnitDeliveryBuildingId(rowId, settlementHallId);
    QueueUnitTargetResourceId(rowId, FindBestResourceNode(role, GetUnitPosXCurrent(rowId), GetUnitPosYCurrent(rowId)));
    QueueUnitCarryType(rowId, ResourceKind.None);
    QueueUnitCarryAmount(rowId, 0);
}

void AssignHarvesters(UnitRoleKind role, int homeBuildingId)
{
    if (!IsBuildingComplete(homeBuildingId))
        return;

    int desiredCount = GetDesiredProducerCount(role);
    int currentCount = CountUnitsByRole(role);
    if (currentCount >= desiredCount)
        return;

    foreach (int rowId in FindClosestIdleUnits(buildingAccessX.Get(homeBuildingId), buildingAccessY.Get(homeBuildingId), desiredCount - currentCount))
        QueueUnitHarvester(rowId, role, homeBuildingId);
}

int CountUnitsByRole(UnitRoleKind role)
{
    return GetUnitsByRoleCurrent(role).Count();
}

List<int> FindClosestIdleUnits(int targetX, int targetY, int desiredCount)
{
    if (desiredCount <= 0)
        return [];

    return GetUnitsByStateCurrent(UnitStateKind.Idle)
        .Where(rowId => GetUnitRoleCurrent(rowId) == UnitRoleKind.Idle)
        .OrderBy(rowId => DistanceSquared(GetUnitPosXCurrent(rowId), GetUnitPosYCurrent(rowId), targetX, targetY))
        .Take(desiredCount)
        .ToList();
}

int CountUnitsAssignedToBuilding(UnitRoleKind role, int siteId)
{
    return GetUnitsAssignedToBuildingCurrent(role, siteId).Count();
}

List<int> FindClosestReassignableProducerUnits(int targetX, int targetY, int desiredCount)
{
    if (desiredCount <= 0)
        return [];

    var candidates = new List<int>();
    AddProducerReassignmentCandidates(candidates, UnitRoleKind.Woodcutter, targetX, targetY);
    AddProducerReassignmentCandidates(candidates, UnitRoleKind.Hunter, targetX, targetY);

    return candidates
        .OrderBy(rowId => DistanceSquared(GetUnitPosXCurrent(rowId), GetUnitPosYCurrent(rowId), targetX, targetY))
        .Take(desiredCount)
        .ToList();
}

void AddProducerReassignmentCandidates(List<int> candidates, UnitRoleKind role, int targetX, int targetY)
{
    int surplus = Math.Max(0, CountUnitsByRole(role) - GetDesiredProducerCount(role));
    if (surplus <= 0)
        return;

    foreach (int rowId in GetUnitsByRoleCurrent(role)
        .OrderBy(id => DistanceSquared(GetUnitPosXCurrent(id), GetUnitPosYCurrent(id), targetX, targetY))
        .Take(surplus))
    {
        candidates.Add(rowId);
    }
}

int GetDesiredBuilderCount(int activeBuildOrderId, int activeSiteId)
{
    if (activeBuildOrderId < 0 || activeSiteId < 0 || !ConstructionMaterialsReady(activeSiteId))
        return 0;

    int remainingWork = Math.Max(0, buildingRequiredWork.Get(activeSiteId) - GetBuildingProgressCurrent(activeSiteId));
    int preferred = Math.Max(1, buildOrderDesiredBuilders.Get(activeBuildOrderId));
    int workPerBuilder = Math.Max(1, GetUnitWorkBatch(UnitRoleKind.Builder));
    int workDriven = Math.Clamp((remainingWork + workPerBuilder - 1) / workPerBuilder, 1, 3);
    return Math.Max(preferred, workDriven);
}

int GetDesiredHaulerCount(int activeBuildOrderId, int activeSiteId)
{
    if (activeBuildOrderId < 0 || activeSiteId < 0 || !ConstructionSiteNeedsMaterials(activeSiteId))
        return 0;

    int totalRemainingMaterials =
        GetConstructionMaterialRemaining(activeSiteId, ResourceKind.Wood) +
        GetConstructionMaterialRemaining(activeSiteId, ResourceKind.Stone) +
        GetConstructionMaterialRemaining(activeSiteId, ResourceKind.Food);
    int preferred = Math.Max(1, buildOrderDesiredHaulers.Get(activeBuildOrderId));
    int loadPerHauler = Math.Max(1, GetUnitCarryCapacity(UnitRoleKind.Hauler));
    int desired = preferred;
    if (totalRemainingMaterials > loadPerHauler * 2)
        desired = Math.Max(desired, 2);
    if (totalRemainingMaterials > loadPerHauler * 4)
        desired = Math.Max(desired, 3);

    return Math.Min(3, desired);
}

int GetDesiredProducerCount(UnitRoleKind role)
{
    int homeBuildingId = role switch
    {
        UnitRoleKind.Woodcutter => loggingCampId,
        UnitRoleKind.Hunter => huntingLodgeId,
        _ => -1,
    };
    if (!IsBuildingComplete(homeBuildingId))
        return 0;

    ResourceKind resourceKind = GetHarvestResourceKind(role);
    int stock = GetBuildingResourceCurrent(settlementHallId, resourceKind);
    int target = GetTargetSettlementStock(resourceKind);
    int deficit = Math.Max(0, target - stock);

    if (deficit <= 0)
        return 1;
    if (deficit <= 8)
        return 1;
    if (deficit <= 20)
        return 2;
    return 3;
}

int GetTargetSettlementStock(ResourceKind kind)
{
    return kind switch
    {
        ResourceKind.Wood => 8 + GetProjectedBuildDemand(ResourceKind.Wood),
        ResourceKind.Stone => Math.Max(4, GetProjectedBuildDemand(ResourceKind.Stone)),
        ResourceKind.Food => Math.Max(16, units.GetRowIds().Count() * 3),
        _ => 0,
    };
}

int GetProjectedBuildDemand(ResourceKind kind)
{
    int total = 0;
    foreach (int rowId in playerBuildOrders.GetRowIds())
    {
        if (GetBuildOrderStatusCurrent(rowId) == BuildOrderStatusKind.Complete)
            continue;

        int placedBuildingId = GetBuildOrderPlacedBuildingIdCurrent(rowId);
        if (placedBuildingId >= 0 && buildings.RowExists(placedBuildingId))
        {
            total += GetConstructionMaterialRemaining(placedBuildingId, kind);
            continue;
        }

        BuildingKind buildingKind = (BuildingKind)buildOrderBuildingKind.Get(rowId);
        CompiledSpatialDefinition definition = spatialDefinitionsByRowId[spatialDefinitionRowByKind[buildingKind]];
        total += kind switch
        {
            ResourceKind.Wood => definition.BuildWoodCost,
            ResourceKind.Stone => definition.BuildStoneCost,
            ResourceKind.Food => definition.BuildFoodCost,
            _ => 0,
        };
    }

    return total;
}

bool IsBuildingComplete(int rowId)
{
    if (rowId < 0 || !buildings.RowExists(rowId))
        return false;

    return GetBuildingProgressCurrent(rowId) >= buildingRequiredWork.Get(rowId);
}

bool ConstructionSiteNeedsMaterials(int rowId)
{
    return GetBuildingWoodCurrent(rowId) < buildingRequiredWood.Get(rowId)
        || GetBuildingStoneCurrent(rowId) < buildingRequiredStone.Get(rowId)
        || GetBuildingFoodCurrent(rowId) < buildingRequiredFood.Get(rowId);
}

bool ConstructionMaterialsReady(int rowId)
{
    return !ConstructionSiteNeedsMaterials(rowId);
}

void SimulateTick()
{
    Fix64 cellMeters = GetSimulationCellMeters();

    foreach (int rowId in units.GetRowIds())
    {
        DetPath path = paths.Get(rowId);
        if (path.IsValid && !path.IsComplete)
        {
            Fix64 moveBudget = GetUnitMoveProgressCurrent(rowId) + GetUnitMoveMetersPerTick(rowId);
            bool advancedPath = false;

            while (path.IsValid && !path.IsComplete)
            {
                var (currentX, currentY) = path.Current(MapWidth);
                var (nextX, nextY) = path.Peek(MapWidth);
                if (nextX < 0 || nextY < 0)
                    break;

                Fix64 stepDistance = GetStepDistanceMeters(cellMeters, currentX, currentY, nextX, nextY);
                if (moveBudget < stepDistance)
                    break;

                moveBudget -= stepDistance;
                path.Advance();
                advancedPath = true;
                QueueUnitPosition(rowId, nextX, nextY);
            }

            if (advancedPath)
                WriteNextPath(rowId, path);

            QueueUnitMoveProgress(rowId, path.IsValid && !path.IsComplete ? moveBudget : Fix64.Zero);
        }
        else
        {
            QueueUnitMoveProgress(rowId, Fix64.Zero);
        }

        if (!path.IsValid || path.IsComplete)
        {
            ClearNextPath(rowId);
            QueueUnitMoveProgress(rowId, Fix64.Zero);
            ResolveArrivalAndAssignNextPath(rowId);
        }
    }
}

void ResolveArrivalAndAssignNextPath(int rowId)
{
    var role = GetUnitRoleCurrent(rowId);
    var stateKind = GetUnitStateCurrent(rowId);

    switch (role)
    {
        case UnitRoleKind.Woodcutter:
        case UnitRoleKind.Hunter:
            HandleHarvester(rowId, role, stateKind);
            break;
        case UnitRoleKind.Hauler:
            HandleHauler(rowId, stateKind);
            break;
        case UnitRoleKind.Builder:
            HandleBuilder(rowId, stateKind);
            break;
        default:
            QueueUnitTask(rowId, "Idle");
            ClearNextPath(rowId);
            break;
    }
}

void HandleHarvester(int rowId, UnitRoleKind role, UnitStateKind stateKind)
{
    int resourceRowId = GetUnitTargetResourceIdCurrent(rowId);
    ResourceKind resourceKind = GetHarvestResourceKind(role);

    if (!IsResourceNodeUsable(resourceRowId))
    {
        resourceRowId = FindBestResourceNode(role, GetUnitPosXCurrent(rowId), GetUnitPosYCurrent(rowId));
        QueueUnitTargetResourceId(rowId, resourceRowId);
    }

    if (resourceRowId < 0)
    {
        QueueUnitState(rowId, UnitStateKind.Idle);
        QueueUnitTask(rowId, "Waiting for source node");
        ClearDestination(rowId);
        return;
    }

    if (stateKind == UnitStateKind.ToSource)
    {
        if (IsAtResource(rowId, resourceRowId))
        {
            int available = GetResourceNodeAmountCurrent(resourceRowId);
            int workBatch = GetUnitWorkBatch(role);
            int carryCapacity = GetUnitCarryCapacity(role);
            int currentCarry = GetUnitCarryAmountCurrent(rowId);
            int remainingCapacity = Math.Max(0, carryCapacity - currentCarry);

            if (remainingCapacity <= 0)
            {
                QueueUnitCarryType(rowId, resourceKind);
                QueueUnitState(rowId, UnitStateKind.ToDropoff);
                QueueUnitTask(rowId, $"Deliver {GetResourceLabel(resourceKind)} to Settlement Hall");
                AssignPathToBuilding(rowId, GetUnitDeliveryBuildingIdCurrent(rowId));
                return;
            }

            int harvested = Math.Min(available, 1 + rng.NextInt(Math.Max(1, workBatch)));
            harvested = Math.Min(harvested, remainingCapacity);

            if (harvested > 0)
            {
                int remainingAmount = available - harvested;
                int nextCarry = currentCarry + harvested;

                QueueResourceNodeAmount(resourceRowId, remainingAmount);
                UpdateResourceLayers(resourceRowId);
                QueueUnitCarryType(rowId, resourceKind);
                QueueUnitCarryAmount(rowId, nextCarry);

                if (nextCarry >= carryCapacity || remainingAmount <= 0)
                {
                    QueueUnitState(rowId, UnitStateKind.ToDropoff);
                    QueueUnitTask(rowId, $"Deliver {GetResourceLabel(resourceKind)} to Settlement Hall");
                    AssignPathToBuilding(rowId, GetUnitDeliveryBuildingIdCurrent(rowId));
                    return;
                }

                QueueUnitTask(rowId, $"Harvest {GetResourceLabel(resourceKind)} at node ({nextCarry}/{carryCapacity})");
                ClearDestination(rowId);
                return;
            }

            if (currentCarry > 0)
            {
                QueueUnitState(rowId, UnitStateKind.ToDropoff);
                QueueUnitTask(rowId, $"Deliver {GetResourceLabel(resourceKind)} to Settlement Hall");
                AssignPathToBuilding(rowId, GetUnitDeliveryBuildingIdCurrent(rowId));
                return;
            }

            resourceRowId = FindBestResourceNode(role, GetUnitPosXCurrent(rowId), GetUnitPosYCurrent(rowId));
            QueueUnitTargetResourceId(rowId, resourceRowId);
            QueueUnitTask(rowId, $"Search for active {GetResourceLabel(resourceKind)} node");
            AssignPathToResource(rowId, resourceRowId);
            return;
        }

        QueueUnitTask(rowId, $"Walk to {GetResourceLabel(resourceKind)} node");
        AssignPathToResource(rowId, resourceRowId);
        return;
    }

    if (stateKind == UnitStateKind.ToDropoff)
    {
        int settlementHallForUnit = GetUnitDeliveryBuildingIdCurrent(rowId);
        if (IsAtBuilding(rowId, settlementHallForUnit))
        {
            int amount = GetUnitCarryAmountCurrent(rowId);
            if (resourceKind == ResourceKind.Wood)
                QueueBuildingWood(settlementHallForUnit, GetBuildingWoodCurrent(settlementHallForUnit) + amount);
            else if (resourceKind == ResourceKind.Food)
                QueueBuildingFood(settlementHallForUnit, GetBuildingFoodCurrent(settlementHallForUnit) + amount);
            else
                QueueBuildingStone(settlementHallForUnit, GetBuildingStoneCurrent(settlementHallForUnit) + amount);

            QueueUnitCarryType(rowId, ResourceKind.None);
            QueueUnitCarryAmount(rowId, 0);
            QueueUnitState(rowId, UnitStateKind.ToSource);
            QueueUnitTask(rowId, $"Return to {GetResourceLabel(resourceKind)} node");

            resourceRowId = GetUnitTargetResourceIdCurrent(rowId);
            if (!IsResourceNodeUsable(resourceRowId))
            {
                resourceRowId = FindBestResourceNode(role, GetUnitPosXCurrent(rowId), GetUnitPosYCurrent(rowId));
                QueueUnitTargetResourceId(rowId, resourceRowId);
            }

            AssignPathToResource(rowId, resourceRowId);
            return;
        }

        QueueUnitTask(rowId, $"Carry {GetResourceLabel(resourceKind)} to Settlement Hall");
        AssignPathToBuilding(rowId, settlementHallForUnit);
        return;
    }

    QueueUnitState(rowId, UnitStateKind.ToSource);
    QueueUnitTask(rowId, $"Walk to {GetResourceLabel(resourceKind)} node");
    AssignPathToResource(rowId, resourceRowId);
}

void HandleHauler(int rowId, UnitStateKind stateKind)
{
    int pickupBuildingId = GetUnitHomeBuildingIdCurrent(rowId);
    int dropoffBuildingId = GetUnitDeliveryBuildingIdCurrent(rowId);
    int carryCapacity = GetUnitCarryCapacity(UnitRoleKind.Hauler);

    if (dropoffBuildingId < 0 || !buildings.RowExists(dropoffBuildingId) || IsBuildingComplete(dropoffBuildingId))
    {
        QueueUnitIdle(rowId, "Waiting at Settlement Hall");
        return;
    }

    if (stateKind == UnitStateKind.ToPickup)
    {
        if (IsAtBuilding(rowId, pickupBuildingId))
        {
            ResourceKind materialKind = GetNextConstructionMaterial(dropoffBuildingId);
            if (materialKind == ResourceKind.None)
            {
                QueueUnitIdle(rowId, "Waiting for construction materials");
                ClearDestination(rowId);
                return;
            }

            int available = GetBuildingResourceCurrent(pickupBuildingId, materialKind);
            int remainingNeed = GetConstructionMaterialRemaining(dropoffBuildingId, materialKind);
            int load = Math.Min(available, Math.Min(remainingNeed, Math.Max(1, carryCapacity)));
            if (load <= 0)
            {
                QueueUnitTask(rowId, $"Waiting for {GetResourceLabel(materialKind)}");
                ClearDestination(rowId);
                return;
            }

            QueueBuildingResource(pickupBuildingId, materialKind, available - load);
            QueueUnitCarryType(rowId, materialKind);
            QueueUnitCarryAmount(rowId, load);
            QueueUnitState(rowId, UnitStateKind.ToDropoff);
            QueueUnitTask(rowId, $"Deliver {GetResourceLabel(materialKind)} to {buildingName.Get(dropoffBuildingId)}");
            AssignPathToBuilding(rowId, dropoffBuildingId);
            return;
        }

        QueueUnitTask(rowId, "Move to Settlement Hall");
        AssignPathToBuilding(rowId, pickupBuildingId);
        return;
    }

    if (stateKind == UnitStateKind.ToDropoff)
    {
        if (IsAtBuilding(rowId, dropoffBuildingId))
        {
            ResourceKind materialKind = GetUnitCarryTypeCurrent(rowId);
            int load = GetUnitCarryAmountCurrent(rowId);
            QueueBuildingResource(dropoffBuildingId, materialKind, GetBuildingResourceCurrent(dropoffBuildingId, materialKind) + load);
            QueueUnitCarryType(rowId, ResourceKind.None);
            QueueUnitCarryAmount(rowId, 0);
            if (ConstructionSiteNeedsMaterials(dropoffBuildingId))
            {
                QueueUnitState(rowId, UnitStateKind.ToPickup);
                QueueUnitTask(rowId, "Return for more materials");
                AssignPathToBuilding(rowId, pickupBuildingId);
            }
            else
            {
                QueueUnitIdle(rowId, "Materials delivered");
            }
            return;
        }

        QueueUnitTask(rowId, "Carry materials to build site");
        AssignPathToBuilding(rowId, dropoffBuildingId);
        return;
    }

    QueueUnitState(rowId, UnitStateKind.ToPickup);
    QueueUnitTask(rowId, "Move to Settlement Hall");
    AssignPathToBuilding(rowId, pickupBuildingId);
}

void HandleBuilder(int rowId, UnitStateKind stateKind)
{
    int siteId = GetUnitDeliveryBuildingIdCurrent(rowId);
    if (siteId < 0 || !buildings.RowExists(siteId))
    {
        QueueUnitIdle(rowId, "Waiting at Settlement Hall");
        return;
    }

    if (!ConstructionMaterialsReady(siteId))
    {
        QueueUnitIdle(rowId, "Waiting for materials");
        return;
    }

    if (IsAtBuilding(rowId, siteId))
    {
        int currentProgress = GetBuildingProgressCurrent(siteId);
        int requiredWork = buildingRequiredWork.Get(siteId);
        int workDone = Math.Min(requiredWork, currentProgress + Math.Max(1, GetUnitWorkBatch(UnitRoleKind.Builder)));
        QueueBuildingProgress(siteId, workDone);

        if (workDone >= requiredWork)
        {
            QueueBuildingStatus(siteId, BuildingStatusKind.Complete);
            QueueUnitIdle(rowId, $"{buildingName.Get(siteId)} completed");
        }
        else
        {
            QueueBuildingStatus(siteId, BuildingStatusKind.Building);
            QueueUnitTask(rowId, $"Build {buildingName.Get(siteId)} ({workDone}/{requiredWork})");
            ClearDestination(rowId);
        }
        return;
    }

    QueueUnitState(rowId, UnitStateKind.ToDropoff);
    QueueUnitTask(rowId, $"Move to {buildingName.Get(siteId)}");
    AssignPathToBuilding(rowId, siteId);
}

void AssignPathToBuilding(int rowId, int buildingRowId)
{
    if (!buildings.RowExists(buildingRowId))
    {
        ClearDestination(rowId);
        return;
    }

    var (startX, startY) = (GetUnitPosXCurrent(rowId), GetUnitPosYCurrent(rowId));
    var (targetX, targetY) = GetClosestBuildingAccessCell(buildingRowId, startX, startY);
    AssignPathToCell(rowId, targetX, targetY);
}

void AssignPathToResource(int rowId, int resourceRowId)
{
    if (!IsResourceNodeUsable(resourceRowId))
    {
        ClearDestination(rowId);
        return;
    }

    var (x, y) = GetResourcePosition(resourceRowId);
    AssignPathToCell(rowId, x, y);
}

void AssignPathToCell(int rowId, int targetX, int targetY)
{
    int startX = GetUnitPosXCurrent(rowId);
    int startY = GetUnitPosYCurrent(rowId);

    QueueUnitDestination(rowId, targetX, targetY);

    if (startX == targetX && startY == targetY)
    {
        ClearNextPath(rowId);
        return;
    }

    DetPath path = pathfinder.FindPath(startX, startY, targetX, targetY, walkable);
    if (!path.IsValid)
    {
        ClearNextPath(rowId);
        return;
    }

    WriteNextPath(rowId, path);
}

void ClearDestination(int rowId)
{
    int x = GetUnitPosXCurrent(rowId);
    int y = GetUnitPosYCurrent(rowId);
    QueueUnitDestination(rowId, x, y);
    ClearNextPath(rowId);
}

bool IsAtBuilding(int rowId, int buildingRowId)
{
    int unitX = GetUnitPosXCurrent(rowId);
    int unitY = GetUnitPosYCurrent(rowId);
    foreach (var cell in EnumerateBuildingConnectorWorldCells(buildingRowId))
    {
        if (unitX == cell.x && unitY == cell.y)
            return true;
    }

    return false;
}

bool IsAtResource(int rowId, int resourceRowId)
{
    var (x, y) = GetResourcePosition(resourceRowId);
    return GetUnitPosXCurrent(rowId) == x && GetUnitPosYCurrent(rowId) == y;
}

(int x, int y) GetPrimaryBuildingAccessCell(int rowId)
    => (buildingAccessX.Get(rowId), buildingAccessY.Get(rowId));

(int x, int y) GetResourcePosition(int rowId)
    => (resourceNodePosX.Get(rowId), resourceNodePosY.Get(rowId));

IEnumerable<(int x, int y)> EnumerateBuildingConnectorWorldCells(int buildingRowId)
{
    int definitionRowId = buildingDefinitionId.Get(buildingRowId);
    CompiledSpatialDefinition definition = spatialDefinitionsByRowId[definitionRowId];
    int anchorX = buildingAnchorX.Get(buildingRowId);
    int anchorY = buildingAnchorY.Get(buildingRowId);

    foreach (var offset in definition.ConnectorOffsets)
        yield return (anchorX + offset.X, anchorY + offset.Y);
}

(int x, int y) GetClosestBuildingAccessCell(int buildingRowId, int fromX, int fromY)
{
    var primary = GetPrimaryBuildingAccessCell(buildingRowId);
    int bestDistance = DistanceSquared(fromX, fromY, primary.x, primary.y);
    var best = primary;

    foreach (var cell in EnumerateBuildingConnectorWorldCells(buildingRowId))
    {
        int distance = DistanceSquared(fromX, fromY, cell.x, cell.y);
        if (distance < bestDistance)
        {
            bestDistance = distance;
            best = cell;
        }
    }

    return best;
}

bool IsResourceNodeUsable(int rowId)
{
    return rowId >= 0 && resourceNodes.RowExists(rowId) && GetResourceNodeAmountCurrent(rowId) > 0;
}

void UpdateResourceLayers(int rowId)
{
    int x = resourceNodePosX.Get(rowId);
    int y = resourceNodePosY.Get(rowId);
    int amount = Math.Max(0, GetResourceNodeAmountCurrent(rowId));
    DetDbCommandList commands = RequireNextFrameCommands();
    commands.SetIntCell("resourceAmount", x, y, amount);
    commands.SetByteCell("resourceType", x, y, amount > 0 ? resourceNodeType.Get(rowId) : (byte)ResourceKind.None);
}

void WriteNextPath(int rowId, DetPath path)
{
    RequireNextFramePaths().Set(rowId, path);
}

void ClearNextPath(int rowId)
{
    RequireNextFramePaths().Clear(rowId);
}

int FindBestResourceNode(UnitRoleKind role, int fromX, int fromY)
{
    ResourceKind resourceKind = GetHarvestResourceKind(role);
    if (resourceKind == ResourceKind.None)
        return -1;

    int bestRowId = -1;
    int bestDistance = int.MaxValue;
    foreach (int rowId in GetResourceNodesByType(resourceKind))
    {
        if (!IsResourceNodeUsable(rowId))
            continue;

        int dx = resourceNodePosX.Get(rowId) - fromX;
        int dy = resourceNodePosY.Get(rowId) - fromY;
        int dist = (dx * dx) + (dy * dy);
        if (dist < bestDistance)
        {
            bestDistance = dist;
            bestRowId = rowId;
        }
    }

    return bestRowId;
}

ResourceKind GetHarvestResourceKind(UnitRoleKind role)
{
    return role switch
    {
        UnitRoleKind.Woodcutter => ResourceKind.Wood,
        UnitRoleKind.Hunter => ResourceKind.Food,
        _ => ResourceKind.None,
    };
}

int PickClosestResourceNode(List<int> nodeIds, int fromX, int fromY)
{
    int bestRowId = -1;
    int bestDistance = int.MaxValue;
    foreach (int rowId in nodeIds)
    {
        int dx = resourceNodePosX.Get(rowId) - fromX;
        int dy = resourceNodePosY.Get(rowId) - fromY;
        int dist = (dx * dx) + (dy * dy);
        if (dist < bestDistance)
        {
            bestDistance = dist;
            bestRowId = rowId;
        }
    }

    return bestRowId;
}

(int x, int y) FindUnitSpawnNearBuilding(int buildingRowId)
{
    var (originX, originY) = GetPrimaryBuildingAccessCell(buildingRowId);

    for (int radius = 0; radius <= 6; radius++)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = originX + dx;
                int y = originY + dy;
                if (!InBounds(x, y) || !walkable.Get(x, y))
                    continue;

                if (unitsByCell.CountAt(x, y) > 0 || placementIdLayer.Get(x, y) >= 0)
                    continue;

                return (x, y);
            }
        }
    }

    return (originX, originY);
}

UnitStateKind GetInitialState(UnitRoleKind role)
{
    return role switch
    {
        UnitRoleKind.Woodcutter => UnitStateKind.ToSource,
        UnitRoleKind.Hunter => UnitStateKind.ToSource,
        UnitRoleKind.Hauler => UnitStateKind.ToPickup,
        UnitRoleKind.Builder => UnitStateKind.ToDropoff,
        _ => UnitStateKind.Idle,
    };
}

int GetUnitCarryCapacity(UnitRoleKind role)
{
    return unitDefinitionCarryCapacity.Get(unitDefinitionRowByRole[role]);
}

int GetUnitWorkBatch(UnitRoleKind role)
{
    return unitDefinitionWorkBatch.Get(unitDefinitionRowByRole[role]);
}

int GetBuildingWoodCurrent(int rowId)
{
    return buildingWood.Get(rowId);
}

int GetBuildingStoneCurrent(int rowId)
{
    return buildingStone.Get(rowId);
}

int GetBuildingFoodCurrent(int rowId)
{
    return buildingFood.Get(rowId);
}

int GetBuildingProgressCurrent(int rowId)
{
    return buildingProgress.Get(rowId);
}

int GetBuildingPopulationGrantedCurrent(int rowId)
{
    return buildingPopulationGranted.Get(rowId);
}

BuildingStatusKind GetBuildingStatusCurrent(int rowId)
{
    return (BuildingStatusKind)buildingStatus.Get(rowId);
}

int GetResourceNodeAmountCurrent(int rowId)
{
    return resourceNodeAmount.Get(rowId);
}

int GetUnitPosXCurrent(int rowId)
{
    return unitPosX.Get(rowId);
}

int GetUnitPosYCurrent(int rowId)
{
    return unitPosY.Get(rowId);
}

Fix64 GetUnitMoveProgressCurrent(int rowId)
{
    return unitMoveProgressMeters.Get(rowId);
}

UnitStateKind GetUnitStateCurrent(int rowId)
{
    return (UnitStateKind)unitState.Get(rowId);
}

UnitRoleKind GetUnitRoleCurrent(int rowId)
{
    return (UnitRoleKind)unitRole.Get(rowId);
}

int GetUnitCarryAmountCurrent(int rowId)
{
    return unitCarryAmount.Get(rowId);
}

ResourceKind GetUnitCarryTypeCurrent(int rowId)
{
    return (ResourceKind)unitCarryType.Get(rowId);
}

int GetUnitTargetResourceIdCurrent(int rowId)
{
    return unitTargetResourceId.Get(rowId);
}

int GetUnitHomeBuildingIdCurrent(int rowId)
{
    return unitHomeBuildingId.Get(rowId);
}

int GetUnitDeliveryBuildingIdCurrent(int rowId)
{
    return unitDeliveryBuildingId.Get(rowId);
}

IEnumerable<int> GetUnitsByRoleCurrent(UnitRoleKind role)
{
    foreach (int rowId in unitsByRole.GetRowIds((byte)role))
        yield return rowId;
}

IEnumerable<int> GetUnitsByStateCurrent(UnitStateKind state)
{
    foreach (int rowId in unitsByState.GetRowIds((byte)state))
        yield return rowId;
}

IEnumerable<int> GetBuildingsByStatusCurrent(BuildingStatusKind status)
{
    foreach (int rowId in buildingsByStatus.GetRowIds((byte)status))
        yield return rowId;
}

IEnumerable<int> GetBuildingsByBuildOrderCurrent(int buildOrderValue)
{
    foreach (int rowId in buildingsByBuildOrder.GetRowIds(buildOrderValue))
        yield return rowId;
}

IEnumerable<int> GetUnitsByDeliveryBuildingCurrent(int buildingRowId)
{
    foreach (int rowId in unitsByDeliveryBuilding.GetRowIds(buildingRowId))
        yield return rowId;
}

IEnumerable<int> GetUnitsAssignedToBuildingCurrent(UnitRoleKind role, int buildingRowId)
{
    List<int> roleRows = GetUnitsByRoleCurrent(role).ToList();
    List<int> deliveryRows = GetUnitsByDeliveryBuildingCurrent(buildingRowId).ToList();
    IEnumerable<int> baseRows = roleRows.Count <= deliveryRows.Count ? roleRows : deliveryRows;

    var yielded = new HashSet<int>();
    foreach (int rowId in baseRows)
    {
        if (GetUnitRoleCurrent(rowId) != role || GetUnitDeliveryBuildingIdCurrent(rowId) != buildingRowId)
            continue;

        if (yielded.Add(rowId))
            yield return rowId;
    }
}

IEnumerable<int> GetResourceNodesByType(ResourceKind kind)
    => resourceNodesByType.GetRowIds((byte)kind);

int GetBuildingResourceCurrent(int rowId, ResourceKind kind)
{
    return kind switch
    {
        ResourceKind.Wood => GetBuildingWoodCurrent(rowId),
        ResourceKind.Stone => GetBuildingStoneCurrent(rowId),
        ResourceKind.Food => GetBuildingFoodCurrent(rowId),
        _ => 0,
    };
}

int GetConstructionMaterialRemaining(int rowId, ResourceKind kind)
{
    return kind switch
    {
        ResourceKind.Wood => Math.Max(0, buildingRequiredWood.Get(rowId) - GetBuildingWoodCurrent(rowId)),
        ResourceKind.Stone => Math.Max(0, buildingRequiredStone.Get(rowId) - GetBuildingStoneCurrent(rowId)),
        ResourceKind.Food => Math.Max(0, buildingRequiredFood.Get(rowId) - GetBuildingFoodCurrent(rowId)),
        _ => 0,
    };
}

ResourceKind GetNextConstructionMaterial(int rowId)
{
    if (GetConstructionMaterialRemaining(rowId, ResourceKind.Wood) > 0)
        return ResourceKind.Wood;

    if (GetConstructionMaterialRemaining(rowId, ResourceKind.Stone) > 0)
        return ResourceKind.Stone;

    if (GetConstructionMaterialRemaining(rowId, ResourceKind.Food) > 0)
        return ResourceKind.Food;

    return ResourceKind.None;
}

void QueueBuildingWood(int rowId, int value)
{
    RequireNextFrameCommands().SetInt("buildings", "stockWood", rowId, value);
}

void QueueBuildingStone(int rowId, int value)
{
    RequireNextFrameCommands().SetInt("buildings", "stockStone", rowId, value);
}

void QueueBuildingFood(int rowId, int value)
{
    RequireNextFrameCommands().SetInt("buildings", "stockFood", rowId, value);
}

void QueueBuildingProgress(int rowId, int value)
{
    RequireNextFrameCommands().SetInt("buildings", "constructionProgress", rowId, value);
}

void QueueBuildingStatus(int rowId, BuildingStatusKind value)
{
    RequireNextFrameCommands().SetByte("buildings", "status", rowId, (byte)value);
}

void QueueBuildingPopulationGranted(int rowId, int value)
{
    RequireNextFrameCommands().SetInt("buildings", "populationGranted", rowId, value);
}

void QueueBuildingResource(int rowId, ResourceKind kind, int value)
{
    switch (kind)
    {
        case ResourceKind.Wood:
            QueueBuildingWood(rowId, value);
            break;
        case ResourceKind.Stone:
            QueueBuildingStone(rowId, value);
            break;
        case ResourceKind.Food:
            QueueBuildingFood(rowId, value);
            break;
    }
}

void QueueResourceNodeAmount(int rowId, int value)
{
    RequireNextFrameCommands().SetInt("resourceNodes", "amount", rowId, value);
}

void QueueUnitRole(int rowId, UnitRoleKind value)
{
    int definitionId = unitDefinitionRowByRole[value];
    DetDbCommandList commands = RequireNextFrameCommands();
    commands.SetByte("units", "role", rowId, (byte)value);
    commands.SetInt("units", "definitionId", rowId, definitionId);
}

void QueueUnitHomeBuildingId(int rowId, int value)
{
    RequireNextFrameCommands().SetInt("units", "homeBuildingId", rowId, value);
}

void QueueUnitDeliveryBuildingId(int rowId, int value)
{
    RequireNextFrameCommands().SetInt("units", "deliveryBuildingId", rowId, value);
}

void QueueUnitPosition(int rowId, int x, int y)
{
    DetDbCommandList commands = RequireNextFrameCommands();
    commands.MoveRow("unitsByCell", rowId, x, y);
    commands.SetInt("units", "posX", rowId, x);
    commands.SetInt("units", "posY", rowId, y);
}

void QueueUnitMoveProgress(int rowId, Fix64 value)
{
    RequireNextFrameCommands().SetFix64("units", "moveProgressMeters", rowId, value);
}

void QueueUnitState(int rowId, UnitStateKind value)
{
    RequireNextFrameCommands().SetByte("units", "state", rowId, (byte)value);
}

void QueueUnitTask(int rowId, string value)
{
    RequireNextFrameCommands().SetString("units", "task", rowId, value);
}

void QueueUnitDestination(int rowId, int x, int y)
{
    DetDbCommandList commands = RequireNextFrameCommands();
    commands.SetInt("units", "destX", rowId, x);
    commands.SetInt("units", "destY", rowId, y);
}

void QueueUnitTargetResourceId(int rowId, int value)
{
    RequireNextFrameCommands().SetInt("units", "targetResourceId", rowId, value);
}

void QueueUnitCarryType(int rowId, ResourceKind value)
{
    RequireNextFrameCommands().SetByte("units", "carryType", rowId, (byte)value);
}

void QueueUnitCarryAmount(int rowId, int value)
{
    RequireNextFrameCommands().SetInt("units", "carryAmount", rowId, value);
}

Fix64 GetSimulationCellMeters()
{
    return simulationCellMeters.Get(simulationSettingsRowId);
}

Fix64 GetSimulationTickSeconds()
{
    return simulationTickSeconds.Get(simulationSettingsRowId);
}

Fix64 GetUnitMoveMetersPerTick(int rowId)
{
    return unitDefinitionMoveMetersPerTick.Get(unitDefinitionId.Get(rowId));
}

Fix64 GetStepDistanceMeters(Fix64 cellMeters, int fromX, int fromY, int toX, int toY)
{
    if (fromX == toX && fromY == toY)
        return Fix64.Zero;

    bool isDiagonal = fromX != toX && fromY != toY;
    return isDiagonal ? cellMeters * diagonalStepMultiplier : cellMeters;
}

string GetRoleLabel(UnitRoleKind role)
{
    return role switch
    {
        UnitRoleKind.Idle => "Idle",
        UnitRoleKind.Hauler => "Hauler",
        UnitRoleKind.Builder => "Builder",
        UnitRoleKind.Woodcutter => "Woodcutter",
        UnitRoleKind.Hunter => "Hunter",
        _ => "Unit",
    };
}

string GetResourceLabel(ResourceKind kind)
{
    return kind switch
    {
        ResourceKind.Wood => "Wood",
        ResourceKind.Stone => "Stone",
        ResourceKind.Food => "Food",
        _ => "None",
    };
}

string GetBuildingStatusLabel(BuildingStatusKind kind)
{
    return kind switch
    {
        BuildingStatusKind.Planned => "Planned",
        BuildingStatusKind.Delivering => "Delivering",
        BuildingStatusKind.Building => "Building",
        BuildingStatusKind.Complete => "Complete",
        _ => "Unknown",
    };
}

string GetBuildOrderStatusLabel(BuildOrderStatusKind kind)
{
    return kind switch
    {
        BuildOrderStatusKind.Planned => "Planned",
        BuildOrderStatusKind.Active => "Active",
        BuildOrderStatusKind.Complete => "Complete",
        _ => "Unknown",
    };
}

string BuildLayoutPreview(string layoutText)
{
    string preview = layoutText.Replace("\r", string.Empty).Replace("\n", " / ");
    return preview.Length <= 56 ? preview : $"{preview[..53]}...";
}

string BuildFootprintSummary(CompiledSpatialDefinition definition)
    => $"{definition.Width}x{definition.Height} | {definition.FootprintOffsets.Length} solid | {definition.ConnectorOffsets.Length} connector";

string BuildConnectorSummary(CompiledSpatialDefinition definition)
{
    if (definition.ConnectorOffsets.Length == 0)
        return "No road connectors";

    return string.Join(
        " | ",
        definition.ConnectorOffsets.Select(offset => $"({FormatSigned(offset.X)},{FormatSigned(offset.Y)}) from anchor"));
}

string BuildStorageSummary(int woodCapacity, int stoneCapacity, int foodCapacity)
{
    var parts = new List<string>();
    if (woodCapacity > 0)
        parts.Add($"Wood {woodCapacity}");
    if (stoneCapacity > 0)
        parts.Add($"Stone {stoneCapacity}");
    if (foodCapacity > 0)
        parts.Add($"Food {foodCapacity}");

    return parts.Count == 0 ? "No storage" : string.Join(" | ", parts);
}

string BuildConstructionSummary(int woodCost, int stoneCost, int foodCost, int workRequired)
{
    var parts = new List<string>();
    if (woodCost > 0)
        parts.Add($"Wood {woodCost}");
    if (stoneCost > 0)
        parts.Add($"Stone {stoneCost}");
    if (foodCost > 0)
        parts.Add($"Food {foodCost}");

    string materials = parts.Count == 0 ? "no material cost" : string.Join(" | ", parts);
    return $"{materials} | work {workRequired}";
}

string BuildYieldSummary(TerrainKind preferredTerrain, int minYield, int maxYield)
{
    if (maxYield <= 0)
        return $"Best on {preferredTerrain} | no natural yield";

    return $"Best on {preferredTerrain} | yield {minYield}-{maxYield}";
}

string BuildSimulationSettingsSummary(Fix64 cellMeters, Fix64 tickSeconds)
{
    return $"{FormatFix64(cellMeters)} meter per cell | {FormatFix64(tickSeconds)} second per tick";
}

string BuildUnitDefinitionSummary(UnitRoleKind role, int carryCapacity, int workBatch, Fix64 moveMetersPerTick)
{
    string batchText = workBatch > 0 ? $"work batch {workBatch}" : "no work batch";
    Fix64 tickSeconds = GetSimulationTickSeconds();
    Fix64 moveMetersPerSecond = tickSeconds > Fix64.Zero ? moveMetersPerTick / tickSeconds : Fix64.Zero;
    return
        $"{GetRoleLabel(role)} | carry {carryCapacity} | {batchText} | " +
        $"move {FormatFix64(moveMetersPerTick)} m/tick ({FormatFix64(moveMetersPerSecond)} m/s)";
}

string BuildBuildOrderSummary(
    string label,
    BuildingKind kind,
    int sequence,
    BuildOrderStatusKind status,
    int placedBuildingId,
    int desiredBuilders,
    int desiredHaulers)
{
    string siteText = placedBuildingId >= 0 ? $"site {placedBuildingId}" : "site pending";
    return $"#{sequence} {label} | {kind} | {GetBuildOrderStatusLabel(status)} | {siteText} | builders {desiredBuilders} | haulers {desiredHaulers}";
}

string BuildDirectorSummary(int activeBuildOrderId, int activeSiteId)
{
    if (activeBuildOrderId < 0 || activeSiteId < 0)
        return "No active build order. Director only keeps resource workers productive.";

    string buildLabel = buildOrderLabel.Get(activeBuildOrderId) ?? $"Order {activeBuildOrderId}";
    if (!buildings.RowExists(activeSiteId))
        return $"Queued {buildLabel} and is waiting for the site snapshot to become current.";

    int desiredBuilders = GetDesiredBuilderCount(activeBuildOrderId, activeSiteId);
    int desiredHaulers = GetDesiredHaulerCount(activeBuildOrderId, activeSiteId);
    int assignedBuilders = CountUnitsAssignedToBuilding(UnitRoleKind.Builder, activeSiteId);
    int assignedHaulers = CountUnitsAssignedToBuilding(UnitRoleKind.Hauler, activeSiteId);
    return
        $"Driving {buildLabel} | builders {assignedBuilders}/{desiredBuilders} | " +
        $"haulers {assignedHaulers}/{desiredHaulers} | progress {GetBuildingProgressCurrent(activeSiteId)}/{buildingRequiredWork.Get(activeSiteId)}";
}

string FormatFix64(Fix64 value)
{
    return value.ToString();
}

string FormatSigned(int value)
{
    if (value > 0)
        return $"+{value}";

    return value.ToString(CultureInfo.InvariantCulture);
}

CompiledSpatialDefinition CompileSpatialDefinition(
    BuildingKind kind,
    string name,
    string category,
    string layoutText,
    int storageWoodCapacity,
    int storageStoneCapacity,
    int storageFoodCapacity,
    int populationCapacity,
    int buildWoodCost,
    int buildStoneCost,
    int buildFoodCost,
    int buildWorkRequired)
{
    string normalized = layoutText.Replace("\r", string.Empty);
    string[] lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    int height = lines.Length;
    int width = lines.Max(line => line.Length);

    var footprintOffsets = new List<CellOffset>();
    var connectorOffsets = new List<CellOffset>();
    bool anchorFound = false;
    int anchorX = 0;
    int anchorY = 0;

    for (int y = 0; y < height; y++)
    {
        string line = lines[y];
        for (int x = 0; x < width; x++)
        {
            char symbol = x < line.Length ? line[x] : '.';
            switch (symbol)
            {
                case '#':
                    footprintOffsets.Add(new CellOffset(x, y));
                    break;
                case 'A':
                    anchorFound = true;
                    anchorX = x;
                    anchorY = y;
                    footprintOffsets.Add(new CellOffset(x, y));
                    break;
                case 'r':
                    connectorOffsets.Add(new CellOffset(x, y));
                    break;
                case '.':
                    break;
                default:
                    throw new InvalidOperationException($"Unknown layout symbol '{symbol}' in {name}.");
            }
        }
    }

    if (!anchorFound)
        throw new InvalidOperationException($"Definition {name} is missing anchor cell 'A'.");

    if (connectorOffsets.Count == 0)
        throw new InvalidOperationException($"Definition {name} is missing road connector cells 'r'.");

    CellOffset[] footprint = new CellOffset[footprintOffsets.Count];
    for (int i = 0; i < footprintOffsets.Count; i++)
        footprint[i] = new CellOffset(footprintOffsets[i].X - anchorX, footprintOffsets[i].Y - anchorY);

    CellOffset[] connectors = new CellOffset[connectorOffsets.Count];
    for (int i = 0; i < connectorOffsets.Count; i++)
        connectors[i] = new CellOffset(connectorOffsets[i].X - anchorX, connectorOffsets[i].Y - anchorY);

    return new CompiledSpatialDefinition
    {
        DefinitionRowId = -1,
        Kind = kind,
        Name = name,
        Category = category,
        LayoutText = layoutText,
        Width = width,
        Height = height,
        AnchorX = anchorX,
        AnchorY = anchorY,
        FootprintOffsets = footprint,
        ConnectorOffsets = connectors,
        StorageWoodCapacity = storageWoodCapacity,
        StorageStoneCapacity = storageStoneCapacity,
        StorageFoodCapacity = storageFoodCapacity,
        PopulationCapacity = populationCapacity,
        BuildWoodCost = buildWoodCost,
        BuildStoneCost = buildStoneCost,
        BuildFoodCost = buildFoodCost,
        BuildWorkRequired = buildWorkRequired,
    };
}

(int x, int y) GetPrimaryConnectorWorldCell(CompiledSpatialDefinition definition, int anchorX, int anchorY)
{
    CellOffset primary = definition.ConnectorOffsets[0];
    return (anchorX + primary.X, anchorY + primary.Y);
}

static bool InBounds(int x, int y)
    => (uint)x < (uint)MapWidth && (uint)y < (uint)MapHeight;

static int DistanceSquared(int ax, int ay, int bx, int by)
{
    int dx = ax - bx;
    int dy = ay - by;
    return (dx * dx) + (dy * dy);
}

static Fix64 ToFix64(double value)
{
    int scaled = (int)Math.Round(value * 100.0, MidpointRounding.AwayFromZero);
    return Fix64.FromRatio(scaled, 100);
}

static double Clamp01(double value)
{
    if (value < 0.0) return 0.0;
    if (value > 1.0) return 1.0;
    return value;
}

void WriteSnapshot(DetSpatialDatabase snapshotDatabase, string directory, DetDbFrameRecord? frameRecord = null)
{
    string tickFile = Path.Combine(directory, $"tick-{snapshotDatabase.Tick:D4}.dmap");
    byte[] bytes = DetSnapshot.Serialize(snapshotDatabase, frameRecord);
    File.WriteAllBytes(tickFile, bytes);
    File.WriteAllBytes(Path.Combine(directory, "latest.dmap"), bytes);
}

void PrintSummary()
{
    string stateHash = database.ComputeStateHashHex();
    string commandText = lastApplyResult is null ? "cmds=seed" : $"cmds={lastApplyResult.CommandCount}";
    int activeSiteId = GetActiveConstructionSiteId();
    int unitCount = units.GetRowIds().Count();
    string siteSummary = activeSiteId < 0
        ? "site=All complete"
        : $"site={buildingName.Get(activeSiteId)} {GetBuildingStatusLabel(GetBuildingStatusCurrent(activeSiteId))} materials {GetBuildingWoodCurrent(activeSiteId)}/{buildingRequiredWood.Get(activeSiteId)}W {GetBuildingStoneCurrent(activeSiteId)}/{buildingRequiredStone.Get(activeSiteId)}S progress {GetBuildingProgressCurrent(activeSiteId)}/{buildingRequiredWork.Get(activeSiteId)}";
    Console.WriteLine(
        $"Tick {database.Tick:D4} | {commandText} | state={stateHash[..12]} | units={unitCount} | hall wood={buildingWood.Get(settlementHallId)} stone={buildingStone.Get(settlementHallId)} food={buildingFood.Get(settlementHallId)} | {siteSummary}");
    Console.WriteLine($"  player={playerSummary.Get(playerRowId)}");
    Console.WriteLine($"  director={directorSummary.Get(directorRowId)}");

    int shown = 0;
    foreach (int rowId in units.GetRowIds())
    {
        Console.WriteLine(
            $"  {unitName.Get(rowId),-14} role={GetRoleLabel((UnitRoleKind)unitRole.Get(rowId)),-10} " +
            $"state={(UnitStateKind)unitState.Get(rowId),-9} pos=({unitPosX.Get(rowId),2},{unitPosY.Get(rowId),2}) " +
            $"dest=({unitDestX.Get(rowId),2},{unitDestY.Get(rowId),2}) carry={GetResourceLabel((ResourceKind)unitCarryType.Get(rowId))}:{unitCarryAmount.Get(rowId),2} " +
            $"task={unitTask.Get(rowId)}");

        shown++;
        if (shown == 6)
            break;
    }
}

static string CreateOutputDirectory()
{
    string repoRoot = FindRepoRoot();
    string runName = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
    string outputDirectory = Path.Combine(repoRoot, "artifacts", "settlement-demo", runName);
    Directory.CreateDirectory(outputDirectory);
    return outputDirectory;
}

static string FindRepoRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "DetMap.sln")))
            return current.FullName;

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate DetMap.sln from the demo output directory.");
}

enum TerrainKind : byte
{
    Water = 0,
    Grass = 1,
    Forest = 2,
    Hill = 3,
    Mountain = 4,
}

enum ResourceKind : byte
{
    None = 0,
    Wood = 1,
    Stone = 2,
    Food = 3,
}

enum BuildingKind : byte
{
    SettlementHall = 1,
    House = 2,
    LoggingCamp = 3,
    HuntingLodge = 4,
}

enum UnitRoleKind : byte
{
    Idle = 0,
    Hauler = 1,
    Builder = 2,
    Woodcutter = 3,
    Hunter = 4,
}

enum BuildingStatusKind : byte
{
    Planned = 0,
    Delivering = 1,
    Building = 2,
    Complete = 3,
}

enum BuildOrderStatusKind : byte
{
    Planned = 0,
    Active = 1,
    Complete = 2,
}

enum UnitStateKind : byte
{
    Idle = 0,
    ToSource = 1,
    ToPickup = 2,
    ToDropoff = 3,
}

readonly struct CellOffset
{
    public CellOffset(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }
    public int Y { get; }
}

readonly struct PlacedBuildingSite
{
    public PlacedBuildingSite(int rowId, int anchorX, int anchorY, int primaryAccessX, int primaryAccessY)
    {
        RowId = rowId;
        AnchorX = anchorX;
        AnchorY = anchorY;
        PrimaryAccessX = primaryAccessX;
        PrimaryAccessY = primaryAccessY;
    }

    public int RowId { get; }
    public int AnchorX { get; }
    public int AnchorY { get; }
    public int PrimaryAccessX { get; }
    public int PrimaryAccessY { get; }
}

sealed class CompiledSpatialDefinition
{
    public int DefinitionRowId { get; set; }
    public BuildingKind Kind { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string LayoutText { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
    public int AnchorX { get; init; }
    public int AnchorY { get; init; }
    public CellOffset[] FootprintOffsets { get; init; } = Array.Empty<CellOffset>();
    public CellOffset[] ConnectorOffsets { get; init; } = Array.Empty<CellOffset>();
    public int StorageWoodCapacity { get; init; }
    public int StorageStoneCapacity { get; init; }
    public int StorageFoodCapacity { get; init; }
    public int PopulationCapacity { get; init; }
    public int BuildWoodCost { get; init; }
    public int BuildStoneCost { get; init; }
    public int BuildFoodCost { get; init; }
    public int BuildWorkRequired { get; init; }
}

struct DemoRandom
{
    private uint _state;

    public DemoRandom(uint seed)
    {
        _state = seed == 0 ? 1u : seed;
    }

    public int NextInt(int maxExclusive)
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return (int)(_state % (uint)maxExclusive);
    }
}

sealed class PerlinNoise2D
{
    private readonly int[] _perm = new int[512];

    public PerlinNoise2D(uint seed)
    {
        int[] source = new int[256];
        for (int i = 0; i < source.Length; i++)
            source[i] = i;

        var rng = new DemoRandom(seed == 0 ? 1u : seed);
        for (int i = source.Length - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (source[i], source[j]) = (source[j], source[i]);
        }

        for (int i = 0; i < 512; i++)
            _perm[i] = source[i & 255];
    }

    public double Sample(double x, double y)
    {
        int xi = FastFloor(x) & 255;
        int yi = FastFloor(y) & 255;
        double xf = x - Math.Floor(x);
        double yf = y - Math.Floor(y);

        double u = Fade(xf);
        double v = Fade(yf);

        int aa = _perm[_perm[xi] + yi];
        int ab = _perm[_perm[xi] + yi + 1];
        int ba = _perm[_perm[xi + 1] + yi];
        int bb = _perm[_perm[xi + 1] + yi + 1];

        double x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1.0, yf), u);
        double x2 = Lerp(Grad(ab, xf, yf - 1.0), Grad(bb, xf - 1.0, yf - 1.0), u);
        return (Lerp(x1, x2, v) + 1.0) * 0.5;
    }

    public double Fractal(double x, double y, int octaves, double persistence, double lacunarity)
    {
        double amplitude = 1.0;
        double frequency = 1.0;
        double total = 0.0;
        double normalization = 0.0;

        for (int i = 0; i < octaves; i++)
        {
            total += Sample(x * frequency, y * frequency) * amplitude;
            normalization += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return normalization == 0.0 ? 0.0 : total / normalization;
    }

    private static int FastFloor(double value)
        => value >= 0.0 ? (int)value : (int)value - 1;

    private static double Fade(double t)
        => t * t * t * (t * ((t * 6.0) - 15.0) + 10.0);

    private static double Lerp(double a, double b, double t)
        => a + ((b - a) * t);

    private static double Grad(int hash, double x, double y)
    {
        return (hash & 7) switch
        {
            0 => x + y,
            1 => -x + y,
            2 => x - y,
            3 => -x - y,
            4 => x,
            5 => -x,
            6 => y,
            _ => -y,
        };
    }
}
