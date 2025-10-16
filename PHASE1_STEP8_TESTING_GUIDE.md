# Phase 1 Step 8: MQTT Broker Testing Guide

**Date:** 2025-01-24  
**Status:** 🧪 Testing in Progress

---

## 🎯 Objective

Deploy a real MQTT broker and validate all VDA 5050 and high-availability functionality with comprehensive integration tests.

---

## 📋 Prerequisites

### Option A: Docker (Recommended)
- **Docker Desktop** installed and running
- **Docker Compose** available

### Option B: Windows Native Mosquitto
- Download from: https://mosquitto.org/download/
- Install to: `C:\Program Files\mosquitto`

### Option C: HiveMQ Community Edition
- Download from: https://www.hivemq.com/downloads/
- Java 11+ required

---

## 🚀 Quick Start - Deploy MQTT Broker

### Option A: Docker Compose (Easiest)

```powershell
# Navigate to project root
cd C:\Users\dedmti.intern\FmsSimulation

# Start Mosquitto broker
docker-compose -f docker-compose.mqtt.yml up -d

# Verify broker is running
docker ps

# Check logs
docker logs fms-mqtt-broker

# Test connection with mosquitto_sub (if installed)
docker exec -it fms-mqtt-broker mosquitto_sub -t "test/#" -v
```

**Broker Details:**
- **Host:** localhost
- **Port:** 1883
- **WebSocket Port:** 9001
- **Web UI:** http://localhost:4000 (MQTT Explorer)

### Option B: Windows Native Mosquitto

```powershell
# Start Mosquitto with custom config
cd C:\Users\dedmti.intern\FmsSimulation
mosquitto -c mosquitto\config\mosquitto.conf

# Or use default config
mosquitto -v
```

### Option C: Docker Run (Manual)

```powershell
docker run -d --name fms-mqtt-broker -p 1883:1883 eclipse-mosquitto:2.0
```

---

## 🧪 Run Integration Tests

### Method 1: Using Test Harness (Automated)

Create a test program in `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FmsSimulator;
using FmsSimulator.Models;
using FmsSimulator.Services;

var serviceProvider = BuildServiceProvider();

var testHarness = serviceProvider.GetRequiredService<MqttIntegrationTestHarness>();

// Start HA orchestrator
var haOrchestrator = serviceProvider.GetRequiredService<MqttHighAvailabilityOrchestrator>();
await haOrchestrator.StartAsync();

// Run all tests
var summary = await testHarness.RunAllTestsAsync();

// Stop HA orchestrator
await haOrchestrator.StopAsync();

Console.WriteLine($"\nTest Summary: {summary.TestsPassed}/{summary.TotalTests} passed");

static IServiceProvider BuildServiceProvider()
{
    var services = new ServiceCollection();

    // Logging
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });

    // Configuration
    services.Configure<MqttBrokerSettings>(options =>
    {
        options.BrokerHost = "localhost";
        options.BrokerPort = 1883;
        options.AutoReconnect = true;
        options.DefaultQoS = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce;
    });

    services.Configure<Vda5050TopicSettings>(options =>
    {
        options.BaseTopicPrefix = "vda5050/v2";
        options.Manufacturer = "FMS";
    });

    services.Configure<MqttHighAvailabilitySettings>(options =>
    {
        options.EnableMessagePersistence = true;
        options.EnableCircuitBreaker = true;
        options.CircuitBreakerThreshold = 5;
    });

    services.Configure<MqttSettings>(options =>
    {
        options.MqttBroker = new MqttBrokerSettings
        {
            BrokerHost = "localhost",
            BrokerPort = 1883,
            AutoReconnect = true
        };
        options.Vda5050Topics = new Vda5050TopicSettings
        {
            BaseTopicPrefix = "vda5050/v2",
            Manufacturer = "FMS"
        };
        options.MqttHighAvailability = new MqttHighAvailabilitySettings
        {
            EnableMessagePersistence = true,
            EnableCircuitBreaker = true
        };
    });

    // MQTT Services
    services.AddSingleton<MqttClientService>();
    services.AddSingleton<MqttHealthMonitor>();
    services.AddSingleton<MqttMessagePersistenceService>();
    services.AddSingleton<MqttHighAvailabilityOrchestrator>();
    services.AddSingleton<Vda5050PublisherService>();
    services.AddSingleton<Vda5050SubscriberService>();

    // Test Harness
    services.AddSingleton<MqttIntegrationTestHarness>();

    return services.BuildServiceProvider();
}
```

