# Night Shift - Code Showcase

This repository contains selected C# scripts and architectural modules from **Night Shift**, a narrative-driven social deduction game developed with **Unity (URP)**.

> **⚠️ Note:** *The full source code and assets are private due to commercial release plans. These samples are shared to demonstrate software architecture, optimization techniques, and tool development skills.*

---

## 📂 Repository Structure & Modules

This showcase is organized into the following modules, reflecting the folder structure of this repository:

### 1. ⚙️ Game Manager (State Machine)
* **Location:** `/Game Manager (State Machine)`
* **Key Script:** `GameManager.cs`
* **Description:** The brain of the game loop. It implements a robust **Finite State Machine (FSM)** to manage complex game phases (Newspaper -> Ignition -> Driving -> Customer Interaction -> End Day or Game Over).
* **Highlights:**
    * Decoupled state logic using C# Actions/Events.
    * Centralized control flow for game progression.

### 2. 🚀 Object Pooling (Optimization)
* **Location:** `/Object Pooling`
* **Key Scripts:** `ObjectPooling.cs`, `PooledObjectManager.cs`, `PoolEnum.cs`
* **Description:** A custom, generic object pooling system designed to eliminate runtime `Instantiate/Destroy` overhead and reduce Garbage Collection (GC) spikes.
* **Highlights:**
    * **Enum-Based Hashing:** Uses `Dictionary<Enum, Queue>` for O(1) access speed, avoiding string comparison overhead.
    * **Automated Lifecycle:** `PooledObject` component handles auto-return logic for particles and temporary objects.

### 3. 💬 Dialogue System (xNode-based)
* **Location:** `/Dialogue System (xNode-based)`
* **Key Script:** `NodeParser.cs`
* **Description:** A runtime graph parser integrated with the **xNode** framework to handle complex, branching narratives.
* **Highlights:**
    * recursive graph traversal for dynamic storytelling.
    * Handles conditional branching (Boolean checks) and inventory-based decisions within the dialogue tree.

### 4. 📈 Economy and Family Managers (Event-Driven)
* **Location:** `/Economy and Family Managers`
* **Key Scripts:** `EconomyManager.cs`, `FamilyManager.cs`
* **Description:** Manages the meta-game loop (Survival & Resource Management) inspired by *Papers, Please*.
* **Highlights:**
    * **Observer Pattern:** Uses C# Events to notify the UI system without direct coupling.
    * **Logic Separation:** Economy logic is strictly separated from the Family status logic for better maintainability.

### 5. 🖥️ UI Systems
* **Location:** `/UI`
* **Key Script:** `UIDayEnd.cs`
* **Description:** A responsive UI manager that listens to game events and updates the "End of Day" report card dynamically.
* **Highlights:**
    * Real-time budget calculation (Affordability checks) for UI toggles.
    * Modular UI updates based on data received from Managers.

---

## 🛠️ Technologies & Skills Demonstrated

* **Engine:** Unity 6+ (Universal Render Pipeline)
* **Language:** C# (Advanced)
* **Architecture:**
    * Finite State Machines (FSM)
    * Event-Driven Architecture (Observer Pattern)
    * Object Pooling Pattern
    * Singleton Pattern (Thread-safe implementation)
* **Tools:** xNode (Graph Editor)

---

## 👤 Developer

**Ceyhun Başkoç**
*Computer Engineering Student & Game Developer*

* **Portfolio / Itch.io:** [https://headcoach45.itch.io/]
* **LinkedIn:** [https://www.linkedin.com/in/ceyhunbaskoc/]
* **Contact:** [ceyhunbaskoc@gmail.com]
