# Battery Management System (BMS) — EV Division

> Sprint 1 · Technical Review · 2025 · Team BMS

## Overview
This project develops a **Battery Management System (BMS)** for Electric Vehicles (EVs).
The BMS is the intelligent core of the battery system, responsible for continuous 
monitoring of voltage, current, and temperature to ensure safe and efficient operation 
of lithium-ion battery packs.

## Key Features
- 🔋 **Cell Monitoring** — Voltage (V), Current (I), Temperature (T)
- 📊 **SOC Estimation** — Coulomb counting method
- 🩺 **SOH Monitoring** — Battery health tracking
- ⚖️ **Cell Balancing** — Active and passive balancing
- 🛡️ **Protection** — Overcharge, deep discharge, thermal runaway prevention
- 🌡️ **Thermal Management** — Cooling control and temperature modeling
- ⚠️ **Fault Detection** — Real-time anomaly detection
- 🔌 **Communication** — CAN / SPI / I2C protocols

## Battery Parameters
| Parameter | Value |
|---|---|
| Nominal Voltage | 3.2 – 3.7 V |
| Max Voltage | 4.2 V |
| Cut-off Voltage | 2.5 – 3.0 V |
| Energy Density | 100 – 270 Wh/kg |
| Cycle Life | 600 – 3000 cycles |
| Operating Temp (charge) | 0 – 45°C |
| Operating Temp (discharge) | -20 – 60°C |

## Tech Stack
- Unity (simulation & visualization)
- Blender (3D EV model)
- C# (simulation logic)
- Git + Git LFS (version control)

## Team
**Team BMS — EV Division**
| Role | Responsibility |
|---|---|
| Simulation | Unity BMS logic, SOC/SOH models |
| 3D Modeling | Blender EV skeleton, battery visualization |
| Documentation | Technical report, sprint reviews |

## Project Structure