**Run:**
```powershell
cd C:\Users\dedmti.intern\FmsSimulation\FmsSimulator
dotnet run
```

### Method 2: Manual Testing with CLI Tools

#### Install Mosquitto CLI Tools

```powershell
# Windows: Install from https://mosquitto.org/download/
# Or use Docker:
docker exec -it fms-mqtt-broker /bin/sh
```

#### Test 1: Subscribe to VDA 5050 Topics

```bash
# Subscribe to all VDA 5050 orders
mosquitto_sub -h localhost -p 1883 -t "vda5050/v2/FMS/+/order" -v

# Subscribe to all state messages
mosquitto_sub -h localhost -p 1883 -t "vda5050/v2/FMS/+/state" -v

# Subscribe to all topics (wildcard)
mosquitto_sub -h localhost -p 1883 -t "vda5050/#" -v
```

#### Test 2: Publish Test Order

```bash
mosquitto_pub -h localhost -p 1883 \
  -t "vda5050/v2/FMS/AGV_001/order" \
  -m '{
    "headerId": 1,
    "timestamp": "2025-01-24T10:00:00Z",
    "version": "2.0.0",
    "manufacturer": "FMS",
    "serialNumber": "AGV_001",
    "orderId": "test_order_001",
    "orderUpdateId": 0,
    "nodes": [
      {
        "nodeId": "NODE_1",
        "sequenceId": 0,
        "released": true,
        "nodePosition": {
          "x": 10.0,
          "y": 20.0,
          "theta": 0.0,
          "mapId": "warehouse"
        }
      }
    ],
    "edges": []
  }'
```

#### Test 3: Publish Test State

```bash
mosquitto_pub -h localhost -p 1883 \
  -t "vda5050/v2/FMS/AGV_001/state" \
  -m '{
    "headerId": 1,
    "timestamp": "2025-01-24T10:00:00Z",
    "version": "2.0.0",
    "manufacturer": "FMS",
    "serialNumber": "AGV_001",
    "orderId": "test_order_001",
    "orderUpdateId": 0,
    "lastNodeId": "NODE_1",
    "lastNodeSequenceId": 0,
    "driving": false,
    "batteryState": {
      "batteryCharge": 85.5,
      "charging": false
    }
  }'
```

---

## 📊 Test Scenarios

### Test 1: Basic Connectivity ✅
**Objective:** Verify FMS can connect to MQTT broker.

**Steps:**
1. Start Mosquitto broker
2. Run FMS application
3. Check connection logs

**Expected Result:**
- `MqttClientService` logs: "Successfully connected to MQTT broker"
- `IsConnected` property returns `true`

**Verification:**
```powershell
# Check mosquitto logs
docker logs fms-mqtt-broker | Select-String "New client connected"
```

---

### Test 2: Publish VDA 5050 Order ✅
**Objective:** Verify FMS can publish VDA 5050 orders to AGVs.

**Steps:**
1. Subscribe to order topic with `mosquitto_sub`
2. Call `Vda5050PublisherService.PublishOrderAsync()`
3. Verify message received

**Expected Result:**
- Order message published to `vda5050/v2/FMS/AGV_001/order`
- JSON structure matches VDA 5050 v2.0 specification
- Contains nodes, edges, actions

**Verification:**
```bash
mosquitto_sub -h localhost -p 1883 -t "vda5050/v2/FMS/+/order" -v
```

---

### Test 3: Subscribe to AGV State ✅
**Objective:** Verify FMS can receive state updates from AGVs.

**Steps:**
1. Call `Vda5050SubscriberService.SubscribeToAgvAsync("AGV_001")`
2. Publish test state message with `mosquitto_pub`
3. Verify `StateReceived` event is raised

**Expected Result:**
- Subscription successful to `vda5050/v2/FMS/AGV_001/state`
- State message deserialized correctly
- Event handler receives `StateMessageReceivedEventArgs`

**Verification:**
```bash
mosquitto_pub -h localhost -p 1883 -t "vda5050/v2/FMS/AGV_001/state" -m '{"orderId":"test","lastNodeId":"NODE_1",...}'
```

---

### Test 4: Auto-Reconnection ✅
**Objective:** Verify automatic reconnection after broker failure.

