# FMS Simulation Logging Strategy

## Communication Planes Logging

### 1. Vertical Communication (ISA-95)
- Order lifecycle events (created, scheduled, in-progress, completed)
- B2MML data transformations
- Resource allocation decisions
- Production schedule updates
- Error conditions and validation failures

### 2. Horizontal Communication (VDA 5050)
- Order state transitions
- Node execution status
- Edge traversal events
- Action execution status
- Protocol version compatibility
- Connection state changes
- Message acknowledgments

### 3. Internal Communication (MQTT)
- Command execution status
- QoS delivery confirmations
- Subsystem state changes
- Error conditions
- Performance metrics
- Resource utilization

## Log Levels Usage

- **TRACE**: Detailed protocol-level messages and data transformations
- **DEBUG**: State transitions and detailed execution flow
- **INFO**: Normal operational events and success confirmations
- **WARN**: Recoverable errors and degraded operation conditions
- **ERROR**: Unrecoverable errors and protocol violations
- **CRITICAL**: System-wide failures and safety-critical issues

## Structured Logging Format

Each log entry should include:
- Timestamp (UTC)
- Communication plane identifier
- Message correlation ID
- Source/destination identifiers
- Protocol version
- Operation type
- Payload summary
- Status code
- Performance metrics (where applicable)

## Performance Monitoring

Key metrics to log:
- Message latency
- Queue depths
- Processing times
- Resource utilization
- Battery levels
- Network performance
- Error rates