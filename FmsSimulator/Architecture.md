# System Architecture (High Level)# System architecture (high level)



This diagram shows the main components and tri-planar communication flow used in the simulation.This diagram shows the main components and data flow used in the simulation.



```mermaid```mermaid

graph TDgraph TD

        subgraph "Vertical Plane (ISA-95 ERP Integration)"        subgraph "Vertical Plane (ISA-95)"

                A[ERP Production Order] --> B(ErpConnectorService)                A[ERP issues Production Order] --> B(ErpConnectorService)

                B -- "Translates to ProductionTask" --> WM[WorkflowManager]        end

        end    

            subgraph "FMS Core"

        subgraph "FMS Core - Conscious Layer"                B -- "Translates Order to Task" --> C[FMS Task Queue]

                WM -- "Phase 1: Plan Generation" --> D(OptimizedPlanGenerator)                C --> D(PlanGenerator)

                D -- "Valid Plans" --> E(OptimizedMcdmEngine)                D -- "Generates Valid Plans" --> E(SimpleMcdmEngine)

                E -- "Phase 2: Decision Making<br/>MCDM Scoring" --> WM                E -- "Selects Best Plan" --> F(DispatcherService)

                        

                WM -- "Phase 3: Execution" --> F(CommunicationService)                %% --- Sequential flow for feedback ---

                F -- "VDA 5050 Orders" --> G[Task Execution]                G[Task Execution & Feedback] --> H(LearningService)

                                H -- "Updates Traffic Model" --> D

                G -- "Phase 4: Learning" --> H(LearningService)        end

                H -- "Updates World Model<br/>Traffic Costs" --> D

                        subgraph "Horizontal & Internal Planes (AMR Fleet)"

                WM -.State Tracking.-> DB[(WorkflowState<br/>Metrics)]                 F -- "Dispatches VDA 5050 Order" --> I(AmrInternalController)

        end                 I -- "Executes Internal Commands" --> G

        end

        subgraph "Horizontal & Internal Planes (AMR Fleet)"

                F -- "Publishes VDA 5050" --> I(InternalAmrController)        %% --- Styling ---

                I -- "Navigation/Lift/Arm Commands" --> G        classDef service fill:#f9f,stroke:#333,stroke-width:2px;

                G -- "Telemetry & Feedback" --> WM        class B,D,E,F,H,I service;

        end```



        %% --- Styling ---Notes

        classDef service fill:#e1f5ff,stroke:#0066cc,stroke-width:2px;- Key files:

        classDef conscious fill:#fff4e6,stroke:#ff9800,stroke-width:3px;    - `Services/PlanGenerator.cs` — plan candidate generation and hard pruning.

        classDef data fill:#f3e5f5,stroke:#9c27b0,stroke-width:2px;    - `Services/SimpleMcdmEngine.cs` — scoring (time, suitability, battery) and selection logic.

            - `Services/LearningService.cs` — simple feedback loop that updates traffic costs via `PlanGenerator.UpdateTrafficCost`.

        class B,D,F,H,I service;    - `Services/JulesMqttClient.cs` / `Services/DispatcherService.cs` — publish VDA 5050 orders to AMRs.

        class WM,E conscious;    - `Services/InternalAmrController.cs` — translates VDA 5050 into internal AMR commands using `Models/InternalAmrCommand.cs`.

        class DB data;

```Keep the diagram and notes in sync with code: if you rename classes, update this file.


## Architecture Overview

### **Tri-Planar Communication Model**

1. **Vertical Plane (ISA-95 ERP ↔ FMS)**
   - ERP systems issue production orders
   - `ErpConnectorService` translates orders into FMS tasks
   - Bidirectional communication for order status and completion

2. **Horizontal Plane (FMS ↔ AMR Fleet)**
   - `CommunicationService` publishes VDA 5050 orders
   - AMRs report status, position, and telemetry
   - Fleet-wide coordination and task distribution

3. **Internal Plane (Intra-AMR Communication)**
   - `InternalAmrController` manages subsystem commands
   - Navigation, lifting, and arm control commands
   - Real-time command execution and feedback

### **Conscious Layer (Decision-Making & Learning)**

The FMS implements a conscious layer through:
- **OptimizedMcdmEngine**: Multi-Criteria Decision Making with weighted scoring
  - Time optimization with exponential decay
  - Non-linear resource utilization
  - Dynamic weight adjustment based on fleet state
- **LearningService**: Continuous improvement through feedback
  - Updates world model with actual execution times
  - Adjusts traffic cost estimates
  - Improves future plan generation accuracy

### **WorkflowManager: Orchestration Hub**

The `WorkflowManager` coordinates all workflow phases:
1. **PLANNING**: Generate and evaluate candidate plans
2. **EXECUTING**: Dispatch orders and monitor execution
3. **LEARNING**: Update models with actual performance data
4. **State Tracking**: Concurrent workflow monitoring with metrics

## Key Components

### Services

- **`ErpConnectorService`** - Fetches and translates ERP orders into FMS tasks
- **`OptimizedPlanGenerator`** - Generates valid assignment plans with hard constraint pruning
- **`OptimizedMcdmEngine`** - Advanced MCDM scoring with exponential time penalties and non-linear resource modeling
- **`CommunicationService`** - Handles VDA 5050 communication and internal AMR commands
- **`LearningService`** - Feedback loop that updates traffic costs and improves predictions
- **`InternalAmrController`** - Translates VDA 5050 into subsystem-specific commands
- **`WorkflowManager`** - Orchestrates end-to-end task execution with state tracking
- **`LoggingService`** - Centralized operational metrics and structured logging

### Models

- **`ProductionTask`** - FMS-internal representation of work to be done
- **`AssignmentPlan`** - A candidate plan assigning an AMR to a task
- **`AmrState`** - Current state and capabilities of an AMR
- **`WorkflowState`** - Tracks task execution state, transitions, and metrics
- **`VDA5050/*`** - Standard AMR communication protocol models
- **`ISA95/*`** - Manufacturing operations management models

## Design Principles

1. **Separation of Concerns**: Each service has a single, well-defined responsibility
2. **Dependency Injection**: All services use constructor injection for testability
3. **Async/Await**: Non-blocking I/O for concurrent task processing
4. **Metrics-Driven**: Comprehensive logging of operational metrics
5. **Feedback Loops**: Continuous learning from execution results

---

**Note**: Keep this diagram and documentation in sync with code changes. When renaming or refactoring classes, update this file accordingly.