**Steps:**
1. Connect FMS to broker
2. Stop broker: `docker stop fms-mqtt-broker`
3. Wait for disconnection log
4. Restart broker: `docker start fms-mqtt-broker`
5. Verify reconnection within 5-10 seconds

**Expected Result:**
- Disconnection detected
- Reconnection attempts logged every 5 seconds
- Connection restored automatically
- Pending messages republished

**Logs to Watch:**
```
Disconnected from MQTT broker
Attempting to reconnect (attempt 1)...
Successfully connected to MQTT broker
Processing 5 pending messages...
```

---

### Test 5: Message Persistence ✅
**Objective:** Verify messages are persisted during broker outage.

**Steps:**
1. Stop broker
2. Attempt to publish order
3. Verify message saved to `./mqtt_persistence`
4. Restart broker
5. Verify message is republished

**Expected Result:**
- File created: `pending_messages_20250124_HHMMSS_{guid}.json`
- Contains persisted message
- Message republished on reconnection
- File deleted after successful publish

**Verification:**
```powershell
# Check persistence directory
ls .\mqtt_persistence\

# View persisted message
cat .\mqtt_persistence\pending_messages_*.json
```

---

### Test 6: Health Monitoring ✅
**Objective:** Verify health checks detect connection issues.

**Steps:**
1. Start health monitor
2. Wait 60 seconds
3. Check health statistics

**Expected Result:**
- Health checks run every 30 seconds
- Latency measured (< 1000ms = healthy)
- Success rate tracked
- Metrics available via `GetHealthStatistics()`

**Verification:**
```csharp
var stats = healthMonitor.GetHealthStatistics();
Console.WriteLine($"Success Rate: {stats.SuccessRate}%");
Console.WriteLine($"Avg Latency: {stats.AverageLatencyMs}ms");
```

---

### Test 7: Circuit Breaker ✅
**Objective:** Verify circuit breaker blocks operations during failures.

**Steps:**
1. Stop broker
2. Wait for 5 consecutive health check failures (~2.5 minutes)
3. Verify circuit breaker opens
4. Attempt to publish (should fail)
5. Restart broker
6. Wait for circuit breaker to close

**Expected Result:**
- Circuit state: `CLOSED` → `OPEN` (after 5 failures)
- Operations blocked when `OPEN`
- `OPEN` → `HALF_OPEN` (after 60s timeout)
- `HALF_OPEN` → `CLOSED` (after successful health check)

**State Transitions:**
```
CLOSED (Normal)
  ↓ [5 failures]
OPEN (Blocked)
  ↓ [60s timeout]
HALF_OPEN (Testing)
  ↓ [success]
CLOSED (Recovered)
```

---

### Test 8: High-Availability Status ✅
**Objective:** Verify HA orchestrator provides comprehensive status.

**Steps:**
1. Call `MqttHighAvailabilityOrchestrator.GetStatusAsync()`
2. Verify all metrics present

**Expected Result:**
```json
{
  "isOperational": true,
  "isConnected": true,
  "circuitState": "Closed",
  "healthStatistics": {
    "totalHealthChecks": 10,
    "successfulHealthChecks": 10,
    "successRate": 100.0,
    "averageLatencyMs": 5
  },
  "persistenceStatistics": {
    "fileCount": 0,
    "totalSizeMB": 0.0
  }
}
```

---

## 🔍 Validation Checklist

### VDA 5050 Protocol Validation

- [ ] **Order Message Structure**
  - [ ] Contains `orderId`, `orderUpdateId`
  - [ ] Nodes have even `sequenceId` (0, 2, 4...)
  - [ ] Edges have odd `sequenceId` (1, 3, 5...)
  - [ ] Actions have valid `blockingType` (NONE, SOFT, HARD)
  - [ ] NodePosition has X, Y, Theta, MapId

- [ ] **State Message Structure**
  - [ ] Contains `orderId`, `lastNodeId`, `driving`
  - [ ] BatteryState has `batteryCharge`, `charging`
  - [ ] Errors array (if present) has valid `errorLevel`
  - [ ] ActionStates have valid `actionStatus`

- [ ] **Topic Structure**
  - [ ] Base prefix: `vda5050/v2`
  - [ ] Format: `vda5050/v2/{manufacturer}/{agvId}/{topic}`
  - [ ] Valid topics: order, instantActions, state, visualization, connection

