namespace VoltRoute

open WebSharper
open VoltRoute.Calculations

[<JavaScript>]
module TripAnalysis =

    /// Represents a simplified high-level evaluation of a trip.
    /// This is a lightweight abstraction over the full simulation result,
    /// intended for quick checks (e.g. feasibility, required charge).
    type TripFeasibility =
        {
            CanReachDestination : bool
            ArrivalSocPercent : float
            RequiredChargeKWh : float
        }

    /// Performs a simplified analysis of the trip based on the full simulation.
    ///
    /// This function wraps `calculateTrip` and extracts only the most relevant
    /// high-level metrics:
    /// - whether the destination is reachable without charging
    /// - estimated SOC at arrival
    /// - required additional energy if charging is needed
    ///
    /// It is useful when a lightweight decision is needed without
    /// processing the full charging stop breakdown.
    let analyzeTrip (input: TripInput) =

        let result = calculateTrip input

        /// Convert remaining energy back to SOC percentage.
        /// This is recalculated explicitly to keep this module independent
        /// from internal assumptions of the calculation layer.
        let arrivalSoc =
            if input.BatteryCapacity <= 0.0 then 0.0
            else (result.RemainingEnergyKWh / input.BatteryCapacity) * 100.0

        {
            // If no charging is required, the destination is directly reachable.
            CanReachDestination = not result.NeedsCharging

            // Estimated battery percentage at arrival.
            ArrivalSocPercent = arrivalSoc

            // Total energy deficit that must be covered by charging.
            RequiredChargeKWh = result.ChargingNeededKWh
        }