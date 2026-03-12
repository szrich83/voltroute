namespace VoltRoute

open WebSharper
open VoltRoute.Calculations

[<JavaScript>]
module TripAnalysis =

    type TripFeasibility =
        {
            CanReachDestination : bool
            ArrivalSocPercent : float
            RequiredChargeKWh : float
        }

    let analyzeTrip (input: TripInput) =

        let result = calculateTrip input

        let arrivalSoc =
            if input.BatteryCapacity <= 0.0 then 0.0
            else (result.RemainingEnergyKWh / input.BatteryCapacity) * 100.0

        {
            CanReachDestination = not result.NeedsCharging
            ArrivalSocPercent = arrivalSoc
            RequiredChargeKWh = result.ChargingNeededKWh
        }