- [ ] **JSON Serialization**
  - [ ] camelCase naming convention
  - [ ] ISO 8601 timestamps
  - [ ] Nullable fields omitted when null
  - [ ] Enums as strings (not integers)

### Performance Validation

- [ ] **Latency**
  - [ ] Connection time < 5 seconds
  - [ ] Publish latency < 100ms
  - [ ] Health check latency < 1000ms

- [ ] **Throughput**
  - [ ] Can publish 100 messages/second
  - [ ] Can handle 1000 pending messages

- [ ] **Reliability**
  - [ ] Auto-reconnect within 10 seconds
  - [ ] Message persistence prevents data loss
  - [ ] Circuit breaker prevents cascade failures

---

## 📈 Expected Results

### Test Summary
```
=================================================
MQTT INTEGRATION TEST HARNESS
=================================================

>>> Test 1: Basic Connectivity
✅ PASSED: Test 1: Basic Connectivity

>>> Test 2: Publish VDA 5050 Order
✅ PASSED: Test 2: Publish VDA 5050 Order

>>> Test 3: Subscribe to AGV State
✅ PASSED: Test 3: Subscribe to AGV State

>>> Test 4: Instant Actions (Emergency Stop)
✅ PASSED: Test 4: Instant Actions (Emergency Stop)

>>> Test 5: Health Monitoring
✅ PASSED: Test 5: Health Monitoring

>>> Test 6: Circuit Breaker Simulation
✅ PASSED: Test 6: Circuit Breaker Simulation

>>> Test 7: Message Persistence
✅ PASSED: Test 7: Message Persistence

>>> Test 8: High-Availability Status
✅ PASSED: Test 8: High-Availability Status

=================================================
TEST SUMMARY
=================================================
Total Tests: 8
Passed: 8 ✅
Failed: 0 ❌
Success Rate: 100.0%
Duration: 15.32s
=================================================
```

---

## 🛠️ Troubleshooting

### Issue: Connection Refused
**Symptom:** `MqttClientConnectException: Connection refused`

**Solutions:**
```powershell
# Check if broker is running
docker ps | Select-String "mosquitto"

# Check broker logs
docker logs fms-mqtt-broker

# Test broker with CLI
mosquitto_sub -h localhost -p 1883 -t "test" -v

# Restart broker
docker restart fms-mqtt-broker
```

### Issue: Messages Not Received
**Symptom:** Subscriptions work but no messages received

**Solutions:**
- Verify topic structure matches exactly
- Check QoS settings (use AtLeastOnce)
- Ensure AGV simulator is publishing to correct topics
- Use MQTT Explorer to monitor all traffic

### Issue: Circuit Breaker Stuck Open
**Symptom:** Operations blocked even after broker recovery

**Solutions:**
- Wait for timeout (60 seconds default)
- Check health check interval (30 seconds)
- Verify health check can publish test messages
- Restart health monitor

### Issue: Persistence Files Growing
**Symptom:** `./mqtt_persistence` directory growing in size

**Solutions:**
```powershell
# Cleanup old files manually
Remove-Item .\mqtt_persistence\*.json -Force

# Or call cleanup method
await persistenceService.CleanupOldFilesAsync();
```

---

## 📚 Tools & Resources

### MQTT Clients
- **MQTT Explorer:** http://mqtt-explorer.com/ (GUI)
- **Mosquitto CLI:** https://mosquitto.org/download/ (Command-line)
- **MQTT.fx:** https://mqttfx.jensd.de/ (GUI)

### VDA 5050 Resources
- **Specification:** https://github.com/VDA5050/VDA5050
- **Validator:** https://vda5050.github.io/VDA5050-Validator/

### Docker Commands
```powershell
# Start broker
docker-compose -f docker-compose.mqtt.yml up -d

# Stop broker
docker-compose -f docker-compose.mqtt.yml down

# View logs
docker logs -f fms-mqtt-broker

# Execute commands in container
docker exec -it fms-mqtt-broker mosquitto_sub -t "#" -v
```

---

## ✅ Step 8 Completion Criteria

- [x] MQTT broker deployed and accessible
- [ ] All 8 integration tests pass
- [ ] VDA 5050 message structure validated
- [ ] Auto-reconnection verified
- [ ] Message persistence verified
- [ ] Circuit breaker tested
- [ ] Health monitoring validated
- [ ] Performance metrics collected

---

**End of Testing Guide**
