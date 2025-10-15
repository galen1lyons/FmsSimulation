# System architecture (high level)

This diagram shows the main components and data flow used in the simulation.

```mermaid
graph TD
        subgraph "Vertical Plane (ISA-95)"
                A[ERP issues Production Order] --> B(ErpConnectorService)
        end
    
        subgraph "FMS Core"
                B -- "Translates Order to Task" --> C[FMS Task Queue]
                C --> D(PlanGenerator)
                D -- "Generates Valid Plans" --> E(SimpleMcdmEngine)
                E -- "Selects Best Plan" --> F(DispatcherService)
        
                %% --- Sequential flow for feedback ---
                G[Task Execution & Feedback] --> H(LearningService)
                H -- "Updates Traffic Model" --> D
        end

        subgraph "Horizontal & Internal Planes (AMR Fleet)"
                 F -- "Dispatches VDA 5050 Order" --> I(AmrInternalController)
                 I -- "Executes Internal Commands" --> G
        end

        %% --- Styling ---
        classDef service fill:#f9f,stroke:#333,stroke-width:2px;
        class B,D,E,F,H,I service;
```

Notes
- Key files:
    - `Services/PlanGenerator.cs` — plan candidate generation and hard pruning.
    - `Services/SimpleMcdmEngine.cs` — scoring (time, suitability, battery) and selection logic.
    - `Services/LearningService.cs` — simple feedback loop that updates traffic costs via `PlanGenerator.UpdateTrafficCost`.
    - `Services/JulesMqttClient.cs` / `Services/DispatcherService.cs` — publish VDA 5050 orders to AMRs.
    - `Services/InternalAmrController.cs` — translates VDA 5050 into internal AMR commands using `Models/InternalAmrCommand.cs`.

Keep the diagram and notes in sync with code: if you rename classes, update this file.
