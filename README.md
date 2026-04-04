# VoltRoute – EV Trip Planner

![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![F#](https://img.shields.io/badge/language-F%23-blueviolet)
![WebSharper](https://img.shields.io/badge/WebSharper-UI-orange)
![Status](https://img.shields.io/badge/status-active-success)
![GitHub repo size](https://img.shields.io/github/repo-size/szrich83/VoltRoute)
![GitHub last commit](https://img.shields.io/github/last-commit/szrich83/VoltRoute)

![VoltRoute Preview](docs/main.png)

---

## Motivation

Planning long-distance trips with electric vehicles is fundamentally different from traditional route planning.

Unlike internal combustion vehicles, EV travel depends on multiple interacting factors:

- limited and dynamic driving range
- non-linear charging behavior
- charger availability constraints
- trade-offs between charging time and number of stops

Most simple calculators ignore these aspects and assume ideal conditions, leading to unrealistic results.

The goal of VoltRoute is to model EV travel as a **simulation problem**, where charging decisions, battery state, and trip progression are evaluated step-by-step to produce more realistic outcomes.

---

## Features

### Core

- Range estimation from battery + consumption
- Charging stop calculation
- Charging time simulation
- Configurable target charge level (e.g. 60% / 80%)
- Trip cost estimation

### Advanced

- **Smart charging logic**
  - avoids unnecessary charging stops
  - only stops when required

- **Charger interval simulation**
  - user-defined average distance between chargers
  - approximates real-world infrastructure

- **Trip timeline**
  - structured sequence: start → drive → charge → arrival

- **SOC graph**
  - battery percentage tracked across the full trip

- **Segment-based simulation**
  - trip is divided into realistic driving + charging segments

---

## Simulation Model

### Charging curve

Charging speed is SOC-dependent:

| SOC range | Effective power |
| --------- | --------------- |
| 0–20%     | 85%             |
| 20–60%    | 100%            |
| 60–80%    | 65%             |
| 80%+      | 30%             |

Charging is simulated incrementally to approximate real behavior.

---

### Smart charging decision

For each segment, the system determines:

- whether a charging stop is required
- how much energy should be added
- whether skipping a charger is optimal

This prevents:

- unnecessary micro-charging stops
- inefficient 100% charging
- unrealistic travel assumptions

---

### SOC tracking

The system tracks battery level:

- at trip start
- after each driving segment
- after each charging event

This enables:

- accurate arrival SOC estimation
- graphical SOC visualization

---

## UI

### Inputs

- Battery capacity (kWh)
- Consumption (kWh / 100 km)
- Trip distance (km)
- Charging power (kW)
- Initial SOC (%)
- Target SOC (%)
- Electricity price
- Average charger interval (km)

---

### Outputs

- Charging stops
- Total charging time
- SOC graph
- Trip timeline
- Trip cost
- Arrival SOC

---

## Tech Stack

- **F#**
- **WebSharper UI**
- Reactive UI (Var / View)
- Functional domain logic

---

## Installation

### Requirements

- .NET 10 SDK
- Node.js
- npm

### Clone the repository

```bash
git clone https://github.com/szrich83/voltroute.git
cd voltroute
```

### Install dependencies

```bash
dotnet restore
npm install
```

### Build

```bash
dotnet build -c Release
```

### Run the project

```bash
dotnet run
```

Then open the URL shown in the terminal (e.g. http://localhost:56910)

---

## Project Structure

```text
VoltRoute/
├── src/
│   ├── Client.fs              # UI logic (WebSharper SPA)
│   ├── Calculations.fs        # EV trip calculation engine
│   ├── VehiclePresets.fs      # Predefined EV models
│   ├── TripAnalysis.fs        # Extended analysis / future features
├── wwwroot/
│   ├── custom.css             # Styling (dark UI + dashboard)
├── index.html                 # Entry point
```

---

## Screenshots

### Main UI

![Main UI](docs/main.png)

### Results Panel

![Results](docs/results.png)

### SOC graph

![SOC graph](docs/soc.png)

---

## Live Demo

https://voltroute.onrender.com

## Author

Richárd Szőke
GNMH44
Software Engineering Student

---
