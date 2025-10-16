# AMR Fleet Management System Documentation

## Overview
This document describes the architecture and implementation of a multi-robot Fleet Management System (FMS) that demonstrates complete end-to-end information flow using industry-standard protocols.

## Communication Planes

### 1. Vertical Communication (ISA-95)
Implements the ISA-95 standard for enterprise-control system integration:
- B2MML-compliant data structures
- Hierarchical organization (Site → Area → WorkCenter)
- Resource capability definitions
- Production scheduling
- Material requirements

#### Logging Example:
```json
{
    "plane": "vertical",
    "protocol": "ISA-95",
    "component": "ErpConnector",
    "orderId": "PO-001",
    "status": "TRANSLATING",
    "timestamp": "2025-10-16T14:30:00Z",
    "details": {
        "materialId": "HEAVY-PALLET-A",
        "priority": "HIGH",
        "processingTime": "150ms"
    }
}
```

### 2. Horizontal Communication (VDA 5050)
Implements the VDA 5050 protocol for AGV communication:
- Standardized message structure
- Order management
- Node-based navigation
- Action execution
- State reporting

#### Logging Example:
```json
{
    "plane": "horizontal",
    "protocol": "VDA 5050",
    "component": "DispatcherService",
    "amrId": "AMR-001",
    "messageType": "order",
    "status": "ACCEPTED",
    "timestamp": "2025-10-16T14:30:05Z",
    "payload": {
        "orderId": "PO-001",
        "nodes": ["start", "waypoint1", "end"]
    }
}
```

### 3. Internal Communication (MQTT)
Implements internal AMR command and control:
- QoS-based message delivery
- Command correlation
- State tracking
- Performance monitoring

#### Logging Example:
```json
{
    "plane": "internal",
    "protocol": "MQTT",
    "component": "AmrController",
    "amrId": "AMR-001",
    "topic": "amr/internal/navigation/command",
    "qosLevel": 2,
    "correlationId": "cmd-123",
    "timestamp": "2025-10-16T14:30:10Z",
    "payload": {
        "action": "moveTo",
        "coordinates": {"x": 10, "y": 20}
    }
}
```

## Performance Monitoring

### Metrics Tracked
1. Message Processing Times
   - Order translation time
   - Command execution time
   - Response latency

2. Resource Utilization
   - AMR battery levels
   - Queue depths
   - System load

3. Error Rates
   - Protocol violations
   - Command failures
   - Communication timeouts

### Example Metric Log:
```json
{
    "type": "metric",
    "component": "ErpConnector",
    "metric": "order_processing_time",
    "value": 150.5,
    "unit": "ms",
    "timestamp": "2025-10-16T14:30:15Z",
    "tags": {
        "order_count": "4",
        "success_count": "4"
    }
}
```

## Error Handling

### Protocol Errors
```json
{
    "plane": "vertical",
    "protocol": "ISA-95",
    "component": "ErpConnector",
    "errorType": "SKU_NOT_FOUND",
    "message": "SKU HEAVY-PALLET-X not found in database",
    "timestamp": "2025-10-16T14:30:20Z",
    "exception": {
        "message": "Invalid SKU identifier",
        "stackTrace": "..."
    }
}
```

## Best Practices

1. Always include correlation IDs for tracking message flows
2. Use appropriate QoS levels based on message importance
3. Include performance metrics in logs
4. Maintain proper protocol versioning
5. Log both successful and failed operations
6. Include relevant context in error logs
7. Use structured logging format for easy parsing
8. Track timing information for performance analysis

## Configuration

See `appsettings.json` for:
- SKU database
- AMR capabilities
- Protocol settings
- Logging levels
- Performance thresholds