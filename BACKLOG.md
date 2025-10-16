# FMS Simulator - Product Backlog
**Last Updated:** October 16, 2025  
**Status:** Active Development  
**Current Phase:** Phase 1 Complete (MQTT Infrastructure)

---

## Overview

This document tracks future enhancements, technical debt items, and planned features for the FMS Simulator project. Items are prioritized using MoSCoW method (Must have, Should have, Could have, Won't have).

---

## Phase 2: Production Planning & Scheduling (Planned)

### Feature: MapService Implementation
**Priority:** 🔴 MUST HAVE (for Phase 2)  
**Effort:** 4 hours  
**Status:** Not Started  
**Target:** Phase 2, Sprint 1

#### Description
Create a MapService to manage warehouse layout, locations, and coordinate mapping. Required for converting location names (e.g., "Dock A", "Assembly B") into physical coordinates for VDA 5050 navigation.

#### Requirements
1. **Location Database**
   - Store location name → (X, Y, Theta) mapping
   - Support multiple map layers (warehouse_map, outdoor_map, etc.)
   - Include location metadata (type, capacity, access restrictions)

2. **Coordinate Resolution**
   - `GetCoordinates(locationName, mapId)` → (X, Y, Theta)
   - `GetNearestLocation(X, Y, mapId)` → locationName
   - `ValidateCoordinate(X, Y, mapId)` → bool (within bounds)

3. **Configuration**
   - Load from JSON/YAML configuration file
   - Support dynamic updates (no restart required)
   - Integrate with FMS settings

4. **Integration Points**
   - `Vda5050PublisherService` - resolve location names in `BuildOrderFromAssignmentPlan()`
   - `PlanGeneratorService` - calculate distances for route optimization
   - `McdmEngine` - use actual distances in decision-making

#### Files to Update
- ✅ Create: `Services/MapService.cs`
- ✅ Create: `Models/LocationDefinition.cs`
- ✅ Create: `appsettings.json` - add warehouse map configuration
- ✅ Update: `Services/MQTT/Vda5050PublisherService.cs` - replace hardcoded (0.0, 0.0) with MapService lookups
- ✅ Update: `Program.cs` - register MapService in DI container

#### Current Workaround
Vda5050PublisherService uses placeholder coordinates (0.0, 0.0) for all locations. AGVs must have their own location database or use only NodeIds (location names) for navigation.

```csharp
// Current implementation (placeholder)
NodePosition = new NodePosition
{
    X = 0.0, // Placeholder - MapService not yet implemented
    Y = 0.0, // Placeholder - MapService not yet implemented
    Theta = 0.0,
    MapId = "warehouse_map",
    AllowedDeviationXy = 0.5,
    AllowedDeviationTheta = 0.1
}
```

#### Acceptance Criteria
- [ ] MapService can resolve all location names used in tests
- [ ] Configuration loaded from appsettings.json
- [ ] VDA 5050 orders contain accurate coordinates
- [ ] Unit tests for coordinate resolution
- [ ] Integration tests with real location database
- [ ] Documentation in README.md

#### Related Issues
- Vda5050PublisherService.cs:326 (start node coordinates)
- Vda5050PublisherService.cs:369 (end node coordinates)
- LSS Validation Report - "Resolve TODOs" action item

---

## Phase 1.5: MQTT Infrastructure Enhancements (Optional)

### Feature: Automatic Message Retry Queue
**Priority:** 🟡 SHOULD HAVE  
**Effort:** 2 hours  
**Status:** Not Started  
**Target:** Phase 1.5 or Phase 2

#### Description
Enhance MqttHighAvailabilityOrchestrator to automatically retry persisted messages when connection is restored. Currently, messages are loaded but not automatically queued for retry.

#### Requirements
1. **Integration with MqttClientService**
   - Add method: `MqttClientService.QueuePendingMessage(message)`
   - Process pending queue on connection restore
   - Respect QoS settings and priority

2. **Retry Logic**
   - Exponential backoff (1s, 2s, 4s, 8s, max 60s)
   - Max retry attempts: 5 (configurable)
   - Log failed messages after max retries
   - Alert on persistent failures

3. **Monitoring**
   - Track retry success/failure rates
   - Log metrics to LoggingService
   - Expose via health monitoring endpoint

#### Files to Update
- ✅ Update: `Services/MqttHighAvailabilityOrchestrator.cs` - integrate retry queue (line 76)
- ✅ Update: `Services/MqttClientService.cs` - add `QueuePendingMessage()` method
- ✅ Update: `Services/MqttHealthMonitor.cs` - track retry metrics
- ✅ Add tests: Integration test for message retry

#### Current Workaround
Persisted messages are loaded and logged, but not automatically queued for retry. Manual intervention or application restart required to retry failed messages.

```csharp
// Current implementation (load but don't retry)
if (persistedMessages.Count > 0)
{
    _logger.LogInformation(
        "Loaded {Count} persisted messages. Will retry publishing after connection.",
        persistedMessages.Count);

    // NOTE: Messages loaded but not yet auto-queued for retry
    // Future enhancement: integrate with MqttClientService pending queue
}
```

#### Acceptance Criteria
- [ ] Persisted messages automatically queued on reconnection
- [ ] Retry logic with exponential backoff implemented
- [ ] Metrics tracked (retry success/failure rates)
- [ ] Integration test validates retry behavior
- [ ] Configuration options in appsettings.json

#### Related Issues
- MqttHighAvailabilityOrchestrator.cs:76 (pending queue integration)
- LSS Validation Report - "TODO items" action item

---

## Phase 3: Warehouse Management Integration (Planned)

### Feature: Real-time Inventory Tracking
**Priority:** 🟡 SHOULD HAVE  
**Effort:** 8 hours  
**Status:** Not Started  
**Target:** Phase 3

#### Description
Integrate with warehouse management system (WMS) to track inventory levels, bin locations, and material availability in real-time.

*(Details to be added in Phase 3 planning)*

---

## Phase 4: Advanced Analytics & Reporting (Planned)

### Feature: Performance Dashboard
**Priority:** 🟢 COULD HAVE  
**Effort:** 6 hours  
**Status:** Not Started  
**Target:** Phase 4

#### Description
Web-based dashboard to visualize FMS performance metrics, AGV utilization, throughput, and system health.

*(Details to be added in Phase 4 planning)*

---

## Technical Debt (From LSS Validation)

### ✅ COMPLETED (Sprint 1 - Option A + Current)
- [x] Fix build warnings (CS7022, CS1998)
- [x] Add .editorconfig for consistent coding standards
- [x] Reorganize VDA5050 files to Services/MQTT/
- [x] Remove duplicate ISA95/ProductionOrder.cs model
- [x] Document TODOs properly in backlog (this file)

### 🔄 IN PROGRESS (Sprint 1 - Ongoing)
- [ ] Review dependency injection configuration
- [ ] Analyze code organization improvements
- [ ] Create Sprint 1 completion report

### ⏳ PLANNED (Future Sprints)
- [ ] Increase test coverage from 60% → 85%
- [ ] Implement MapService (resolve coordinate TODOs)
- [ ] Add automatic message retry queue
- [ ] Remove hardcoded values (replace with configuration)
- [ ] Refactor McdmEngine for pluggable strategies
- [ ] Add performance profiling instrumentation

---

## Code Quality Improvements

### Refactoring Candidates
**Priority:** 🟢 COULD HAVE  
**Effort:** 4-8 hours each  
**Target:** Phase 2+

1. **Strategy Pattern for MCDM**
   - Replace hardcoded weights with configurable strategy
   - Support multiple decision algorithms (AHP, TOPSIS, SAW)
   - Enable runtime algorithm switching

2. **Repository Pattern for Data Access**
   - Abstract ERP connector behind repository interface
   - Support multiple ERP systems (SAP, Oracle, custom)
   - Mock repositories for testing

3. **Message Queue Abstraction**
   - Abstract MQTT behind generic message bus interface
   - Support additional protocols (AMQP, Kafka, Azure Service Bus)
   - Enable protocol switching without code changes

---

## Configuration Enhancements

### Externalize Hardcoded Values
**Priority:** 🟡 SHOULD HAVE  
**Effort:** 2 hours  
**Target:** Phase 2

#### Current Hardcoded Values
1. **MQTT Topics** (Vda5050TopicSettings) - ✅ Already configurable
2. **Health Check Interval** (30s) - ⚠️ Hardcoded in MqttHealthMonitor
3. **Circuit Breaker Thresholds** (5 failures) - ⚠️ Hardcoded in MqttCircuitBreaker
4. **Retry Settings** (max attempts, delays) - ⚠️ Hardcoded in various services
5. **Map IDs** ("warehouse_map") - ⚠️ Hardcoded in Vda5050PublisherService
6. **Coordinate Deviations** (0.5m, 0.1 rad) - ⚠️ Hardcoded in Vda5050PublisherService

#### Proposed Solution
Add configuration section to appsettings.json:

```json
{
  "MqttSettings": {
    "HealthCheckIntervalSeconds": 30,
    "CircuitBreakerFailureThreshold": 5,
    "CircuitBreakerTimeoutSeconds": 60,
    "MaxRetryAttempts": 5,
    "RetryDelaySeconds": [1, 2, 4, 8, 16]
  },
  "Vda5050Settings": {
    "DefaultMapId": "warehouse_map",
    "AllowedDeviationXy": 0.5,
    "AllowedDeviationTheta": 0.1
  }
}
```

---

## Documentation Improvements

### High Priority
- [ ] Create ARCHITECTURE.md (system design overview)
- [ ] Create DEPLOYMENT.md (production deployment guide)
- [ ] Create CONTRIBUTING.md (developer guidelines)
- [ ] Add XML documentation comments to public APIs
- [ ] Create sequence diagrams for key workflows

### Medium Priority
- [ ] API documentation (Swagger/OpenAPI if REST added)
- [ ] Performance tuning guide
- [ ] Troubleshooting guide
- [ ] Metrics and monitoring setup

---

## Testing Improvements

### Unit Test Coverage
**Current:** 60%  
**Target:** 85%  
**Effort:** 10 hours

#### Areas Needing Coverage
1. **VDA 5050 Models** (Vda5050Models.cs)
   - Serialization/deserialization tests
   - Required field validation
   - Edge cases (null, empty arrays)

2. **Publisher/Subscriber Services**
   - Mock MQTT client tests
   - Error handling tests
   - Edge case scenarios

3. **Learning Service**
   - World model update logic
   - Edge cases (negative values, extreme inputs)

4. **MCDM Engine**
   - Individual criterion calculations
   - Weight normalization
   - Tie-breaking logic

### Integration Test Enhancements
- [ ] Add chaos testing (network failures, slow responses)
- [ ] Add load testing (concurrent operations)
- [ ] Add end-to-end workflow tests
- [ ] Add compatibility tests (different AGV implementations)

---

## Performance Optimization

### Profiling Targets
**Priority:** 🟢 COULD HAVE  
**Effort:** 4 hours  
**Target:** Phase 3+

1. **MQTT Message Throughput**
   - Baseline: Unknown (needs profiling)
   - Target: 1000 msg/sec

2. **Decision-Making Latency**
   - Baseline: Unknown (needs profiling)
   - Target: <100ms for plan selection

3. **Memory Usage**
   - Baseline: Unknown (needs profiling)
   - Target: <500MB for typical workload

---

## Security Enhancements

### Future Considerations
**Priority:** 🔴 MUST HAVE (for production)  
**Effort:** 8 hours  
**Target:** Before production deployment

1. **MQTT Authentication**
   - Username/password authentication
   - TLS/SSL encryption
   - Certificate-based auth

2. **Authorization**
   - Topic-level ACLs
   - Role-based access control
   - AGV identity verification

3. **Audit Logging**
   - Track all commands sent to AGVs
   - Log configuration changes
   - Security event monitoring

---

## Notes

### Prioritization Guidelines
- 🔴 **MUST HAVE**: Blocking for next phase, high business value
- 🟡 **SHOULD HAVE**: Important but can be deferred
- 🟢 **COULD HAVE**: Nice to have, low priority
- ⚪ **WON'T HAVE**: Explicitly out of scope for now

### Effort Estimates
- Small: 1-2 hours
- Medium: 3-4 hours
- Large: 5-8 hours
- X-Large: 9+ hours

### Status Definitions
- **Not Started**: Item in backlog, not yet begun
- **In Progress**: Active development
- **Blocked**: Waiting on dependency or decision
- **Complete**: Implemented, tested, documented

---

**Maintained By:** FMS Development Team  
**Review Frequency:** Bi-weekly sprint planning  
**Next Review:** October 30, 2025
